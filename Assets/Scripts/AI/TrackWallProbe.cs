using UnityEngine;

namespace BuddahGo.AI
{
    // Sideways raycasts from each racing-line sample to the nearest wall collider on either side.
    // Distances are measured from the sample point along Cross(up, tangent) (right) and its negative
    // (left), from 1.5 m above the sample, ignoring triggers. A side with no hit within MaxDistance is
    // reported as float.PositiveInfinity (open section). Pure physics queries; nothing is written.
    public static class TrackWallProbe
    {
        public const float MaxDistance = 80f, Height = 1.5f;

        public static void Measure(Vector3[] points, out float[] left, out float[] right, int layerMask = Physics.DefaultRaycastLayers)
        {
            int n = points.Length;
            left = new float[n]; right = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 delta = points[(i + 1) % n] - points[i]; delta.y = 0f;
                Vector3 tangent = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector3.forward;
                Vector3 normal = Vector3.Cross(Vector3.up, tangent);
                Vector3 origin = points[i] + Vector3.up * Height;
                right[i] = Physics.Raycast(origin, normal, out RaycastHit hitRight, MaxDistance, layerMask, QueryTriggerInteraction.Ignore)
                    ? hitRight.distance : float.PositiveInfinity;
                left[i] = Physics.Raycast(origin, -normal, out RaycastHit hitLeft, MaxDistance, layerMask, QueryTriggerInteraction.Ignore)
                    ? hitLeft.distance : float.PositiveInfinity;
            }
        }
    }
}
