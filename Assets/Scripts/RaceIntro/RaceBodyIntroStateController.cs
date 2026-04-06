using FishNet.Object;
using UnityEngine;

[DisallowMultipleComponent]
public class RaceBodyIntroStateController : MonoBehaviour
{
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
    private double _networkToLocalOffsetSeconds;

    public int OwnerId => networkObject != null ? networkObject.OwnerId : -1;
    public NetworkObject NetworkObject => networkObject;
    public Rigidbody TargetRigidbody => targetRigidbody;
    public bool IsIntroActive => _phase == IntroPhase.IntroKinematic || _phase == IntroPhase.HandoffWindow;

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
        if (now >= _assignment.goNetworkTime)
        {
            if (!_goApplied)
                CompleteGoTransition(now);

            return;
        }

        double handoffStartTime = _assignment.goNetworkTime - GetHandoffLeadTime();
        bool inHandoffWindow = now >= handoffStartTime;
        _phase = inHandoffWindow ? IntroPhase.HandoffWindow : IntroPhase.IntroKinematic;
    }

    private void FixedUpdate()
    {
        if (!_hasAssignment || _assignedPath == null || targetRigidbody == null || _goApplied)
            return;

        double now = GetSmoothedNetworkTimeSeconds();
        if (now >= _assignment.goNetworkTime)
            return;

        DriveSplinePose(now);
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
        _networkToLocalOffsetSeconds = IntroTimeUtility.GetNetworkTimeSeconds() - Time.unscaledTimeAsDouble;
        EnterIntroState();

        double now = GetSmoothedNetworkTimeSeconds();
        if (now >= assignment.goNetworkTime)
        {
            CompleteGoTransition(now);
            return;
        }

        DriveSplinePose(System.Math.Max(assignment.introStartNetworkTime, now));
    }

    public void EnterIntroState()
    {
        ResolveReferences();

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

    private void CompleteGoTransition(double handoffNetworkTime)
    {
        if (_goApplied || targetRigidbody == null)
            return;

        _goApplied = true;
        _phase = IntroPhase.RaceLive;

        bool isLocalOwner = networkObject != null && networkObject.IsOwner;
        double resolvedHandoffTime = System.Math.Max(_assignment.introStartNetworkTime, handoffNetworkTime);
        SampleSnapshotAtTime(resolvedHandoffTime, out LaunchHandoffSnapshot snapshot);
        _latestSplineSnapshot = snapshot;

        if (movementController != null && isLocalOwner)
        {
            movementController.BeginLaunchHandoff(
                snapshot,
                Mathf.Max(0.1f, GetHandoffLeadTime()),
                0.15f,
                false);
        }

        if (movementController != null && !isLocalOwner)
            movementController.SetExternalKinematicControlActive(false);

        if (movementController != null)
            movementController.SetIntroControlActive(false);

        if (targetRigidbody != null && !isLocalOwner)
            targetRigidbody.isKinematic = true;

        SetCollisionsEnabled(true);
        _hasAssignment = false;
    }

    private void DriveSplinePose(double networkTime)
    {
        SampleSnapshotAtTime(networkTime, out LaunchHandoffSnapshot snapshot);
        _latestSplineSnapshot = snapshot;
        if (targetRigidbody.isKinematic)
        {
            targetRigidbody.MovePosition(snapshot.Position);
            targetRigidbody.MoveRotation(snapshot.Rotation);
        }
        else
        {
            targetRigidbody.position = snapshot.Position;
            targetRigidbody.rotation = snapshot.Rotation;
        }
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

        float elapsedSeconds = Mathf.Max(0f, (float)(networkTime - _assignment.introStartNetworkTime));
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
        return Time.unscaledTimeAsDouble + _networkToLocalOffsetSeconds;
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
