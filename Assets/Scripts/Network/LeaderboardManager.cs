using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class LeaderboardManager : NetworkBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float refreshIntervalSeconds = 1f;

    public readonly SyncList<RankEntry> Rankings = new SyncList<RankEntry>();
    private readonly Dictionary<int, PlayerProgress> _progressByClientId = new Dictionary<int, PlayerProgress>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(ServerRefreshLoop());
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        Rankings.Clear();
        _progressByClientId.Clear();
    }

    public void RegisterPlayer(int clientId, string displayName)
    {
        if (!IsServerInitialized)
            return;

        if (_progressByClientId.ContainsKey(clientId))
            return;

        _progressByClientId[clientId] = new PlayerProgress
        {
            ClientId = clientId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? $"Player {clientId}" : displayName,
            CheckpointIndex = 0
        };
    }

    public void UnregisterPlayer(int clientId)
    {
        if (!IsServerInitialized)
            return;

        if (_progressByClientId.Remove(clientId))
        {
            BuildRankings();
        }
    }

    public bool TryAdvanceCheckpoint(int clientId, int checkpointId)
    {
        if (!IsServerInitialized)
            return false;

        if (!_progressByClientId.TryGetValue(clientId, out PlayerProgress progress))
        {
            RegisterPlayer(clientId, $"Player {clientId}");
            progress = _progressByClientId[clientId];
        }

        // Enforce sequential checkpoints: 1, 2, 3 ...
        if (checkpointId != progress.CheckpointIndex + 1)
            return false;

        progress.CheckpointIndex = checkpointId;
        _progressByClientId[clientId] = progress;
        return true;
    }
    public void ReportSplineProgress(int clientId, float distanceOnTrack, float forwardDot)
    {
        if (!IsServerInitialized)
            return;

        if (!_progressByClientId.TryGetValue(clientId, out PlayerProgress progress))
        {
            RegisterPlayer(clientId, $"Player {clientId}");
            progress = _progressByClientId[clientId];
        }

        progress.DistanceOnTrack = Mathf.Max(0f, distanceOnTrack);
        progress.ForwardDot = forwardDot;

        _progressByClientId[clientId] = progress;
    }


    private IEnumerator ServerRefreshLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.1f, refreshIntervalSeconds));
        while (true)
        {
            if (IsServerInitialized)
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

        List<RankEntry> list = new List<RankEntry>(_progressByClientId.Count);
        foreach (KeyValuePair<int, PlayerProgress> kvp in _progressByClientId)
        {
            PlayerProgress progress = kvp.Value;
            list.Add(new RankEntry
            {
                ClientId = progress.ClientId,
                DisplayName = progress.DisplayName,
                Checkpoints = progress.CheckpointIndex,
                DistanceOnTrack = progress.DistanceOnTrack
            });
        }

        list.Sort((a, b) =>
        {
            // 先按 checkpoints（如果你没用 checkpoints，全员为0，等价于只按距离）
            int byCp = b.Checkpoints.CompareTo(a.Checkpoints);
            if (byCp != 0) return byCp;

            // 再按 spline 距离（完成度）
            int byDist = b.DistanceOnTrack.CompareTo(a.DistanceOnTrack);
            if (byDist != 0) return byDist;

            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
        });


        Rankings.Clear();
        for (int i = 0; i < list.Count; i++)
        {
            Rankings.Add(list[i]);
        }
    }

    private struct PlayerProgress
    {
        public int ClientId;
        public string DisplayName;
        public int CheckpointIndex;

        public float DistanceOnTrack; // 新增：沿线米数
        public float ForwardDot;      // 可选：调试/显示用
    }
}

[Serializable]
public struct RankEntry : IEquatable<RankEntry>
{
    public int ClientId;
    public string DisplayName;
    public int Checkpoints;

    public float DistanceOnTrack; // 新增

    public bool Equals(RankEntry other)
    {
        return ClientId == other.ClientId
            && DisplayName == other.DisplayName
            && Checkpoints == other.Checkpoints
            && Mathf.Approximately(DistanceOnTrack, other.DistanceOnTrack);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ClientId, DisplayName, Checkpoints, DistanceOnTrack);
    }
}

