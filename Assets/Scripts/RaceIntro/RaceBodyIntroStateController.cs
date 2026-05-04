using FishNet.Object;
using SteamMultiplayer.Network;
using UnityEngine;

[DisallowMultipleComponent]
public class RaceBodyIntroStateController : MonoBehaviour
{
    private const float SplineDiagnosticHeartbeatSeconds = 0.25f;
    private const float SplineDiagnosticBackwardMeters = 0.01f;
    private const float SplineDiagnosticOvershootFactor = 1.75f;
    private const float SplineDiagnosticLargeSnapMeters = 0.35f;

    private enum IntroPhase
    {
        None,
        IntroKinematic,
        HandoffWindow,
        RaceLive
    }

    [Header("References")]
    [SerializeField] private NetworkObject networkObject;
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private BuddahMovement movementController;
    [SerializeField] private Collider[] collidersToToggle;

    [Header("Handoff")]
    [SerializeField, Min(0f)] private float defaultHandoffLeadTime = 0.5f;
    [SerializeField, Min(0.001f)] private float velocitySampleDeltaSeconds = 0.02f;

    private IntroAssignmentData _assignment;
    private SplineIntroPath _assignedPath;
    private IntroPhase _phase = IntroPhase.None;
    private bool _hasAssignment;
    private bool _goApplied;
    private int _activeSequenceId = -1;
    private LaunchHandoffSnapshot _latestSplineSnapshot;
    private IntroRuntimeState _runtimeState = IntroRuntimeState.Idle;
    private bool _visualStarted;
    private bool _authoritativeGoIssued;
    private bool _introShellEntered;
    private bool _authoritativeGoPendingTransition;
    private double _authoritativeGoIssuedNetworkTime;
    private double _authoritativeScheduledGoNetworkTime;
    private double _resolvedIntroStartNetworkTime = -1d;
    private double _resolvedGoNetworkTime = -1d;
    private Vector3 _lastSplineDiagnosticPosition;
    private double _lastSplineDiagnosticNetworkTime;
    private bool _hasSplineDiagnosticSample;
    private float _lastSplineDiagnosticLogTime = float.NegativeInfinity;
    private IntroPhase _lastLoggedIntroPhase = IntroPhase.None;

    public int OwnerId => networkObject != null ? networkObject.OwnerId : -1;
    public NetworkObject NetworkObject => networkObject;
    public Rigidbody TargetRigidbody => targetRigidbody;
    public bool IsIntroActive => _phase == IntroPhase.IntroKinematic || _phase == IntroPhase.HandoffWindow;
    public int ActiveSequenceId => _activeSequenceId;
    public bool HasAssignment => _hasAssignment;

    private void Awake()
    {
        ResolveReferences();
        AutoAssignMissingReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
        AutoAssignMissingReferences();
    }

    private void Update()
    {
        if (!_hasAssignment || _assignedPath == null || targetRigidbody == null)
            return;

        double now = GetSmoothedNetworkTimeSeconds();
        TryCompleteAuthoritativeGoTransition(now);

        if (_goApplied)
            return;

        if (!_visualStarted)
            return;

        if (!TryGetResolvedTiming(out IntroSequenceTiming timing))
            return;

        double handoffStartTime = timing.GoNetworkTime - GetHandoffLeadTime();
        bool inHandoffWindow = now >= handoffStartTime;
        _phase = inHandoffWindow ? IntroPhase.HandoffWindow : IntroPhase.IntroKinematic;
        if (_authoritativeGoIssued)
            _runtimeState = IntroRuntimeState.WaitingForGo;
        else
            _runtimeState = inHandoffWindow ? IntroRuntimeState.WaitingForGo : IntroRuntimeState.IntroRunning;

        LogIntroPhaseIfChanged(now);
    }

    private void FixedUpdate()
    {
        if (!_hasAssignment || _assignedPath == null || targetRigidbody == null || _goApplied || !_visualStarted)
            return;

        double now = GetSmoothedNetworkTimeSeconds();
        TryCompleteAuthoritativeGoTransition(now);
        if (_goApplied)
            return;

        if (!TryGetResolvedTiming(out IntroSequenceTiming timing))
            return;

        DriveSplinePose(IntroTimeUtility.GetClampedIntroNetworkTime(timing, now));
    }

