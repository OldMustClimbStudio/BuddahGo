using UnityEngine;

namespace BuddahGo.Match
{
    // Pure placement math for the Practice training dummies (README A1): dummy i stands at
    // startDistance + startOffset + spacing * i along the race direction (+tangent), spread left / centre / right
    // along Cross(up, tangent), facing along the flat tangent. Scene queries (spline sampling, wall and ground
    // raycasts) stay in MatchSpawnManager.
    internal static class TrainingDummyLayout
    {
        // Distance along the closed track in metres, wrapped into [0, trackLength).
        internal static float DistanceAlongTrack(float startDistance, float startOffset, float spacing, int index, float trackLength)
        {
            if (trackLength <= 0f)
                return 0f;
            return Mathf.Repeat(startDistance + startOffset + spacing * index, trackLength);
        }

        // -1 (left) .. +1 (right), evenly spread; a single dummy stands on the centre line.
        internal static float LateralFactor(int index, int count)
        {
            if (count <= 1)
                return 0f;
            return 2f * Mathf.Clamp(index, 0, count - 1) / (count - 1) - 1f;
        }

        // Signed side offset (negative = left of the racing direction). Each side dummy keeps `clearance` metres
        // (body radius plus margin) from the wall on its own side; an unmeasured wall (infinity) keeps the desired offset.
        internal static float ResolveLateralOffset(int index, int count, float desiredOffset, float leftWallDistance, float rightWallDistance, float clearance)
        {
            float factor = LateralFactor(index, count);
            if (Mathf.Approximately(factor, 0f))
                return 0f;
            float wall = factor < 0f ? leftWallDistance : rightWallDistance;
            float magnitude = Mathf.Abs(desiredOffset * factor);
            if (!float.IsPositiveInfinity(wall))
                magnitude = Mathf.Min(magnitude, Mathf.Max(0f, wall - clearance));
            return Mathf.Sign(factor) * magnitude;
        }

        // Placement candidate `attempt` of `attempts`: the resolved offset first, then evenly closer to the racing line,
        // the last candidate on it. A single attempt keeps the offset.
        internal static float LateralCandidate(float lateralOffset, int attempt, int attempts)
        {
            if (attempts <= 1)
                return lateralOffset;
            float remaining = 1f - Mathf.Clamp(attempt, 0, attempts - 1) / (float)(attempts - 1);
            return lateralOffset * remaining;
        }

        internal static Vector3 FlatTangent(Vector3 tangent)
        {
            tangent.y = 0f;
            return tangent.sqrMagnitude > 1e-6f ? tangent.normalized : Vector3.forward;
        }

        // Same convention as TrackWallProbe: right = Cross(up, tangent).
        internal static Vector3 RightOf(Vector3 tangent) => Vector3.Cross(Vector3.up, FlatTangent(tangent)).normalized;

        internal static Quaternion FacingAlong(Vector3 tangent) => Quaternion.LookRotation(FlatTangent(tangent), Vector3.up);

        internal static Vector3 Place(Vector3 trackPoint, Vector3 tangent, float lateralOffset) => trackPoint + RightOf(tangent) * lateralOffset;
    }
}
