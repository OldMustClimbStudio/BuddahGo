using System.Collections;
using FishNet.Object;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Integration;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BuddahRespawn : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private string respawnFloorTag = "RespawnFloor";
    [SerializeField] private float respawnDelaySeconds = 2f;
    [SerializeField, Min(0f)] private float postRaceStartRespawnProtectionSeconds = 4f;

    [Header("Placement")]
    [SerializeField] private float respawnHeightOffset = 3f;
    [SerializeField, Min(0f)] private float respawnCollisionGraceSeconds = 0.75f;
    [SerializeField, Min(0f)] private float respawnClearanceCheckRadius = 0.75f;
    [SerializeField, Min(0f)] private float additionalRespawnLiftStep = 1.5f;
    [SerializeField, Min(0f)] private float maxAdditionalRespawnLift = 6f;
    [SerializeField, Min(0f)] private float minimumDistanceBeforeAnotherFallRespawn = 8f;

    [Header("Debug")]
    [SerializeField] private bool enableVerboseRespawnLogs = true;

    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private SplineProgressTracker progressTracker;
    [SerializeField] private BuddahPredictionBootstrap predictionBootstrap;
    [SerializeField] private BuddahPredictionRespawnBridge predictionRespawnBridge;
    [SerializeField] private RaceBodyIntroStateController introStateController;

    private Coroutine _respawnRoutine;
    private float _ignoreRespawnUntilTime;
    private Vector3 _lastRespawnWorldPosition;
    private float _lastRespawnTime = float.NegativeInfinity;
    private bool _observedRaceStart;
    private float _raceStartObservedTime = float.PositiveInfinity;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (progressTracker == null)
            progressTracker = GetComponent<SplineProgressTracker>();
        if (predictionBootstrap == null)
            predictionBootstrap = GetComponent<BuddahPredictionBootstrap>();
        if (predictionRespawnBridge == null)
            predictionRespawnBridge = GetComponent<BuddahPredictionRespawnBridge>();
        if (introStateController == null)
            introStateController = GetComponent<RaceBodyIntroStateController>();
        if (predictionBootstrap != null && predictionRespawnBridge == null)
            predictionRespawnBridge = gameObject.AddComponent<BuddahPredictionRespawnBridge>();
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

    private void Update()
    {
        if (_observedRaceStart)
            return;

        RoomStateManager room = RoomStateManager.Instance;
        if (room != null && room.IsMatchPhaseActive && room.IsRaceStarted)
        {
            _observedRaceStart = true;
            _raceStartObservedTime = Time.time;
            DebugLog($"Observed race start. Respawn protection active for {postRaceStartRespawnProtectionSeconds:0.00}s.");
        }
    }

    public bool RespawnToTrackProgress(float targetProgress01, bool resetSkillEffects = false, bool preserveObsession = true, string reason = "respawn")
    {
        if (!IsLocalOwner())
            return false;

        if (!ResultAreaInteractionGate.ShouldProcessRaceProgress(gameObject))
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

        return TeleportToTrackProgress(clampedTargetProgress01, reason);
    }

    public bool RequestWrongWayCorrectionRespawn(float targetProgress01, bool resetSkillEffects = false, bool preserveObsession = true, string reason = "wrong-way-correction")
    {
        return RespawnToTrackProgress(targetProgress01, resetSkillEffects, preserveObsession, reason);
    }

    private void TryQueueRespawn(Collider other)
    {
        if (!IsLocalOwner() || !enabled || other == null)
            return;

        if (!ResultAreaInteractionGate.ShouldProcessRaceProgress(gameObject))
            return;

        if (ShouldIgnoreRespawnBecauseGameplayIsNotLive())
            return;

        if (ShouldIgnoreRespawnBecauseOfRaceStartProtection())
            return;

        if (other.isTrigger)
            return;

        if (Time.time < _ignoreRespawnUntilTime)
        {
            DebugLog($"Ignored respawn collision during grace window. collider='{other.name}' remaining={_ignoreRespawnUntilTime - Time.time:0.00}s");
            return;
        }

        if (string.IsNullOrWhiteSpace(respawnFloorTag) || !other.CompareTag(respawnFloorTag))
            return;

        if (WasRecentlyRespawnedNearCurrentPosition())
        {
            DebugLog($"Ignored repeated respawn collision near last respawn point. collider='{other.name}' playerPos={transform.position}");
            return;
        }

        if (_respawnRoutine == null)
        {
            DebugLog($"Queued fall respawn from collider='{other.name}' playerPos={transform.position}");
            _respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelaySeconds);
        _respawnRoutine = null;

        if (!ResultAreaInteractionGate.ShouldProcessRaceProgress(gameObject))
            yield break;

        if (ShouldIgnoreRespawnBecauseGameplayIsNotLive())
            yield break;

        float currentProgress = progressTracker != null ? progressTracker.progress01 : 0f;
        DebugLog($"Executing fall respawn. progress01={currentProgress:0.000} playerPos={transform.position}");
        RespawnToTrackProgress(currentProgress, false, true, "fall-respawn");
    }

    private bool TeleportToTrackProgress(float targetProgress01, string reason)
    {
        ResolveReferences();

        TrackSplineRef track = TrackSplineRef.Instance;
        if (track == null)
            return false;

        if (!track.TryEvaluateWorldPoseAtProgress01(targetProgress01, out Vector3 trackPosition, out Vector3 trackForward))
            return false;

        Vector3 respawnPosition = trackPosition + Vector3.up * respawnHeightOffset;
        Quaternion respawnRotation = trackForward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(trackForward, Vector3.up)
            : Quaternion.identity;
        respawnPosition = ResolveSafeRespawnPosition(respawnPosition);

        if (predictionBootstrap != null && predictionBootstrap.IsPredictionModeActive() && predictionRespawnBridge != null)
        {
            BuddahPredictedTeleportSourceType sourceType = ResolveTeleportSourceType(reason);
            bool routed = predictionRespawnBridge.TryRespawnToTrackProgress(respawnPosition, respawnRotation, targetProgress01, sourceType, reason);
            if (routed)
            {
                MarkTeleportRequested(respawnPosition);
                DebugLog($"PredictionV2 respawn routed. reason={reason} source={sourceType} progress01={targetProgress01:0.000} pos={respawnPosition}");
                return true;
            }
        }

        if (!TeleportToWorldPose(respawnPosition, respawnRotation))
            return false;

        progressTracker?.SnapToTrackProgress(targetProgress01);
        return true;
    }

    public bool TeleportToWorldPose(Vector3 targetPosition, Quaternion targetRotation, bool zeroVelocity = true, bool rebaseTrails = true)
    {
        ResolveReferences();
        _ignoreRespawnUntilTime = Time.time + Mathf.Max(0f, respawnCollisionGraceSeconds);
        _lastRespawnWorldPosition = targetPosition;
        _lastRespawnTime = Time.time;

        if (rebaseTrails)
            NotifyTeleportTrailRebases();

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
        if (predictionBootstrap == null)
            predictionBootstrap = GetComponent<BuddahPredictionBootstrap>();
        if (predictionRespawnBridge == null)
            predictionRespawnBridge = GetComponent<BuddahPredictionRespawnBridge>();
        if (introStateController == null)
            introStateController = GetComponent<RaceBodyIntroStateController>();
        if (predictionBootstrap != null && predictionRespawnBridge == null)
            predictionRespawnBridge = gameObject.AddComponent<BuddahPredictionRespawnBridge>();
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

        Vector3 candidatePosition = basePosition;
        float liftedAmount = 0f;
        while (liftedAmount <= maxAdditionalRespawnLift)
        {
            if (!WouldOverlapRespawnFloor(candidatePosition))
                return candidatePosition;

            candidatePosition += Vector3.up * additionalRespawnLiftStep;
            liftedAmount += additionalRespawnLiftStep;
        }

        return candidatePosition;
    }

    private bool WouldOverlapRespawnFloor(Vector3 candidatePosition)
    {
        Collider[] overlaps = Physics.OverlapSphere(candidatePosition, respawnClearanceCheckRadius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap == null || overlap.transform.IsChildOf(transform))
                continue;

            if (overlap.CompareTag(respawnFloorTag))
                return true;
        }

        return false;
    }

    private bool WasRecentlyRespawnedNearCurrentPosition()
    {
        if (minimumDistanceBeforeAnotherFallRespawn <= 0f || float.IsNegativeInfinity(_lastRespawnTime))
            return false;

        return Vector3.Distance(transform.position, _lastRespawnWorldPosition) < minimumDistanceBeforeAnotherFallRespawn;
    }

    private bool ShouldIgnoreRespawnBecauseOfRaceStartProtection()
    {
        RoomStateManager room = RoomStateManager.Instance;
        if (room == null)
            return false;

        if (!room.IsMatchPhaseActive || !room.IsRaceStarted)
        {
            DebugLog("Ignored respawn collision because the race has not started yet.");
            return true;
        }

        if (!_observedRaceStart)
        {
            _observedRaceStart = true;
            _raceStartObservedTime = Time.time;
        }

        float protectionSeconds = Mathf.Max(0f, postRaceStartRespawnProtectionSeconds);
        if (protectionSeconds <= 0f)
            return false;

        bool withinProtectionWindow = Time.time < (_raceStartObservedTime + protectionSeconds);
        if (withinProtectionWindow)
        {
            DebugLog($"Ignored respawn collision during post-start protection window. remaining={(_raceStartObservedTime + protectionSeconds) - Time.time:0.00}s");
            return true;
        }

        return false;
    }

    private bool ShouldIgnoreRespawnBecauseGameplayIsNotLive()
    {
        if (introStateController != null && introStateController.IsIntroActive)
        {
            DebugLog("Ignored respawn collision because intro is still active.");
            return true;
        }

        RoomStateManager room = RoomStateManager.Instance;
        if (room == null)
            return false;

        if (room.IsMatchPhaseActive && !room.IsGameplayMovementUnlocked)
        {
            DebugLog("Ignored respawn collision because gameplay is not unlocked yet.");
            return true;
        }

        return false;
    }

    private void DebugLog(string message)
    {
        if (enableVerboseRespawnLogs)
            Debug.Log($"[BuddahRespawn] {message}");
    }

    private void MarkTeleportRequested(Vector3 targetPosition)
    {
        _ignoreRespawnUntilTime = Time.time + Mathf.Max(0f, respawnCollisionGraceSeconds);
        _lastRespawnWorldPosition = targetPosition;
        _lastRespawnTime = Time.time;
    }

    private BuddahPredictedTeleportSourceType ResolveTeleportSourceType(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return BuddahPredictedTeleportSourceType.Unknown;

        string lowered = reason.ToLowerInvariant();
        if (lowered.Contains("wrong"))
            return BuddahPredictedTeleportSourceType.WrongWayCorrection;
        if (lowered.Contains("fall") || lowered.Contains("drop") || lowered.Contains("respawn"))
            return BuddahPredictedTeleportSourceType.DropRespawn;
        return BuddahPredictedTeleportSourceType.Manual;
    }
}
