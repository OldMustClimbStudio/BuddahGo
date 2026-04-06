using System;
using System.Collections.Generic;
using FishNet.Object;
using SteamMultiplayer.Network;
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
    [SerializeField, Min(0.1f)] private float introSpeedMetersPerSecond = 20f;
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

    private void Start()
    {
        TryResolveRoomStateManager();
        EnsureIntroClientController();
    }

    private void Update()
    {
        TryResolveRoomStateManager();
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

        if (roomStateManager.IsRaceStarted)
        {
            if (_authorityState == IntroAuthorityState.AssignmentsBroadcast)
                TriggerGoAndHandoff();

            return;
        }

        if (_authorityState == IntroAuthorityState.AssignmentsBroadcast)
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

        List<IntroAssignmentData> networkAssignments = new List<IntroAssignmentData>(_slotAssignments.Count);
        double now = IntroTimeUtility.GetNetworkTimeSeconds();
        double introStartTime = now + introLeadInSeconds;
        double goTime = introStartTime + duration;

        for (int i = 0; i < _slotAssignments.Count; i++)
        {
            SlotAssignment assignment = _slotAssignments[i];
            if (assignment.body == null || assignment.slot == null)
                continue;

            networkAssignments.Add(BuildAssignmentData(assignment.body, assignment.slot, introStartTime, goTime));
        }

        if (networkAssignments.Count == 0)
            return;

        BroadcastAssignmentsObserversRpc(networkAssignments.ToArray());
        _authorityState = IntroAuthorityState.AssignmentsBroadcast;
    }

    public void TriggerGoAndHandoff()
    {
        if (!IsServerInitialized || _authorityState != IntroAuthorityState.AssignmentsBroadcast)
            return;

        NotifyGoObserversRpc(_activeSequenceId);
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

    private void StartIntroFromRoomCountdown()
    {
        float duration = Mathf.Max(0.1f, roomStateManager != null
            ? roomStateManager.RaceCountdownSecondsRemaining
            : fallbackIntroDuration);

        BeginIntroSequence(duration);
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

    private IntroAssignmentData BuildAssignmentData(RaceBodyIntroStateController body, IntroSlot slot, double introStartTime, double goTime)
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
            introStartNetworkTime = introStartTime,
            introSpeedMetersPerSecond = introSpeedMetersPerSecond,
            goNetworkTime = goTime,
            handoffLeadTime = handoffLeadSeconds
        };
    }

    [ObserversRpc(BufferLast = true)]
    private void BroadcastAssignmentsObserversRpc(IntroAssignmentData[] assignments)
    {
        EnsureIntroClientController();
        if (timelineDirector != null)
        {
            timelineDirector.time = 0d;
            timelineDirector.Evaluate();
            timelineDirector.Play();
        }

        introClientController?.ReceiveAssignments(assignments);
    }

    [ObserversRpc]
    private void NotifyGoObserversRpc(int sequenceId)
    {
        EnsureIntroClientController();
        introClientController?.CompleteGoSequence(sequenceId);
    }

    [ObserversRpc]
    private void NotifyCancelObserversRpc(int sequenceId)
    {
        EnsureIntroClientController();
        introClientController?.CancelSequence(sequenceId);
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
}
