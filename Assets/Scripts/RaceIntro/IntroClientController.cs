using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public class IntroClientController : MonoBehaviour
{
    [SerializeField] private IntroSplineRegistry splineRegistry;
    [SerializeField, Min(0.05f)] private float pendingRetryIntervalSeconds = 0.1f;

    private readonly Dictionary<int, RaceBodyIntroStateController> _bodiesByObjectId = new Dictionary<int, RaceBodyIntroStateController>();
    private readonly Dictionary<int, IntroAssignmentData> _pendingAssignmentsByObjectId = new Dictionary<int, IntroAssignmentData>();

    private int _activeSequenceId = -1;
    private float _nextPendingRetryTime;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (_pendingAssignmentsByObjectId.Count == 0 || Time.unscaledTime < _nextPendingRetryTime)
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
            return;

        _activeSequenceId = sequenceId;
        _pendingAssignmentsByObjectId.Clear();
        for (int i = 0; i < assignments.Length; i++)
            _pendingAssignmentsByObjectId[assignments[i].playerObjectId] = assignments[i];

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
        _pendingAssignmentsByObjectId.Clear();
        _activeSequenceId = sequenceId;
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

    private void TryApplyPendingAssignments()
    {
        ResolveReferences();
        if (splineRegistry == null || _pendingAssignmentsByObjectId.Count == 0)
            return;

        RebuildBodyCache();
        List<int> appliedObjectIds = null;

        foreach (KeyValuePair<int, IntroAssignmentData> kvp in _pendingAssignmentsByObjectId)
        {
            IntroAssignmentData assignment = kvp.Value;
            if (!_bodiesByObjectId.TryGetValue(kvp.Key, out RaceBodyIntroStateController body) || body == null)
                continue;

            SplineIntroPath splinePath = splineRegistry.GetPathById(assignment.splineId);
            if (splinePath == null)
                continue;

            body.ApplyIntroAssignment(assignment, splinePath);
            appliedObjectIds ??= new List<int>();
            appliedObjectIds.Add(kvp.Key);
        }

        if (appliedObjectIds == null)
            return;

        for (int i = 0; i < appliedObjectIds.Count; i++)
            _pendingAssignmentsByObjectId.Remove(appliedObjectIds[i]);
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
    }
}
