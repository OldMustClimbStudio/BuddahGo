using FishNet.Managing.Timing;
using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    // Read helpers for skill-driven motor modifiers (freeze, burst, backfire max-speed penalty),
    // plus the one-shot body writes those commands make when they land. Times are in this peer's
    // local clock: owners and observers get server deadlines translated by reconcile.
    public partial class BuddahPredictedMotor
    {
        private const float FlatForwardMinSqrMagnitude = 0.0001f;

        public bool IsFrozen
        {
            get
            {
                TimeManager clock = SkillStateClock;
                return clock != null && _modifierState.FreezeUntilTick > clock.LocalTick;
            }
        }

        public float FreezeSecondsLeft => SkillStateSecondsUntil(_modifierState.FreezeUntilTick);
        public float BurstSecondsLeft => SkillStateSecondsUntil(_modifierState.BurstUntilTick);
        public float MaxSpeedPenaltySecondsLeft => SkillStateSecondsUntil(_modifierState.MaxSpeedPenaltyUntilTick);

        // NetworkBehaviour.TimeManager dereferences the NetworkObject cache; go through
        // NetworkObject so HUD / overhead readouts are safe before network initialization.
        private TimeManager SkillStateClock => NetworkObject != null ? NetworkObject.TimeManager : null;

        private float SkillStateSecondsUntil(uint untilTick)
        {
            TimeManager clock = SkillStateClock;
            if (clock == null)
                return 0f;
            uint now = clock.LocalTick;
            return untilTick > now ? (untilTick - now) * (float)clock.TickDelta : 0f;
        }

        // Freeze lands: drop queued forces and stop moving and turning at once. Every following
        // tick the rooted stop step keeps the body still.
        private void StopBodyForFreeze()
        {
            if (rb == null || _predictionRigidbody == null)
                return;
            _predictionRigidbody.ClearPendingForces();
            SetPredictionVelocitiesSafely(Vector3.zero, Vector3.zero);
        }

        // Burst lands: an instant dash along the flat facing at the freshly resolved cap
        // (base + extras, after penalty, 0 when rooted/frozen). Vertical velocity is kept.
        // ClampPlanarSpeed then follows the decaying cap every tick.
        private void LaunchBodyForBurst()
        {
            if (rb == null || _predictionRigidbody == null || rb.isKinematic)
                return;
            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < FlatForwardMinSqrMagnitude)
                flatForward = Vector3.forward;
            flatForward.Normalize();
            Vector3 planar = flatForward * _computedStats.FinalMaxSpeed;
            _predictionRigidbody.Velocity(new Vector3(planar.x, rb.velocity.y, planar.z));
        }
    }
}
