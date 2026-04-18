using NewBuddah.PredictionV2.Bootstrap;
using UnityEngine;

namespace NewBuddah.PredictionV2.Validation
{
    [DisallowMultipleComponent]
    public class BuddahPredictionRuntimeHealthReport : MonoBehaviour
    {
        [SerializeField] private BuddahPredictionBootstrap bootstrap;
        [SerializeField] private BuddahPredictionCompatibilityRegistry compatibilityRegistry;

        private string _lastHighRiskSummary = string.Empty;

        private void Awake()
        {
            ResolveReferences();
            RefreshHealthReport();
        }

        private void LateUpdate()
        {
            RefreshHealthReport();
        }

        public void ResolveReferences()
        {
            if (bootstrap == null)
                bootstrap = GetComponent<BuddahPredictionBootstrap>();
            if (compatibilityRegistry == null)
                compatibilityRegistry = GetComponent<BuddahPredictionCompatibilityRegistry>();
        }

        public void RefreshHealthReport()
        {
            ResolveReferences();
            if (bootstrap == null)
                return;

            compatibilityRegistry?.RefreshRegistry();

            var state = bootstrap.DebugState;
            bool predictionActive = bootstrap.IsPredictionModeActive();
            bool primaryTrackComplete = !predictionActive
                || (bootstrap.PredictedMotor != null
                    && bootstrap.SkillMovementBridge != null
                    && bootstrap.VisualRootBridge != null
                    && bootstrap.CameraBridge != null
                    && bootstrap.PresentationBridge != null);

            state.primaryTrackComplete = primaryTrackComplete;
            state.fallbackActive = predictionActive;
            state.healthSummary = predictionActive
                ? (primaryTrackComplete ? "PredictionV2 main track complete with compatibility layers" : "PredictionV2 active but missing required bridge(s)")
                : "Legacy main track active";

            if (!string.Equals(_lastHighRiskSummary, state.highRiskFlagsSummary))
            {
                _lastHighRiskSummary = state.highRiskFlagsSummary;
                if (!string.IsNullOrWhiteSpace(_lastHighRiskSummary) && _lastHighRiskSummary != "none")
                    bootstrap.LogVerbose($"health risk flags -> {_lastHighRiskSummary}");
            }
        }
    }
}
