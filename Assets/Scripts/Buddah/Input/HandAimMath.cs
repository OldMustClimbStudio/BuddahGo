using UnityEngine;

internal enum HandAimSampleKind
{
    /// <summary>The cursor ray never reaches the aim plane (or the body forward is vertical): use the arrow-key fallback.</summary>
    Miss,
    /// <summary>The cursor is too close to the body to give a stable direction: hold the current target.</summary>
    InsideDeadRadius,
    /// <summary>The yaw offset is valid.</summary>
    Aim
}

/// <summary>
/// Mouse hand aim, pure math. The yaw offset is body-relative: the server rebuilds the push direction as
/// AngleAxis(yawOffset, up) * movement-root forward, so the offset is measured from the flattened movement-root forward.
/// </summary>
internal static class HandAimMath
{
    private const float MinPlanarSqrMagnitude = 1e-6f;

    /// <summary>
    /// Projects the cursor ray onto the horizontal plane y = planeHeight and returns the signed yaw (degrees, −180..180,
    /// positive = clockwise seen from above) from the flattened body forward to the planar direction body → hit.
    /// </summary>
    internal static HandAimSampleKind TryGetTargetYawOffset(Ray cursorRay, float planeHeight, Vector3 bodyPosition,
        Vector3 bodyForward, float deadRadius, out float yawOffsetDeg)
    {
        yawOffsetDeg = 0f;

        Vector3 flatForward = new Vector3(bodyForward.x, 0f, bodyForward.z);
        if (flatForward.sqrMagnitude < MinPlanarSqrMagnitude)
            return HandAimSampleKind.Miss;

        var plane = new Plane(Vector3.up, new Vector3(0f, planeHeight, 0f));
        if (!plane.Raycast(cursorRay, out float enter))
            return HandAimSampleKind.Miss;

        Vector3 hit = cursorRay.GetPoint(enter);
        Vector3 planar = new Vector3(hit.x - bodyPosition.x, 0f, hit.z - bodyPosition.z);
        float safeDeadRadius = Mathf.Max(0f, deadRadius);
        if (planar.sqrMagnitude <= safeDeadRadius * safeDeadRadius || planar.sqrMagnitude < MinPlanarSqrMagnitude)
            return HandAimSampleKind.InsideDeadRadius;

        yawOffsetDeg = Vector3.SignedAngle(flatForward, planar, Vector3.up);
        return HandAimSampleKind.Aim;
    }

    /// <summary>
    /// Exponential smoothing of an angle toward a target along the short way. The result stays continuous with
    /// <paramref name="currentDeg"/> (no wrapping), so unwrapped values are fine. timeConstant ≤ 0 snaps to the target.
    /// </summary>
    internal static float SmoothAngle(float currentDeg, float targetDeg, float deltaTime, float timeConstantSeconds)
    {
        if (deltaTime <= 0f)
            return currentDeg;

        float gap = Mathf.DeltaAngle(currentDeg, targetDeg);
        if (timeConstantSeconds <= 0f)
            return currentDeg + gap;

        float alpha = 1f - Mathf.Exp(-deltaTime / timeConstantSeconds);
        return currentDeg + gap * alpha;
    }

    /// <summary>
    /// The signed step (degrees) from <paramref name="currentDeg"/> toward <paramref name="targetDeg"/> along the short way,
    /// clamped to ±maxDegPerSecond·deltaTime. Add it to an unwrapped yaw to keep that yaw continuous.
    /// </summary>
    internal static float RateLimitedStep(float currentDeg, float targetDeg, float maxDegPerSecond, float deltaTime)
    {
        float maxStep = Mathf.Max(0f, maxDegPerSecond) * Mathf.Max(0f, deltaTime);
        return Mathf.Clamp(Mathf.DeltaAngle(currentDeg, targetDeg), -maxStep, maxStep);
    }
}
