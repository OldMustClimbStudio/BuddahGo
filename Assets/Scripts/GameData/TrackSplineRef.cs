using UnityEngine;
using UnityEngine.Splines;

public class TrackSplineRef : MonoBehaviour
{
    public static TrackSplineRef Instance { get; private set; }

    public SplineContainer container;

    [SerializeField] private int samples = 400; // 越大越精确（UI用400很够）
    private float[] _tTable;
    private float[] _lenTable;

    public float TrackLength { get; private set; }

    private void Awake()
    {
        Instance = this;
        if (!container) container = GetComponent<SplineContainer>();
        BuildArcLengthTable();
    }

    private void BuildArcLengthTable()
    {
        _tTable = new float[samples + 1];
        _lenTable = new float[samples + 1];

        float total = 0f;
        Vector3 prev = container.EvaluatePosition(0f);

        _tTable[0] = 0f;
        _lenTable[0] = 0f;

        for (int i = 1; i <= samples; i++)
        {
            float t = (float)i / samples;
            Vector3 p = container.EvaluatePosition(t);
            total += Vector3.Distance(prev, p);
            prev = p;

            _tTable[i] = t;
            _lenTable[i] = total;
        }

        TrackLength = total;
    }

    // t(0..1) -> 距离(米)
    public float DistanceAtT(float t)
    {
        if (_tTable == null || _lenTable == null || _tTable.Length < 2)
            return 0f;

        t = Mathf.Repeat(t, 1f);

        // 二分查找所在区间
        int lo = 0, hi = _tTable.Length - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (_tTable[mid] <= t) lo = mid;
            else hi = mid;
        }

        float t0 = _tTable[lo], t1 = _tTable[hi];
        float l0 = _lenTable[lo], l1 = _lenTable[hi];

        float u = (t - t0) / Mathf.Max(1e-6f, (t1 - t0));
        return Mathf.Lerp(l0, l1, u);
    }

    // 方便：直接返回“按米均匀”的完成度
    public float Progress01AtT(float t)
    {
        float d = DistanceAtT(t);
        return TrackLength > 1e-6f ? d / TrackLength : 0f;
    }

    public float TAtDistance(float distance)
    {
        if (TrackLength <= 1e-6f)
            return 0f;

        float targetDistance = Mathf.Clamp(distance, 0f, TrackLength);
        float low = 0f;
        float high = 1f;

        for (int i = 0; i < 18; i++)
        {
            float mid = (low + high) * 0.5f;
            float midDistance = DistanceAtT(mid);
            if (midDistance < targetDistance)
                low = mid;
            else
                high = mid;
        }

        return (low + high) * 0.5f;
    }

    public float TAtProgress01(float progress01)
    {
        return TAtDistance(Mathf.Clamp01(progress01) * TrackLength);
    }

    public bool TryEvaluateWorldPoseAtProgress01(float progress01, out Vector3 worldPosition, out Vector3 worldForward)
    {
        worldPosition = Vector3.zero;
        worldForward = Vector3.forward;

        if (container == null || TrackLength <= 1e-6f)
            return false;

        float t = TAtProgress01(progress01);
        worldPosition = container.EvaluatePosition(t);
        worldForward = container.EvaluateTangent(t);

        if (worldForward.sqrMagnitude <= 0.0001f)
            worldForward = container.transform.forward;

        worldForward.y = 0f;
        if (worldForward.sqrMagnitude <= 0.0001f)
            worldForward = Vector3.forward;
        else
            worldForward.Normalize();

        return true;
    }
}