    // Samples the spline pose at the current render-time network tick (sub-tick precise).
    // Allows the visual root to render continuously while the rigidbody keeps its single-writer
    // fixed-step position assignment in DriveSplinePose.
    public bool TrySampleVisualPoseAtRenderTime(out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;

        if (!_hasAssignment || _assignedPath == null || _goApplied || !_visualStarted)
            return false;

        if (!TryGetResolvedTiming(out IntroSequenceTiming timing))
            return false;

        double clampedTime = IntroTimeUtility.GetClampedIntroNetworkTime(timing, GetSmoothedNetworkTimeSeconds());
        SampleSnapshotAtTime(clampedTime, out LaunchHandoffSnapshot snapshot);
        position = snapshot.Position;
        rotation = snapshot.Rotation;
        return true;
    }

    public void ApplyIntroAssignment(IntroAssignmentData assignment, SplineIntroPath splinePath)
    {
        ResolveReferences();
        if (targetRigidbody == null || splinePath == null)
            return;

        if (_hasAssignment && assignment.sequenceId < _activeSequenceId)
            return;

        _assignment = assignment;
        _assignedPath = splinePath;
        _activeSequenceId = assignment.sequenceId;
        _hasAssignment = true;
        _goApplied = false;
        _phase = IntroPhase.None;
        _runtimeState = IntroRuntimeState.AssignmentsReceived;
        _visualStarted = false;
        _authoritativeGoIssued = false;
        _introShellEntered = false;
        _authoritativeGoPendingTransition = false;
        _authoritativeGoIssuedNetworkTime = 0d;
        _authoritativeScheduledGoNetworkTime = -1d;
        _resolvedIntroStartNetworkTime = -1d;
        _resolvedGoNetworkTime = -1d;
        _hasSplineDiagnosticSample = false;
        _lastSplineDiagnosticLogTime = float.NegativeInfinity;
        _lastLoggedIntroPhase = IntroPhase.None;
        Debug.Log(
            $"[IntroState][Body:{name}] Assignment prepared seq={assignment.sequenceId} ownerId={OwnerId} " +
            $"isLocalOwner={(networkObject != null && networkObject.IsOwner)} objId={(networkObject != null ? networkObject.ObjectId : -1)} " +
            $"spline={splinePath.SplineId} introStart={assignment.introStartNetworkTime:0.000} go={assignment.goNetworkTime:0.000} " +
            $"now={GetSmoothedNetworkTimeSeconds():0.000} shellEntered={_introShellEntered} visualStarted={_visualStarted}");
        Debug.Log($"[IntroVisual][Body:{name}] Waiting visual start seq={assignment.sequenceId}; no intro shell enter and no visible snap during prepared phase.");
        _runtimeState = IntroRuntimeState.IntroPrepared;
    }

    public void ApplyVisualStart(int sequenceId, double introStartNetworkTime, double goNetworkTime)
    {
        if (!_hasAssignment || sequenceId != _activeSequenceId || _assignedPath == null || targetRigidbody == null)
        {
            Debug.Log($"[SequenceGuard][Body:{name}] Ignored visual start seq={sequenceId} active={_activeSequenceId} hasAssignment={_hasAssignment}");
            return;
        }

        if (_goApplied)
        {
            Debug.Log($"[SequenceGuard][Body:{name}] Ignored late visual start after go seq={sequenceId} goApplied={_goApplied} authoritativeGo={_authoritativeGoIssued}");
            return;
        }

        if (_visualStarted)
        {
            Debug.Log($"[IntroVisual][Body:{name}] Duplicate visual start seq={sequenceId} ignored (already started).");
            return;
        }

        IntroSequenceTiming timing = new IntroSequenceTiming(_activeSequenceId, introStartNetworkTime, goNetworkTime);
        if (!timing.IsValid)
        {
            Debug.LogWarning($"[IntroVisual][Body:{name}] Rejected invalid visual start seq={sequenceId} introStart={introStartNetworkTime:0.000} go={goNetworkTime:0.000}");
            return;
        }

        _resolvedIntroStartNetworkTime = introStartNetworkTime;
        _resolvedGoNetworkTime = goNetworkTime;
        _authoritativeScheduledGoNetworkTime = goNetworkTime;
        EnterIntroState();
        _visualStarted = true;
        double now = GetSmoothedNetworkTimeSeconds();
        double driveTime = IntroTimeUtility.GetClampedIntroNetworkTime(timing, System.Math.Max(now, introStartNetworkTime));
        DriveSplinePose(driveTime);
        _runtimeState = IntroTimeUtility.HasReachedGo(timing, now)
            ? IntroRuntimeState.WaitingForGo
            : IntroRuntimeState.VisualStarted;
        Debug.Log($"[IntroVisual][Body:{name}] Visual start seq={sequenceId} now={now:0.000} seekTime={driveTime:0.000} introStart={introStartNetworkTime:0.000} go={goNetworkTime:0.000}");
    }

