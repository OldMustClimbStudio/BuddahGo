using System;
using System.Collections.Generic;
using FishNet.Object;
using SteamMultiplayer.Network;
using SteamMultiplayer.UI;
using UnityEngine;
using UnityEngine.Playables;

public class IntroSequenceManager : NetworkBehaviour
{
    [Serializable]
    public class IntroLayout
    {
        public string name;
        public int playerCount;
        public IntroSlot[] slots;
    }

    [Serializable]
    private struct SlotAssignment
    {
        public RaceBodyIntroStateController body;
        public IntroSlot slot;
    }

    private enum IntroAuthorityState
    {
        Idle,
        AssignmentsBroadcast,
        GoBroadcast,
        Cancelled
    }

    [Header("Layouts")]
    [SerializeField] private IntroLayout[] layouts;

    [Header("Sequence")]
    [SerializeField, Min(0.1f)] private float fallbackIntroDuration = 3f;
    // Phase 6 — total time T (seconds) for buddah to complete spline traversal.
    // v_max = 2L/T (linear deceleration formula per contract Section 3.1).
    // FormerlySerializedAs preserves scene refs from the pre-Phase-6 constant-velocity field.
    [SerializeField, Min(0.1f)]
    [UnityEngine.Serialization.FormerlySerializedAs("introSpeedMetersPerSecond")]
    private float introTraversalTimeSeconds = 18f;
    [SerializeField, Min(0f)] private float handoffLeadSeconds = 0.5f;
    [SerializeField, Min(0f)] private float introLeadInSeconds = 1f;
    [SerializeField] private bool randomizeSlots = true;
    [SerializeField] private int deterministicShuffleSeed = 20260404;

    [Header("Optional Scene Hooks")]
    [SerializeField] private PlayableDirector timelineDirector;
    [SerializeField] private RoomStateManager roomStateManager;
    [SerializeField] private IntroClientController introClientController;

    private readonly List<SlotAssignment> _slotAssignments = new List<SlotAssignment>();
    private IntroAuthorityState _authorityState = IntroAuthorityState.Idle;
    private int _lastPreparedPlayerCount = -1;
    private int _activeSequenceId;
    private bool _clientIntroVisualArmed;
    private bool _clientIntroVisualStarted;
    private double _clientIntroStartNetworkTime;
    private double _clientAssignedGoNetworkTime;
    private bool _serverIntroVisualsBroadcast;
    private double _serverAssignedIntroStartNetworkTime;
    private bool _serverGoIssued;
    private double _serverAssignedGoNetworkTime;
    private double _serverConfiguredIntroDurationSeconds;
    private IntroRuntimeState _clientRuntimeState = IntroRuntimeState.Idle;
    private IntroRuntimeState _serverRuntimeState = IntroRuntimeState.Idle;

    private void Start()
    {
        TryResolveRoomStateManager();
        EnsureIntroClientController();
        ResetTimelineToIntroStart();
    }

    private void Update()
    {
        TryResolveRoomStateManager();
        TryStartLocalIntroVisuals();
        if (roomStateManager == null)
            return;

        if (!roomStateManager.IsRaceSceneLoadedLocally)
        {
            ResetAuthorityState();
            return;
        }

        if (!IsServerInitialized)
            return;

        if (roomStateManager.IsRaceCountdownActive)
        {
            if (_authorityState == IntroAuthorityState.Idle || _authorityState == IntroAuthorityState.Cancelled)
                StartIntroFromRoomCountdown();
            return;
        }

        if (_authorityState == IntroAuthorityState.AssignmentsBroadcast && !_serverIntroVisualsBroadcast && IsReadyToScheduleVisualsServer())
        {
            ScheduleAndBroadcastVisualStartServer();
        }

        if (_authorityState == IntroAuthorityState.AssignmentsBroadcast && _serverIntroVisualsBroadcast && !_serverGoIssued)
        {
            IntroSequenceTiming timing = new IntroSequenceTiming(_activeSequenceId, _serverAssignedIntroStartNetworkTime, _serverAssignedGoNetworkTime);
            if (IntroTimeUtility.HasReachedGo(timing, IntroTimeUtility.GetNetworkTimeSeconds()))
                TriggerGoAndHandoff();
        }

        if (_authorityState == IntroAuthorityState.AssignmentsBroadcast
            && !_serverIntroVisualsBroadcast
            && roomStateManager != null
            && !roomStateManager.IsPregameCountdownCompleted)
            CancelIntroAndReleaseAssignments();
    }

