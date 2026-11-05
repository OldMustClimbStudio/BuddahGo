using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Core;
using UnityEngine;

namespace NewBuddah.PredictionV2.Integration
{
    [DisallowMultipleComponent]
    public class BuddahPredictionSkillMovementBridge : MonoBehaviour
    {
        [SerializeField] private BuddahPredictionBootstrap bootstrap;
        [SerializeField] private BuddahPredictionModifierBridge modifierBridge;

        private void Awake()
        {
            if (bootstrap == null)
                bootstrap = GetComponent<BuddahPredictionBootstrap>();
            if (modifierBridge == null)
                modifierBridge = GetComponent<BuddahPredictionModifierBridge>();
        }

        public bool IsPredictionMovementActive()
        {
            return bootstrap != null && bootstrap.IsPredictionModeActive() && modifierBridge != null;
        }

        public bool TryApplyAcceleration(float extraForwardForce, float extraMaxSpeed, float durationSeconds, string source)
        {
            return modifierBridge != null && modifierBridge.TryApply(BuddahPredictedModifierCommandData.CreateAcceleration(extraForwardForce, extraMaxSpeed, durationSeconds, source));
        }

        public bool TryApplyRootThenAcceleration(float rootDurationSeconds, float extraForwardForce, float extraMaxSpeed, float accelDurationSeconds, string source)
        {
            return modifierBridge != null && modifierBridge.TryApply(BuddahPredictedModifierCommandData.CreateRootThenAcceleration(rootDurationSeconds, extraForwardForce, extraMaxSpeed, accelDurationSeconds, source));
        }

        public bool TryApplyInvertTurn(float durationSeconds, string source)
        {
            return modifierBridge != null && modifierBridge.TryApply(BuddahPredictedModifierCommandData.CreateInvertTurn(durationSeconds, source));
        }

        public bool TryApplyScale(float scaleMultiplier, float durationSeconds, float massMultiplier, float forwardForceMultiplier, string source)
        {
            return modifierBridge != null && modifierBridge.TryApply(BuddahPredictedModifierCommandData.CreateScale(scaleMultiplier, durationSeconds, massMultiplier, forwardForceMultiplier, source));
        }

        public bool TryApplyFreeze(float seconds, string source)
        {
            return modifierBridge != null && modifierBridge.TryApply(BuddahPredictedModifierCommandData.CreateFreeze(seconds, source));
        }

        public bool TryEndModifier(BuddahPredictedModifierCommandType target, string source)
        {
            return modifierBridge != null && modifierBridge.TryApply(BuddahPredictedModifierCommandData.CreateEndModifier(target, source));
        }

        public bool TryApplyBurst(float extraMaxSpeed, float seconds, string source)
        {
            return modifierBridge != null && modifierBridge.TryApply(BuddahPredictedModifierCommandData.CreateBurst(extraMaxSpeed, seconds, source));
        }

        public bool TryApplyMaxSpeedMultiplier(float multiplier, float seconds, string source)
        {
            return modifierBridge != null && modifierBridge.TryApply(BuddahPredictedModifierCommandData.CreateMaxSpeedMultiplier(multiplier, seconds, source));
        }
    }
}
