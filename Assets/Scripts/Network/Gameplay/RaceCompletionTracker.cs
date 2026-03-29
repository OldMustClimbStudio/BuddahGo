using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(SplineProgressTracker))]
[RequireComponent(typeof(LapProgress))]
public class RaceCompletionTracker : NetworkBehaviour
{
    private const int DefaultLapsToFinish = 3;

    [Header("Wrong-Way Lock")]
    [SerializeField, Range(0f, 1f)] private float wrongWayWrapStartThreshold = 0.2f;
    [SerializeField, Range(0f, 1f)] private float wrongWayWrapEndThreshold = 0.8f;
    [SerializeField] private float wrongWayForwardDotThreshold = 0f;
    [SerializeField, Range(0f, 1f)] private float wrongWayRecoveryLapProgressThreshold = 0.05f;
    [SerializeField, Range(0f, 1f)] private float wrongWayForceRespawnLapProgressThreshold = 0.85f;
    [SerializeField, Range(0f, 1f)] private float wrongWayRespawnLapProgress = 0.01f;
    [SerializeField, Range(0f, 1f)] private float wrongWayLap0RespawnLapProgress = 0.99f;

    [Header("Read Only")]
    [SerializeField] private int currentLap;
    [SerializeField, Range(0f, 1f)] private float currentLapProgress01;
    [SerializeField, Range(0f, 100f)] private float finalCompletionPercent;
    [SerializeField] private bool isFinished;
    [SerializeField] private int finishOrder;
    [SerializeField] private double finishServerTime = -1d;
    [SerializeField] private bool hasWrongWayLock;
    [SerializeField, Range(0f, 100f)] private float wrongWayLockedCompletionPercent;
    [SerializeField] private int wrongWayLockedLapIndex;
    [SerializeField, Range(0f, 100f)] private float lastLegalLapFloorPercent;
    [SerializeField, Range(0f, 1f)] private float lastObservedPreviousLapProgress01;
    [SerializeField] private float lastObservedForwardDot;

    private SplineProgressTracker _splineProgressTracker;
    private LapProgress _lapProgress;
    private BuddahRespawn _buddahRespawn;

    public int CurrentLap => currentLap;
    public float CurrentLapProgress01 => currentLapProgress01;
    public float FinalCompletionPercent => finalCompletionPercent;
    public bool IsFinished => isFinished;
    public int FinishOrder => finishOrder;
    public double FinishServerTime => finishServerTime;
    public bool HasWrongWayLock => hasWrongWayLock;
    public float WrongWayLockedCompletionPercent => wrongWayLockedCompletionPercent;
    public int WrongWayLockedLapIndex => wrongWayLockedLapIndex;
    public float LastLegalLapFloorPercent => lastLegalLapFloorPercent;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        ResolveDependencies();
        if (_lapProgress == null || _splineProgressTracker == null)
            return;