    public void PrepareAssignments()
    {
        _slotAssignments.Clear();

        RaceBodyIntroStateController[] bodies = FindIntroBodies();
        Array.Sort(bodies, CompareBodiesForDeterminism);

        IntroLayout layout = ResolveLayout(bodies.Length);
        if (layout == null || layout.slots == null || layout.slots.Length < bodies.Length)
        {
            Debug.LogWarning($"[IntroSequenceManager] Missing layout for {bodies.Length} players.");
            _lastPreparedPlayerCount = -1;
            return;
        }

        List<IntroSlot> candidateSlots = new List<IntroSlot>(layout.slots);
        if (randomizeSlots)
            ShuffleSlots(candidateSlots, deterministicShuffleSeed);

        int count = Mathf.Min(bodies.Length, candidateSlots.Count);
        for (int i = 0; i < count; i++)
        {
            RaceBodyIntroStateController body = bodies[i];
            if (body == null || candidateSlots[i] == null)
                continue;

            _slotAssignments.Add(new SlotAssignment
            {
                body = body,
                slot = candidateSlots[i]
            });
        }

        _lastPreparedPlayerCount = bodies.Length;
    }

    public void BeginIntroSequence(float duration)
    {
        if (!IsServerInitialized || _authorityState == IntroAuthorityState.AssignmentsBroadcast || _authorityState == IntroAuthorityState.GoBroadcast)
            return;

        if (NeedsAssignmentRefresh())
            PrepareAssignments();

        if (_slotAssignments.Count == 0)
            return;

        EnsureIntroClientController();
        _activeSequenceId++;
        _serverGoIssued = false;
        _serverIntroVisualsBroadcast = false;
        _serverAssignedIntroStartNetworkTime = -1d;
        _serverAssignedGoNetworkTime = -1d;
        _serverConfiguredIntroDurationSeconds = GetConfiguredIntroDurationSecondsServer();
        _serverRuntimeState = IntroRuntimeState.AssignmentsReceived;

        List<IntroAssignmentData> networkAssignments = new List<IntroAssignmentData>(_slotAssignments.Count);

        for (int i = 0; i < _slotAssignments.Count; i++)
        {
            SlotAssignment assignment = _slotAssignments[i];
            if (assignment.body == null || assignment.slot == null)
                continue;

            networkAssignments.Add(BuildAssignmentData(assignment.body, assignment.slot));
        }

        if (networkAssignments.Count == 0)
            return;

        Debug.Log(
            $"[IntroState][Server] Assignments broadcast seq={_activeSequenceId} introDuration={_serverConfiguredIntroDurationSeconds:0.000} " +
            $"requestedDuration={duration:0.000} waitingForAllVisualPrepared=true");
        BroadcastAssignmentsObserversRpc(networkAssignments.ToArray());
        _authorityState = IntroAuthorityState.AssignmentsBroadcast;
    }

    public void TriggerGoAndHandoff()
    {
        if (!IsServerInitialized || _authorityState != IntroAuthorityState.AssignmentsBroadcast || !_serverIntroVisualsBroadcast)
            return;

        _serverGoIssued = true;
        _serverRuntimeState = IntroRuntimeState.AuthoritativeGoIssued;
        double goIssuedNow = IntroTimeUtility.GetNetworkTimeSeconds();
        roomStateManager?.MarkAuthoritativeGoIssuedServer();
        Debug.Log($"[IntroGo][Server] Authoritative go issued seq={_activeSequenceId} goIssuedNow={goIssuedNow:0.000} scheduledGoTime={_serverAssignedGoNetworkTime:0.000}");
        NotifyGoObserversRpc(_activeSequenceId, _serverAssignedGoNetworkTime, goIssuedNow);
        _authorityState = IntroAuthorityState.GoBroadcast;
    }

    public void CancelIntroAndReleaseAssignments()
    {
        if (!IsServerInitialized || _authorityState != IntroAuthorityState.AssignmentsBroadcast)
            return;

        NotifyCancelObserversRpc(_activeSequenceId);
        _authorityState = IntroAuthorityState.Cancelled;
        _slotAssignments.Clear();
    }

    public void HandleTimelineIntroStartSignal()
    {
        EnsureIntroClientController();
        introClientController?.HandleTimelineVisualIntroSignal();
    }

    public void HandleTimelineGoSignal()
    {
        EnsureIntroClientController();
        introClientController?.HandleTimelineVisualGameplayCameraSignal();
    }

