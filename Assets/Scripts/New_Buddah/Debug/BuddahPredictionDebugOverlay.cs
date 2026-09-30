using FishNet.Object;
using NewBuddah.PredictionV2.Bootstrap;
using UnityEngine;

namespace NewBuddah.PredictionV2.Debugging
{
    [DisallowMultipleComponent]
    public class BuddahPredictionDebugOverlay : MonoBehaviour
    {
        [SerializeField] private BuddahPredictionBootstrap bootstrap;
        [Header("Console Mirror")]
        [SerializeField] private bool mirrorSummaryToConsole = true;
        [SerializeField, Min(0.1f)] private float consoleMirrorIntervalSeconds = 0.5f;

        private NetworkObject _networkObject;
        private float _lastConsoleMirrorTime = float.NegativeInfinity;
        private string _lastConsoleMirrorSummary = string.Empty;

        private void Awake()
        {
            if (bootstrap == null)
                bootstrap = GetComponent<BuddahPredictionBootstrap>();

            _networkObject = GetComponent<NetworkObject>();
        }

        // The component name/GUID stays stable for serialized prefabs. It now only logs.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LateUpdate()
        {
            if (bootstrap == null || bootstrap.DebugState == null || !mirrorSummaryToConsole)
                return;

            if (_networkObject != null && _networkObject.IsClientInitialized && !_networkObject.IsOwner)
                return;

            if (!BuddahDiagnosticLogSampling.TryBeginSample(true, Time.unscaledTime,
                    consoleMirrorIntervalSeconds, ref _lastConsoleMirrorTime))
                return;

            bootstrap.PredictedMotor?.RefreshConsoleDebugSummaries();

            string summary = BuildConsoleSummary(bootstrap.DebugState);
            if (string.Equals(summary, _lastConsoleMirrorSummary, System.StringComparison.Ordinal))
                return;

            _lastConsoleMirrorSummary = summary;
            GameLog.Verbose($"[PredictionOverlay:{name}] {summary}");
        }

#endif

        private static string BuildConsoleSummary(BuddahPredictionDebugState state)
        {
            return
                $"tick={state.currentTick} launch={state.launchState} intro={state.introControlActive} external={state.externalKinematicControlActive} " +
                $"handoff={state.handoffActive} blend={state.handoffBlendAlpha:0.00} allowed={state.movementAllowed} blocked={state.gateBlocked} " +
                $"speed={state.planarSpeed:0.00} postHandoff={state.postHandoffSpeed:0.00} visDelta={state.motorVisualPosDelta:0.000}/{state.motorVisualYawDelta:0.0} " +
                $"ownerVis={state.ownerVisualRootStabilizationApplied}:{state.ownerVisualRootStabilizationReason} " +
                $"rep='{state.replicateSummary}' rec='{state.reconcileSummary}'";
        }
    }
}
