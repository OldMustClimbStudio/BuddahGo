using NewBuddah.PredictionV2.Bootstrap;

namespace NewBuddah.PredictionV2.Integration
{
    public sealed class BuddahPredictionLegacyIsolationBridge
    {
        public void ApplyMode(
            BuddahMovementRuntimeMode mode,
            BuddahLegacyComponentRefs legacyRefs,
            Core.BuddahPredictedMotor predictedMotor,
            Debugging.BuddahPredictionDebugState debugState)
        {
            bool predictionActive = mode == BuddahMovementRuntimeMode.PredictionV2;

            if (legacyRefs?.Movement != null)
                legacyRefs.Movement.enabled = !predictionActive;

            if (predictedMotor != null)
                predictedMotor.enabled = predictionActive;

            if (debugState == null)
                return;

            debugState.currentMode = mode;
            debugState.predictionActive = predictionActive;
            debugState.legacyMovementEnabled = legacyRefs?.Movement != null && legacyRefs.Movement.enabled;
            debugState.predictedMotorEnabled = predictedMotor != null && predictedMotor.enabled;
        }
    }
}