    public void HandleTimelineCountdownSignal()
    {
        EnsureIntroClientController();
        introClientController?.HandleTimelineVisualCountdownSignal();
    }

    public void PlayTimelineIfAssigned()
    {
        if (timelineDirector != null)
            timelineDirector.Play();
    }

    public bool TryPrepareLocalVisual(int sequenceId)
    {
        if (sequenceId < _activeSequenceId)
        {
            Debug.Log($"[SequenceGuard][Client] Ignored stale visual prepare seq={sequenceId} active={_activeSequenceId}");
            return false;
        }

        _activeSequenceId = sequenceId;
        _clientRuntimeState = IntroRuntimeState.VisualPrepared;

        if (timelineDirector != null)
        {
            timelineDirector.time = 0d;
            timelineDirector.Evaluate();
            timelineDirector.Stop();
            Debug.Log($"[IntroVisual][Client] Visual prepared seq={sequenceId} preload=timeline-reset-only");
        }
        else
        {
            Debug.Log($"[IntroVisual][Client] Visual prepared seq={sequenceId} without timeline");
        }

        return true;
    }

    private void StartIntroFromRoomCountdown()
    {
        BeginIntroSequence(fallbackIntroDuration);
    }

    private bool NeedsAssignmentRefresh()
    {
        RaceBodyIntroStateController[] bodies = FindIntroBodies();
        return bodies.Length != _lastPreparedPlayerCount;
    }

    private void TryResolveRoomStateManager()
    {
        if (roomStateManager == null)
            roomStateManager = RoomStateManager.Instance;
    }

    private void EnsureIntroClientController()
    {
        if (introClientController == null)
            introClientController = FindFirstObjectByType<IntroClientController>(FindObjectsInactive.Include);

        if (introClientController == null)
            introClientController = gameObject.AddComponent<IntroClientController>();
    }

    private void ResetAuthorityState()
    {
        _authorityState = IntroAuthorityState.Idle;
        _lastPreparedPlayerCount = -1;
        _slotAssignments.Clear();
        _clientIntroVisualArmed = false;
        _clientIntroVisualStarted = false;
        _clientIntroStartNetworkTime = -1d;
        _clientAssignedGoNetworkTime = -1d;
        _serverIntroVisualsBroadcast = false;
        _serverAssignedIntroStartNetworkTime = -1d;
        _serverAssignedGoNetworkTime = -1d;
        _serverGoIssued = false;
        _serverConfiguredIntroDurationSeconds = 0d;
        _clientRuntimeState = IntroRuntimeState.Idle;
        _serverRuntimeState = IntroRuntimeState.Idle;
        ResetTimelineToIntroStart();
    }

    private IntroLayout ResolveLayout(int playerCount)
    {
        if (layouts == null)
            return null;

        for (int i = 0; i < layouts.Length; i++)
        {
            IntroLayout layout = layouts[i];
            if (layout != null && layout.playerCount == playerCount)
                return layout;
        }

        return null;
    }

    private IntroAssignmentData BuildAssignmentData(RaceBodyIntroStateController body, IntroSlot slot)
    {
        slot.BindPathToSlot();
        SplineIntroPath splinePath = slot.IntroPath as SplineIntroPath;
        NetworkObject networkObject = body != null ? body.GetComponent<NetworkObject>() : null;
        return new IntroAssignmentData
        {
            sequenceId = _activeSequenceId,
            playerOwnerId = body != null ? body.OwnerId : -1,
            playerObjectId = networkObject != null ? networkObject.ObjectId : -1,
            slotIndex = slot.SlotIndex,
            splineId = splinePath != null ? splinePath.SplineId : string.Empty,
            introStartNetworkTime = -1d,
            introTraversalTimeSeconds = introTraversalTimeSeconds,
            goNetworkTime = -1d,
            handoffLeadTime = handoffLeadSeconds
        };
    }

    [ObserversRpc(BufferLast = true)]
    private void BroadcastAssignmentsObserversRpc(IntroAssignmentData[] assignments)
    {
        if (assignments == null || assignments.Length == 0)
            return;

        int sequenceId = assignments[0].sequenceId;
        if (sequenceId < _activeSequenceId)
        {
            Debug.Log($"[SequenceGuard][Client] Ignored stale assignment broadcast seq={sequenceId} active={_activeSequenceId}");
            return;
        }

        EnsureIntroClientController();
        _clientIntroVisualStarted = false;
        _clientIntroVisualArmed = false;
        _clientRuntimeState = IntroRuntimeState.AssignmentsReceived;
        _activeSequenceId = sequenceId;
        _clientIntroStartNetworkTime = -1d;
        _clientAssignedGoNetworkTime = -1d;
        ResetTimelineToIntroStart();
        Debug.Log(
            $"[IntroState][Client] Assignment broadcast accepted seq={sequenceId} count={assignments.Length}; " +
            "bodies stay prepared-only until visual start.");

        introClientController?.ReceiveAssignments(assignments);
    }

