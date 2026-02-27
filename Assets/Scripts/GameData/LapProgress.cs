using FishNet.Object;
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
    [SerializeField] private float minForwardDot = 0.0f;

    [Header("Read Only")]
    [SerializeField] private int currentLap = 0;
    [SerializeField] private bool hasStartedLap = false;
    [SerializeField] private bool lapArmed = false;

    private SplineProgressTracker _tracker;
    private float _nextAllowedCrossTime = 0f;

    public int CurrentLap => currentLap;
    public bool HasStartedLap => hasStartedLap;
    public float TotalProgress01 => Mathf.Max(0f, Mathf.Max(0, currentLap - 1) + (_tracker != null ? _tracker.progress01 : 0f));
    public float TotalProgressPercent => TotalProgress01 * 100f;

    private void Awake()
    {
        _tracker = GetComponent<SplineProgressTracker>();
        ResolveStartPointReference();
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (_tracker == null)
            return;

        if (hasStartedLap && !lapArmed && _tracker.progress01 >= nearEndThreshold01 && _tracker.forwardDot >= minForwardDot)
        {
            lapArmed = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner)
            return;

        if (_tracker == null)
            return;

        if (!IsStartPoint(other))
            return;

        if (Time.time < _nextAllowedCrossTime)
            return;

        if (_tracker.forwardDot < minForwardDot)
            return;

        if (!hasStartedLap)
        {
            hasStartedLap = true;
            currentLap = 1;
            _nextAllowedCrossTime = Time.time + minimumCrossingCooldownSeconds;
            return;
        }

        bool crossedNearStart = _tracker.progress01 <= nearStartThreshold01;
        if (!crossedNearStart || !lapArmed)
            return;

        currentLap += 1;
        lapArmed = false;
        _nextAllowedCrossTime = Time.time + minimumCrossingCooldownSeconds;
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
}
