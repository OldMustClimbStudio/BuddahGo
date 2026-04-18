using System.Collections;
using FishNet.Object;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Integration;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BuddahRespawn : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private string respawnFloorTag = "RespawnFloor";
    [SerializeField, Min(0f)] private float continuousContactRespawnSeconds = 2f;
    [SerializeField, Min(0f)] private float postRespawnCooldownSeconds = 1.5f;

    [Header("Placement")]
    [SerializeField] private float respawnHeightOffset = 3f;
    [SerializeField, Min(0f)] private float respawnClearanceCheckRadius = 0.75f;
    [SerializeField, Min(0f)] private float additionalRespawnLiftStep = 1.5f;
    [SerializeField, Min(0f)] private float maxAdditionalRespawnLift = 6f;

    [Header("Debug")]
    [SerializeField] private bool enableVerboseRespawnLogs = true;

    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private SplineProgressTracker progressTracker;
    [SerializeField] private BuddahPredictionBootstrap predictionBootstrap;
    [SerializeField] private BuddahPredictionRespawnBridge predictionRespawnBridge;

    private Coroutine _respawnRoutine;
    private int _respawnContactCount;
    private float _contactStartTime = float.PositiveInfinity;
    private float _respawnCooldownUntil;
    private float _nextStayLogTime;
    private const float StayLogIntervalSeconds = 0.25f;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (progressTracker == null) progressTracker = GetComponent<SplineProgressTracker>();
        if (predictionBootstrap == null) predictionBootstrap = GetComponent<BuddahPredictionBootstrap>();
        if (predictionRespawnBridge == null) predictionRespawnBridge = GetComponent<BuddahPredictionRespawnBridge>();
        if (predictionBootstrap != null && predictionRespawnBridge == null)
            predictionRespawnBridge = gameObject.AddComponent<BuddahPredictionRespawnBridge>();
    }

    private void OnDisable()
    {
        CancelContactState();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Collider c = collision.collider;
        bool isTagged = IsRespawnTaggedSolidCollider(c);
        DebugLog($"[ENTER] collider='{(c != null ? c.name : "null")}' tag='{(c != null ? c.tag : "null")}' isTrigger={(c != null && c.isTrigger)} isRespawnTag={isTagged} localOwner={IsLocalOwner()} enabledFlag={enabled}");

        if (!isTagged) return;
        if (_respawnContactCount == 0)
        {
            _contactStartTime = Time.time;
            _nextStayLogTime = Time.time;
            DebugLog($"[ENTER->TRACK] Started continuous-contact timer at {Time.time:0.000}. threshold={continuousContactRespawnSeconds:0.0}s cooldownRemaining={Mathf.Max(0f, _respawnCooldownUntil - Time.time):0.00}s");
        }
        _respawnContactCount++;
        DebugLog($"[ENTER->COUNT] respawnContactCount now={_respawnContactCount}");
    }

    private void OnCollisionStay(Collision collision)
    {
        Collider c = collision.collider;
        if (!IsRespawnTaggedSolidCollider(c)) return;

        float elapsed = Time.time - _contactStartTime;
        float cooldownRemaining = Mathf.Max(0f, _respawnCooldownUntil - Time.time);

        if (Time.time >= _nextStayLogTime)
        {
            _nextStayLogTime = Time.time + StayLogIntervalSeconds;
            DebugLog($"[STAY] collider='{c.name}' elapsed={elapsed:0.00}s / {continuousContactRespawnSeconds:0.0}s cooldownRemaining={cooldownRemaining:0.00}s contactCount={_respawnContactCount} routineActive={(_respawnRoutine != null)} pos={transform.position}");
        }

        if (!IsLocalOwner()) { DebugLog("[STAY->SKIP] not local owner"); return; }
        if (!enabled) { DebugLog("[STAY->SKIP] component disabled"); return; }
        if (Time.time < _respawnCooldownUntil) return;
        if (elapsed < continuousContactRespawnSeconds) return;
        if (_respawnRoutine != null) return;

        DebugLog($"[STAY->FIRE] Continuous contact {continuousContactRespawnSeconds:0.0}s reached on '{c.name}'. Triggering respawn. elapsed={elapsed:0.00}s");
        _respawnRoutine = StartCoroutine(ExecuteFallRespawn());
    }

    private void OnCollisionExit(Collision collision)
    {
        Collider c = collision.collider;
        bool isTagged = IsRespawnTaggedSolidCollider(c);
        DebugLog($"[EXIT] collider='{(c != null ? c.name : "null")}' tag='{(c != null ? c.tag : "null")}' isRespawnTag={isTagged} countBefore={_respawnContactCount}");

        if (!isTagged) return;
        _respawnContactCount = Mathf.Max(0, _respawnContactCount - 1);
        DebugLog($"[EXIT->COUNT] respawnContactCount now={_respawnContactCount}");

        if (_respawnContactCount == 0)
        {
            _contactStartTime = float.PositiveInfinity;
            if (_respawnRoutine != null)
            {
                StopCoroutine(_respawnRoutine);
                _respawnRoutine = null;
                DebugLog("[EXIT->CANCEL] Respawn cancelled: left respawn surface.");
            }
            else
            {
                DebugLog("[EXIT->RESET] Contact timer cleared (no active routine).");
            }
        }
    }

    private IEnumerator ExecuteFallRespawn()
    {
        yield return null;
        _respawnRoutine = null;
        float currentProgress = progressTracker != null ? progressTracker.progress01 : 0f;
        DebugLog($"Executing fall respawn. progress01={currentProgress:0.000} pos={transform.position}");
        RespawnToTrackProgress(currentProgress, reason: "fall-respawn");
    }

    public bool RespawnToTrackProgress(float targetProgress01, bool resetSkillEffects = false, bool preserveObsession = true, string reason = "respawn")
    {
        if (!IsLocalOwner()) return false;
        ResolveReferences();
        float clamped = Mathf.Clamp01(targetProgress01);
        Debug.Log($"[Respawn] reason={reason} progress01={clamped:0.000}");
        return TeleportToTrackProgress(clamped, reason);
    }

    public bool RequestWrongWayCorrectionRespawn(float targetProgress01, bool resetSkillEffects = false, bool preserveObsession = true, string reason = "wrong-way-correction")
    {
        return RespawnToTrackProgress(targetProgress01, resetSkillEffects, preserveObsession, reason);
    }

    private bool TeleportToTrackProgress(float targetProgress01, string reason)
    {
        ResolveReferences();
        TrackSplineRef track = TrackSplineRef.Instance;
        if (track == null) return false;
        if (!track.TryEvaluateWorldPoseAtProgress01(targetProgress01, out Vector3 trackPosition, out Vector3 trackForward))
            return false;

        Vector3 respawnPosition = ResolveSafeRespawnPosition(trackPosition + Vector3.up * respawnHeightOffset);
        Quaternion respawnRotation = trackForward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(trackForward, Vector3.up)
            : Quaternion.identity;

        if (predictionBootstrap != null && predictionBootstrap.IsPredictionModeActive() && predictionRespawnBridge != null)
        {
            BuddahPredictedTeleportSourceType sourceType = ResolveTeleportSourceType(reason);
            bool routed = predictionRespawnBridge.TryRespawnToTrackProgress(respawnPosition, respawnRotation, targetProgress01, sourceType, reason);
            if (routed)
            {
                ApplyRespawnCooldown(respawnPosition);
                DebugLog($"PredictionV2 respawn routed. reason={reason} source={sourceType} progress01={targetProgress01:0.000} pos={respawnPosition}");
                return true;
            }
        }

        if (!TeleportToWorldPose(respawnPosition, respawnRotation)) return false;
        progressTracker?.SnapToTrackProgress(targetProgress01);
        return true;
    }

    public bool TeleportToWorldPose(Vector3 targetPosition, Quaternion targetRotation, bool zeroVelocity = true, bool rebaseTrails = true)
    {
        ResolveReferences();
        ApplyRespawnCooldown(targetPosition);

        if (rebaseTrails) NotifyTeleportTrailRebases();

        if (rb != null)
        {
            if (zeroVelocity)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.position = targetPosition;
            rb.rotation = targetRotation;
            rb.Sleep();
            rb.WakeUp();
        }
        else
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }
        return true;
    }

    private void ApplyRespawnCooldown(Vector3 respawnPosition)
    {
        _respawnCooldownUntil = Time.time + Mathf.Max(0f, postRespawnCooldownSeconds);
        CancelContactState();
        DebugLog($"Respawn cooldown set for {postRespawnCooldownSeconds:0.0}s. Contact state reset.");
    }

    private void CancelContactState()
    {
        _respawnContactCount = 0;
        _contactStartTime = float.PositiveInfinity;
        if (_respawnRoutine != null)
        {
            StopCoroutine(_respawnRoutine);
            _respawnRoutine = null;
        }
    }

    private bool IsRespawnTaggedSolidCollider(Collider other)
    {
        return other != null && !other.isTrigger
            && !string.IsNullOrWhiteSpace(respawnFloorTag)
            && other.CompareTag(respawnFloorTag);
    }

    private bool IsLocalOwner()
    {
        NetworkObject networkObject = GetComponent<NetworkObject>();
        return networkObject == null || networkObject.IsOwner;
    }

    private Vector3 ResolveSafeRespawnPosition(Vector3 basePosition)
    {
        if (string.IsNullOrWhiteSpace(respawnFloorTag) || respawnClearanceCheckRadius <= 0f)
            return basePosition;

        Vector3 candidate = basePosition;
        float lifted = 0f;
        while (lifted <= maxAdditionalRespawnLift)
        {
            if (!WouldOverlapRespawnFloor(candidate)) return candidate;
            candidate += Vector3.up * additionalRespawnLiftStep;
            lifted += additionalRespawnLiftStep;
        }
        return candidate;
    }

    private bool WouldOverlapRespawnFloor(Vector3 candidatePosition)
    {
        Collider[] overlaps = Physics.OverlapSphere(candidatePosition, respawnClearanceCheckRadius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < overlaps.Length; i++)
        {
            if (overlaps[i] == null || overlaps[i].transform.IsChildOf(transform)) continue;
            if (overlaps[i].CompareTag(respawnFloorTag)) return true;
        }
        return false;
    }

    private void NotifyTeleportTrailRebases()
    {
        foreach (var t in GetComponentsInChildren<PlayerBlackCurtainTrail>(true))
            t?.NotifyTeleportRebase();
        foreach (var t in GetComponentsInChildren<PlayerAccelerationTrail>(true))
            t?.NotifyTeleportRebase();
    }

    private void ResolveReferences()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (progressTracker == null) progressTracker = GetComponent<SplineProgressTracker>();
        if (predictionBootstrap == null) predictionBootstrap = GetComponent<BuddahPredictionBootstrap>();
        if (predictionRespawnBridge == null) predictionRespawnBridge = GetComponent<BuddahPredictionRespawnBridge>();
        if (predictionBootstrap != null && predictionRespawnBridge == null)
            predictionRespawnBridge = gameObject.AddComponent<BuddahPredictionRespawnBridge>();
    }

    private BuddahPredictedTeleportSourceType ResolveTeleportSourceType(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return BuddahPredictedTeleportSourceType.Unknown;
        string lowered = reason.ToLowerInvariant();
        if (lowered.Contains("wrong")) return BuddahPredictedTeleportSourceType.WrongWayCorrection;
        if (lowered.Contains("fall") || lowered.Contains("drop") || lowered.Contains("respawn"))
            return BuddahPredictedTeleportSourceType.DropRespawn;
        return BuddahPredictedTeleportSourceType.Manual;
    }

    private void DebugLog(string message)
    {
        if (enableVerboseRespawnLogs)
            Debug.Log($"[BuddahRespawn] {message}");
    }
}