    [ObserversRpc(BufferLast = true)]
    private void BroadcastIntroVisualsStartObserversRpc(int sequenceId, double introStartNetworkTime, double goNetworkTime)
    {
        if (sequenceId < _activeSequenceId)
        {
            Debug.Log($"[SequenceGuard][Client] Ignored stale visual start seq={sequenceId} active={_activeSequenceId}");
            return;
        }

        _activeSequenceId = sequenceId;
        _clientIntroVisualStarted = false;
        _clientIntroVisualArmed = true;
        _clientIntroStartNetworkTime = introStartNetworkTime;
        _clientAssignedGoNetworkTime = goNetworkTime;
        _clientRuntimeState = IntroRuntimeState.VisualStarted;
        introClientController?.ApplyVisualStart(sequenceId, introStartNetworkTime, goNetworkTime);
        Debug.Log($"[IntroVisual][Client] Visual start armed seq={sequenceId} introStart={introStartNetworkTime:0.000} go={goNetworkTime:0.000}");
    }

    [ObserversRpc(BufferLast = true)]
    private void NotifyGoObserversRpc(int sequenceId, double scheduledGoNetworkTime, double goIssuedNetworkTime)
    {
        if (sequenceId < _activeSequenceId)
        {
            Debug.Log($"[SequenceGuard][Client] Ignored stale authoritative go seq={sequenceId} active={_activeSequenceId}");
            return;
        }

        EnsureIntroClientController();
        _activeSequenceId = sequenceId;
        _clientAssignedGoNetworkTime = scheduledGoNetworkTime;
        Debug.Log(
            $"[IntroGo][Client] Received authoritative go seq={sequenceId} goIssuedNow={goIssuedNetworkTime:0.000} " +
            $"scheduledGoTime={scheduledGoNetworkTime:0.000} localNow={IntroTimeUtility.GetNetworkTimeSeconds():0.000}");
        _clientRuntimeState = IntroRuntimeState.AuthoritativeGoIssued;
        introClientController?.ApplyAuthoritativeGo(sequenceId, scheduledGoNetworkTime, goIssuedNetworkTime);
    }

    [ObserversRpc]
    private void NotifyCancelObserversRpc(int sequenceId)
    {
        EnsureIntroClientController();
        introClientController?.CancelSequence(sequenceId);
        _clientIntroVisualArmed = false;
        _clientIntroVisualStarted = false;
        _clientIntroStartNetworkTime = -1d;
        _clientAssignedGoNetworkTime = -1d;
        _clientRuntimeState = IntroRuntimeState.Cancelled;
        ResetTimelineToIntroStart();
    }

    private bool IsReadyToScheduleVisualsServer()
    {
        return roomStateManager != null
            && roomStateManager.IsPregameCountdownCompleted
            && roomStateManager.AreAllClientsIntroVisualsReadyForSequenceServer(_activeSequenceId);
    }

    private void ScheduleAndBroadcastVisualStartServer()
    {
        double now = IntroTimeUtility.GetNetworkTimeSeconds();
        double introDuration = Mathf.Max(0.1f, (float)(_serverConfiguredIntroDurationSeconds > 0d
            ? _serverConfiguredIntroDurationSeconds
            : GetConfiguredIntroDurationSecondsServer()));
        double introStartTime = now + introLeadInSeconds;
        double goTime = introStartTime + introDuration;
        _serverAssignedIntroStartNetworkTime = introStartTime;
        _serverAssignedGoNetworkTime = goTime;
        _serverIntroVisualsBroadcast = true;
        _serverRuntimeState = IntroRuntimeState.VisualStarted;
        Debug.Log(
            $"[IntroVisual][Server] Visual start issued seq={_activeSequenceId} introStart={introStartTime:0.000} " +
            $"go={goTime:0.000} duration={introDuration:0.000}");
        BroadcastIntroVisualsStartObserversRpc(_activeSequenceId, introStartTime, goTime);
    }

