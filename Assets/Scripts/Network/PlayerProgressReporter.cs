using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class PlayerProgressReporter : NetworkBehaviour
{
    private int _lastReportedCheckpoint;

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

    public void ReportCheckpoint(int checkpointId)
    {
        if (!IsOwner)
            return;

        if (checkpointId <= _lastReportedCheckpoint)
            return;

        _lastReportedCheckpoint = checkpointId;
        ReportCheckpointServerRpc(checkpointId);
    }

    [ServerRpc]
    private void ReportCheckpointServerRpc(int checkpointId)
    {
        if (LeaderboardManager.Instance == null)
            return;

        LeaderboardManager.Instance.TryAdvanceCheckpoint(OwnerId, checkpointId);
    }
}
