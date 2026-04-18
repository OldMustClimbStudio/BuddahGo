using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using SteamMultiplayer.Network;
using UnityEngine;

namespace SteamMultiplayer.Network.Results
{
    public enum MatchResultPresentationStage
    {
        Racing,
        FinishingWindow,
        FinalizingResults,
        InTransition,
        ResultReveal,
        ResultInteractive
    }

    [RequireComponent(typeof(NetworkObject))]
    public class MatchResultPresentationCoordinator : NetworkBehaviour
    {
        public static MatchResultPresentationCoordinator Instance { get; private set; }

        [Header("References")]
        [SerializeField] private RaceResultAreaManager raceResultAreaManager;
        [SerializeField] private ResultDecisionManager resultDecisionManager;
        [SerializeField] private ResultPresentationTimelineBridge timelineBridge;

        [Header("Player Presentation")]
        [SerializeField, Min(0.01f)] private float personalFinishDissolveOutSeconds = 0.75f;
        [SerializeField, Min(0.01f)] private float forcedFinishDissolveOutSeconds = 0.6f;
        [SerializeField, Min(0.01f)] private float resultAreaDissolveInSeconds = 1f;

        [Header("Result Area Rules")]
        [SerializeField] private bool allowMovementInResultArea = true;
        [SerializeField] private bool allowSkillsInResultArea = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        private readonly FishNet.Object.Synchronizing.SyncVar<MatchResultPresentationStage> _presentationStage =
            new FishNet.Object.Synchronizing.SyncVar<MatchResultPresentationStage>();
        private bool _finalPresentationStarted;
        private bool _teleportTriggeredByTimeline;
        private bool _revealTriggeredByTimeline;
        private List<FinalMatchResultEntry> _pendingFinalResults;
        private readonly HashSet<int> _presentationStartedClientIds = new HashSet<int>();

        public MatchResultPresentationStage CurrentStage => _presentationStage.Value;
        public bool IsGlobalResultPresentationActive =>
            _presentationStage.Value == MatchResultPresentationStage.InTransition
            || _presentationStage.Value == MatchResultPresentationStage.ResultReveal;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[MatchResultPresentationCoordinator] Duplicate instance detected during Awake. Keeping scene NetworkObject alive and allowing network lifecycle to resolve the active instance.");
                return;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Instance = this;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _finalPresentationStarted = false;
            _teleportTriggeredByTimeline = false;
            _revealTriggeredByTimeline = false;
            _pendingFinalResults = null;
            _presentationStartedClientIds.Clear();
            _presentationStage.Value = MatchResultPresentationStage.Racing;
            if (Instance == this)
                Instance = null;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            ResultPresentationTimelineBridge.Instance?.ReleaseSharedPresentationCamera();
            if (Instance == this)
                Instance = null;
        }

        public void NotifyPlayerFinishedServer(int clientId, int finishOrder)
        {
            if (!IsServerInitialized || _finalPresentationStarted)
                return;

            if (!TryGetReporter(clientId, out PlayerProgressReporter reporter))
                return;

            _presentationStage.Value = MatchResultPresentationStage.FinishingWindow;

            if (raceResultAreaManager == null)
                raceResultAreaManager = RaceResultAreaManager.Instance;

            if (raceResultAreaManager == null)
                Debug.LogWarning($"[MatchResultPresentationCoordinator] Missing RaceResultAreaManager for finished player clientId={clientId} finishOrder={finishOrder}.");

            reporter.BeginPersonalFinishPresentationServer(personalFinishDissolveOutSeconds, finishOrder, false);
            _presentationStartedClientIds.Add(clientId);
            DebugLog($"NotifyPlayerFinishedServer clientId={clientId} finishOrder={finishOrder}");
        }

        [Server]
        public bool BeginFinalResultPresentationServer(IReadOnlyList<FinalMatchResultEntry> finalResults)
        {
            if (!IsServerInitialized)
                return false;

            if (_finalPresentationStarted)
            {
                Debug.LogWarning("[MatchResultPresentationCoordinator] Final result presentation already started.");
                return false;
            }

            _finalPresentationStarted = true;
            _teleportTriggeredByTimeline = false;
            _revealTriggeredByTimeline = false;
            _pendingFinalResults = finalResults != null ? new List<FinalMatchResultEntry>(finalResults) : new List<FinalMatchResultEntry>();
            BeginFinalResultPresentationInternal();
            return true;
        }

        private void BeginFinalResultPresentationInternal()
        {
            RoomStateManager.Instance?.MarkTransitionToResultServer();

            if (raceResultAreaManager == null)
                raceResultAreaManager = RaceResultAreaManager.Instance;

            if (_pendingFinalResults != null)
            {
                for (int i = 0; i < _pendingFinalResults.Count; i++)
                {
                    FinalMatchResultEntry entry = _pendingFinalResults[i];
                    if (entry == null)
                        continue;

                    if (_presentationStartedClientIds.Contains(entry.ClientId))
                        continue;

                    if (!TryGetReporter(entry.ClientId, out PlayerProgressReporter reporter))
                        continue;

                    reporter.BeginPersonalFinishPresentationServer(forcedFinishDissolveOutSeconds, entry.FinishOrder, true);
                }
            }

            _presentationStage.Value = MatchResultPresentationStage.InTransition;
            PlayGlobalResultTransitionObserversRpc();
        }