    private double GetConfiguredIntroDurationSecondsServer()
    {
        double timelineDuration = timelineDirector != null && timelineDirector.duration > 0d
            ? timelineDirector.duration
            : fallbackIntroDuration;
        double pathDuration = GetLongestAssignedSplineDurationSecondsServer();
        return System.Math.Max(timelineDuration, pathDuration);
    }

    private double GetLongestAssignedSplineDurationSecondsServer()
    {
        // Phase 6 — duration is the traversalTime parameter directly (linear-decel
        // formula completes the path in exactly T seconds regardless of length).
        // Returns the configured T clamped to fallback. Per-slot variance not
        // supported in current scaffolding; T is global per-stage.
        double duration = Mathf.Max(0.1f, introTraversalTimeSeconds);
        return _slotAssignments.Count > 0 ? duration : fallbackIntroDuration;
    }

    private RaceBodyIntroStateController[] FindIntroBodies()
    {
        BuddahMovement[] movements = FindObjectsByType<BuddahMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < movements.Length; i++)
        {
            BuddahMovement movement = movements[i];
            if (movement == null || movement.GetComponent<RaceBodyIntroStateController>() != null)
                continue;

            movement.gameObject.AddComponent<RaceBodyIntroStateController>();
        }

        return FindObjectsByType<RaceBodyIntroStateController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }

    private static int CompareBodiesForDeterminism(RaceBodyIntroStateController a, RaceBodyIntroStateController b)
    {
        int aOwner = a != null ? a.OwnerId : int.MaxValue;
        int bOwner = b != null ? b.OwnerId : int.MaxValue;
        int compare = aOwner.CompareTo(bOwner);
        if (compare != 0)
            return compare;

        string aName = a != null ? a.name : string.Empty;
        string bName = b != null ? b.name : string.Empty;
        return string.CompareOrdinal(aName, bName);
    }

    private static void ShuffleSlots(List<IntroSlot> slots, int seed)
    {
        System.Random random = new System.Random(seed);
        for (int i = slots.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(0, i + 1);
            (slots[i], slots[swapIndex]) = (slots[swapIndex], slots[i]);
        }
    }

    private void TryStartLocalIntroVisuals()
    {
        if (!_clientIntroVisualArmed || _clientIntroVisualStarted)
            return;

        IntroSequenceTiming timing = new IntroSequenceTiming(_activeSequenceId, _clientIntroStartNetworkTime, _clientAssignedGoNetworkTime);
        if (!timing.IsValid)
            return;

        double now = IntroTimeUtility.GetNetworkTimeSeconds();
        if (now < _clientIntroStartNetworkTime)
            return;

        _clientIntroVisualStarted = true;
        bool hasReachedGo = IntroTimeUtility.HasReachedGo(timing, now);
        _clientRuntimeState = hasReachedGo ? IntroRuntimeState.WaitingForGo : IntroRuntimeState.IntroRunning;
        if (timelineDirector != null)
        {
            double seekTime = GetTimelineSeekTime(now);
            timelineDirector.time = seekTime;
            timelineDirector.Evaluate();
            if (!hasReachedGo)
            {
                timelineDirector.Play();
                Debug.Log($"[IntroVisual][Client] Timeline play seq={_activeSequenceId} now={now:0.000} seek={seekTime:0.000}");
            }
            else
            {
                timelineDirector.Stop();
                Debug.Log($"[LateJoin][Client] Timeline skipped to end seq={_activeSequenceId} now={now:0.000} seek={seekTime:0.000} go={timing.GoNetworkTime:0.000}");
            }
        }

        SceneFadeController.ReleaseHeldBlackScreen();
    }

    private void ResetTimelineToIntroStart()
    {
        if (timelineDirector == null)
            return;

        timelineDirector.Stop();
        timelineDirector.time = 0d;
        timelineDirector.Evaluate();
    }

    private double GetTimelineSeekTime(double networkTimeSeconds)
    {
        if (timelineDirector == null)
            return 0d;

        double resolvedGoTime = _serverAssignedGoNetworkTime > 0d ? _serverAssignedGoNetworkTime : _clientAssignedGoNetworkTime;
        IntroSequenceTiming timing = new IntroSequenceTiming(_activeSequenceId, _clientIntroStartNetworkTime, resolvedGoTime);
        if (!timing.IsValid)
            return 0d;

        double timelineDuration = timelineDirector.duration > 0d ? timelineDirector.duration : fallbackIntroDuration;
        return IntroTimeUtility.GetTimelineSeekSeconds(timing, networkTimeSeconds, timelineDuration);
    }
}
