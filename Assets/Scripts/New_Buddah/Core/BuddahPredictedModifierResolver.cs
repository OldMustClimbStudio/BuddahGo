using System;
using NewBuddah.PredictionV2.Config;
using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    public static class BuddahPredictedModifierResolver
    {
        // Order: base -> scale -> additive accel / post-root accel / burst -> max-speed penalty
        // (multiplies after every additive bonus) -> root/freeze (zero force and cap) -> clamp >= 0.
        public static BuddahPredictedMotorComputedStats Resolve(BuddahPredictedModifierState state, BuddahPredictedMotorConfig config, uint tick)
        {
            BuddahPredictedMotorComputedStats result = new BuddahPredictedMotorComputedStats
            {
                FinalForwardForce = config != null ? config.ForwardForce : 0f,
                FinalMaxSpeed = config != null ? config.MaxSpeed : 0f,
                FinalTurnTorque = config != null ? config.TurnTorque : 0f,
                FinalSteeringSign = 1f,
                ScaleMultiplier = 1f,
                ScaleMassMultiplier = 1f,
                ScaleForwardForceMultiplier = 1f
            };

            bool rootActive = state.RootUntilTick > tick;
            bool accelActive = state.AccelUntilTick > tick;
            bool postRootAccelActive = state.PostRootAccelUntilTick > tick;
            bool scaleActive = state.ScaleUntilTick > tick;
            bool invertTurnActive = state.InvertTurnUntilTick > tick;
            bool pushGraceActive = state.PushGraceUntilTick > tick;
            bool suppressSteeringActive = state.SuppressSteeringUntilTick > tick;
            bool roomBypassActive = state.RoomBypassUntilTick > tick;
            bool freezeActive = state.FreezeUntilTick > tick;
            bool burstActive = state.BurstUntilTick > tick;
            bool maxSpeedPenaltyActive = state.MaxSpeedPenaltyUntilTick > tick;

            if (scaleActive)
            {
                result.ScaleMultiplier = Mathf.Max(0.1f, state.ScaleMultiplier);
                result.ScaleMassMultiplier = Mathf.Max(0.1f, state.ScaleMassMultiplier);
                result.ScaleForwardForceMultiplier = Mathf.Max(0.1f, state.ScaleForwardForceMultiplier);
            }

            result.FinalTurnTorque *= result.ScaleMultiplier;
            result.FinalForwardForce *= result.ScaleForwardForceMultiplier;

            if (accelActive)
            {
                result.FinalForwardForce += state.AccelExtraForwardForce;
                result.FinalMaxSpeed += state.AccelExtraMaxSpeed;
            }

            if (postRootAccelActive)
            {
                result.FinalForwardForce += state.PostRootAccelExtraForwardForce;
                result.FinalMaxSpeed += state.PostRootAccelExtraMaxSpeed;
            }

            if (burstActive)
                result.FinalMaxSpeed += state.BurstExtraMaxSpeed * BurstShape(BurstRemaining01(state, tick));

            if (maxSpeedPenaltyActive)
                result.FinalMaxSpeed *= BuddahPredictedModifierStateOps.ClampMaxSpeedMultiplier(state.MaxSpeedPenaltyMultiplier);

            if (invertTurnActive)
                result.FinalSteeringSign = -1f;

            if (rootActive || freezeActive)
            {
                result.FinalForwardForce = 0f;
                result.FinalMaxSpeed = 0f;
            }

            if (freezeActive)
                result.FinalTurnTorque = 0f;

            result.FinalMaxSpeed = Mathf.Max(0f, result.FinalMaxSpeed);
            result.FinalForwardForce = Mathf.Max(0f, result.FinalForwardForce);
            result.FinalTurnTorque = Mathf.Max(0f, result.FinalTurnTorque);
            result.IsRooted = rootActive || freezeActive;
            result.IsInvertTurnActive = invertTurnActive;
            result.IsPushGraceActive = pushGraceActive;
            result.IsSteeringSuppressed = suppressSteeringActive || freezeActive;
            result.IsRoomBypassActive = roomBypassActive;
            result.IsFrozen = freezeActive;
            result.IsBurstActive = burstActive && !result.IsRooted;
            result.IsMaxSpeedPenaltyActive = maxSpeedPenaltyActive;

            return result;
        }

        // 1 on the apply tick, falling linearly to 0 at BurstUntilTick. Integer tick inputs only, so
        // both peers evaluate the identical value for the same tick.
        internal static float BurstRemaining01(BuddahPredictedModifierState state, uint tick)
        {
            if (state.BurstUntilTick <= tick)
                return 0f;
            return Mathf.Clamp01((state.BurstUntilTick - tick) / (float)Math.Max(1u, state.BurstDurationTicks));
        }

        // Ease-out decay of the burst extra: extra * remaining01^2 (fast drop first, soft landing).
        internal static float BurstShape(float remaining01)
        {
            return remaining01 * remaining01;
        }
    }
}