        [Server]
        public void HandleTimelineBlackScreenFullyCoveredServer()
        {
            if (!IsServerInitialized || !_finalPresentationStarted || _teleportTriggeredByTimeline)
                return;

            _teleportTriggeredByTimeline = true;
            DebugLog("HandleTimelineBlackScreenFullyCoveredServer");

            if (_pendingFinalResults != null)
            {
                for (int i = 0; i < _pendingFinalResults.Count; i++)
                {
                    FinalMatchResultEntry entry = _pendingFinalResults[i];
                    if (entry == null)
                        continue;

                    if (!TryGetReporter(entry.ClientId, out PlayerProgressReporter reporter))
                        continue;

                    if (raceResultAreaManager != null
                        && raceResultAreaManager.TryGetPlacementTransform(entry.FinalRank - 1, out Vector3 worldPosition, out Quaternion worldRotation))
                    {
                        reporter.TeleportToHiddenResultAreaServer(worldPosition, worldRotation);
                        raceResultAreaManager.NotifyPlacementApplied(entry.ClientId);
                    }
                    else
                    {
                        Debug.LogWarning($"[MatchResultPresentationCoordinator] Missing result-area placement during black-screen teleport for clientId={entry.ClientId} finishOrder={entry.FinishOrder}.");
                    }
                }
            }
        }

        [Server]
        public void HandleTimelineFinishedServer()
        {
            if (!IsServerInitialized || !_finalPresentationStarted || _revealTriggeredByTimeline)
                return;

            if (!_teleportTriggeredByTimeline)
            {
                Debug.LogWarning("[MatchResultPresentationCoordinator] Timeline finished before black-screen teleport fired. Applying teleport immediately.");
                HandleTimelineBlackScreenFullyCoveredServer();
            }

            _revealTriggeredByTimeline = true;
            DebugLog("HandleTimelineFinishedServer -> begin reveal");
            _presentationStage.Value = MatchResultPresentationStage.ResultReveal;
            RoomStateManager.Instance?.EnterResultPhaseServer();

            if (_pendingFinalResults != null)
            {
                for (int i = 0; i < _pendingFinalResults.Count; i++)
                {
                    FinalMatchResultEntry entry = _pendingFinalResults[i];
                    if (entry == null)
                        continue;

                    if (TryGetReporter(entry.ClientId, out PlayerProgressReporter reporter))
                        reporter.BeginResultAreaRevealServer(resultAreaDissolveInSeconds);
                }
            }

            StartCoroutine(EnterInteractiveAfterRevealCoroutine());
        }

        [ObserversRpc]
        private void PlayGlobalResultTransitionObserversRpc()
        {
            if (timelineBridge == null)
                timelineBridge = ResultPresentationTimelineBridge.Instance;

            timelineBridge?.PlayResultTransitionTimeline();
        }

        private bool TryGetReporter(int clientId, out PlayerProgressReporter reporter)
        {
            PlayerProgressReporter[] reporters = FindObjectsByType<PlayerProgressReporter>(FindObjectsSortMode.None);
            for (int i = 0; i < reporters.Length; i++)
            {
                PlayerProgressReporter candidate = reporters[i];
                if (candidate == null || !candidate.IsSpawned || candidate.OwnerId != clientId)
                    continue;

                reporter = candidate;
                return true;
            }

            reporter = null;
            return false;
        }

        private IEnumerator EnterInteractiveAfterRevealCoroutine()
        {
            if (resultAreaDissolveInSeconds > 0f)
                yield return new WaitForSeconds(resultAreaDissolveInSeconds);

            _presentationStage.Value = MatchResultPresentationStage.ResultInteractive;

            if (_pendingFinalResults != null)
            {
                for (int i = 0; i < _pendingFinalResults.Count; i++)
                {
                    FinalMatchResultEntry entry = _pendingFinalResults[i];
                    if (entry == null)
                        continue;

                    if (TryGetReporter(entry.ClientId, out PlayerProgressReporter reporter))
                        reporter.EnterResultAreaInteractiveServer(allowMovementInResultArea, allowSkillsInResultArea);
                }
            }

            if (resultDecisionManager == null)
                resultDecisionManager = ResultDecisionManager.Instance;

            if (resultDecisionManager != null)
                resultDecisionManager.BeginDecisionPhaseServer();
            else
                Debug.LogWarning("[MatchResultPresentationCoordinator] ResultDecisionManager missing. Decision phase not started.");
        }

        private void DebugLog(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[MatchResultPresentationCoordinator] {message}");
        }
    }
}
