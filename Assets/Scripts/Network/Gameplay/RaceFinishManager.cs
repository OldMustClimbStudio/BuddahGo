using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class RaceFinishManager : NetworkBehaviour
{
    public static RaceFinishManager Instance { get; private set; }

    [Header("Race Rules")]
    [SerializeField, Min(1)] private int lapsToFinish = 3;
    [SerializeField, Min(0f)] private float postFirstFinishCountdownSeconds = 15f;
    [SerializeField] private bool enableVerboseLogs = true;
    [SerializeField, Tooltip("Deprecated: result flow no longer loads a separate scene. Kept only for inspector migration.")]
    private string resultSceneName = "RaceMapEndField";

    [Header("Read Only")]
    [SerializeField] private bool hasFirstFinisher;
    [SerializeField] private int firstFinisherClientId = -1;
    [SerializeField] private float countdownRemaining;
    [SerializeField] private bool isRaceForceEnded;

    private readonly HashSet<int> _finishedClientIds = new HashSet<int>();
    private int _nextFinishOrder = 1;
    private double _countdownStartServerTime = -1d;
    private bool _matchEndTriggered;

    public int LapsToFinish => Mathf.Max(1, lapsToFinish);
    public float PostFirstFinishCountdownSeconds => Mathf.Max(0f, postFirstFinishCountdownSeconds);
    public bool HasFirstFinisher => hasFirstFinisher;
    public int FirstFinisherClientId => firstFinisherClientId;
    public float CountdownRemaining => countdownRemaining;
    public bool IsRaceForceEnded => isRaceForceEnded;
    public bool CanUseLocalDebugFinishButton()
    {
        if (!IsClientInitialized)
            return false;

        if (RoomStateManager.Instance == null)
            return false;

        if (!RoomStateManager.Instance.TryGetLocalPlayer(out RoomPlayerState localPlayer))
            return false;

        return localPlayer.IsHost && RoomStateManager.Instance.CurrentMatchSessionPhase == MatchSessionPhase.InMatch;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if (!IsServerInitialized || !hasFirstFinisher || isRaceForceEnded)
            return;

        double elapsed = Time.unscaledTimeAsDouble - _countdownStartServerTime;
        countdownRemaining = Mathf.Max(0f, PostFirstFinishCountdownSeconds - (float)elapsed);

        if (countdownRemaining <= 0f)
        {
            ForceEndRaceServer();
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        _finishedClientIds.Clear();
        _nextFinishOrder = 1;
        hasFirstFinisher = false;
        firstFinisherClientId = -1;
        countdownRemaining = 0f;
        isRaceForceEnded = false;
        _countdownStartServerTime = -1d;
        _matchEndTriggered = false;
    }

    public bool TryRegisterFinish(RaceCompletionTracker completionTracker)
    {
        if (!IsServerInitialized || completionTracker == null || isRaceForceEnded)
            return false;

        int clientId = completionTracker.OwnerId;
        if (_finishedClientIds.Contains(clientId))
            return false;

        int finishOrder = _nextFinishOrder++;
        double finishTime = Time.unscaledTimeAsDouble;

        _finishedClientIds.Add(clientId);
        completionTracker.MarkFinishedServer(finishOrder, finishTime);
        MatchResultPresentationCoordinator.Instance?.NotifyPlayerFinishedServer(clientId, finishOrder);

        if (!hasFirstFinisher)
        {
            hasFirstFinisher = true;
            firstFinisherClientId = clientId;
            countdownRemaining = PostFirstFinishCountdownSeconds;
            _countdownStartServerTime = finishTime;
        }

        return true;
    }

    public void ForceEndRaceServer()
    {
        EndMatchAndLoadResultScene();
    }

    public void TriggerDebugFinishRaceFromLocalUi()
    {
        if (!CanUseLocalDebugFinishButton())
        {
            Debug.LogWarning("[RaceFinishManager] Local debug finish button ignored because this client is not the host in an active match.");
            return;
        }

        RequestDebugChampionFinishServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestDebugChampionFinishServerRpc(NetworkConnection caller = null)
    {
        if (!IsServerInitialized || caller == null || !caller.IsAuthenticated)
            return;

        bool callerIsHost = RoomStateManager.Instance != null
            && RoomStateManager.Instance.TryGetPlayer(caller.ClientId, out RoomPlayerState playerState)
            && playerState.IsHost;

        if (!callerIsHost)
        {
            Debug.LogWarning($"[RaceFinishManager] Ignored debug champion-finish request from non-host client {caller.ClientId}.");
            return;
        }

        if (!TryGetOwnedCompletionTracker(caller.ClientId, out RaceCompletionTracker completionTracker))
        {
            Debug.LogWarning($"[RaceFinishManager] Debug champion-finish failed because no live RaceCompletionTracker was found for client {caller.ClientId}.");
            return;
        }

        if (completionTracker.IsFinished)
        {
            Debug.LogWarning($"[RaceFinishManager] Debug champion-finish ignored because client {caller.ClientId} is already finished.");
            return;
        }

        Debug.Log($"[RaceFinishManager] Debug champion-finish requested by client {caller.ClientId}. Simulating a first-place finish presentation for the host player.");
        bool registered = TryRegisterFinish(completionTracker);
        if (!registered)
        {
            Debug.LogWarning($"[RaceFinishManager] Debug champion-finish could not register a finish for client {caller.ClientId}.");
            return;
        }

        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.ReportSplineProgress(
                caller.ClientId,
                distanceOnTrack: 0f,
                forwardDot: 1f,
                lap: LapsToFinish + 1,
                lapProgress01: 1f,
                finalCompletionPercent: 100f,
                isFinished: true,
                finishOrder: completionTracker.FinishOrder,
                finishServerTime: completionTracker.FinishServerTime);
        }
    }

    [Server]
    public void EndMatchAndLoadResultScene()
    {
        if (!IsServerInitialized)
            return;

        if (_matchEndTriggered)
        {
            Debug.LogWarning("[RaceFinishManager] EndMatchAndLoadResultScene ignored because the match end flow already ran once.");
            return;
        }

        _matchEndTriggered = true;
        isRaceForceEnded = true;
        countdownRemaining = 0f;

        List<FinalMatchResultEntry> finalResults = null;
        LeaderboardManager leaderboard = LeaderboardManager.Instance;
        if (leaderboard == null)
        {
            Debug.LogWarning("[RaceFinishManager] Cannot end match cleanly because LeaderboardManager.Instance is null.");
        }
        else
        {
            leaderboard.FreezeRankings();
            finalResults = leaderboard.BuildFinalResultsSnapshot(ResolvePlayerNameForClient);

            DebugLog($"Final results frozen. count={finalResults.Count}");
        }

        MatchResultPresentationCoordinator presentationCoordinator = MatchResultPresentationCoordinator.Instance;
        if (presentationCoordinator == null)
        {
            Debug.LogWarning("[RaceFinishManager] MatchResultPresentationCoordinator.Instance is null. Match is frozen but result presentation did not start.");
            return;
        }

        if (finalResults == null)
        {
            finalResults = new List<FinalMatchResultEntry>();
        }

        if (!presentationCoordinator.BeginFinalResultPresentationServer(finalResults))
        {
            Debug.LogWarning("[RaceFinishManager] Final result presentation was rejected or already running.");
            return;
        }

        DebugLog($"Started in-scene result presentation flow. legacySceneField='{resultSceneName}'.");
    }

    private string ResolvePlayerNameForClient(int clientId)
    {
        if (RoomStateManager.Instance != null && RoomStateManager.Instance.TryGetPlayer(clientId, out RoomPlayerState playerState))
        {
            if (!string.IsNullOrWhiteSpace(playerState.PlayerName))
                return playerState.PlayerName;
        }

        return $"Player {clientId}";
    }

    private bool TryGetOwnedCompletionTracker(int clientId, out RaceCompletionTracker completionTracker)
    {
        PlayerProgressReporter[] reporters = FindObjectsByType<PlayerProgressReporter>(FindObjectsSortMode.None);
        for (int i = 0; i < reporters.Length; i++)
        {
            PlayerProgressReporter reporter = reporters[i];
            if (reporter == null || !reporter.IsSpawned || reporter.OwnerId != clientId)
                continue;

            completionTracker = reporter.GetComponent<RaceCompletionTracker>()
                ?? reporter.GetComponentInParent<RaceCompletionTracker>()
                ?? reporter.GetComponentInChildren<RaceCompletionTracker>(true);

            if (completionTracker != null)
                return true;
        }

        completionTracker = null;
        return false;
    }

    private void DebugLog(string message)
    {
        if (enableVerboseLogs)
            Debug.Log($"[RaceFinishManager] {message}");
    }
}
