using SteamMultiplayer.Network.Results;
using BuddahGo.Match;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Config;
using Unity.Profiling;
using UnityEngine;

namespace BuddahGo.AI
{
    [DisallowMultipleComponent]
    public sealed class AIRacerDriver : MonoBehaviour, ISteeringOverride
    {
        public AIDifficultyProfile Profile;
        public int Steering { get; private set; }
        public double LastPlanMilliseconds { get; private set; }
        public SplineRacingLine Line { get; private set; }
        public MotionParameters Parameters { get; private set; }
        public event System.Action<Collision> Contact;
        public event System.Action<uint, PlanObservation> PlanObserved;
        public event System.Action<MotionComparison> ModelCompared;
        private BuddahPredictedMotor _motor;
        private BuddahPredictedMotorConfig _config;
        private Rigidbody _body;
        private RaceCompletionTracker _completion;
        private ISteeringPlanner _planner;
        private bool _usingThrustVector;
        private uint _lastPlanTick;
        private bool _hasPlan;
        private bool _ownsProfile;
        private MotionState _forecast;
        private int _forecastTicks, _pendingSteering;
        private bool _forecastCollided, _pending;
        private uint _applyTick;
        private float _groundDeceleration, _lastGroundContactTime = float.NegativeInfinity;
        private static readonly ProfilerMarker PlanMarker = new ProfilerMarker("AI.Plan");
        private System.Random _jitter;
        private AISkillDifficulty _skillDifficulty;
        private SkillPerceptionState _perception;
        private AIDifficultyProfile _impairedProfile;
        private readonly AISteeringPerception _steeringPerception = new AISteeringPerception();
        public float PerceivedSteeringSign => _steeringPerception.Sign;
        public bool IsVisionImpaired => _perception != null && _motor != null && _motor.TimeManager != null
            && _perception.VisionImpairedUntilTick > _motor.TimeManager.LocalTick;

        public void ConfigureSkillPerception(AISkillDifficulty difficulty, SkillPerceptionState perception)
        {
            _skillDifficulty = difficulty; _perception = perception;
            if (_impairedProfile != null) Destroy(_impairedProfile);
            if (Profile == null) return;
            _impairedProfile = Instantiate(Profile);
            _impairedProfile.LookaheadSeconds *= difficulty.ImpairedLookaheadMultiplier;
            _impairedProfile.RolloutSeconds *= difficulty.ImpairedLookaheadMultiplier;
            _impairedProfile.HorizonSeconds *= difficulty.ImpairedLookaheadMultiplier;
            _impairedProfile.MistakeProbability = Mathf.Clamp01(Profile.MistakeProbability + difficulty.ImpairedMistakeAddition);
            _impairedProfile.ReactionTicks += difficulty.ImpairedReactionTicks;
        }
        public void ResetSkillPerception()
        {
            _steeringPerception.Reset();
            _pending = false; _hasPlan = false;
        }
        private int ReactionJitter()
        {
            if (Profile == null || Profile.ReactionJitterTicks <= 0) return 0;
            _jitter ??= new System.Random(Profile.NoiseSeed != 0 ? Profile.NoiseSeed + 17 : GetInstanceID());
            return _jitter.Next(0, Profile.ReactionJitterTicks + 1);
        }

        private void Awake()
        {
            _motor = GetComponent<BuddahPredictedMotor>(); _body = GetComponent<Rigidbody>();
            _config = GetComponent<BuddahPredictedMotorConfig>();
            _completion = GetComponent<RaceCompletionTracker>();
        }
        private void OnDisable() { _hasPlan = false; Steering = 0; _forecastTicks = 0; _pending = false; }
        private void OnCollisionEnter(Collision collision)
        {
            if (!isActiveAndEnabled) return;
            ObserveContacts(collision);
            Contact?.Invoke(collision);
        }
        private void OnCollisionStay(Collision collision)
        {
            if (isActiveAndEnabled) ObserveContacts(collision);
        }
        private void ObserveContacts(Collision collision)
        {
            // GetContact avoids the ContactPoint[] that Collision.contacts allocates on every physics step.
            for (int i = 0, count = collision.contactCount; i < count; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                if (Mathf.Abs(contact.normal.y) < 0.7f)
                {
                    _forecastCollided = true;
                    if (_planner is ThrustVectorPlanner thrust) thrust.NotifyCollision();
                    continue;
                }
                if (contact.normal.y < 0.99f) continue; // This planar model only treats flat supporting surfaces.
                var own = contact.thisCollider.sharedMaterial; var ground = contact.otherCollider.sharedMaterial;
                float a = own != null ? own.dynamicFriction : 0.6f;
                float b = ground != null ? ground.dynamicFriction : 0.6f;
                var ca = own != null ? own.frictionCombine : PhysicMaterialCombine.Average;
                var cb = ground != null ? ground.frictionCombine : PhysicMaterialCombine.Average;
                float friction = ca == PhysicMaterialCombine.Maximum || cb == PhysicMaterialCombine.Maximum ? Mathf.Max(a, b)
                    : ca == PhysicMaterialCombine.Multiply || cb == PhysicMaterialCombine.Multiply ? a * b
                    : ca == PhysicMaterialCombine.Minimum || cb == PhysicMaterialCombine.Minimum ? Mathf.Min(a, b) : (a + b) * 0.5f;
                _groundDeceleration = friction * Mathf.Abs(Physics.gravity.y);
                _lastGroundContactTime = Time.time;
            }
        }
        private void OnDestroy()
        {
            if (_ownsProfile && Profile != null) Destroy(Profile);
            if (_impairedProfile != null) Destroy(_impairedProfile);
        }
        // Product path: the spawner hands each AI its own difficulty profile instance; the driver destroys it with the racer.
        public void AdoptProfile(AIDifficultyProfile profile) { if (_ownsProfile && Profile != null && Profile != profile) Destroy(Profile); Profile = profile; _ownsProfile = true; }

