using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [SerializeField] private int checkpointId = 1;

    private void Awake()
    {
        // Match/TrackCheckpoint owns the checkpoint transforms for the current track; the scene placement is the fallback.
        ProjectConfigDatabase db = DesignTableOverlay.Db;
        if (db == null)
            return;
        string trackId = DesignTableOverlay.CurrentTrackId(this);
        TrackCheckpointRecord row = DesignTableOverlay.Single(db.trackCheckpoints, r => r.trackId == trackId && r.checkpointId == checkpointId);
        if (row != null)
            transform.SetPositionAndRotation(row.position, Quaternion.Euler(0f, row.rotationY, 0f));
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerProgressReporter reporter = other.GetComponentInParent<PlayerProgressReporter>();
        if (reporter == null)
            return;

        reporter.ReportCheckpoint(checkpointId);
    }
}
