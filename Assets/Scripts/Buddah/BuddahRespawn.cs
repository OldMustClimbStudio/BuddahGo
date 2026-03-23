using System.Collections;
using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BuddahRespawn : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private string respawnFloorTag = "RespawnFloor";
    [SerializeField] private float respawnDelaySeconds = 2f;

    [Header("Placement")]
    [SerializeField] private float respawnHeightOffset = 3f;

    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private SplineProgressTracker progressTracker;

    private Coroutine _respawnRoutine;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (progressTracker == null)
            progressTracker = GetComponent<SplineProgressTracker>();
    }

    private void OnDisable()
    {
        if (_respawnRoutine != null)
        {
            StopCoroutine(_respawnRoutine);
            _respawnRoutine = null;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryQueueRespawn(collision.collider);
    }

    private void TryQueueRespawn(Collider other)
    {
        if (!IsLocalOwner() || !enabled || other == null)
            return;

        if (other.isTrigger)
            return;

        if (string.IsNullOrWhiteSpace(respawnFloorTag) || !other.CompareTag(respawnFloorTag))
            return;

        if (_respawnRoutine == null)
            _respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelaySeconds);
        _respawnRoutine = null;
        RespawnToCurrentProgress();
    }

    private void RespawnToCurrentProgress()
    {
        if (progressTracker == null)
            progressTracker = GetComponent<SplineProgressTracker>();

        TrackSplineRef track = TrackSplineRef.Instance;
        if (track == null || progressTracker == null)
            return;

        if (!track.TryEvaluateWorldPoseAtProgress01(progressTracker.progress01, out Vector3 trackPosition, out Vector3 trackForward))
            return;

        Vector3 respawnPosition = trackPosition + Vector3.up * respawnHeightOffset;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = respawnPosition;

            if (trackForward.sqrMagnitude > 0.0001f)
                rb.rotation = Quaternion.LookRotation(trackForward, Vector3.up);
            else
                rb.rotation = Quaternion.identity;

            rb.Sleep();
            rb.WakeUp();
        }
        else
        {
            transform.position = respawnPosition;
            if (trackForward.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(trackForward, Vector3.up);
        }
    }

    private bool IsLocalOwner()
    {
        NetworkObject networkObject = GetComponent<NetworkObject>();
        return networkObject == null || networkObject.IsOwner;
    }
}
