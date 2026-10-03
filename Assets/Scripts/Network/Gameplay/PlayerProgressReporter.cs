using FishNet.Object;
using BuddahGo.Match;
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
    public bool TryGetRacerId(out RacerId id) => RacerAuthority.TryGetId(this, out id);
    public int RacerIdValue => TryGetRacerId(out var id) ? id.Value : -1;
    public string RacerName => GetComponent<RacerIdentity>() is RacerIdentity identity && identity.IsAssigned
        ? identity.DisplayName : PlayerIdentity.FallbackName(OwnerId);
    private void Awake()
    {
        ResolveProgressDependencies();
    }

    private void Update()
    {
        if (!RacerAuthority.IsProgressAuthority(this))
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
        if (RacerAuthority.IsServerAI(this))
            ApplySplineProgressServer(_tracker.distanceOnTrack, _tracker.forwardDot, lap, _tracker.progress01, _tracker.PreviousProgress01);
        else
            ReportSplineProgressServerRpc(_tracker.distanceOnTrack, _tracker.forwardDot, lap, _tracker.progress01, _tracker.PreviousProgress01);
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
            LeaderboardManager.Instance.UnregisterPlayer(RacerIdValue);
        }

        _registeredWithLeaderboard = false;
    }

    [ServerRpc]
    private void ReportSplineProgressServerRpc(float distanceOnTrack, float forwardDot, int lap, float progress01, float previousProgress01)
    {
        ApplySplineProgressServer(distanceOnTrack, forwardDot, lap, progress01, previousProgress01);
    }

    private void ApplySplineProgressServer(float distanceOnTrack, float forwardDot, int lap, float progress01, float previousProgress01)
    {
        if (!IsServerInitialized || !TryGetRacerId(out var racerId)) return;
        if (!ResultAreaInteractionGate.ShouldProcessRaceProgress(gameObject))
            return;

        if (LeaderboardManager.Instance == null)
            return;

        ResolveProgressDependencies();
        if (_completionTracker == null)
            return;

        int lapsToFinish = GetConfiguredLapsToFinish();
        _completionTracker.UpdateCompletionFromLapAndSpline(lap, progress01, lapsToFinish, previousProgress01, forwardDot);

        // Preserve accepted Solo crossing times before the periodic observation or finish can record now.
        // No timestamp is received over RPC; LapProgress captured the authoritative local server clock.
        if (RacerAuthority.IsProgressAuthority(this) && IsServerInitialized && MatchRules.Current.IsSolo)
            _lapTracker?.ObserveAcceptedLapTimes(racerId, lapsToFinish);

        if (MatchServices.Clock != null)
            MatchServices.Timing?.ObserveCompletedLaps(racerId, Mathf.Clamp(lap - 1, 0, lapsToFinish), MatchServices.Clock.Now);

        RaceFinishManager finishManager = RaceFinishManager.Instance;
        if (finishManager != null && _completionTracker.ShouldMarkFinished(lapsToFinish))
        {
            finishManager.TryRegisterFinish(_completionTracker);
        }

        LeaderboardManager.Instance.ReportSplineProgress(
            racerId.Value,
            distanceOnTrack,
            forwardDot,
            lap,
            progress01,
            _completionTracker.FinalCompletionPercent,
            _completionTracker.IsFinished,
            _completionTracker.FinishOrder,
            _completionTracker.FinishServerTime);
        finishManager?.EvaluateRaceEndServer();
    }

    public void ReportCheckpoint(int checkpointId)
    {
        if (!RacerAuthority.IsProgressAuthority(this))
            return;

        if (_lapTracker == null)
            ResolveProgressDependencies();

        _lapTracker?.TryAdvanceCheckpoint(checkpointId);
    }

    #region Result area server entry points

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

    #endregion

    #region Progress dependencies and registration

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
                if (!TryGetRacerId(out var racerId)) yield break;
                LeaderboardManager.Instance.RegisterPlayer(racerId.Value, RacerName);
                _registeredWithLeaderboard = true;
                yield break;
            }

            yield return null;
        }
    }

    private static void DebugLog(string message)
    {
        if (NetDebug.EnableVerboseLog)
            GameLog.Verbose(message);
    }

    private int GetConfiguredLapsToFinish()
    {
        if (RaceFinishManager.Instance != null)
            return RaceFinishManager.Instance.LapsToFinish;

        return RaceRules.DefaultLapsToFinish;
    }

    #endregion

    #region Result area RPCs and local presentation

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
    #endregion

}
