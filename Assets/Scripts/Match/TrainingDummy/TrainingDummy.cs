using FishNet.Managing.Timing;
using FishNet.Object;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Debugging;
using SteamMultiplayer.Network.Results;
using UnityEngine;

namespace BuddahGo.Match
{
    // Practice training dummy (README A1). MatchSpawnManager adds this plain MonoBehaviour to an ownerless racer
    // prefab instance before spawn; it is never on the prefab. Server side only:
    // - parks the body through the motor's steering override (throttle 0, no steering) while normal physics,
    //   impulses, body collisions and skill effects (slow, invert, root, freeze, scale) still act on it;
    // - once the body is displaced from its home pose, teleports it home `resetDelaySeconds` after the last
    //   disturbance (resting contact with another body does not count) and clears its server-side skill state.
    //   Falls off the track use the regular BuddahRespawn path.
    [DisallowMultipleComponent]
    public sealed class TrainingDummy : MonoBehaviour, ISteeringOverride
    {
        private const string ResetReason = "training-dummy-reset";

        [SerializeField, Min(0f)] private float resetDelaySeconds = 3f;
        [Tooltip("Planar speed above which the dummy still counts as disturbed (m/s).")]
        [SerializeField, Min(0f)] private float disturbedPlanarSpeed = 0.5f;
        [Tooltip("Relative speed of a body contact above which it counts as a disturbance (m/s). Resting against another body (a parked dummy, an idle racer) does not, so dummies pushed together still reset.")]
        [SerializeField, Min(0f)] private float disturbedContactSpeed = 0.5f;
        [Tooltip("Planar distance from the home position that counts as displaced (m). Height is ignored, so settling onto the ground never triggers a reset.")]
        [SerializeField, Min(0f)] private float displacedDistance = 1f;
        [Tooltip("Yaw difference from the home rotation that counts as displaced (degrees).")]
        [SerializeField, Range(0f, 180f)] private float displacedYawDegrees = 10f;
        [Tooltip("How long the last received impulse stays on the overhead readout (s).")]
        [SerializeField, Min(0f)] private float impulseDisplaySeconds = 2f;

        private RacerIdentity _identity;
        private NetworkObject _networkObject;
        private BuddahPredictedMotor _motor;
        private BuddahPredictionBootstrap _bootstrap;
        private SkillExecutor _executor;
        private Rigidbody _body;
        private Vector3 _homePosition;
        private Quaternion _homeRotation = Quaternion.identity;
        private float _homeProgress01;
        private bool _hasHome;
        private TrainingDummyResetClock _clock;
        private uint _seenImpulseEventId;
        private float _lastImpulseMagnitude;
        private float _lastImpulseTime = float.NegativeInfinity;

        internal bool IsServerActive => RacerAuthority.IsServerDummy(_networkObject);
        internal string DisplayName => _identity != null && !string.IsNullOrEmpty(_identity.DisplayName) ? _identity.DisplayName : name;

        // Called by MatchSpawnManager between Instantiate and ServerManager.Spawn.
        internal void ConfigureBeforeSpawn(Vector3 homePosition, Quaternion homeRotation, float homeProgress01, float resetDelay)
        {
            _homePosition = homePosition;
            _homeRotation = homeRotation;
            _homeProgress01 = Mathf.Clamp01(homeProgress01);
            resetDelaySeconds = Mathf.Max(0f, resetDelay);
            _hasHome = true;
            _clock.Restart(Time.time);
        }

        private void Awake()
        {
            _identity = GetComponent<RacerIdentity>();
            _networkObject = GetComponent<NetworkObject>();
            _motor = GetComponent<BuddahPredictedMotor>();
            _bootstrap = GetComponent<BuddahPredictionBootstrap>();
            _executor = GetComponent<SkillExecutor>();
            _body = GetComponent<Rigidbody>();
            // The motor caches its steering sources in Awake, before this component existed.
            if (_motor != null)
                _motor.RefreshSteeringProviders();
            if (_bootstrap != null && _bootstrap.DebugState != null)
                _seenImpulseEventId = _bootstrap.DebugState.lastImpulseEventId;
        }

        // Parked: zero throttle and no steering. Only a flagged dummy overrides, so an accidental copy on a racer is inert.
        public bool TryGetOverride(out int steering, out bool drive)
        {
            steering = 0;
            drive = false;
            return _identity != null && _identity.IsTrainingDummy;
        }

        private void FixedUpdate()
        {
            if (!_hasHome || !IsServerActive)
                return;

            float now = Time.time;
            PollConsumedImpulse(now);
            if (IsMovingOrAffected())
                _clock.MarkDisturbed(now);

            if (!ResultAreaInteractionGate.ShouldProcessRaceProgress(gameObject))
                return;
            if (_clock.ShouldReset(now, IsDisplaced(), resetDelaySeconds))
                ResetToHome(now);
        }

