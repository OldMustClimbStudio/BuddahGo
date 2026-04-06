using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(SplineContainer))]
public class SplineIntroPath : IntroPath
{
    [Header("Identity")]
    [SerializeField] private string splineId;

    [Header("Spline")]
    [SerializeField] private SplineContainer splineContainer;
    [SerializeField, Min(16)] private int arcLengthSamples = 128;

    private float[] _tTable;
    private float[] _distanceTable;
    private float _totalLength;
    private bool _cacheDirty = true;

    public string SplineId => GetResolvedSplineId();

    public float TotalLength
    {
        get
        {
            EnsureCache();
            return _totalLength;
        }
    }

    private void Awake()
    {
        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>();

        EnsureSplineId();
        RebuildCache();
    }

    private void OnValidate()
    {
        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>();

        EnsureSplineId();
        _cacheDirty = true;
    }

    public override void BindTerminalPose(Transform launchPoint, Transform forwardReference)
    {
        base.BindTerminalPose(launchPoint, forwardReference);
        _cacheDirty = true;
    }

    public override Vector3 EvaluatePosition(float t)
    {
        EnsureCache();

        if (lockTerminalPositionToSlot && BoundLaunchPoint != null && t >= 0.9999f)
            return BoundLaunchPoint.position;

        float splineT = DistanceNormalizedToSplineT(Mathf.Clamp01(t));
        return splineContainer != null ? splineContainer.EvaluatePosition(splineT) : transform.position;
    }

    public override Vector3 EvaluateTangent(float t)
    {
        EnsureCache();

        if (lockTerminalForwardToSlot && BoundForwardReference != null && t >= 0.9999f)
            return BoundForwardReference.forward.normalized;

        if (splineContainer == null)
            return transform.forward;

        float splineT = DistanceNormalizedToSplineT(Mathf.Clamp01(t));
        Vector3 tangent = splineContainer.EvaluateTangent(splineT);
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = transform.forward;

        tangent.y = 0f;
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = Vector3.forward;

        return tangent.normalized;
    }

    public float FindNearestT(Vector3 worldPosition)
    {
        EnsureCache();
        if (splineContainer == null)
            return 0f;

        Vector3 localPoint = splineContainer.transform.InverseTransformPoint(worldPosition);
        SplineUtility.GetNearestPoint(splineContainer.Spline, localPoint, out _, out float nearestT);
        return Mathf.Clamp01(nearestT);
    }

    public float DistanceAtT(float t)
    {
        EnsureCache();
        t = Mathf.Clamp01(t);

        for (int i = 1; i < _tTable.Length; i++)
        {
            if (t > _tTable[i])
                continue;

            float alpha = Mathf.InverseLerp(_tTable[i - 1], _tTable[i], t);
            return Mathf.Lerp(_distanceTable[i - 1], _distanceTable[i], alpha);
        }

        return _totalLength;
    }

    public float TAtDistance(float distance)
    {
        EnsureCache();
        distance = Mathf.Clamp(distance, 0f, _totalLength);

        for (int i = 1; i < _distanceTable.Length; i++)
        {
            if (distance > _distanceTable[i])
                continue;

            float alpha = Mathf.InverseLerp(_distanceTable[i - 1], _distanceTable[i], distance);
            return Mathf.Lerp(_tTable[i - 1], _tTable[i], alpha);
        }

        return 1f;
    }

    private void EnsureCache()
    {
        if (_cacheDirty || _tTable == null || _distanceTable == null || _tTable.Length < 2)
            RebuildCache();
    }

    private void RebuildCache()
    {
        _cacheDirty = false;

        if (splineContainer == null)
            splineContainer = GetComponent<SplineContainer>();

        if (splineContainer == null)
        {
            _tTable = new[] { 0f, 1f };
            _distanceTable = new[] { 0f, 1f };
            _totalLength = 1f;
            return;
        }

        int sampleCount = Mathf.Max(16, arcLengthSamples);
        _tTable = new float[sampleCount + 1];
        _distanceTable = new float[sampleCount + 1];

        _tTable[0] = 0f;
        _distanceTable[0] = 0f;

        Vector3 previous = splineContainer.EvaluatePosition(0f);
        float cumulativeDistance = 0f;

        for (int i = 1; i <= sampleCount; i++)
        {
            float sampleT = i / (float)sampleCount;
            Vector3 current = splineContainer.EvaluatePosition(sampleT);
            cumulativeDistance += Vector3.Distance(previous, current);
            previous = current;

            _tTable[i] = sampleT;
            _distanceTable[i] = cumulativeDistance;
        }

        _totalLength = Mathf.Max(0.0001f, cumulativeDistance);
    }

    private float DistanceNormalizedToSplineT(float distance01)
    {
        if (_distanceTable == null || _distanceTable.Length < 2)
            return distance01;

        float targetDistance = Mathf.Clamp01(distance01) * _totalLength;
        int lastIndex = _distanceTable.Length - 1;

        for (int i = 1; i <= lastIndex; i++)
        {
            if (targetDistance > _distanceTable[i])
                continue;

            float d0 = _distanceTable[i - 1];
            float d1 = _distanceTable[i];
            float t0 = _tTable[i - 1];
            float t1 = _tTable[i];
            float lerp = Mathf.InverseLerp(d0, d1, targetDistance);
            return Mathf.LerpUnclamped(t0, t1, lerp);
        }

        return 1f;
    }

    private void EnsureSplineId()
    {
        if (!string.IsNullOrWhiteSpace(splineId))
            return;

        splineId = GetFallbackSplineId();
    }

    private string GetResolvedSplineId()
    {
        EnsureSplineId();
        return splineId;
    }

    private string GetFallbackSplineId()
    {
        Transform current = transform;
        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = $"{current.name}/{path}";
        }

        return path.Replace(' ', '_');
    }
}
