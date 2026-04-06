using FishNet.Object;
using System.Collections;
using SteamMultiplayer.Network;
using SteamMultiplayer.Network.Results;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(RaceCompletionTracker))]
public class PlayerProgressReporter : NetworkBehaviour
{
    [SerializeField] private float reportIntervalSeconds = 0.1f;

    private float _nextReportTime;
    private SplineProgressTracker _tracker;
    private LapProgress _lapTracker;
    private RaceCompletionTracker _completionTracker;
    private Rigidbody _rigidbody;
    private BuddahRespawn _respawn;
    private PlayerFinishPresentationController _finishPresentationController;
    private bool _registeredWithLeaderboard;
    private void Awake()
    {
        ResolveProgressDependencies();
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (!ResultAreaInteractionGate.ShouldProcessRaceProgress(gameObject))
            return;

        if (Time.time < _nextReportTime)
            return;

        _nextReportTime = Time.time + reportIntervalSeconds;
        ResolveProgressDependencies();

        if (_tracker == null || _completionTracker == null)
            return;

        int lap = (_lapTracker != null) ? _lapTracker.CurrentLap : 0;
        _completionTracker.UpdateCompletionFromLapAndSpline(lap, _tracker.progress01, GetConfiguredLapsToFinish());

        DebugLog($"[Leaderboard] Reporting progress OwnerId={OwnerId} distance={_tracker.distanceOnTrack:0.00} lap={lap} dot={_tracker.forwardDot:0.00}");
        ReportSplineProgressServerRpc(
            _tracker.distanceOnTrack,
            _tracker.forwardDot,
            lap,
            _tracker.progress01,
            _tracker.PreviousProgress01);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        DebugLog($"[Spawn] Player ready for conn {OwnerId}");
        DebugLog($"[Leaderboard] Register player request OwnerId={OwnerId} object={name}");
        StartCoroutine(RegisterWithLeaderboardWhenReady());
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        if (_registeredWithLeaderboard && LeaderboardManager.Instance != null)
        {
            LeaderboardManager.Instance.UnregisterPlayer(OwnerId);
        }

        _registeredWithLeaderboard = false;
    }

    [ServerRpc]
    private void ReportSplineProgressServerRpc(float distanceOnTrack, float forwardDot, int lap, float progress01, float previousProgress01)
    {
        if (!ResultAreaInteractionGate.ShouldProcessRaceProgress(gameObject))
            return;

        if (LeaderboardManager.Instance == null)
            return;

        ResolveProgressDependencies();
        if (_completionTracker == null)
            return;

        int lapsToFinish = GetConfiguredLapsToFinish();
        _completionTracker.UpdateCompletionFromLapAndSpline(lap, progress01, lapsToFinish, previousProgress01, forwardDot);

        RaceFinishManager finishManager = RaceFinishManager.Instance;
        if (finishManager != null && _completionTracker.ShouldMarkFinished(lapsToFinish))
        {
            finishManager.TryRegisterFinish(_completionTracker);
        }

        LeaderboardManager.Instance.ReportSplineProgress(
            OwnerId,
            distanceOnTrack,
            forwardDot,
            lap,
            progress01,
            _completionTracker.FinalCompletionPercent,
            _completionTracker.IsFinished,
            _completionTracker.FinishOrder,
            _completionTracker.FinishServerTime);
    }

    public void ReportCheckpoint(int checkpointId)
    {
        if (!IsOwner)
            return;

        if (_lapTracker == null)
            ResolveProgressDependencies();

        _lapTracker?.TryAdvanceCheckpoint(checkpointId);
    }

    [Server]
    public void EnterResultAreaServer(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (!IsServerInitialized)
            return;

        ApplyResultAreaTeleportLocally(worldPosition, worldRotation);
        SyncResultAreaTeleportObserversRpc(worldPosition, worldRotation);
    }

    [Server]
    public void BeginPersonalFinishPresentationServer(float dissolveDurationSeconds, int finishOrder, bool forcedByGlobalEnd)
    {
        ApplyPersonalFinishPresentationLocally(dissolveDurationSeconds, finishOrder, forcedByGlobalEnd);
        BeginPersonalFinishPresentationObserversRpc(dissolveDurationSeconds, finishOrder, forcedByGlobalEnd);
    }

    [Server]
    public void TeleportToHiddenResultAreaServer(Vector3 worldPosition, Quaternion worldRotation)
    {
        ApplyTeleportToHiddenResultAreaLocally(worldPosition, worldRotation);
        TeleportToHiddenResultAreaObserversRpc(worldPosition, worldRotation);
    }

    [Server]
    public void BeginResultAreaRevealServer(float dissolveDurationSeconds)
    {
        ApplyResultAreaRevealLocally(dissolveDurationSeconds);
        BeginResultAreaRevealObserversRpc(dissolveDurationSeconds);
    }

    [Server]
    public void EnterResultAreaInteractiveServer(bool allowMovement, bool allowSkills)
    {
        ApplyResultAreaInteractiveLocally(allowMovement, allowSkills);
        EnterResultAreaInteractiveObserversRpc(allowMovement, allowSkills);
    }