        UpdateCompletionFromLapAndSpline(
            _lapProgress.CurrentLap,
            _splineProgressTracker.progress01,
            GetConfiguredLapsToFinish(),
            _splineProgressTracker.PreviousProgress01,
            _splineProgressTracker.forwardDot);
    }

    public void UpdateCompletionFromLapAndSpline(int lap, float lapProgress01, int lapsToFinish)
    {
        float previousLapProgress01 = _splineProgressTracker != null ? _splineProgressTracker.PreviousProgress01 : lapProgress01;
        float forwardDot = _splineProgressTracker != null ? _splineProgressTracker.forwardDot : 0f;
        UpdateCompletionFromLapAndSpline(lap, lapProgress01, lapsToFinish, previousLapProgress01, forwardDot);
    }

    public void UpdateCompletionFromLapAndSpline(
        int lap,
        float lapProgress01,
        int lapsToFinish,
        float previousLapProgress01,
        float forwardDot)
    {
        currentLap = Mathf.Max(0, lap);
        currentLapProgress01 = Mathf.Clamp01(lapProgress01);
        lastObservedPreviousLapProgress01 = Mathf.Clamp01(previousLapProgress01);
        lastObservedForwardDot = forwardDot;
        int safeLapsToFinish = Mathf.Max(1, lapsToFinish);

        if (isFinished || currentLap > safeLapsToFinish)
        {
            finalCompletionPercent = 100f;
            return;
        }

        UpdateLastLegalLapFloorPercent(safeLapsToFinish);
        EvaluateWrongWayState(safeLapsToFinish);

        if (hasWrongWayLock)
        {
            finalCompletionPercent = wrongWayLockedCompletionPercent;
            return;
        }

        if (currentLap <= 0)
        {
            finalCompletionPercent = 0f;
            return;
        }

        finalCompletionPercent = CalculateFinalCompletionPercent(currentLap, currentLapProgress01, safeLapsToFinish);
    }

    public bool ShouldMarkFinished(int lapsToFinish)
    {
        return !isFinished && currentLap > Mathf.Max(1, lapsToFinish);
    }

    public void MarkFinishedServer(int order, double serverTime)
    {
        if (!IsServerInitialized || isFinished)
            return;

        isFinished = true;
        finishOrder = Mathf.Max(1, order);
        finishServerTime = serverTime;
        finalCompletionPercent = 100f;
        ClearWrongWayLock();
    }

    public float GetComparableCompletionValue()
    {
        return isFinished ? 100f : finalCompletionPercent;
    }

    public void NotifyCorrectionRespawnApplied()
    {
        ClearWrongWayLock();
        UpdateLastLegalLapFloorPercent(GetConfiguredLapsToFinish());
        currentLapProgress01 = GetCorrectionRespawnLapProgress();

        if (currentLap <= 0)
        {
            finalCompletionPercent = 0f;
            return;
        }

        finalCompletionPercent = CalculateFinalCompletionPercent(currentLap, currentLapProgress01, GetConfiguredLapsToFinish());
    }

    public static float CalculateFinalCompletionPercent(int lap, float lapProgress01, int lapsToFinish)
    {
        int totalLaps = Mathf.Max(1, lapsToFinish);
        if (lap <= 0)
            return 0f;

        int completedLaps = Mathf.Clamp(Mathf.Max(0, lap - 1), 0, totalLaps - 1);
        float clampedLapProgress01 = Mathf.Clamp01(lapProgress01);
        float completion01 = (completedLaps + clampedLapProgress01) / totalLaps;
        return Mathf.Clamp(completion01 * 100f, 0f, 100f);
    }

    private void EvaluateWrongWayState(int lapsToFinish)
    {
        if (_splineProgressTracker == null)
            return;

        if (currentLap <= 0)
        {
            if (currentLapProgress01 <= wrongWayForceRespawnLapProgressThreshold)
            {
                TryApplyWrongWayCorrectionRespawn();
            }

            return;
        }

        if (!hasWrongWayLock && DidWrongWayCrossStartLine())
        {
            ApplyWrongWayLock(lapsToFinish);
        }

        if (hasWrongWayLock
            && lastObservedForwardDot < wrongWayForwardDotThreshold
            && currentLapProgress01 <= wrongWayForceRespawnLapProgressThreshold)
        {
            TryApplyWrongWayCorrectionRespawn();
            return;
        }

        if (hasWrongWayLock
            && currentLapProgress01 <= wrongWayRecoveryLapProgressThreshold
            && lastObservedForwardDot >= wrongWayForwardDotThreshold)
        {
            ClearWrongWayLock();
        }
    }

    private bool DidWrongWayCrossStartLine()
    {
        return lastObservedForwardDot < wrongWayForwardDotThreshold
            && lastObservedPreviousLapProgress01 <= wrongWayWrapStartThreshold
            && currentLapProgress01 >= wrongWayWrapEndThreshold;
    }

    private void ApplyWrongWayLock(int lapsToFinish)
    {
        hasWrongWayLock = true;
        wrongWayLockedLapIndex = Mathf.Clamp(currentLap, 0, lapsToFinish);
        wrongWayLockedCompletionPercent = GetLapFloorPercent(wrongWayLockedLapIndex, lapsToFinish);
        finalCompletionPercent = wrongWayLockedCompletionPercent;
    }

    private void ClearWrongWayLock()
    {
        hasWrongWayLock = false;
        wrongWayLockedCompletionPercent = 0f;
        wrongWayLockedLapIndex = 0;
    }

    private void TryApplyWrongWayCorrectionRespawn()
    {
        if (!IsOwner)
            return;

        if (_buddahRespawn == null)
            return;

        float respawnLapProgress = GetCorrectionRespawnLapProgress();
        if (_buddahRespawn.RequestWrongWayCorrectionRespawn(respawnLapProgress, false, true))
        {
            if (_splineProgressTracker != null)
                _splineProgressTracker.SnapToTrackProgress(respawnLapProgress);

            NotifyCorrectionRespawnApplied();
        }
    }

    private void UpdateLastLegalLapFloorPercent(int lapsToFinish)
    {
        int lapFloorIndex = Mathf.Clamp(currentLap, 0, Mathf.Max(1, lapsToFinish));
        lastLegalLapFloorPercent = GetLapFloorPercent(lapFloorIndex, lapsToFinish);
    }

    private float GetLapFloorPercent(int lapIndex, int lapsToFinish)
    {
        int safeLapsToFinish = Mathf.Max(1, lapsToFinish);
        if (lapIndex <= 0)
            return 0f;

        int completedLaps = Mathf.Clamp(lapIndex - 1, 0, safeLapsToFinish - 1);
        return (completedLaps / (float)safeLapsToFinish) * 100f;
    }

    private float GetCorrectionRespawnLapProgress()
    {
        return Mathf.Clamp01(currentLap <= 0 ? wrongWayLap0RespawnLapProgress : wrongWayRespawnLapProgress);
    }

    private int GetConfiguredLapsToFinish()
    {
        if (RaceFinishManager.Instance != null)
            return RaceFinishManager.Instance.LapsToFinish;

        return DefaultLapsToFinish;
    }

    private void ResolveDependencies()
    {
        _splineProgressTracker ??= GetComponent<SplineProgressTracker>();
        _lapProgress ??= GetComponent<LapProgress>();
        _buddahRespawn ??= GetComponent<BuddahRespawn>();
    }
}
