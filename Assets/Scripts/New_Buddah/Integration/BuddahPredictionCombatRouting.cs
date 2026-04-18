using FishNet.Object;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Debugging;
using UnityEngine;

namespace NewBuddah.PredictionV2.Integration
{
    public static class BuddahPredictionCombatRouting
    {
        public static bool TryRouteImpulse(
            NetworkObject victimNetworkObject,
            Vector3 impulse,
            float turnTorqueImpulse,
            BuddahPredictedImpulseSourceType sourceType,
            NetworkObject sourceObject)
        {
            if (victimNetworkObject == null)
                return false;

            BuddahPredictionBootstrap bootstrap = victimNetworkObject.GetComponent<BuddahPredictionBootstrap>();
            BuddahPredictedMotor predictedMotor = victimNetworkObject.GetComponent<BuddahPredictedMotor>();
            int sourceObjectId = sourceObject != null ? sourceObject.ObjectId : 0;

            if (bootstrap != null && predictedMotor != null && bootstrap.IsPredictionModeActive())
                return predictedMotor.TryApplyServerAuthoritativeImpulse(impulse, turnTorqueImpulse, sourceType, sourceObjectId);

            BuddahPredictionPushTargetBox pushTargetBox = victimNetworkObject.GetComponent<BuddahPredictionPushTargetBox>();
            if (pushTargetBox != null)
                return pushTargetBox.TryApplyServerImpulse(impulse, turnTorqueImpulse, sourceType, sourceObjectId);

            return false;
        }
    }
}