    private void ResolveProgressDependencies()
    {
        _tracker ??= GetComponent<SplineProgressTracker>();
        _tracker ??= GetComponentInParent<SplineProgressTracker>();
        _tracker ??= GetComponentInChildren<SplineProgressTracker>(true);

        _lapTracker ??= GetComponent<LapProgress>();
        _lapTracker ??= GetComponentInParent<LapProgress>();
        _lapTracker ??= GetComponentInChildren<LapProgress>(true);

        _completionTracker ??= GetComponent<RaceCompletionTracker>();
        _completionTracker ??= GetComponentInParent<RaceCompletionTracker>();
        _completionTracker ??= GetComponentInChildren<RaceCompletionTracker>(true);
        _rigidbody ??= GetComponent<Rigidbody>();
        _rigidbody ??= GetComponentInParent<Rigidbody>();
        _rigidbody ??= GetComponentInChildren<Rigidbody>(true);
        _respawn ??= GetComponent<BuddahRespawn>();
        _respawn ??= GetComponentInParent<BuddahRespawn>();
        _respawn ??= GetComponentInChildren<BuddahRespawn>(true);
        _finishPresentationController ??= GetComponent<PlayerFinishPresentationController>();
        if (_finishPresentationController == null)
            _finishPresentationController = gameObject.AddComponent<PlayerFinishPresentationController>();
    }

    private IEnumerator RegisterWithLeaderboardWhenReady()
    {
        while (IsServerInitialized && !_registeredWithLeaderboard)
        {
            if (LeaderboardManager.Instance != null)
            {
                string displayName = string.IsNullOrWhiteSpace(gameObject.name)
                    ? $"Player {OwnerId}"
                    : $"{gameObject.name} #{OwnerId}";

                LeaderboardManager.Instance.RegisterPlayer(OwnerId, displayName);
                DebugLog($"[Leaderboard] Register player success OwnerId={OwnerId} displayName={displayName}");
                _registeredWithLeaderboard = true;
                yield break;
            }

            yield return null;
        }
    }

    private static void DebugLog(string message)
    {
        if (NetDebug.EnableVerboseLog)
            Debug.Log(message);
    }

    private int GetConfiguredLapsToFinish()
    {
        if (RaceFinishManager.Instance != null)
            return RaceFinishManager.Instance.LapsToFinish;

        return 3;
    }

    [ObserversRpc]
    private void SyncResultAreaTeleportObserversRpc(Vector3 worldPosition, Quaternion worldRotation)
    {
        ApplyResultAreaTeleportLocally(worldPosition, worldRotation);
    }

    private void ApplyResultAreaTeleportLocally(Vector3 worldPosition, Quaternion worldRotation)
    {
        ResolveProgressDependencies();

        if (_respawn != null)
        {
            _respawn.TeleportToWorldPose(worldPosition, worldRotation, true, true);
            return;
        }

        if (_rigidbody != null)
        {
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.position = worldPosition;
            _rigidbody.rotation = worldRotation;
            _rigidbody.Sleep();
            _rigidbody.WakeUp();
            return;
        }

        transform.SetPositionAndRotation(worldPosition, worldRotation);
    }

    [ObserversRpc]
    private void BeginPersonalFinishPresentationObserversRpc(float dissolveDurationSeconds, int finishOrder, bool forcedByGlobalEnd)
    {
        ApplyPersonalFinishPresentationLocally(dissolveDurationSeconds, finishOrder, forcedByGlobalEnd);
    }

    [ObserversRpc]
    private void BeginResultAreaRevealObserversRpc(float dissolveDurationSeconds)
    {
        ApplyResultAreaRevealLocally(dissolveDurationSeconds);
    }

    [ObserversRpc]
    private void EnterResultAreaInteractiveObserversRpc(bool allowMovement, bool allowSkills)
    {
        ApplyResultAreaInteractiveLocally(allowMovement, allowSkills);
    }

    [ObserversRpc]
    private void TeleportToHiddenResultAreaObserversRpc(Vector3 worldPosition, Quaternion worldRotation)
    {
        ApplyTeleportToHiddenResultAreaLocally(worldPosition, worldRotation);
    }

    private void ApplyPersonalFinishPresentationLocally(float dissolveDurationSeconds, int finishOrder, bool forcedByGlobalEnd)
    {
        ResolveProgressDependencies();
        _finishPresentationController?.ApplyFinishedPresentation(dissolveDurationSeconds, forcedByGlobalEnd);
    }

    private void ApplyResultAreaRevealLocally(float dissolveDurationSeconds)
    {
        ResolveProgressDependencies();
        _finishPresentationController?.ApplyResultReveal(dissolveDurationSeconds);
    }

    private void ApplyResultAreaInteractiveLocally(bool allowMovement, bool allowSkills)
    {
        ResolveProgressDependencies();
        _finishPresentationController?.EnterResultInteractive(allowMovement, allowSkills);
    }

    private void ApplyTeleportToHiddenResultAreaLocally(Vector3 worldPosition, Quaternion worldRotation)
    {
        ResolveProgressDependencies();
        ApplyResultAreaTeleportLocally(worldPosition, worldRotation);
        _finishPresentationController?.EnterHiddenInResultAreaWaitingReveal();
    }
}
