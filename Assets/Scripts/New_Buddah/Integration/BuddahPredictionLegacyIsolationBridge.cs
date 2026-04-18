using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Events;
using UnityEngine;

namespace NewBuddah.PredictionV2.Integration
{
    public sealed class BuddahPredictionLegacyIsolationBridge
    {
        public void ApplyMode(
            BuddahMovementRuntimeMode mode,
            BuddahLegacyComponentRefs legacyRefs,
            Core.BuddahPredictedMotor predictedMotor,
            Debugging.BuddahPredictionDebugState debugState,
            BuddahPredictionCommandBus commandBus)
        {
            bool predictionActive = mode == BuddahMovementRuntimeMode.PredictionV2;

            if (legacyRefs?.Movement != null)
                legacyRefs.Movement.enabled = !predictionActive;

            if (predictedMotor != null)
                predictedMotor.enabled = predictionActive;

            // Phase 2: every legacy<->prediction transition clears pending command-bus events so the
            // new mode starts from a known-empty state. Log stays on until Phase 3+ starts consuming.
            if (commandBus != null)
            {
                int cleared = commandBus.TryClearChannels(BuddahPredictionChannelMask.All);
                Debug.Log(
                    $"{BuddahPredictionCommandBus.LogPrefix}:ClearAll triggeredBy=ModeSwitch channelsCleared={cleared} mode={mode}",
                    commandBus);
            }

            if (debugState == null)
                return;

            debugState.currentMode = mode;
            debugState.predictionActive = predictionActive;
            debugState.legacyMovementEnabled = legacyRefs?.Movement != null && legacyRefs.Movement.enabled;
            debugState.predictedMotorEnabled = predictedMotor != null && predictedMotor.enabled;
        }
    }
}