        private void OnCollisionEnter(Collision collision) => MarkBodyContact(collision);

        private void OnCollisionStay(Collision collision) => MarkBodyContact(collision);

        // Another dynamic body (racer, giant, push box) hitting or shoving the dummy; static track geometry is ignored.
        // Resting contact (no relative motion) is not a disturbance: two displaced dummies left touching each other
        // would otherwise refresh each other's clock on every step and never reset. Active shoving that moves the
        // dummy is still caught by the planar-speed check, and hits by the consumed-impulse poll.
        private void MarkBodyContact(Collision collision)
        {
            if (!_hasHome || collision == null || collision.rigidbody == null || collision.rigidbody == _body || !IsServerActive)
                return;
            if (!TrainingDummyResetClock.IsDisturbingContact(collision.relativeVelocity.magnitude, disturbedContactSpeed))
                return;
            _clock.MarkDisturbed(Time.time);
        }

        // The motor records every consumed impulse in its debug state (Events.cs ConsumeImpulseAuthoritativeEntry);
        // ids are monotonic per channel, so a changed id is a new hit on this body.
        private void PollConsumedImpulse(float now)
        {
            BuddahPredictionDebugState debug = _bootstrap != null ? _bootstrap.DebugState : null;
            if (debug == null || debug.lastImpulseEventId == _seenImpulseEventId)
                return;
            _seenImpulseEventId = debug.lastImpulseEventId;
            _lastImpulseMagnitude = debug.lastImpulseVector.magnitude;
            _lastImpulseTime = now;
            _clock.MarkDisturbed(now);
        }

        private bool IsMovingOrAffected()
        {
            if (_body != null)
            {
                Vector3 velocity = _body.velocity;
                velocity.y = 0f;
                if (velocity.sqrMagnitude > disturbedPlanarSpeed * disturbedPlanarSpeed)
                    return true;
            }
            return TryCaptureEffects(out TrainingDummyEffects effects) && effects.AnyActive;
        }

        private bool IsDisplaced()
        {
            Vector3 offset = transform.position - _homePosition;
            offset.y = 0f;
            if (offset.sqrMagnitude > displacedDistance * displacedDistance)
                return true;
            Vector3 facing = TrainingDummyLayout.FlatTangent(transform.forward);
            Vector3 homeFacing = TrainingDummyLayout.FlatTangent(_homeRotation * Vector3.forward);
            return Vector3.Angle(facing, homeFacing) > displacedYawDegrees;
        }

        private void ResetToHome(float now)
        {
            _clock.Restart(now);
            if (_motor == null)
                return;
            bool queued = _motor.TryApplyServerAuthoritativeTeleport(
                _homePosition,
                _homeRotation,
                _homeProgress01,
                BuddahPredictedTeleportSourceType.Manual,
                ResetReason,
                snapProgress: true,
                zeroLinearVelocity: true,
                zeroAngularVelocity: true,
                resetModifiers: true,
                resetImpulseQueue: true,
                resetPushGrace: true,
                rebaseTrails: true);
            // Server-controlled cleanup (pending cast, hand buffs, perception, modifiers, slow-trap zone).
            if (queued)
                _executor?.ResetActiveSkillEffectsServer();
            GameLog.Verbose($"[TrainingDummy] {name} reset to home queued={queued} pos={_homePosition} progress01={_homeProgress01:0.000}");
        }

        // Effects with remaining seconds, read from this host's server motor (Practice is always a local host).
        internal bool TryCaptureEffects(out TrainingDummyEffects effects)
        {
            effects = default;
            TimeManager clock = _networkObject != null ? _networkObject.TimeManager : null;
            if (_motor == null || clock == null)
                return false;
            effects = TrainingDummyEffects.FromModifiers(_motor.CurrentSkillModifiers, clock.LocalTick, clock.TickDelta,
                _motor.IsFrozen ? _motor.FreezeSecondsLeft : 0f);
            return true;
        }

        // Last impulse magnitude while it is still on display, otherwise -1.
        internal float VisibleImpulseMagnitude => Time.time - _lastImpulseTime <= impulseDisplaySeconds ? _lastImpulseMagnitude : -1f;

        // Seconds before the pending reset, or -1 when the dummy is home or resets are paused.
        internal float SecondsUntilReset => _hasHome && IsServerActive && ResultAreaInteractionGate.ShouldProcessRaceProgress(gameObject)
            ? _clock.SecondsUntilReset(Time.time, IsDisplaced(), resetDelaySeconds)
            : -1f;
    }
}
