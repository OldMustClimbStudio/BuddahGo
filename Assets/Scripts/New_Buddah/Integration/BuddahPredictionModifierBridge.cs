using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Core;
using UnityEngine;

namespace NewBuddah.PredictionV2.Integration
{
    [DisallowMultipleComponent]
    public class BuddahPredictionModifierBridge : MonoBehaviour
    {
        [SerializeField] private BuddahPredictionBootstrap bootstrap;
        [SerializeField] private BuddahPredictedMotor predictedMotor;

        public bool IsPredictionActive => bootstrap != null && bootstrap.IsPredictionModeActive() && predictedMotor != null;

        private void Awake()
        {
            if (bootstrap == null)
                bootstrap = GetComponent<BuddahPredictionBootstrap>();
            if (predictedMotor == null)
                predictedMotor = GetComponent<BuddahPredictedMotor>();
        }

        public bool TryApply(BuddahPredictedModifierCommandData command)
        {
            if (!IsPredictionActive)
                return false;

            return predictedMotor.TryApplyModifierCommand(command);
        }
    }
}