    public void ApplyAuthoritativeGo(int sequenceId, double scheduledGoNetworkTime, double goIssuedNetworkTime)
    {
        if (!_hasAssignment || sequenceId != _activeSequenceId)
        {
            Debug.Log($"[SequenceGuard][Body:{name}] Ignored authoritative go seq={sequenceId} active={_activeSequenceId} hasAssignment={_hasAssignment}");
            return;
        }

        if (_goApplied)
        {
            Debug.Log($"[IntroGo][Body:{name}] Duplicate authoritative go ignored seq={sequenceId} goIssued={goIssuedNetworkTime:0.000}");
            return;
        }

        if (!_visualStarted)
            Debug.Log($"[LateJoin][Body:{name}] Authoritative go arrived before visual start seq={sequenceId} goIssued={goIssuedNetworkTime:0.000}");

        double resolvedScheduledGo = scheduledGoNetworkTime >= 0d ? scheduledGoNetworkTime : _resolvedGoNetworkTime;
        _authoritativeScheduledGoNetworkTime = resolvedScheduledGo;
        if (resolvedScheduledGo >= 0d)
            _resolvedGoNetworkTime = resolvedScheduledGo;
        _authoritativeGoIssuedNetworkTime = goIssuedNetworkTime;
        _authoritativeGoIssued = true;
        _authoritativeGoPendingTransition = true;
        _runtimeState = IntroRuntimeState.AuthoritativeGoIssued;
        double now = GetSmoothedNetworkTimeSeconds();
        bool transitionDelayed = now < resolvedScheduledGo;
        Debug.Log(
            $"[IntroGo][Body:{name}] Authoritative go approved seq={sequenceId} goIssuedNow={goIssuedNetworkTime:0.000} " +
            $"scheduledGoTime={resolvedScheduledGo:0.000} rpcScheduledGo={scheduledGoNetworkTime:0.000} localNow={now:0.000} " +
            $"delayUntilScheduledGo={transitionDelayed}");

        TryCompleteAuthoritativeGoTransition(now);
    }

    public void EnterIntroState()
    {
        if (_introShellEntered)
            return;

        ResolveReferences();
        _introShellEntered = true;
        Debug.Log(
            $"[IntroVisual][Body:{name}] EnterIntroState shell seq={_activeSequenceId} ownerId={OwnerId} isLocalOwner={(networkObject != null && networkObject.IsOwner)} " +
            $"rbKinematicBefore={(targetRigidbody != null && targetRigidbody.isKinematic)}");

        if (movementController != null)
        {
            movementController.SetIntroControlActive(true);
            movementController.SetExternalKinematicControlActive(true);
        }

        if (targetRigidbody != null)
            targetRigidbody.isKinematic = true;

        SetCollisionsEnabled(false);
    }

