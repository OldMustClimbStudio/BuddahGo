using BuddahGo.Match;
using System;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using SteamMultiplayer.Network.Results;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class LeaderboardManager : NetworkBehaviour
{
    public static LeaderboardManager Instance { get; private set; }
    private const bool EnableVerboseRankingLogs = false;

    [Header("Settings")]
    [SerializeField] private float refreshIntervalSeconds = 0.1f;

    public readonly SyncList<RankEntry> Rankings = new SyncList<RankEntry>();
    private readonly SyncVar<string> _leaderboardSnapshotText = new SyncVar<string>();
    private readonly Dictionary<int, PlayerProgress> _progressByRacerId = new Dictionary<int, PlayerProgress>();
    private bool _rankingsDirty;
    private bool _rankingsFrozen;

    public string LeaderboardSnapshotText => _leaderboardSnapshotText.Value;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[LeaderboardManager] Duplicate instance detected during Awake. Keeping scene NetworkObject alive and allowing network lifecycle to resolve the active instance.");
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
        EnsureServerPlayerRegistrations();
        if (_rankingsDirty)
            BuildRankings();
        StartCoroutine(ServerRefreshLoop());
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        Rankings.Clear();
        _leaderboardSnapshotText.Value = string.Empty;
        _progressByRacerId.Clear();
        _rankingsFrozen = false;
        if (Instance == this)
            Instance = null;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (Instance == this)
            Instance = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterPlayer(int racerId, string displayName)
    {
        if (!IsServerInitialized || _rankingsFrozen)
            return;

        if (!RacerId.IsValidValue(racerId)) return;
        if (_progressByRacerId.ContainsKey(racerId)) return;

        _progressByRacerId[racerId] = new PlayerProgress
        {
            RacerId = racerId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? PlayerIdentity.FallbackName(racerId) : displayName,
            CheckpointIndex = 0,
            FinishOrder = 0,
            FinishServerTime = -1d
        };
        _rankingsDirty = true;
        BuildRankings();
    }

    public void UnregisterPlayer(int racerId)
    {
        if (!IsServerInitialized || _rankingsFrozen)
            return;

        if (_progressByRacerId.Remove(racerId))
        {
            BuildRankings();
        }
    }

    public void ReportSplineProgress(
        int racerId,
        float distanceOnTrack,
        float forwardDot,
        int lap,
        float lapProgress01,
        float finalCompletionPercent,
        bool isFinished,
        int finishOrder,
        double finishServerTime)
    {
        if (!IsServerInitialized || _rankingsFrozen)
            return;

        if (!_progressByRacerId.TryGetValue(racerId, out PlayerProgress progress))
        {
            RegisterPlayer(racerId, PlayerIdentity.FallbackName(racerId));
            progress = _progressByRacerId[racerId];
        }

        progress.DistanceOnTrack = Mathf.Max(0f, distanceOnTrack);
        progress.ForwardDot = forwardDot;
        progress.Lap = Mathf.Max(0, lap);
        progress.LapProgress01 = Mathf.Clamp01(lapProgress01);
        progress.FinalCompletionPercent = Mathf.Clamp(finalCompletionPercent, 0f, 100f);
        progress.IsFinished = isFinished;
        progress.FinishOrder = isFinished ? Mathf.Max(1, finishOrder) : 0;
        progress.FinishServerTime = isFinished ? finishServerTime : -1d;

        _progressByRacerId[racerId] = progress;
        _rankingsDirty = true;
    }

    public void FreezeRankings()
    {
        if (!IsServerInitialized || _rankingsFrozen)
            return;

        BuildRankings();
        _rankingsFrozen = true;
        GameLog.Verbose($"[Leaderboard] Rankings frozen count={Rankings.Count}");
    }

    public List<FinalMatchResultEntry> BuildFinalResultsSnapshot(Func<int, string> playerNameResolver = null)
    {
        if (!IsServerInitialized)
            return new List<FinalMatchResultEntry>();

        if (_rankingsDirty)
            BuildRankings();

        List<FinalMatchResultEntry> snapshot = new List<FinalMatchResultEntry>(Rankings.Count);
        for (int i = 0; i < Rankings.Count; i++)
        {
            RankEntry entry = Rankings[i];
            string playerName = playerNameResolver?.Invoke(entry.RacerId);
            if (string.IsNullOrWhiteSpace(playerName))
                playerName = entry.DisplayName;

            bool hasTiming = MatchServices.Timing != null && MatchServices.Timing.TryGetResult(BuddahGo.Match.RacerId.FromValue(entry.RacerId), out _);
            RaceTimingResult timing = default;
            if (hasTiming) MatchServices.Timing.TryGetResult(BuddahGo.Match.RacerId.FromValue(entry.RacerId), out timing);
            snapshot.Add(new FinalMatchResultEntry
            {
                RacerId = entry.RacerId,
                PlayerName = playerName,
                TotalSeconds = hasTiming && timing.Finished ? timing.TotalSeconds : -1d,
                LapSeconds = hasTiming ? timing.LapSeconds : Array.Empty<double>(),
                FinalRank = i + 1,
                FinalCompletionPercent = entry.FinalCompletionPercent,
                IsFinished = entry.IsFinished,
                FinishOrder = entry.FinishOrder,
                FinishServerTime = entry.FinishServerTime,
                Lap = entry.Lap,
                Checkpoints = entry.Checkpoints,
                DistanceOnTrack = entry.DistanceOnTrack,
                LapProgress01 = entry.LapProgress01
            });
        }

        return snapshot;
    }

    private IEnumerator ServerRefreshLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.1f, refreshIntervalSeconds));
        while (true)
        {
            if (IsServerInitialized)
            {
                EnsureServerPlayerRegistrations();
            }

            if (IsServerInitialized && _rankingsDirty)
            {
                BuildRankings();
            }

            yield return wait;
        }
    }

    private void BuildRankings()
    {
        if (!IsServerInitialized)
            return;

        List<RankEntry> list = new List<RankEntry>(_progressByRacerId.Count);
        foreach (KeyValuePair<int, PlayerProgress> kvp in _progressByRacerId)
        {
            PlayerProgress progress = kvp.Value;
            list.Add(new RankEntry
            {
                RacerId = progress.RacerId,
                DisplayName = progress.DisplayName,
                Checkpoints = progress.CheckpointIndex,
                Lap = progress.Lap,
                DistanceOnTrack = progress.DistanceOnTrack,
                LapProgress01 = progress.LapProgress01,
                FinalCompletionPercent = progress.FinalCompletionPercent,
                IsFinished = progress.IsFinished,
                FinishOrder = progress.FinishOrder,
                FinishServerTime = progress.FinishServerTime
            });
        }

        list.Sort((a, b) =>
        {
            int byFinishState = b.IsFinished.CompareTo(a.IsFinished);
            if (byFinishState != 0) return byFinishState;

            if (a.IsFinished && b.IsFinished)
            {
                int byFinishOrder = a.FinishOrder.CompareTo(b.FinishOrder);
                if (byFinishOrder != 0) return byFinishOrder;

                int byFinishTime = a.FinishServerTime.CompareTo(b.FinishServerTime);
                if (byFinishTime != 0) return byFinishTime;
            }

            int byCompletion = b.FinalCompletionPercent.CompareTo(a.FinalCompletionPercent);
            if (byCompletion != 0) return byCompletion;

            int byLap = b.Lap.CompareTo(a.Lap);
            if (byLap != 0) return byLap;

            int byCheckpoint = b.Checkpoints.CompareTo(a.Checkpoints);
            if (byCheckpoint != 0) return byCheckpoint;

            int byDistance = b.DistanceOnTrack.CompareTo(a.DistanceOnTrack);
            if (byDistance != 0) return byDistance;

            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
        });

        Rankings.Clear();
        for (int i = 0; i < list.Count; i++)
        {
            Rankings.Add(list[i]);
        }

        _leaderboardSnapshotText.Value = BuildLeaderboardSnapshotText(list);

        if (EnableVerboseRankingLogs)
            GameLog.Verbose($"[Leaderboard] BuildRankings count={list.Count}");
        _rankingsDirty = false;
    }

    private static string BuildLeaderboardSnapshotText(List<RankEntry> list)
    {
        if (list == null || list.Count == 0)
            return "(empty)";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < list.Count; i++)
        {
            RankEntry entry = list[i];
            string finishSuffix = entry.IsFinished ? $" - Finished #{entry.FinishOrder}" : string.Empty;
            sb.Append(i + 1)
                .Append(". ")
                .Append(entry.DisplayName)
                .Append(" - Lap ")
                .Append(entry.Lap)
                .Append(" - ")
                .Append(entry.FinalCompletionPercent.ToString("0.0"))
                .Append('%')
                .Append(finishSuffix);

            if (i < list.Count - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    private void EnsureServerPlayerRegistrations()
    {
        if (!IsServerInitialized || _rankingsFrozen)
            return;

        PlayerProgressReporter[] reporters = FindObjectsByType<PlayerProgressReporter>(FindObjectsSortMode.None);
        for (int i = 0; i < reporters.Length; i++)
        {
            PlayerProgressReporter reporter = reporters[i];
            if (reporter == null || !reporter.IsSpawned)
                continue;

            int racerId = reporter.RacerIdValue;
            if (racerId < 0) continue;
            if (_progressByRacerId.ContainsKey(racerId))
                continue;

            string displayName = reporter.RacerName;

            RegisterPlayer(racerId, displayName);
        }
    }

    private struct PlayerProgress
    {
        public int RacerId;
        public string DisplayName;
        public int CheckpointIndex;
        public int Lap;
        public float DistanceOnTrack;
        public float ForwardDot;
        public float LapProgress01;
        public float FinalCompletionPercent;
        public bool IsFinished;
        public int FinishOrder;
        public double FinishServerTime;
    }
}

[Serializable]
public struct RankEntry : IEquatable<RankEntry>
{
    public int RacerId;
    public string DisplayName;
    public int Checkpoints;
    public int Lap;
    public float DistanceOnTrack;
    public float LapProgress01;
    public float FinalCompletionPercent;
    public bool IsFinished;
    public int FinishOrder;
    public double FinishServerTime;

    public bool Equals(RankEntry other)
    {
        return RacerId == other.RacerId
            && DisplayName == other.DisplayName
            && Checkpoints == other.Checkpoints
            && Lap == other.Lap
            && Mathf.Approximately(DistanceOnTrack, other.DistanceOnTrack)
            && Mathf.Approximately(LapProgress01, other.LapProgress01)
            && Mathf.Approximately(FinalCompletionPercent, other.FinalCompletionPercent)
            && IsFinished == other.IsFinished
            && FinishOrder == other.FinishOrder
            && FinishServerTime.Equals(other.FinishServerTime);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            HashCode.Combine(RacerId, DisplayName, Checkpoints, Lap, DistanceOnTrack),
            HashCode.Combine(LapProgress01, FinalCompletionPercent, IsFinished, FinishOrder),
            FinishServerTime);
    }
}
