using FishNet.Object;
using BuddahGo.Match;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using UnityEngine;

[RequireComponent(typeof(SplineProgressTracker))]
public class LapProgress : NetworkBehaviour
{
    [Header("Start Point")]
    [Tooltip("Starting point GameObject. If assigned, any collider on this object or its children is accepted.")]
    [SerializeField] private GameObject startPointObject;

    [Tooltip("Used when StartPointObject is not assigned.")]
    [SerializeField] private string startPointTag = "StartingPoint";

    [Header("Lap Validation")]
    [SerializeField, Range(0.5f, 0.99f)] private float nearEndThreshold01 = 0.85f;
    [SerializeField, Range(0.0f, 0.3f)] private float nearStartThreshold01 = 0.20f;
    [SerializeField] private float minimumCrossingCooldownSeconds = 0.5f;
    [SerializeField, Range(0.25f, 0.99f)] private float minimumForwardWrapDelta01 = 0.50f;
    [SerializeField, Range(0.05f, 0.99f)] private float maximumProgressJumpWithoutCheckpoint01 = 0.50f;
    [SerializeField] private float minForwardDot = 0.0f;
    [SerializeField] private float minimumWorldDistanceFromStartToArmLap = 25f;

    [Header("Checkpoint Validation")]
    [SerializeField] private bool useSequentialCheckpoints = true;
    [SerializeField] private bool autoAdvanceSplineCheckpoints = true;
    [SerializeField, Min(0)] private int requiredCheckpointCount = 3;
    [SerializeField] private float[] fallbackCheckpointProgresses01 = new float[] { 0.25f, 0.50f, 0.75f };

    [Header("Read Only")]
    [SerializeField] private int currentLap = 0;
    [SerializeField] private bool hasStartedLap = false;
    [SerializeField] private bool hasLeftStartZoneSinceLastCross = false;
    [SerializeField] private bool hasReachedLapValidationDistance = false;
    [SerializeField] private int nextCheckpointIndex = 1;

    private GameObject _cachedStartPoint;
    private Collider[] _startPointColliders;
    private SplineProgressTracker _tracker;
    private float _nextAllowedCrossTime = 0f;
    private float _lastProgress01 = 0f;
    private bool _hasLastProgressSample = false;

    public int CurrentLap => currentLap;
    public int NextCheckpointIndex => nextCheckpointIndex;
    public bool HasStartedLap => hasStartedLap;
    public float TotalProgress01 => Mathf.Max(0f, Mathf.Max(0, currentLap - 1) + (_tracker != null ? _tracker.progress01 : 0f));
    public float TotalProgressPercent => TotalProgress01 * 100f;

    private void Awake()
    {
        _tracker = GetComponent<SplineProgressTracker>();
        ResolveStartPointReference();
    }

    private bool HasLocalTimingAuthority => RacerAuthority.IsProgressAuthority(this) && IsServerInitialized
        && MatchRules.Current.IsSolo;

    // Solo runs the human and every AI on the host, so a validated crossing is recorded on the authoritative
    // clock the moment it happens; no timestamp ever crosses an RPC. RaceTiming itself rejects duplicates,
    // regressions and lap jumps, and the race-length clamp matches the periodic server report. Online racers
    // keep that periodic observation only.
    private void RecordCompletedLap(int completedLaps)
    {
        if (MatchServices.Clock == null || MatchServices.Timing == null || !RacerAuthority.TryGetId(this, out RacerId racer))
            return;
        int lapsToFinish = RaceFinishManager.Instance != null ? RaceFinishManager.Instance.LapsToFinish : RaceRules.DefaultLapsToFinish;
        if (completedLaps <= lapsToFinish)
            MatchServices.Timing.ObserveCompletedLaps(racer, completedLaps, MatchServices.Clock.Now);
    }

    private void Update()
    {
        if (!RacerAuthority.IsProgressAuthority(this))
            return;

        if (!ResultAreaInteractionGate.ShouldProcessRaceProgress(gameObject))
            return;

        if (_tracker == null)
            return;

        if (!hasStartedLap)
            return;

        UpdateLapValidationState();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!RacerAuthority.IsProgressAuthority(this))
            return;