    public void ForceExitIntroState()
    {
        _assignment = default;
        _assignedPath = null;
        _phase = IntroPhase.None;
        _hasAssignment = false;
        _goApplied = false;
        _latestSplineSnapshot = default;
        _runtimeState = IntroRuntimeState.Cancelled;
        _visualStarted = false;
        _authoritativeGoIssued = false;
        _introShellEntered = false;
        _authoritativeGoPendingTransition = false;
        _authoritativeGoIssuedNetworkTime = 0d;
        _authoritativeScheduledGoNetworkTime = -1d;
        _resolvedIntroStartNetworkTime = -1d;
        _resolvedGoNetworkTime = -1d;
        _hasSplineDiagnosticSample = false;
        _lastSplineDiagnosticLogTime = float.NegativeInfinity;
        _lastLoggedIntroPhase = IntroPhase.None;
        bool isLocalOwner = networkObject != null && networkObject.IsOwner;

        if (movementController != null)
        {
            movementController.SetExternalKinematicControlActive(false);
            movementController.SetIntroControlActive(false);
        }

        if (targetRigidbody != null)
            targetRigidbody.isKinematic = !isLocalOwner;

        SetCollisionsEnabled(true);
    }

    private void CompleteGoTransition()
    {
        if (_goApplied || targetRigidbody == null)
            return;

        _goApplied = true;
        _authoritativeGoPendingTransition = false;
        _phase = IntroPhase.RaceLive;

        bool isLocalOwner = networkObject != null && networkObject.IsOwner;
        double resolvedHandoffTime = _resolvedGoNetworkTime >= 0d
            ? System.Math.Max(_resolvedIntroStartNetworkTime, _resolvedGoNetworkTime)
            : GetSmoothedNetworkTimeSeconds();
        SampleSnapshotAtTime(resolvedHandoffTime, out LaunchHandoffSnapshot snapshot);
        _latestSplineSnapshot = snapshot;
        Debug.Log(
            $"[IntroGo][Body:{name}] CompleteGoTransition seq={_activeSequenceId} ownerId={OwnerId} isLocalOwner={isLocalOwner} " +
            $"goIssuedNow={_authoritativeGoIssuedNetworkTime:0.000} scheduledGoTime={_resolvedGoNetworkTime:0.000} handoffSampleTime={resolvedHandoffTime:0.000} " +
            $"snapshotPos={snapshot.Position} snapshotSpeed={snapshot.Velocity.magnitude:0.00} " +
            $"introActive={IsIntroActive}");

        // Phase 6 — owner-initiated BeginLaunchHandoff retired (Section 11.1
        // option a). Race-start lock is now driven by server-side SyncVar
        // (RoomStateManager._raceStartTick) — Area 3 wires spline-complete
        // notification + tick-stamped unlock. Existing intro-state cleanup
        // (SetExternalKinematicControlActive + SetIntroControlActive below)
        // still runs here; the handoff-trigger branch is the only deletion.
        _runtimeState = IntroRuntimeState.AuthoritativeHandoffApplied;
        Debug.Log($"[IntroHandoff][Body:{name}] Authoritative go applied seq={_activeSequenceId} isLocalOwner={isLocalOwner} (Phase 6: owner handoff RPC chain retired).");

        if (movementController != null && !isLocalOwner)
            movementController.SetExternalKinematicControlActive(false);

        if (movementController != null)
            movementController.SetIntroControlActive(false);

        if (targetRigidbody != null && !isLocalOwner)
            targetRigidbody.isKinematic = true;

        SetCollisionsEnabled(true);
        _hasAssignment = false;
        _visualStarted = false;
        _introShellEntered = false;
    }

    private void TryCompleteAuthoritativeGoTransition(double currentNetworkTime)
    {
        if (!_hasAssignment || !_authoritativeGoIssued || !_authoritativeGoPendingTransition || _goApplied)
            return;

        if (!_visualStarted)
            return;

        double scheduledGoTime = _authoritativeScheduledGoNetworkTime >= 0d
            ? _authoritativeScheduledGoNetworkTime
            : _resolvedGoNetworkTime;
        if (scheduledGoTime < 0d)
            return;

        if (currentNetworkTime < scheduledGoTime)
            return;

        Debug.Log(
            $"[IntroGo][Body:{name}] Scheduled go reached seq={_activeSequenceId} localNow={currentNetworkTime:0.000} " +
            $"scheduledGoTime={scheduledGoTime:0.000} issuingCompleteTransition=true visualStarted={_visualStarted}");
        CompleteGoTransition();
    }

