using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
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
    [SerializeField] private string resultSceneName = "RaceMapEndField";

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

        RequestDebugFinishRaceServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestDebugFinishRaceServerRpc(NetworkConnection caller = null)
    {
        if (!IsServerInitialized || caller == null || !caller.IsAuthenticated)
            return;

        bool callerIsHost = RoomStateManager.Instance != null
            && RoomStateManager.Instance.TryGetPlayer(caller.ClientId, out RoomPlayerState playerState)
            && playerState.IsHost;

        if (!callerIsHost)
        {
            Debug.LogWarning($"[RaceFinishManager] Ignored debug finish request from non-host client {caller.ClientId}.");
            return;
        }

        Debug.Log($"[RaceFinishManager] Debug finish requested by client {caller.ClientId}. Finalizing race from current leaderboard state.");
        EndMatchAndLoadResultScene();
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
        RoomStateManager.Instance?.MarkTransitionToResultServer();

        LeaderboardManager leaderboard = LeaderboardManager.Instance;
        if (leaderboard == null)
        {
            Debug.LogWarning("[RaceFinishManager] Cannot end match cleanly because LeaderboardManager.Instance is null.");
            MatchResultCache.Clear();
        }
        else
        {
            leaderboard.FreezeRankings();
            List<FinalMatchResultEntry> finalResults = leaderboard.BuildFinalResultsSnapshot(ResolvePlayerNameForClient);
            MatchResultCache.SetResults(finalResults);
            Debug.Log($"[RaceFinishManager] Final results cached. count={finalResults.Count}");
        }

        if (string.IsNullOrWhiteSpace(resultSceneName) || InstanceFinder.SceneManager == null)
        {
            Debug.LogWarning("[RaceFinishManager] Result scene load skipped because resultSceneName is empty or FishNet SceneManager is missing.");
            return;
        }

        SceneLoadData sceneLoadData = new SceneLoadData(resultSceneName)
        {
            ReplaceScenes = ReplaceOption.All
        };

        InstanceFinder.SceneManager.LoadGlobalScenes(sceneLoadData);
        Debug.Log($"[RaceFinishManager] Loading result scene '{resultSceneName}'.");
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
}
