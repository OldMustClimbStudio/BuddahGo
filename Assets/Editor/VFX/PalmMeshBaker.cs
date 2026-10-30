using UnityEditor;
using UnityEngine;

namespace BuddahGo.VFX.EditorTools
{
    // Bakes the palm body mesh (Docs/vfx/push-projectile-hands.md 4.4, section 8 item 2) from Assets/Sources/Buddah/
    // 佛手Projectile.fbx, which stays untouched: an upright palm-forward hand (+Z palm, +Y fingers, 49k triangles)
    // standing on a display base. The bake cuts the base off, fans the four fingers slightly apart ("五指微张"),
    // reduces the mesh below the 3000-triangle budget and fits its bounds to the reference collider (60 × 90 × 20,
    // centred, palm facing +Z) that SkillVfxPhaseController rescales to the live collider.
    internal static class PalmMeshBaker
    {
        const string SourceGuid = "8ab0269dfb6aeff41b475e530de8bb9b";
        internal const string OutputPath = "Assets/VFX/如来神掌/Mesh_FX_Palm.asset";
        internal static readonly Vector3 ReferenceCollider = new Vector3(60f, 90f, 20f);

        // The base occupies the bottom 7.5 % of the source bounds (cross-section measured 2026-10-28).
        const float BaseCutFraction = 0.085f;
        const float KnuckleHeight = 0.70f;
        const float FanBlendHeight = 0.10f;
        const float FanDegrees = 8f;
        const int TargetTriangles = 2800;

        [MenuItem("Tools/BuddahGo/VFX/Bake Palm Mesh")]
        internal static void BakeMenu()
        {
            Mesh mesh = Bake();
            Debug.Log("[PalmMeshBaker] " + OutputPath + ": " + mesh.triangles.Length / 3 + " triangles, bounds " + mesh.bounds.size);
        }

        internal static Mesh Bake()
        {
            Mesh source = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(SourceGuid)))
            {
                if (asset is Mesh m)
                    source = m;
            }
            if (source == null)
                throw new System.InvalidOperationException("佛手Projectile.fbx mesh not found (GUID " + SourceGuid + ")");

            Vector3[] positions = source.vertices;
            int[] triangles = source.triangles;
            Bounds bounds = source.bounds;

            // Normalise: base cut at 0, fingertip at 1.
            float cutY = bounds.min.y + bounds.size.y * BaseCutFraction;
            float height = bounds.max.y - cutY;
            var normalised = new Vector3[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i];
                normalised[i] = new Vector3(p.x - bounds.center.x, p.y - cutY, p.z - bounds.center.z) / height;
            }

            var kept = new System.Collections.Generic.List<int>(triangles.Length);
            for (int t = 0; t < triangles.Length; t += 3)
            {
                if (normalised[triangles[t]].y < 0f || normalised[triangles[t + 1]].y < 0f || normalised[triangles[t + 2]].y < 0f)
                    continue;
                kept.Add(triangles[t]); kept.Add(triangles[t + 1]); kept.Add(triangles[t + 2]);
            }

            FanFingers(normalised);

            FxMeshSimplifier.Weld(normalised, kept.ToArray(), 1e-5f, out Vector3[] welded, out int[] weldedTriangles);
            FxMeshSimplifier.Simplify(welded, weldedTriangles, TargetTriangles, 7.0, out Vector3[] simplified, out int[] simplifiedTriangles);

            // Fit the bounds to the reference collider, centred on the origin.
            var fitBounds = new Bounds(simplified[0], Vector3.zero);
            foreach (Vector3 p in simplified)
                fitBounds.Encapsulate(p);
            var scale = new Vector3(ReferenceCollider.x / fitBounds.size.x, ReferenceCollider.y / fitBounds.size.y, ReferenceCollider.z / fitBounds.size.z);
            for (int i = 0; i < simplified.Length; i++)
                simplified[i] = Vector3.Scale(simplified[i] - fitBounds.center, scale);

            Mesh output = AssetDatabase.LoadAssetAtPath<Mesh>(OutputPath);
            bool create = output == null;
            if (create)
                output = new Mesh();
            output.Clear();
            output.name = "Mesh_FX_Palm";
            output.vertices = simplified;
            output.triangles = simplifiedTriangles;
            output.RecalculateNormals();
            output.RecalculateBounds();
            output.RecalculateTangents();
            if (create)
                AssetDatabase.CreateAsset(output, OutputPath);
            else
                EditorUtility.SetDirty(output);
            AssetDatabase.SaveAssets();
            return output;
        }

        // Rotates everything above the knuckles about a pivot on the knuckle line, by an angle proportional to the
        // vertex's sideways position, blended in over FanBlendHeight. Continuous, so nothing tears at the finger roots.
        static void FanFingers(Vector3[] positions)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (Vector3 p in positions)
            {
                if (p.y < KnuckleHeight + FanBlendHeight)
                    continue;
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
            }
            if (minX > maxX)
                return;

            float centreX = (minX + maxX) * 0.5f;
            float halfWidth = Mathf.Max(1e-4f, (maxX - minX) * 0.5f);
            var pivot = new Vector2(centreX, KnuckleHeight);
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i];
                float weight = Mathf.SmoothStep(0f, 1f, (p.y - KnuckleHeight) / FanBlendHeight);
                if (weight <= 0f)
                    continue;
                float side = Mathf.Clamp((p.x - centreX) / halfWidth, -1f, 1f);
                float angle = -FanDegrees * Mathf.Deg2Rad * side * weight;
                float dx = p.x - pivot.x, dy = p.y - pivot.y;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                positions[i] = new Vector3(pivot.x + dx * cos - dy * sin, pivot.y + dx * sin + dy * cos, p.z);
            }
        }
    }
}
