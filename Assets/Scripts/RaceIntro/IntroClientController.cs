using System.Collections.Generic;
using FishNet.Object;
using SteamMultiplayer.Network;
using UnityEngine;

public class IntroClientController : MonoBehaviour
{
    private sealed class SequenceRuntimeRecord
    {
        public readonly Dictionary<int, IntroAssignmentData> PendingAssignmentsByObjectId = new Dictionary<int, IntroAssignmentData>();
        public readonly HashSet<int> VisualAppliedBodyObjectIds = new HashSet<int>();
        public readonly HashSet<int> GoAppliedBodyObjectIds = new HashSet<int>();
        public bool HasAssignments;
        public bool HasVisualStart;
        public double VisualStartNetworkTime;
        public double VisualStartGoNetworkTime;
        public bool HasAuthoritativeGo;
        public double AuthoritativeGoScheduledNetworkTime;
        public double AuthoritativeGoIssuedNetworkTime;
        public bool LocalAssignmentReported;
        public bool LocalVisualPreparedReported;
    }

    [SerializeField] private IntroSplineRegistry splineRegistry;
    [SerializeField] private IntroSequenceManager introSequenceManager;
    [SerializeField, Min(0.05f)] private float pendingRetryIntervalSeconds = 0.1f;

    private readonly Dictionary<int, RaceBodyIntroStateController> _bodiesByObjectId = new Dictionary<int, RaceBodyIntroStateController>();
    private readonly Dictionary<int, SequenceRuntimeRecord> _runtimeBySequenceId = new Dictionary<int, SequenceRuntimeRecord>();
    private readonly List<int> _sequenceScratch = new List<int>();

    private int _activeSequenceId = -1;
    private float _nextPendingRetryTime;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (!HasAnyPendingAssignments() || Time.unscaledTime < _nextPendingRetryTime)
            return;

