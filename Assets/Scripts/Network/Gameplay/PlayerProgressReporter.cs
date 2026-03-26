using FishNet.Object;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class PlayerProgressReporter : NetworkBehaviour
{
    [SerializeField] private float reportIntervalSeconds = 0.1f;
    private float _nextReportTime;
    private SplineProgressTracker _tracker;
    private LapProgress _lapTracker;
    private bool _registeredWithLeaderboard;

    private void Awake()
    {
        ResolveProgressDependencies();
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (Time.time < _nextReportTime)
            return;

        _nextReportTime = Time.time + reportIntervalSeconds;

        if (_tracker == null)
            ResolveProgressDependencies();

        if (_tracker == null)
            return;

        int lap = (_lapTracker != null) ? _lapTracker.CurrentLap : 0;
        Debug.Log($"[Leaderboard] Reporting progress OwnerId={OwnerId} distance={_tracker.distanceOnTrack:0.00} lap={lap} dot={_tracker.forwardDot:0.00}");
        ReportSplineProgressServerRpc(_tracker.distanceOnTrack, _tracker.forwardDot, lap);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log($"[Leaderboard] Register player request OwnerId={OwnerId} object={name}");
        StartCoroutine(RegisterWithLeaderboardWhenReady());
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (_registeredWithLeaderboard && LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.UnregisterPlayer(OwnerId);
        }

        _registeredWithLeaderboard = false;
    }

    [ServerRpc]
    private void ReportSplineProgressServerRpc(float distanceOnTrack, float forwardDot, int lap)
    {
        if (LeaderboardManager.Instance == null)
            return;

        LeaderboardManager.Instance.ReportSplineProgress(OwnerId, distanceOnTrack, forwardDot, lap);
    }

    // Checkpoints are no longer required; progress is spline-based.
    public void ReportCheckpoint(int checkpointId)
    {
        if (!IsOwner)
            return;

        if (_lapTracker == null)
            ResolveProgressDependencies();

        _lapTracker?.TryAdvanceCheckpoint(checkpointId);
    }

    private void ResolveProgressDependencies()
    {
        _tracker ??= GetComponent<SplineProgressTracker>();
        _tracker ??= GetComponentInParent<SplineProgressTracker>();
        _tracker ??= GetComponentInChildren<SplineProgressTracker>(true);

        _lapTracker ??= GetComponent<LapProgress>();
        _lapTracker ??= GetComponentInParent<LapProgress>();
        _lapTracker ??= GetComponentInChildren<LapProgress>(true);
    }

    private IEnumerator RegisterWithLeaderboardWhenReady()
    {
        while (IsServerInitialized && !_registeredWithLeaderboard)
        {
            if (LeaderboardManager.Instance != null)
            {
                string displayName = string.IsNullOrWhiteSpace(gameObject.name)
                    ? $"Player {OwnerId}"
                    : $"{gameObject.name} #{OwnerId}";

                LeaderboardManager.Instance.RegisterPlayer(OwnerId, displayName);
                Debug.Log($"[Leaderboard] Register player success OwnerId={OwnerId} displayName={displayName}");
                _registeredWithLeaderboard = true;
                yield break;
            }

            yield return null;
        }
    }

}
