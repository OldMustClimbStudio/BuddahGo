using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Core;
using UnityEngine;

namespace NewBuddah.PredictionV2.Integration
{
    [DisallowMultipleComponent]
    public class BuddahPredictionRespawnBridge : MonoBehaviour
    {
        [SerializeField] private BuddahPredictionBootstrap bootstrap;
        [SerializeField] private BuddahPredictedMotor predictedMotor;

        private void Awake()
        {
            if (bootstrap == null)
                bootstrap = GetComponent<BuddahPredictionBootstrap>();
            if (predictedMotor == null)
                predictedMotor = GetComponent<BuddahPredictedMotor>();
        }

        public bool IsPredictionRespawnActive()
        {
            return bootstrap != null && bootstrap.IsPredictionModeActive() && predictedMotor != null;
        }

        public bool TryRespawnToTrackProgress(
            Vector3 targetPosition,
            Quaternion targetRotation,
            float targetProgress01,
            BuddahPredictedTeleportSourceType sourceType,
            string reason)
        {
            if (!IsPredictionRespawnActive())
                return false;

            return predictedMotor.RequestAuthoritativeTeleportFromOwner(
                targetPosition,
                targetRotation,
                targetProgress01,
                sourceType,
                reason,
                snapProgress: true,
                zeroLinearVelocity: true,
                zeroAngularVelocity: true,
                resetModifiers: true,
                resetImpulseQueue: true,
                resetPushGrace: true,
                rebaseTrails: true);
        }
    }
}
