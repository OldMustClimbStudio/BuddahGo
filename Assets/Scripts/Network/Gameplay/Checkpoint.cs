using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [SerializeField] private int checkpointId = 1;

    private void OnTriggerEnter(Collider other)
    {
        PlayerProgressReporter reporter = other.GetComponentInParent<PlayerProgressReporter>();
        if (reporter == null)
            return;

        reporter.ReportCheckpoint(checkpointId);
    }
}
