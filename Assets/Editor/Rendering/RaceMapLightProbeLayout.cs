// Generates RaceMap's light probe layout from the track spline. Menu: BuddahGo/Rendering/Generate RaceMap Light Probes.
//
// Players are lit per object from the probe tetrahedra around them, so the track needs probes where players drive:
// along the MapPathLine spline every StepAlongTrack metres, at the centre and just inside each wall (measured per
// sample with BuddahGo.AI.TrackWallProbe) at player height, plus one row above the player model. A coarse grid over
// the visible sand (skipping points near the track centre line) and a grid over the end field complete the hull, so no
// region the cameras show is extrapolated. Probes that touch colliders or lie inside closed geometry are dropped,
// because a probe inside geometry bakes black and darkens everything that interpolates it.
// Re-run after the track changes, then rebake lighting.
using System.Collections.Generic;
using BuddahGo.AI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace BuddahGo.Rendering.Editor
{
    public static class RaceMapLightProbeLayout
    {
        const string ProbeGroupName = "Light Probe Group";
        const string TrackSplineName = "MapPathLine";
        const string EndFieldName = "EndField";

        const float TrackSurfaceY = 2.62f;   // top of the lane mesh, measured by raycast
        const float SandSurfaceY = 0f;       // rendered sand plane (its BoxCollider top is at -2.33)
        const float StepAlongTrack = 20f;
        const float WallInset = 2f;          // side probes stay this far inside the measured wall
        const float MaxSideOffset = 26f;     // used where a side has no wall within TrackWallProbe.MaxDistance
        static readonly float[] TrackHeights = { 1.5f, 10f };   // above the lane: player body, top of the player model
        const float GridStep = 160f;
        const float GridMargin = 320f;
        const float TrackFootprint = 40f;    // grid points closer than this to the centre line would sit inside the track body
        static readonly float[] GridHeights = { 1.5f, 40f };    // above the sand
        const float EndFieldHalfSize = 120f;
        const float EndFieldStep = 40f;
        static readonly float[] EndFieldHeights = { 1.5f, 10f };
        const float ClearanceRadius = 0.5f;

        [MenuItem("BuddahGo/Rendering/Generate RaceMap Light Probes")]
        static void GenerateFromMenu()
        {
            Debug.Log(Generate());
        }

        public static string Generate()
        {
            var group = FindProbeGroup();
            var splineGo = GameObject.Find(TrackSplineName);
            var container = splineGo != null ? splineGo.GetComponent<SplineContainer>() : null;
            if (group == null || container == null)
                return "FAILED: needs '" + ProbeGroupName + "' and '" + TrackSplineName + "' in the open scene";

            var probeColliders = new List<GameObject>();
            var previousBackfaces = Physics.queriesHitBackfaces;
            try
            {
                CreateTemporaryPropColliders(probeColliders);
                Physics.queriesHitBackfaces = true;
                return GenerateLayout(group, container);
            }
            finally
            {
                Physics.queriesHitBackfaces = previousBackfaces;
                foreach (var go in probeColliders)
                    if (go != null)
                        Object.DestroyImmediate(go);
            }
        }

        static string GenerateLayout(LightProbeGroup group, SplineContainer container)
        {
            Physics.SyncTransforms();
            var world = new List<Vector3>();
            int rejected = 0;

            // --- along the track -------------------------------------------------------------------------------
            float length = container.Spline.CalculateLength(container.transform.localToWorldMatrix);
            int samples = Mathf.Max(8, Mathf.CeilToInt(length / StepAlongTrack));
            var centres = new Vector3[samples];
            for (int i = 0; i < samples; i++)
            {
                Vector3 p = container.EvaluatePosition((float)i / samples);
                p.y = TrackSurfaceY;
                centres[i] = p;
            }
            float[] left, right;
            TrackWallProbe.Measure(centres, out left, out right);

            var trackBounds = new Bounds(centres[0], Vector3.zero);
            for (int i = 0; i < samples; i++)
            {
                Vector3 delta = centres[(i + 1) % samples] - centres[i];
                delta.y = 0f;
                Vector3 tangent = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(Vector3.up, tangent);   // TrackWallProbe's "right"
                float rightOffset = SideOffset(right[i]);
                float leftOffset = SideOffset(left[i]);
                foreach (float h in TrackHeights)
                {
                    rejected += Add(world, centres[i] + Vector3.up * h);
                    if (h < 5f)
                    {
                        rejected += Add(world, centres[i] + side * rightOffset + Vector3.up * h);
                        rejected += Add(world, centres[i] - side * leftOffset + Vector3.up * h);
                    }
                }
                trackBounds.Encapsulate(centres[i]);
            }

            // --- sand grid around the track, skipping the track footprint ---------------------------------------
            var gridBounds = trackBounds;
            gridBounds.Expand(new Vector3(GridMargin * 2f, 0f, GridMargin * 2f));
            int skipped = 0;
            for (float x = gridBounds.min.x; x <= gridBounds.max.x + 0.1f; x += GridStep)
                for (float z = gridBounds.min.z; z <= gridBounds.max.z + 0.1f; z += GridStep)
                {
                    if (DistanceToTrack(new Vector3(x, 0f, z), centres) < TrackFootprint) { skipped++; continue; }
                    foreach (float h in GridHeights)
                        rejected += Add(world, new Vector3(x, SandSurfaceY + h, z));
                }

            // --- end field (results ceremony), outside the lap's grid ------------------------------------------
            int endProbes = 0;
            var endField = GameObject.Find(EndFieldName);
            var floor = endField != null ? endField.transform.Find("Floor") : null;
            if (floor != null)
            {
                Vector3 c = floor.position;
                for (float x = -EndFieldHalfSize; x <= EndFieldHalfSize + 0.1f; x += EndFieldStep)
                    for (float z = -EndFieldHalfSize; z <= EndFieldHalfSize + 0.1f; z += EndFieldStep)
                        foreach (float h in EndFieldHeights)
                        {
                            int before = world.Count;
                            rejected += Add(world, new Vector3(c.x + x, c.y + h, c.z + z));
                            endProbes += world.Count - before;
                        }
            }

            var local = new Vector3[world.Count];
            for (int i = 0; i < world.Count; i++)
                local[i] = group.transform.InverseTransformPoint(world[i]);
            Undo.RecordObject(group, "Generate RaceMap light probes");
            group.probePositions = local;
            EditorUtility.SetDirty(group);

            return "probes=" + world.Count + " (track samples " + samples + " over " + length.ToString("F0") + " m, end-field grid " + endProbes
                + ", grid points skipped near the centre line " + skipped + ", rejected inside geometry " + rejected + ")";
        }

        static float SideOffset(float wallDistance)
        {
            if (float.IsInfinity(wallDistance))
                return MaxSideOffset;
            return Mathf.Max(0f, wallDistance - WallInset);
        }

        /// <summary>
        /// Adds a probe unless it touches a collider or lies inside closed geometry. Returns 1 when rejected.
        /// Inside test: with backface hits enabled, the first surface hit upwards and sideways is a back face.
        /// </summary>
        static int Add(List<Vector3> list, Vector3 p)
        {
            if (Physics.CheckSphere(p, ClearanceRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return 1;
            if (IsInsideGeometry(p))
                return 1;
            list.Add(p);
            return 0;
        }

        static readonly Vector3[] InsideTestDirections = { Vector3.up, Vector3.right, Vector3.forward };

        static bool IsInsideGeometry(Vector3 p)
        {
            foreach (var dir in InsideTestDirections)
            {
                RaycastHit hit;
                if (!Physics.Raycast(p, dir, out hit, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    return false;   // escapes to open space in this direction
                if (Vector3.Dot(hit.normal, dir) <= 0f)
                    return false;   // hit a front face first: outside
            }
            return true;
        }

        /// <summary>
        /// Static props without colliders (the structure at the lap start) still block light in the bake, so give each
        /// a temporary MeshCollider for the probe tests. The flat ground planes are skipped: probes sit above them.
        /// </summary>
        static void CreateTemporaryPropColliders(List<GameObject> result)
        {
            foreach (var r in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (!r.gameObject.isStatic || !r.enabled || r.bounds.size.y < 0.5f)
                    continue;
                if (r.GetComponentInChildren<Collider>() != null)
                    continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                    continue;
                var go = new GameObject("RaceMapProbeTestCollider") { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                go.transform.localScale = r.transform.lossyScale;
                go.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                result.Add(go);
            }
        }

        static float DistanceToTrack(Vector3 p, Vector3[] centres)
        {
            float best = float.MaxValue;
            foreach (var c in centres)
            {
                float dx = c.x - p.x, dz = c.z - p.z;
                best = Mathf.Min(best, dx * dx + dz * dz);
            }
            return Mathf.Sqrt(best);
        }

        static LightProbeGroup FindProbeGroup()
        {
            foreach (var g in Object.FindObjectsOfType<LightProbeGroup>(true))
                if (g.name == ProbeGroupName)
                    return g;
            return null;
        }
    }
}
