using FishNet.Object;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class SplineProgressTracker : NetworkBehaviour
{
    [Header("Read Only")]
    [Range(0f, 1f)] public float progress01;      // 完成度 0..1
    public float distanceOnTrack;                 // 0..trackLength (米，估算)
    public float forwardDot;                      // <0 逆行倾向（可选）

    [SerializeField] private Rigidbody rb;
    private float _lastT01;
    private float _lastDistance;
    private bool _hasLast;

    [SerializeField] private float jumpMetersThreshold = 8f; // 5~15 
    [SerializeField] private float windowRadiusT = 0.1f;    // 0.03~0.10
    [SerializeField] private int windowSteps = 40;
    [SerializeField] private float maxProjectionDistance = 20f; // 离赛道中心线太远就不更新（防飞出去乱跳）
    [SerializeField] private float maxStepFactor = 1.5f;        // 单帧最大允许前进 = 速度 * dt * factor



    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        var track = TrackSplineRef.Instance;
        if (track == null || track.container == null)
            return;

        var container = track.container;
        var spline = container.Spline;

        float L = track.TrackLength;
        if (L <= 1e-6f)
            return;

        float dt = Time.deltaTime;
        float speed = (rb != null) ? rb.velocity.magnitude : 0f;
        float maxStepMeters = Mathf.Max(2f, speed * dt * maxStepFactor); // 最小给2m，避免低速抖动过严

        // ====== 1) 全局最近点 ======
        // NOTE: SplineUtility APIs operate in the spline's local space.
        // Convert world position into container-local space before projecting.
        Vector3 posW = transform.position;
        Vector3 posLWorld = container.transform.InverseTransformPoint(posW);

        float3 nearest;
        float t;
        SplineUtility.GetNearestPoint(spline, (float3)posLWorld, out nearest, out t);
        float tGlobal = Mathf.Repeat(t, 1f);
        float dGlobal = track.DistanceAtT(tGlobal);

        // 当前点离样条太远：不要更新（比如飞出去/撞飞到旁边段附近）
        float3 posGlobalL, tanGlobalL, upGlobalL;
        SplineUtility.Evaluate(spline, tGlobal, out posGlobalL, out tanGlobalL, out upGlobalL);
        Vector3 pGlobalW = container.transform.TransformPoint((Vector3)posGlobalL);
        if ((posW - pGlobalW).sqrMagnitude > maxProjectionDistance * maxProjectionDistance)
        {
            // 仍然更新 wrong-way（用当前朝向大概估一下也行）
            // 但进度就保持上次
            if (!_hasLast)
            {
                distanceOnTrack = dGlobal;
                progress01 = dGlobal / L;
                _lastT01 = tGlobal;
                _lastDistance = dGlobal;
                _hasLast = true;
            }
            return;
        }

        if (!_hasLast)
        {
            _hasLast = true;
            _lastT01 = tGlobal;
            _lastDistance = dGlobal;
        }

        // ====== 2) 本地窗口最近点（围绕上一帧 t） ======
        float tLocal = FindNearestTInWindow(track, posW, _lastT01);
        float dLocal = track.DistanceAtT(tLocal);

        // 计算两个候选相对上一帧的“环形增量”
        float deltaGlobal = CircularDelta(dGlobal, _lastDistance, L);
        float deltaLocal  = CircularDelta(dLocal,  _lastDistance, L);

        // 选“更连续”的那个
        float chosenT = tGlobal;
        float chosenD = dGlobal;
        float chosenDelta = deltaGlobal;

        // 只要全局出现异常跳变，优先考虑本地窗口；否则两者择优
        bool globalLooksJump = Mathf.Abs(deltaGlobal) > jumpMetersThreshold;
        if (globalLooksJump || Mathf.Abs(deltaLocal) < Mathf.Abs(deltaGlobal))
        {
            chosenT = tLocal;
            chosenD = dLocal;
            chosenDelta = deltaLocal;
        }

        // ====== 3) 速度限制：硬切掉单帧不可能的跳跃 ======
        if (Mathf.Abs(chosenDelta) > maxStepMeters)
        {
            // 用“上一帧距离 + 被限制的delta”得到新距离（连续）
            chosenDelta = Mathf.Clamp(chosenDelta, -maxStepMeters, maxStepMeters);
            chosenD = Mathf.Repeat(_lastDistance + chosenDelta, L);

            // t 不强求精确，保留上一帧附近即可（避免抖动）
            chosenT = _lastT01;
        }

        // 输出
        distanceOnTrack = chosenD;
        progress01 = chosenD / L;

        // 更新缓存
        _lastDistance = chosenD;
        _lastT01 = chosenT;

        // ====== Wrong-way：用 chosenT 对应的切线做 dot ======
        float3 posL, tanL, upL;
        SplineUtility.Evaluate(spline, chosenT, out posL, out tanL, out upL);
        Vector3 tangentW = container.transform.TransformDirection((Vector3)tanL);
        tangentW.y = 0f;
        tangentW = tangentW.sqrMagnitude > 0.0001f ? tangentW.normalized : transform.forward;

        Vector3 v = rb ? rb.velocity : Vector3.zero;
        v.y = 0f;
        Vector3 vDir = v.sqrMagnitude > 0.01f ? v.normalized : transform.forward;

        forwardDot = Vector3.Dot(vDir, tangentW);
    }


    private float CircularDelta(float cur, float prev, float loopLength)
    {
        float d = cur - prev;
        if (loopLength <= 0.0001f) return d;

        if (d >  loopLength * 0.5f) d -= loopLength;
        if (d < -loopLength * 0.5f) d += loopLength;
        return d;
    }

    private float FindNearestTInWindow(TrackSplineRef track, Vector3 pos, float centerT01)
    {
        var container = track.container;
        var spline = container.Spline;
        Vector3 posLocal = container.transform.InverseTransformPoint(pos);

        float bestT = centerT01;
        float bestD2 = float.MaxValue;

        for (int i = 0; i <= windowSteps; i++)
        {
            float u = (float)i / windowSteps; // 0..1
            float t = centerT01 - windowRadiusT + 2f * windowRadiusT * u;
            t = Mathf.Repeat(t, 1f);

            float3 pL, tanL, upL;
            SplineUtility.Evaluate(spline, t, out pL, out tanL, out upL);
            Vector3 pLocal = (Vector3)pL;
            float d2 = (pLocal - posLocal).sqrMagnitude;
            if (d2 < bestD2)
            {
                bestD2 = d2;
                bestT = t;
            }
        }
        return bestT;
    }

}