        if (!ResultAreaInteractionGate.ShouldProcessRaceProgress(gameObject))
            return;

        if (_tracker == null)
            return;

        if (!IsStartPoint(other))
            return;

        if (Time.time < _nextAllowedCrossTime)
            return;

        if (_tracker.forwardDot < minForwardDot)
            return;

        float currentProgress = Mathf.Clamp01(_tracker.progress01);
        UpdateLapValidationState(currentProgress);

        if (!hasStartedLap)
        {
            hasStartedLap = true;
            currentLap = 1; // Initial line entry starts lap 1; GO remains the timing origin.
            ResetCrossState();
            SetLastProgressSample(currentProgress);
            _nextAllowedCrossTime = Time.time + minimumCrossingCooldownSeconds;
            return;
        }

        if (!hasLeftStartZoneSinceLastCross
            || !hasReachedLapValidationDistance
            || !HasCompletedCheckpointSequence())
            return;

        currentLap += 1;
        // Only validated local server crossings are timed. Reporting/finishing stays outside the trigger.
        if (HasLocalTimingAuthority)
            RecordCompletedLap(currentLap - 1);
        ResetCrossState();
        SetLastProgressSample(currentProgress);
        _nextAllowedCrossTime = Time.time + minimumCrossingCooldownSeconds;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!RacerAuthority.IsProgressAuthority(this))
            return;

        if (!ResultAreaInteractionGate.ShouldProcessRaceProgress(gameObject))
            return;

        if (!hasStartedLap)
            return;

        if (!IsStartPoint(other))
            return;

        hasLeftStartZoneSinceLastCross = true;
    }

    private bool IsStartPoint(Collider other)
    {
        if (startPointObject == null || !startPointObject.scene.IsValid())
        {
            ResolveStartPointReference();
        }

        if (startPointObject != null)
        {
            Transform root = startPointObject.transform;
            return other.transform == root || other.transform.IsChildOf(root);
        }

        return !string.IsNullOrWhiteSpace(startPointTag) && other.CompareTag(startPointTag);
    }

    private void ResolveStartPointReference()
    {
        if (startPointObject != null && startPointObject.scene.IsValid())
            return;

        if (string.IsNullOrWhiteSpace(startPointTag))
            return;

        GameObject found = GameObject.FindWithTag(startPointTag);
        if (found != null)
        {
            startPointObject = found;
        }
    }

    private void UpdateLapValidationState()
    {
        UpdateLapValidationState(Mathf.Clamp01(_tracker.progress01));
    }

    private void UpdateLapValidationState(float currentProgress)
    {
        if (!hasStartedLap)
            return;

        if (!hasLeftStartZoneSinceLastCross && !IsInsideStartPointZone())
            hasLeftStartZoneSinceLastCross = true;

        if (!hasReachedLapValidationDistance && GetDistanceFromStartPoint() >= minimumWorldDistanceFromStartToArmLap)
            hasReachedLapValidationDistance = true;

        if (!_hasLastProgressSample)
        {
            SetLastProgressSample(currentProgress);
            return;
        }

        float rawDelta = currentProgress - _lastProgress01;
        TryAdvanceSplineCheckpoints(currentProgress, rawDelta);

        bool isValidForwardWrap = hasLeftStartZoneSinceLastCross
            && hasReachedLapValidationDistance
            && _lastProgress01 >= nearEndThreshold01
            && currentProgress <= nearStartThreshold01
            && rawDelta <= -minimumForwardWrapDelta01
            && _tracker.forwardDot >= minForwardDot;

        _lastProgress01 = currentProgress;
    }

    private void ResetCrossState()
    {
        hasLeftStartZoneSinceLastCross = false;
        hasReachedLapValidationDistance = false;
        nextCheckpointIndex = 1;
    }

    private void SetLastProgressSample(float currentProgress)
    {
        _lastProgress01 = Mathf.Clamp01(currentProgress);
        _hasLastProgressSample = true;
    }

    private Collider[] GetStartPointColliders()
    {
        bool refresh = _cachedStartPoint != startPointObject || _startPointColliders == null
            || _startPointColliders.Length == 0;
        if (!refresh)
        {
            for (int i = 0; i < _startPointColliders.Length; i++)
            {
                if (_startPointColliders[i] != null)
                    continue;
                refresh = true;
                break;
            }
        }
        if (refresh)
        {
            _cachedStartPoint = startPointObject;
            _startPointColliders = startPointObject.GetComponentsInChildren<Collider>(true);
        }
        return _startPointColliders;
    }
    private bool IsInsideStartPointZone()
    {
        if (startPointObject == null || !startPointObject.scene.IsValid())
            ResolveStartPointReference();

        if (startPointObject == null)
            return false;

        Collider[] startColliders = GetStartPointColliders();
        if (startColliders == null || startColliders.Length == 0)
            return false;

        Vector3 pos = transform.position;
        for (int i = 0; i < startColliders.Length; i++)
        {
            Collider c = startColliders[i];
            if (c == null || !c.enabled)
                continue;

            Vector3 closest = c.ClosestPoint(pos);
            if ((closest - pos).sqrMagnitude <= 0.0001f)
                return true;
        }

        return false;
    }

    private float GetDistanceFromStartPoint()
    {
        if (startPointObject == null || !startPointObject.scene.IsValid())
            ResolveStartPointReference();

        if (startPointObject == null)
            return float.PositiveInfinity;

        Collider[] startColliders = GetStartPointColliders();
        Vector3 pos = transform.position;
        float bestSqrDistance = float.PositiveInfinity;

        if (startColliders != null)
        {
            for (int i = 0; i < startColliders.Length; i++)
            {
                Collider c = startColliders[i];
                if (c == null || !c.enabled)
                    continue;

                Vector3 closest = c.ClosestPoint(pos);
                float sqrDistance = (closest - pos).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                    bestSqrDistance = sqrDistance;
            }
        }

        if (float.IsPositiveInfinity(bestSqrDistance))
            bestSqrDistance = (startPointObject.transform.position - pos).sqrMagnitude;

        return Mathf.Sqrt(bestSqrDistance);
    }

    public bool TryAdvanceCheckpoint(int checkpointId)
    {
        if (!RacerAuthority.IsProgressAuthority(this) || !hasStartedLap || !useSequentialCheckpoints)
            return false;

        int checkpointCount = GetRequiredCheckpointCount();
        if (checkpointCount <= 0)
            return false;

        if (checkpointId < 1 || checkpointId > checkpointCount)
            return false;

        if (checkpointId != nextCheckpointIndex)
            return false;

        nextCheckpointIndex++;
        return true;
    }

    private void TryAdvanceSplineCheckpoints(float currentProgress, float rawDelta)
    {
        if (!useSequentialCheckpoints || !autoAdvanceSplineCheckpoints)
            return;

        if (_tracker.forwardDot < minForwardDot)
            return;

        if (rawDelta <= 0f || rawDelta > maximumProgressJumpWithoutCheckpoint01)
            return;

        if (fallbackCheckpointProgresses01 == null || fallbackCheckpointProgresses01.Length == 0)
            return;

        int checkpointCount = GetRequiredCheckpointCount();
        if (checkpointCount <= 0)
            checkpointCount = fallbackCheckpointProgresses01.Length;

        while (nextCheckpointIndex <= checkpointCount && nextCheckpointIndex - 1 < fallbackCheckpointProgresses01.Length)
        {
            float gateProgress = Mathf.Clamp01(fallbackCheckpointProgresses01[nextCheckpointIndex - 1]);
            if (_lastProgress01 < gateProgress && currentProgress >= gateProgress)
            {
                nextCheckpointIndex++;
                continue;
            }

            break;
        }
    }

    private bool HasCompletedCheckpointSequence()
    {
        if (!useSequentialCheckpoints)
            return true;

        return nextCheckpointIndex > GetRequiredCheckpointCount();
    }

    private int GetRequiredCheckpointCount()
    {
        if (requiredCheckpointCount > 0)
            return requiredCheckpointCount;

        return fallbackCheckpointProgresses01 != null ? fallbackCheckpointProgresses01.Length : 0;
    }
}
