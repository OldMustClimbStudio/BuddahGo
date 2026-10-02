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
        public event System.Action<MotionComparison> ModelCompared;
        private BuddahPredictedMotor _motor;
        private BuddahPredictedMotorConfig _config;
        private Rigidbody _body;
        private RaceCompletionTracker _completion;
        private readonly ISteeringPlanner _planner = new ForwardSimPlanner();
        private uint _lastPlanTick;
        private bool _hasPlan;
        private bool _ownsProfile;
        private MotionState _forecast;
        private int _forecastTicks, _pendingSteering;
        private bool _forecastCollided, _pending;
        private uint _applyTick;
        private float _groundDeceleration, _lastGroundContactTime = float.NegativeInfinity;
        private static readonly ProfilerMarker PlanMarker = new ProfilerMarker("AI.Plan");

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
            foreach (var contact in collision.contacts)
            {
                if (Mathf.Abs(contact.normal.y) < 0.7f) { _forecastCollided = true; continue; }
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
        private void OnDestroy() { if (_ownsProfile && Profile != null) Destroy(Profile); }

        public bool TryGetOverride(out int steering, out bool drive)
        {
            steering = 0; drive = true;
            if (!isActiveAndEnabled || _motor == null || !_motor.IsServerInitialized
                || !(_motor.IsOwner || !_motor.Owner.IsValid)) return false;
            if (_completion != null && _completion.IsFinished) { Steering = 0; drive = false; return true; }
            if (Profile == null) { Profile = ScriptableObject.CreateInstance<AIDifficultyProfile>(); _ownsProfile = true; }
            var track = TrackSplineRef.Instance;
            if (track == null || track.TrackLength <= 0f || _motor.TimeManager == null) return true;
            if (Line == null) Line = SplineRacingLine.Capture(track);
            // The ordinary launch writer retains exclusive control until the inherited launch completes.
            if (_motor.IsLaunchHandoffActive || _motor.IsAuthoritativeLaunchHandoffPending) return true;
            uint tick = _motor.TimeManager.LocalTick;
            if (_pending && unchecked((int)(tick - _applyTick)) >= 0) { Steering = _pendingSteering; _pending = false; }
            Parameters = new MotionParameters { Stats = _motor.CurrentComputedStats,
                Mass = _body.mass, InverseYawInertia = MotionParameters.ReadInverseYawInertia(_body),
                Drag = _body.drag, AngularDrag = _body.angularDrag, MaxAngularVelocity = _body.maxAngularVelocity,
                TurnDecay = _config.TurnDecayPerSecond, TurnMultiplier = _config.TurnInputMultiplier,
                PushExtraSpeed = _config.PushExtraMaxSpeed,
                GroundDeceleration = Time.time - _lastGroundContactTime < 0.1f ? _groundDeceleration : 0f };
            if (!_pending && (!_hasPlan || tick - _lastPlanTick >= Mathf.Max(1, Profile.ReplanTicks)))
            {
                long started = System.Diagnostics.Stopwatch.GetTimestamp();
                int key;
                using (PlanMarker.Auto()) key = _planner.Plan(MotionState.Read(_body), Parameters, Line,
                    Profile, (float)_motor.TimeManager.TickDelta, Steering);
                if (Profile.ReactionTicks == 0) Steering = key;
                else { _pendingSteering = key; _pending = true; _applyTick = tick + (uint)Profile.ReactionTicks; }
                LastPlanMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                _lastPlanTick = tick; _hasPlan = true;
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