    private void DriveSplinePose(double networkTime)
    {
        SampleSnapshotAtTime(networkTime, out LaunchHandoffSnapshot snapshot);
        _latestSplineSnapshot = snapshot;
        if (targetRigidbody == null)
            return;

        float normalizedT = GetNormalizedDistanceT(networkTime);
        Vector3 prePosition = targetRigidbody.position;

        // Intro spline motion should be single-writer and exact; MovePosition/MoveRotation
        // adds another interpolation layer that shows up as visible jitter under prediction.
        targetRigidbody.position = snapshot.Position;
        targetRigidbody.rotation = snapshot.Rotation;

        MaybeLogSplineDriveDiagnostics(networkTime, normalizedT, prePosition, snapshot);
    }

    private void SampleSnapshotAtTime(double networkTime, out LaunchHandoffSnapshot snapshot)
    {
        float t = GetNormalizedDistanceT(networkTime);
        Vector3 position = _assignedPath.EvaluatePosition(t);
        Vector3 tangent = GetResolvedForwardAtT(t);
        Quaternion rotation = Quaternion.LookRotation(tangent, Vector3.up);
        float speed = GetIntroSpeedMetersPerSecond();
        Vector3 velocity = tangent * speed;
        Vector3 angularVelocity = EstimateAngularVelocity(networkTime, t, rotation);

        snapshot = new LaunchHandoffSnapshot
        {
            Position = position,
            Rotation = rotation,
            Velocity = velocity,
            AngularVelocity = angularVelocity,
            Forward = tangent
        };
    }

    private float GetNormalizedDistanceT(double networkTime)
    {
        if (_assignedPath == null)
            return 0f;

        if (_resolvedIntroStartNetworkTime < 0d)
            return 0f;

        float elapsedSeconds = Mathf.Max(0f, (float)(networkTime - _resolvedIntroStartNetworkTime));
        float distance = elapsedSeconds * GetIntroSpeedMetersPerSecond();
        float totalLength = Mathf.Max(0.0001f, _assignedPath.TotalLength);
        return _assignedPath.TAtDistance(Mathf.Min(distance, totalLength));
    }

    private float GetHandoffLeadTime()
    {
        return _assignment.handoffLeadTime > 0f ? _assignment.handoffLeadTime : defaultHandoffLeadTime;
    }

    private float GetIntroSpeedMetersPerSecond()
    {
        return Mathf.Max(0.1f, _assignment.introSpeedMetersPerSecond);
    }