        public bool TryGetOverride(out int steering, out bool drive)
        {
            steering = 0; drive = true;
            if (!isActiveAndEnabled || _motor == null || !_motor.IsServerInitialized
                || !RacerAuthority.HasLocalControl(_motor.NetworkObject)) return false;
            if ((_completion != null && _completion.IsFinished) || !ResultAreaInteractionGate.ShouldProcessRaceProgress(gameObject))
            { Steering = 0; _pending = false; _hasPlan = false; drive = false; return true; }
            if (Profile == null)
            {
                // Spawners always adopt a product profile; a missing one is a setup error, not a reason
                // to fall back to the retired beam planner that a blank profile would select.
                Debug.LogError($"[AI] {name} has no difficulty profile; using the Normal product profile.", this);
                Profile = AIDifficultyProfiles.Resolve(SoloDifficulty.Normal, 0); _ownsProfile = true;
            }
            var track = TrackSplineRef.Instance;
            if (track == null || track.TrackLength <= 0f || _motor.TimeManager == null) return true;
            if (Line == null) Line = SplineRacingLine.Capture(track);
            // The ordinary launch writer retains exclusive control until the inherited launch completes.
            if (_motor.IsLaunchHandoffActive || _motor.IsAuthoritativeLaunchHandoffPending) return true;
            if (_planner == null || _usingThrustVector != Profile.UseThrustVector)
            {
                _usingThrustVector = Profile.UseThrustVector;
                _planner = _usingThrustVector ? (ISteeringPlanner)new ThrustVectorPlanner() : new ForwardSimPlanner();
                _hasPlan = false; _pending = false;
            }
            uint tick = _motor.TimeManager.LocalTick;
            if (_pending && unchecked((int)(tick - _applyTick)) >= 0) { Steering = _pendingSteering; _pending = false; }
            Parameters = new MotionParameters { Stats = _motor.CurrentComputedStats,
                Mass = _body.mass, InverseYawInertia = MotionParameters.ReadInverseYawInertia(_body),
                Drag = _body.drag, AngularDrag = _body.angularDrag, MaxAngularVelocity = _body.maxAngularVelocity,
                TurnDecay = _config.TurnDecayPerSecond, TurnMultiplier = _config.TurnInputMultiplier,
                PushExtraSpeed = _config.PushExtraMaxSpeed,
                GroundDeceleration = Time.time - _lastGroundContactTime < 0.1f ? _groundDeceleration : 0f };
            var planningParameters = Parameters;
            if (_skillDifficulty != null)
            {
                var observedStats = planningParameters.Stats;
                observedStats.FinalSteeringSign = _steeringPerception.Observe(observedStats.FinalSteeringSign,
                    tick, (float)_motor.TimeManager.TickDelta, _skillDifficulty.AdaptSeconds, _skillDifficulty.ReadaptSeconds);
                planningParameters.Stats = observedStats;
            }
            var decisionProfile = IsVisionImpaired && _impairedProfile != null ? _impairedProfile : Profile;
            if (!_pending && (!_hasPlan || tick - _lastPlanTick >= Profile.EffectiveReplanTicks))
            {
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                int key;
                using (PlanMarker.Auto()) key = _planner.Plan(MotionState.Read(_body), planningParameters, Line,
                    decisionProfile, (float)_motor.TimeManager.TickDelta, Steering);
                if (decisionProfile.ReactionTicks == 0 && decisionProfile.ReactionJitterTicks == 0) Steering = key;
                else { _pendingSteering = key; _pending = true; _applyTick = tick + (uint)decisionProfile.ReactionTicks + (uint)ReactionJitter(); }
                LastPlanMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                _lastPlanTick = tick; _hasPlan = true;
                PlanObserved?.Invoke(tick, _planner.LastObservation);
            }
            // Optional evidence only; no allocations or prediction samples without an observer.
            if (ModelCompared != null)
            {
                var actual = MotionState.Read(_body);
                if (_forecastTicks == 60)
                {
                    Vector3 offset = actual.Position - _forecast.Position; offset.y = 0;
                    ModelCompared.Invoke(new MotionComparison { Tick = tick, PositionError = offset.magnitude,
                        VelocityError = Vector3.Distance(actual.Velocity, _forecast.Velocity),
                        YawError = Mathf.Abs(Mathf.DeltaAngle(actual.Yaw * Mathf.Rad2Deg, _forecast.Yaw * Mathf.Rad2Deg)),
                        Collision = _forecastCollided });
                    _forecastTicks = 0;
                }
                if (_forecastTicks == 0) { _forecast = actual; _forecastCollided = false; }
                BuddahMotionModel.Step(ref _forecast, Parameters, Steering, (float)_motor.TimeManager.TickDelta);
                _forecastTicks++;
            }
            steering = Steering;
            return true;
        }
        [System.Serializable]
        public struct MotionComparison { public uint Tick; public float PositionError, VelocityError, YawError; public bool Collision; }
    }
}
