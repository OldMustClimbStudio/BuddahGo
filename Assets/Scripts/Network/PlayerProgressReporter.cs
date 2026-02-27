using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class PlayerProgressReporter : NetworkBehaviour
{
    [SerializeField] private float reportIntervalSeconds = 0.2f;
    private float _nextReportTime;
    private SplineProgressTracker _tracker;
    private LapProgress _lapTracker;

    private void Awake()
    {
        _tracker = GetComponent<SplineProgressTracker>();
        _lapTracker = GetComponent<LapProgress>();
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (Time.time < _nextReportTime)
            return;

        _nextReportTime = Time.time + reportIntervalSeconds;

        if (_tracker == null)
            return;

        int lap = (_lapTracker != null) ? _lapTracker.CurrentLap : 0;
        ReportSplineProgressServerRpc(_tracker.distanceOnTrack, _tracker.forwardDot, lap);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (LeaderboardManager.Instance != null)
        {
            string displayName = string.IsNullOrWhiteSpace(gameObject.name)
                ? $"Player {OwnerId}"
                : $"{gameObject.name} #{OwnerId}";
            LeaderboardManager.Instance.RegisterPlayer(OwnerId, displayName);
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.UnregisterPlayer(OwnerId);
        }
    }

    [ServerRpc]
    private void ReportSplineProgressServerRpc(float distanceOnTrack, float forwardDot, int lap)
    {
        if (LeaderboardManager.Instance == null)
            return;

        LeaderboardManager.Instance.ReportSplineProgress(OwnerId, distanceOnTrack, forwardDot, lap);
    }

    // Checkpoints are no longer required; progress is spline-based.
    // Kept as a compatibility no-op because some scenes may still have Checkpoint triggers.
    public void ReportCheckpoint(int checkpointId)
    {
        // Intentionally no-op.
    }

}