    private Vector3 EstimateAngularVelocity(double networkTime, float currentT, Quaternion currentRotation)
    {
        double previousTime = networkTime - velocitySampleDeltaSeconds;
        float previousT = Mathf.Min(currentT, GetNormalizedDistanceT(previousTime));
        Quaternion previousRotation = Quaternion.LookRotation(GetResolvedForwardAtT(previousT), Vector3.up);
        Quaternion deltaRotation = currentRotation * Quaternion.Inverse(previousRotation);
        deltaRotation.ToAngleAxis(out float angleDegrees, out Vector3 axis);
        if (float.IsNaN(axis.x) || axis.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        if (angleDegrees > 180f)
            angleDegrees -= 360f;

        float radiansPerSecond = (angleDegrees * Mathf.Deg2Rad) / Mathf.Max(0.0001f, velocitySampleDeltaSeconds);
        return axis.normalized * radiansPerSecond;
    }

    private double GetSmoothedNetworkTimeSeconds()
    {
        return IntroTimeUtility.GetNetworkTimeSeconds();
    }

    private void LogIntroPhaseIfChanged(double networkTime)
    {
        if (_lastLoggedIntroPhase == _phase)
            return;

        _lastLoggedIntroPhase = _phase;
        Debug.Log(
            $"[IntroSplineDiag][Body:{name}] Phase -> {_phase} seq={_activeSequenceId} owner={(networkObject != null && networkObject.IsOwner)} " +
            $"net={networkTime:0.000} introStart={_resolvedIntroStartNetworkTime:0.000} go={_resolvedGoNetworkTime:0.000}");
    }

    private void MaybeLogSplineDriveDiagnostics(double networkTime, float normalizedT, Vector3 prePosition, LaunchHandoffSnapshot snapshot)
    {
        if (networkObject == null || !networkObject.IsOwner)
            return;

        float expectedStep = GetIntroSpeedMetersPerSecond() * Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        Vector3 appliedDelta = snapshot.Position - prePosition;
        float appliedDistance = appliedDelta.magnitude;
        float appliedForward = Vector3.Dot(appliedDelta, snapshot.Forward);

        bool periodic = Time.unscaledTime - _lastSplineDiagnosticLogTime >= SplineDiagnosticHeartbeatSeconds;
        bool backwardApplied = appliedForward < -SplineDiagnosticBackwardMeters;
        bool largeAppliedSnap = appliedDistance > Mathf.Max(SplineDiagnosticLargeSnapMeters, expectedStep * SplineDiagnosticOvershootFactor);

        Vector3 sampledDelta = Vector3.zero;
        float sampledDistance = 0f;
        float sampledForward = 0f;
        bool backwardSampled = false;
        bool largeSampledStep = false;

        if (_hasSplineDiagnosticSample)
        {
            sampledDelta = snapshot.Position - _lastSplineDiagnosticPosition;
            sampledDistance = sampledDelta.magnitude;
            sampledForward = Vector3.Dot(sampledDelta, snapshot.Forward);
            float sampledExpectedStep = GetIntroSpeedMetersPerSecond() * Mathf.Max((float)(networkTime - _lastSplineDiagnosticNetworkTime), Time.fixedDeltaTime);
            backwardSampled = sampledForward < -SplineDiagnosticBackwardMeters;
            largeSampledStep = sampledDistance > Mathf.Max(SplineDiagnosticLargeSnapMeters, sampledExpectedStep * SplineDiagnosticOvershootFactor);
        }

        if (!periodic && !backwardApplied && !largeAppliedSnap && !backwardSampled && !largeSampledStep)
        {
            _lastSplineDiagnosticPosition = snapshot.Position;
            _lastSplineDiagnosticNetworkTime = networkTime;
            _hasSplineDiagnosticSample = true;
            return;
        }

        _lastSplineDiagnosticLogTime = Time.unscaledTime;
        _lastSplineDiagnosticPosition = snapshot.Position;
        _lastSplineDiagnosticNetworkTime = networkTime;
        _hasSplineDiagnosticSample = true;

        Debug.Log(
            $"[IntroSplineDiag][Body:{name}] seq={_activeSequenceId} phase={_phase} t={normalizedT:0.000} net={networkTime:0.000} " +
            $"prePos={prePosition} snapPos={snapshot.Position} appliedDist={appliedDistance:0.000} appliedForward={appliedForward:0.000} " +
            $"sampleDist={sampledDistance:0.000} sampleForward={sampledForward:0.000} expectedStep={expectedStep:0.000} " +
            $"flags(backApplied={backwardApplied}, snapApplied={largeAppliedSnap}, backSample={backwardSampled}, snapSample={largeSampledStep})");
    }

    private bool TryGetResolvedTiming(out IntroSequenceTiming timing)
    {
        timing = new IntroSequenceTiming(_activeSequenceId, _resolvedIntroStartNetworkTime, _resolvedGoNetworkTime);
        return timing.IsValid;
    }

    private Vector3 GetResolvedForwardAtT(float t)
    {
        Vector3 tangent = _assignedPath.EvaluateTangent(t);
        tangent.y = 0f;
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = transform.forward.sqrMagnitude > 0.0001f ? transform.forward : Vector3.forward;

        tangent.Normalize();
        return tangent;
    }

    private void ResolveReferences()
    {
        if (networkObject == null)
            networkObject = GetComponent<NetworkObject>();

        if (targetRigidbody == null)
            targetRigidbody = GetComponent<Rigidbody>();

        if (movementController == null)
            movementController = GetComponent<BuddahMovement>();
    }

    private void SetCollisionsEnabled(bool enabled)
    {
        if (collidersToToggle == null)
            return;

        for (int i = 0; i < collidersToToggle.Length; i++)
        {
            if (collidersToToggle[i] != null)
                collidersToToggle[i].enabled = enabled;
        }
    }

    private void AutoAssignMissingReferences()
    {
        if (collidersToToggle == null || collidersToToggle.Length == 0)
            collidersToToggle = GetComponentsInChildren<Collider>(true);
    }
}
