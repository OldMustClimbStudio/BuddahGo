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

    public bool RespawnToTrackProgress(float targetProgress01, bool resetSkillEffects = false, bool preserveObsession = true, string reason = "respawn")
    {
        if (!IsLocalOwner())
            return false;

        ResolveReferences();

        float clampedTargetProgress01 = Mathf.Clamp01(targetProgress01);

        if (resetSkillEffects || !preserveObsession)
        {
            Debug.LogWarning(
                $"[Respawn] RespawnToTrackProgress currently does not implement resetSkillEffects/preserveObsession behavior. " +
                $"requestedResetSkillEffects={resetSkillEffects} requestedPreserveObsession={preserveObsession}");
        }

        Debug.Log(
            $"[Respawn] reason={reason} targetProgress01={clampedTargetProgress01:0.000} " +
            $"requestedResetSkillEffects={resetSkillEffects} requestedPreserveObsession={preserveObsession} " +
            $"appliedSkillEffectReset=false appliedObsessionChange=false");

        return TeleportToTrackProgress(clampedTargetProgress01);
    }

    public bool RequestWrongWayCorrectionRespawn(float targetProgress01, bool resetSkillEffects = false, bool preserveObsession = true, string reason = "wrong-way-correction")
    {
        return RespawnToTrackProgress(targetProgress01, resetSkillEffects, preserveObsession, reason);
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
        float currentProgress = progressTracker != null ? progressTracker.progress01 : 0f;
        RespawnToTrackProgress(currentProgress, false, true, "fall-respawn");
    }

    private bool TeleportToTrackProgress(float targetProgress01)
    {
        ResolveReferences();

        TrackSplineRef track = TrackSplineRef.Instance;
        if (track == null)
            return false;

        if (!track.TryEvaluateWorldPoseAtProgress01(targetProgress01, out Vector3 trackPosition, out Vector3 trackForward))
            return false;

        NotifyTeleportTrailRebases();

        Vector3 respawnPosition = trackPosition + Vector3.up * respawnHeightOffset;
        Quaternion respawnRotation = trackForward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(trackForward, Vector3.up)
            : Quaternion.identity;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = respawnPosition;
            rb.rotation = respawnRotation;
            rb.Sleep();
            rb.WakeUp();
        }
        else
        {
            transform.position = respawnPosition;
            transform.rotation = respawnRotation;
        }

        progressTracker?.SnapToTrackProgress(targetProgress01);
        return true;
    }

    private void NotifyTeleportTrailRebases()
    {
        PlayerBlackCurtainTrail[] blackCurtainTrails = GetComponentsInChildren<PlayerBlackCurtainTrail>(true);
        for (int i = 0; i < blackCurtainTrails.Length; i++)
        {
            if (blackCurtainTrails[i] != null)
                blackCurtainTrails[i].NotifyTeleportRebase();
        }

        PlayerAccelerationTrail[] accelerationTrails = GetComponentsInChildren<PlayerAccelerationTrail>(true);
        for (int i = 0; i < accelerationTrails.Length; i++)
        {
            if (accelerationTrails[i] != null)
                accelerationTrails[i].NotifyTeleportRebase();
        }
    }

    private void ResolveReferences()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (progressTracker == null)
            progressTracker = GetComponent<SplineProgressTracker>();
    }

    private bool IsLocalOwner()
    {
        NetworkObject networkObject = GetComponent<NetworkObject>();
        return networkObject == null || networkObject.IsOwner;
    }
}
