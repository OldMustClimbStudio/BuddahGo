using NewBuddah.PredictionV2.Config;
using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    public static class BuddahPredictedModifierResolver
    {
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

            if (invertTurnActive)
                result.FinalSteeringSign = -1f;

            if (rootActive)
            {
                result.FinalForwardForce = 0f;
                result.FinalMaxSpeed = 0f;
            }

            result.FinalMaxSpeed = Mathf.Max(0f, result.FinalMaxSpeed);
            result.FinalForwardForce = Mathf.Max(0f, result.FinalForwardForce);
            result.FinalTurnTorque = Mathf.Max(0f, result.FinalTurnTorque);
            result.IsRooted = rootActive;
            result.IsInvertTurnActive = invertTurnActive;
            result.IsPushGraceActive = pushGraceActive;
            result.IsSteeringSuppressed = suppressSteeringActive;
            result.IsRoomBypassActive = roomBypassActive;

            return result;
        }
    }
}
