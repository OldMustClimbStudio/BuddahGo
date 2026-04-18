using FishNet.Object;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class SplineProgressTracker : NetworkBehaviour
{
    private const float ProjectionDiagnosticHeartbeatSeconds = 0.25f;
    private const float ProjectionDiagnosticDeltaDifferenceMeters = 3f;
    private const float IntroProjectionMinStepMeters = 4f;
    private const float ModerateMovementProjectionMinStepMeters = 2.5f;
    private const float FastMovementProjectionMinStepMeters = 3.5f;

    [Header("Read Only")]
    [Range(0f, 1f)] public float progress01;
    public float distanceOnTrack;
    public float forwardDot;
    [SerializeField, Range(0f, 1f)] private float previousProgress01;
    [SerializeField] private float rawProgressDelta01;
    [SerializeField] private bool wrappedFromStartToEndThisFrame;
    [SerializeField] private bool wrappedFromEndToStartThisFrame;

    [SerializeField] private Rigidbody rb;
    [SerializeField] private RaceBodyIntroStateController introStateController;
    private float _lastT01;
    private float _lastDistance;
    private bool _hasLast;
    private float _lastProjectionDiagnosticLogTime = float.NegativeInfinity;
    private float _lastChosenDeltaMeters;
    private bool _hasLastChosenDeltaMeters;

    [SerializeField] private float jumpMetersThreshold = 8f;
    [SerializeField] private float windowRadiusT = 0.1f;
    [SerializeField] private int windowSteps = 40;
    [SerializeField] private float maxProjectionDistance = 40f;
    [SerializeField] private float maxStepFactor = 1.5f;

    public float PreviousProgress01 => previousProgress01;
    public float RawProgressDelta01 => rawProgressDelta01;
    public bool WrappedFromStartToEndThisFrame => wrappedFromStartToEndThisFrame;
    public bool WrappedFromEndToStartThisFrame => wrappedFromEndToStartThisFrame;
    public bool IsClearlyWrongWay => forwardDot < 0f;

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        if (introStateController == null)
            introStateController = GetComponent<RaceBodyIntroStateController>();
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
        float maxStepMeters = Mathf.Max(2f, speed * dt * maxStepFactor);
        bool introActive = introStateController != null && introStateController.IsIntroActive;
        if (introActive)
            maxStepMeters = Mathf.Max(maxStepMeters, IntroProjectionMinStepMeters);
        else if (speed >= 25f)
            maxStepMeters = Mathf.Max(maxStepMeters, FastMovementProjectionMinStepMeters);
        else if (speed >= 15f)
            maxStepMeters = Mathf.Max(maxStepMeters, ModerateMovementProjectionMinStepMeters);

        Vector3 posW = transform.position;
        Vector3 posLWorld = container.transform.InverseTransformPoint(posW);

        float3 nearest;
        float t;
        SplineUtility.GetNearestPoint(spline, (float3)posLWorld, out nearest, out t);
        float tGlobal = Mathf.Repeat(t, 1f);
        float dGlobal = track.DistanceAtT(tGlobal);

        float3 posGlobalL, tanGlobalL, upGlobalL;
        SplineUtility.Evaluate(spline, tGlobal, out posGlobalL, out tanGlobalL, out upGlobalL);
        Vector3 pGlobalW = container.transform.TransformPoint((Vector3)posGlobalL);
        if ((posW - pGlobalW).sqrMagnitude > maxProjectionDistance * maxProjectionDistance)
        {
            if (!_hasLast)
            {
                ApplyProgressState(dGlobal / L, dGlobal, tGlobal);
            }
            return;
        }

        if (!_hasLast)
        {
            ApplyProgressState(dGlobal / L, dGlobal, tGlobal);
        }

        float tLocal = FindNearestTInWindow(track, posW, _lastT01);
        float dLocal = track.DistanceAtT(tLocal);

        float deltaGlobal = CircularDelta(dGlobal, _lastDistance, L);
        float deltaLocal = CircularDelta(dLocal, _lastDistance, L);

        float chosenT = tGlobal;
        float chosenD = dGlobal;
        float chosenDelta = deltaGlobal;

        bool globalLooksJump = Mathf.Abs(deltaGlobal) > jumpMetersThreshold;
        if (globalLooksJump || Mathf.Abs(deltaLocal) < Mathf.Abs(deltaGlobal))
        {
            chosenT = tLocal;
            chosenD = dLocal;
            chosenDelta = deltaLocal;
        }

        if (Mathf.Abs(chosenDelta) > maxStepMeters)
        {
            chosenDelta = Mathf.Clamp(chosenDelta, -maxStepMeters, maxStepMeters);
            chosenD = Mathf.Repeat(_lastDistance + chosenDelta, L);
            chosenT = _lastT01;
        }

        ApplyProgressState(chosenD / L, chosenD, chosenT);

        float3 posL, tanL, upL;
        SplineUtility.Evaluate(spline, chosenT, out posL, out tanL, out upL);
        Vector3 tangentW = container.transform.TransformDirection((Vector3)tanL);
        tangentW.y = 0f;
        tangentW = tangentW.sqrMagnitude > 0.0001f ? tangentW.normalized : transform.forward;

        Vector3 v = rb ? rb.velocity : Vector3.zero;
        v.y = 0f;
        Vector3 vDir = v.sqrMagnitude > 0.01f ? v.normalized : transform.forward;

        forwardDot = Vector3.Dot(vDir, tangentW);
        UpdateWrapFlags();
        MaybeLogProjectionDiagnostics(
            introActive,
            speed,
            maxStepMeters,
            tGlobal,
            dGlobal,
            deltaGlobal,
            tLocal,
            dLocal,
            deltaLocal,
            chosenT,
            chosenD,
            chosenDelta,
            globalLooksJump);
    }

    public void SnapToTrackProgress(float targetProgress01)
    {
        targetProgress01 = Mathf.Clamp01(targetProgress01);
        TrackSplineRef track = TrackSplineRef.Instance;
        if (track == null || track.TrackLength <= 1e-6f)
            return;

        float distance = targetProgress01 * track.TrackLength;
        float t = track.TAtProgress01(targetProgress01);
        ApplyProgressState(targetProgress01, distance, t);
        forwardDot = 0f;
        UpdateWrapFlags();
    }

    public void SnapToWorldPosition(Vector3 worldPosition)
    {
        TrackSplineRef track = TrackSplineRef.Instance;
        if (track == null || track.container == null || track.TrackLength <= 1e-6f)
            return;

        SplineContainer container = track.container;
        Vector3 localPosition = container.transform.InverseTransformPoint(worldPosition);
        SplineUtility.GetNearestPoint(container.Spline, (float3)localPosition, out _, out float t);
        float t01 = Mathf.Repeat(t, 1f);
        float distance = track.DistanceAtT(t01);
        ApplyProgressState(distance / track.TrackLength, distance, t01);
        forwardDot = 0f;
        UpdateWrapFlags();
    }

    private void ApplyProgressState(float newProgress01, float newDistance, float newT01)
    {
        float clampedProgress01 = Mathf.Clamp01(newProgress01);
        previousProgress01 = progress01;
        progress01 = clampedProgress01;
        distanceOnTrack = Mathf.Max(0f, newDistance);
        rawProgressDelta01 = progress01 - previousProgress01;
        _lastDistance = distanceOnTrack;
        _lastT01 = Mathf.Repeat(newT01, 1f);
        _hasLast = true;
    }

    private void UpdateWrapFlags()
    {
        wrappedFromStartToEndThisFrame = previousProgress01 <= 0.2f && progress01 >= 0.8f;
        wrappedFromEndToStartThisFrame = previousProgress01 >= 0.8f && progress01 <= 0.2f;
    }

    private float CircularDelta(float cur, float prev, float loopLength)
    {
        float d = cur - prev;
        if (loopLength <= 0.0001f) return d;

        if (d > loopLength * 0.5f) d -= loopLength;
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
        float searchRadius = windowRadiusT;

        for (int i = 0; i <= windowSteps; i++)
        {
            float u = (float)i / windowSteps;
            float t = centerT01 - searchRadius + 2f * searchRadius * u;
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

        float refineRadius = Mathf.Max(searchRadius / Mathf.Max(1, windowSteps), 0.0005f);
        for (int pass = 0; pass < 3; pass++)
        {
            float passBestT = bestT;
            float passBestD2 = bestD2;

            for (int i = -2; i <= 2; i++)
            {
                float u = i / 2f;
                float t = Mathf.Repeat(bestT + u * refineRadius, 1f);

                float3 pL, tanL, upL;
                SplineUtility.Evaluate(spline, t, out pL, out tanL, out upL);
                Vector3 pLocal = (Vector3)pL;
                float d2 = (pLocal - posLocal).sqrMagnitude;
                if (d2 < passBestD2)
                {
                    passBestD2 = d2;
                    passBestT = t;
                }
            }

            bestT = passBestT;
            bestD2 = passBestD2;
            refineRadius *= 0.5f;
        }

        return bestT;
    }

    private void MaybeLogProjectionDiagnostics(
        bool introActive,
        float speed,
        float maxStepMeters,
        float tGlobal,
        float dGlobal,
        float deltaGlobal,
        float tLocal,
        float dLocal,
        float deltaLocal,
        float chosenT,
        float chosenD,
        float chosenDelta,
        bool globalLooksJump)
    {
        if (!IsOwner)
        {
            _lastChosenDeltaMeters = chosenDelta;
            _hasLastChosenDeltaMeters = true;
            return;
        }

        bool signFlip = _hasLastChosenDeltaMeters
                        && Mathf.Abs(chosenDelta) > 0.01f
                        && Mathf.Abs(_lastChosenDeltaMeters) > 0.01f
                        && Mathf.Sign(chosenDelta) != Mathf.Sign(_lastChosenDeltaMeters);
        bool localGlobalDisagree = Mathf.Abs(deltaLocal - deltaGlobal) > ProjectionDiagnosticDeltaDifferenceMeters;
        bool nearClamp = Mathf.Abs(chosenDelta) >= maxStepMeters * 0.95f;
        bool periodic = introActive && (Time.unscaledTime - _lastProjectionDiagnosticLogTime) >= ProjectionDiagnosticHeartbeatSeconds;

        if (!periodic && !globalLooksJump && !localGlobalDisagree && !nearClamp && !signFlip)
        {
            _lastChosenDeltaMeters = chosenDelta;
            _hasLastChosenDeltaMeters = true;
            return;
        }

        _lastProjectionDiagnosticLogTime = Time.unscaledTime;
        if (NetDebug.EnableVerboseLog)
        {
            Debug.Log(
                $"[SplineDiag][Tracker:{name}] introActive={introActive} progress={progress01:0.000} prev={previousProgress01:0.000} " +
                $"chosenDeltaM={chosenDelta:0.000} globalDeltaM={deltaGlobal:0.000} localDeltaM={deltaLocal:0.000} " +
                $"chosenT={chosenT:0.000} globalT={tGlobal:0.000} localT={tLocal:0.000} " +
                $"speed={speed:0.000} maxStep={maxStepMeters:0.000} forwardDot={forwardDot:0.000} " +
                $"flags(globalJump={globalLooksJump}, localGlobalDisagree={localGlobalDisagree}, nearClamp={nearClamp}, signFlip={signFlip})");
        }

        _lastChosenDeltaMeters = chosenDelta;
        _hasLastChosenDeltaMeters = true;
    }
}