        _nextPendingRetryTime = Time.unscaledTime + pendingRetryIntervalSeconds;
        TryApplyPendingAssignments();
    }

    public void ReceiveAssignments(IntroAssignmentData[] assignments)
    {
        ResolveReferences();

        if (assignments == null || assignments.Length == 0)
            return;

        int sequenceId = assignments[0].sequenceId;
        if (sequenceId < _activeSequenceId)
        {
            Debug.Log($"[SequenceGuard][Client] Ignored stale assignments seq={sequenceId} active={_activeSequenceId}");
            return;
        }

        if (sequenceId > _activeSequenceId)
        {
            _activeSequenceId = sequenceId;
            PruneStaleRuntimeRecords(_activeSequenceId);
        }

        SequenceRuntimeRecord runtime = GetOrCreateSequenceRuntime(sequenceId);
        runtime.HasAssignments = true;
        runtime.PendingAssignmentsByObjectId.Clear();

        Debug.Log(
            $"[IntroState][Client] ReceiveAssignments seq={sequenceId} count={assignments.Length} " +
            $"reuseCachedVisual={runtime.HasVisualStart} reuseCachedGo={runtime.HasAuthoritativeGo}");

        for (int i = 0; i < assignments.Length; i++)
        {
            IntroAssignmentData assignment = assignments[i];
            if (assignment.sequenceId != sequenceId)
            {
                Debug.LogWarning($"[SequenceGuard][Client] Assignment payload sequence mismatch expected={sequenceId} actual={assignment.sequenceId} obj={assignment.playerObjectId}");
                continue;
            }

            runtime.PendingAssignmentsByObjectId[assignment.playerObjectId] = assignment;
            runtime.VisualAppliedBodyObjectIds.Remove(assignment.playerObjectId);
            runtime.GoAppliedBodyObjectIds.Remove(assignment.playerObjectId);
            Debug.Log(
                $"[IntroState][Client] Pending assignment seq={assignment.sequenceId} obj={assignment.playerObjectId} " +
                $"owner={assignment.playerOwnerId} slot={assignment.slotIndex} spline={assignment.splineId} " +
                $"introStart={assignment.introStartNetworkTime:0.000} go={assignment.goNetworkTime:0.000}");
        }

        if (runtime.HasVisualStart || runtime.HasAuthoritativeGo)
        {
            Debug.Log(
                $"[LateJoin][Client] Assignments seq={sequenceId} will reuse cached commands " +
                $"visual={runtime.HasVisualStart} go={runtime.HasAuthoritativeGo}");
        }

        TryApplyPendingAssignments();
    }

    public void CompleteGoSequence(int sequenceId)
    {
        if (sequenceId < _activeSequenceId)
            return;

        ResolveReferences();
        TryApplyPendingAssignments();
    }

    public void CancelSequence(int sequenceId)
    {
        if (sequenceId < _activeSequenceId)
            return;

        ForceExitAllBodies();
        _activeSequenceId = sequenceId;
        PruneCancelledRuntimeRecords(sequenceId);
        Debug.Log($"[IntroState][Client] CancelSequence seq={sequenceId} active={_activeSequenceId} runtimeRecords={_runtimeBySequenceId.Count}");
    }

    public void ApplyAuthoritativeGo(int sequenceId, double scheduledGoNetworkTime, double goIssuedNetworkTime)
    {
        if (sequenceId < _activeSequenceId)
        {
            Debug.Log($"[SequenceGuard][Client] Ignored stale authoritative go seq={sequenceId} active={_activeSequenceId}");
            return;
        }

        if (sequenceId > _activeSequenceId)
        {
            _activeSequenceId = sequenceId;
            PruneStaleRuntimeRecords(_activeSequenceId);
        }

        ResolveReferences();
        SequenceRuntimeRecord runtime = GetOrCreateSequenceRuntime(sequenceId);
        bool overwriteKnownGo = runtime.HasAuthoritativeGo
                                && (runtime.AuthoritativeGoIssuedNetworkTime != goIssuedNetworkTime
                                    || runtime.AuthoritativeGoScheduledNetworkTime != scheduledGoNetworkTime);
        runtime.HasAuthoritativeGo = true;
        runtime.AuthoritativeGoScheduledNetworkTime = scheduledGoNetworkTime;
        runtime.AuthoritativeGoIssuedNetworkTime = goIssuedNetworkTime;
        if (overwriteKnownGo)
            runtime.GoAppliedBodyObjectIds.Clear();

        Debug.Log(
            $"[IntroGo][Client] Cached authoritative go seq={sequenceId} goIssuedNow={goIssuedNetworkTime:0.000} " +
            $"scheduledGoTime={scheduledGoNetworkTime:0.000} localNow={IntroTimeUtility.GetNetworkTimeSeconds():0.000} " +
            $"overwriteKnown={overwriteKnownGo} hasAssignments={runtime.PendingAssignmentsByObjectId.Count > 0 || runtime.HasAssignments}");

        TryApplyPendingAssignments();
        RebuildBodyCache();
        ReplayRuntimeCommandsForSequence(sequenceId, runtime);
    }

    public void HandleTimelineVisualIntroSignal()
    {
    }

    public void HandleTimelineVisualCountdownSignal()
    {
    }

    public void HandleTimelineVisualGameplayCameraSignal()
    {
    }

    public void ApplyVisualStart(int sequenceId, double introStartNetworkTime, double goNetworkTime)
    {
        if (sequenceId < _activeSequenceId)
        {
            Debug.Log($"[SequenceGuard][Client] Ignored stale visual start seq={sequenceId} active={_activeSequenceId}");
            return;
        }

        if (sequenceId > _activeSequenceId)
        {
            _activeSequenceId = sequenceId;
            PruneStaleRuntimeRecords(_activeSequenceId);
        }

        ResolveReferences();
        SequenceRuntimeRecord runtime = GetOrCreateSequenceRuntime(sequenceId);
        bool overwriteKnownVisualStart = runtime.HasVisualStart
                                        && (runtime.VisualStartNetworkTime != introStartNetworkTime
                                            || runtime.VisualStartGoNetworkTime != goNetworkTime);
        runtime.HasVisualStart = true;
        runtime.VisualStartNetworkTime = introStartNetworkTime;
        runtime.VisualStartGoNetworkTime = goNetworkTime;
        if (overwriteKnownVisualStart)
            runtime.VisualAppliedBodyObjectIds.Clear();

        Debug.Log(
            $"[IntroVisual][Client] Cached visual start seq={sequenceId} introStart={introStartNetworkTime:0.000} go={goNetworkTime:0.000} " +
            $"overwriteKnown={overwriteKnownVisualStart} hasAssignments={runtime.PendingAssignmentsByObjectId.Count > 0 || runtime.HasAssignments}");

        TryApplyPendingAssignments();
        RebuildBodyCache();
        ReplayRuntimeCommandsForSequence(sequenceId, runtime);
    }

    private void TryApplyPendingAssignments()
    {
        ResolveReferences();
        if (splineRegistry == null || _runtimeBySequenceId.Count == 0)
            return;

        RebuildBodyCache();
        _sequenceScratch.Clear();
        foreach (KeyValuePair<int, SequenceRuntimeRecord> kvp in _runtimeBySequenceId)
            _sequenceScratch.Add(kvp.Key);
        _sequenceScratch.Sort();

        for (int i = 0; i < _sequenceScratch.Count; i++)
        {
            int sequenceId = _sequenceScratch[i];
            if (!_runtimeBySequenceId.TryGetValue(sequenceId, out SequenceRuntimeRecord runtime))
                continue;

            if (sequenceId < _activeSequenceId)
            {
                Debug.Log($"[SequenceGuard][Client] Removing stale runtime seq={sequenceId} active={_activeSequenceId}");
                _runtimeBySequenceId.Remove(sequenceId);
                continue;
            }

            TryApplyAssignmentsForSequence(sequenceId, runtime);
            ReplayRuntimeCommandsForSequence(sequenceId, runtime);
        }
    }

    private void TryApplyAssignmentsForSequence(int sequenceId, SequenceRuntimeRecord runtime)
    {
        if (runtime == null || runtime.PendingAssignmentsByObjectId.Count == 0)
            return;

        List<int> appliedObjectIds = null;
        foreach (KeyValuePair<int, IntroAssignmentData> kvp in runtime.PendingAssignmentsByObjectId)
        {
            IntroAssignmentData assignment = kvp.Value;
            if (assignment.sequenceId < _activeSequenceId)
            {
                Debug.Log($"[SequenceGuard][Client] Dropping stale pending assignment seq={assignment.sequenceId} active={_activeSequenceId} obj={kvp.Key}");
                appliedObjectIds ??= new List<int>();
                appliedObjectIds.Add(kvp.Key);
                continue;
            }

            if (!_bodiesByObjectId.TryGetValue(kvp.Key, out RaceBodyIntroStateController body) || body == null)
            {
                Debug.Log($"[LateJoin][Client] Assignment waiting for body obj={kvp.Key} seq={assignment.sequenceId} spline={assignment.splineId}");
                continue;
            }

            SplineIntroPath splinePath = splineRegistry.GetPathById(assignment.splineId);
            if (splinePath == null)
            {
                Debug.Log($"[LateJoin][Client] Assignment waiting for spline obj={kvp.Key} seq={assignment.sequenceId} spline={assignment.splineId}");
                continue;
            }

            Debug.Log(
                $"[IntroState][Client] Applying assignment seq={assignment.sequenceId} obj={kvp.Key} owner={assignment.playerOwnerId} " +
                $"slot={assignment.slotIndex} spline={assignment.splineId} body={body.name}");
            body.ApplyIntroAssignment(assignment, splinePath);
            Debug.Log(
                $"[IntroState][Client] Body prepared seq={assignment.sequenceId} obj={kvp.Key} body={body.name} " +
                $"cachedVisual={runtime.HasVisualStart} cachedGo={runtime.HasAuthoritativeGo}");
            runtime.VisualAppliedBodyObjectIds.Remove(kvp.Key);
            runtime.GoAppliedBodyObjectIds.Remove(kvp.Key);

            NetworkObject bodyNetworkObject = body.NetworkObject;
            if (bodyNetworkObject != null && bodyNetworkObject.IsOwner && !runtime.LocalAssignmentReported)
            {
                Debug.Log(
                    $"[IntroState][Client] Local intro assignment ready seq={assignment.sequenceId} obj={bodyNetworkObject.ObjectId} " +
                    $"owner={body.OwnerId} body={body.name}");
                runtime.LocalAssignmentReported = true;
                RoomStateManager.Instance?.ReportLocalIntroAssignmentApplied(assignment.sequenceId);
            }

            if (bodyNetworkObject != null
                && bodyNetworkObject.IsOwner
                && !runtime.LocalVisualPreparedReported
                && introSequenceManager != null
                && introSequenceManager.TryPrepareLocalVisual(assignment.sequenceId))
            {
                Debug.Log(
                    $"[IntroVisual][Client] Local visual prepared seq={assignment.sequenceId} obj={bodyNetworkObject.ObjectId} " +
                    $"owner={body.OwnerId} body={body.name}");
                runtime.LocalVisualPreparedReported = true;
                RoomStateManager.Instance?.ReportLocalIntroVisualPrepared(assignment.sequenceId);
            }

            ApplyRuntimeCommandsToBody(runtime, sequenceId, kvp.Key, body, true);
            appliedObjectIds ??= new List<int>();
            appliedObjectIds.Add(kvp.Key);
        }

        if (appliedObjectIds == null)
            return;

        for (int i = 0; i < appliedObjectIds.Count; i++)
            runtime.PendingAssignmentsByObjectId.Remove(appliedObjectIds[i]);
    }

    private void ReplayRuntimeCommandsForSequence(int sequenceId, SequenceRuntimeRecord runtime)
    {
        if (runtime == null || (!runtime.HasVisualStart && !runtime.HasAuthoritativeGo))
            return;

        foreach (KeyValuePair<int, RaceBodyIntroStateController> kvp in _bodiesByObjectId)
        {
            if (kvp.Value == null)
                continue;

            ApplyRuntimeCommandsToBody(runtime, sequenceId, kvp.Key, kvp.Value, true);
        }
    }

    private void ForceExitAllBodies()
    {
        RebuildBodyCache();
        foreach (KeyValuePair<int, RaceBodyIntroStateController> kvp in _bodiesByObjectId)
        {
            if (kvp.Value != null)
                kvp.Value.ForceExitIntroState();
        }
    }

    private void RebuildBodyCache()
    {
        _bodiesByObjectId.Clear();
        EnsureBodyControllersPresent();

        RaceBodyIntroStateController[] bodies = FindObjectsByType<RaceBodyIntroStateController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < bodies.Length; i++)
        {
            RaceBodyIntroStateController body = bodies[i];
            if (body == null)
                continue;

            NetworkObject networkObject = body.NetworkObject;
            if (networkObject == null)
                continue;

            _bodiesByObjectId[networkObject.ObjectId] = body;
        }
    }

    private void EnsureBodyControllersPresent()
    {
        BuddahMovement[] movements = FindObjectsByType<BuddahMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < movements.Length; i++)
        {
            BuddahMovement movement = movements[i];
            if (movement == null || movement.GetComponent<RaceBodyIntroStateController>() != null)
                continue;

            movement.gameObject.AddComponent<RaceBodyIntroStateController>();
        }
    }

    private void ResolveReferences()
    {
        if (splineRegistry == null)
            splineRegistry = FindFirstObjectByType<IntroSplineRegistry>(FindObjectsInactive.Include);

        if (splineRegistry == null)
            splineRegistry = gameObject.GetComponent<IntroSplineRegistry>() ?? gameObject.AddComponent<IntroSplineRegistry>();

        if (introSequenceManager == null)
            introSequenceManager = FindFirstObjectByType<IntroSequenceManager>(FindObjectsInactive.Include);
    }

    private void ApplyRuntimeCommandsToBody(SequenceRuntimeRecord runtime, int sequenceId, int objectId, RaceBodyIntroStateController body, bool isReplay)
    {
        if (runtime == null || body == null)
            return;

        if (!body.HasAssignment || body.ActiveSequenceId != sequenceId)
            return;

        if (runtime.HasVisualStart && !runtime.VisualAppliedBodyObjectIds.Contains(objectId))
        {
            string source = isReplay ? "cached" : "direct";
            Debug.Log(
                $"[LateJoin][Client] Replaying {source} visual start seq={sequenceId} onto body={body.name} obj={objectId} " +
                $"introStart={runtime.VisualStartNetworkTime:0.000} go={runtime.VisualStartGoNetworkTime:0.000} " +
                $"bodyPrepared={body.HasAssignment} bodyActiveSeq={body.ActiveSequenceId}");
            body.ApplyVisualStart(sequenceId, runtime.VisualStartNetworkTime, runtime.VisualStartGoNetworkTime);
            runtime.VisualAppliedBodyObjectIds.Add(objectId);
        }

        if (runtime.HasAuthoritativeGo && !runtime.GoAppliedBodyObjectIds.Contains(objectId))
        {
            string source = isReplay ? "cached" : "direct";
            Debug.Log(
                $"[LateJoin][Client] Replaying {source} authoritative go seq={sequenceId} onto body={body.name} obj={objectId} " +
                $"goIssuedNow={runtime.AuthoritativeGoIssuedNetworkTime:0.000} scheduledGoTime={runtime.AuthoritativeGoScheduledNetworkTime:0.000} " +
                $"bodyPrepared={body.HasAssignment} bodyActiveSeq={body.ActiveSequenceId}");
            body.ApplyAuthoritativeGo(sequenceId, runtime.AuthoritativeGoScheduledNetworkTime, runtime.AuthoritativeGoIssuedNetworkTime);
            runtime.GoAppliedBodyObjectIds.Add(objectId);
        }
    }

    private SequenceRuntimeRecord GetOrCreateSequenceRuntime(int sequenceId)
    {
        if (!_runtimeBySequenceId.TryGetValue(sequenceId, out SequenceRuntimeRecord runtime))
        {
            runtime = new SequenceRuntimeRecord();
            _runtimeBySequenceId[sequenceId] = runtime;
        }

        return runtime;
    }

    private void PruneStaleRuntimeRecords(int keepFromSequenceId)
    {
        _sequenceScratch.Clear();
        foreach (KeyValuePair<int, SequenceRuntimeRecord> kvp in _runtimeBySequenceId)
        {
            if (kvp.Key < keepFromSequenceId)
                _sequenceScratch.Add(kvp.Key);
        }

        for (int i = 0; i < _sequenceScratch.Count; i++)
        {
            int sequenceId = _sequenceScratch[i];
            _runtimeBySequenceId.Remove(sequenceId);
            Debug.Log($"[SequenceGuard][Client] Pruned stale runtime record seq={sequenceId} keepFrom={keepFromSequenceId}");
        }
    }

    private void PruneCancelledRuntimeRecords(int cancelledSequenceId)
    {
        _sequenceScratch.Clear();
        foreach (KeyValuePair<int, SequenceRuntimeRecord> kvp in _runtimeBySequenceId)
        {
            if (kvp.Key <= cancelledSequenceId)
                _sequenceScratch.Add(kvp.Key);
        }

        for (int i = 0; i < _sequenceScratch.Count; i++)
        {
            int sequenceId = _sequenceScratch[i];
            _runtimeBySequenceId.Remove(sequenceId);
            Debug.Log($"[SequenceGuard][Client] Cleared cancelled runtime record seq={sequenceId} cancelledAt={cancelledSequenceId}");
        }
    }

    private bool HasAnyPendingAssignments()
    {
        foreach (KeyValuePair<int, SequenceRuntimeRecord> kvp in _runtimeBySequenceId)
        {
            if (kvp.Value != null && kvp.Value.PendingAssignmentsByObjectId.Count > 0)
                return true;
        }

        return false;
    }
}
