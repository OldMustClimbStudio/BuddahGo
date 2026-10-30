using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.VFX.EditorTools
{
    // Bakes the two 3D textures FX/InkVolume ray-marches through (Docs/vfx/palm-mesh-tripo.md section 10):
    // - BakeSdf: a signed distance field of a mesh asset on a grid covering its bounds plus a margin. Distance is the
    //   exact point-triangle distance (brute force, parallel over slices); the sign comes from ray-parity votes along
    //   the three axes through a temporary MeshCollider, so it is robust on a closed mesh. Stored as RHalf in object
    //   units, texture coordinates map the grid box (volume origin + size are returned for the material).
    // - BakeNoise3D: a tiling value-noise fbm cube (R8, 0..1), the ink's churning noise.
    public static class PalmVolumeBaker
    {
        public static string BakeSdf(string meshAssetPath, string outputAssetPath, int rx, int ry, int rz, float margin)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
            if (mesh == null) throw new Exception("mesh not found: " + meshAssetPath);
            Vector3[] v = mesh.vertices;
            int[] tri = mesh.triangles;
            Bounds b = mesh.bounds;
            Vector3 origin = b.min - Vector3.one * margin;
            Vector3 size = b.size + Vector3.one * (2f * margin);

            var dist = new float[rx * ry * rz];
            int triCount = tri.Length / 3;
            var a = new Vector3[triCount]; var bb = new Vector3[triCount]; var c = new Vector3[triCount];
            for (int t = 0; t < triCount; t++) { a[t] = v[tri[t * 3]]; bb[t] = v[tri[t * 3 + 1]]; c[t] = v[tri[t * 3 + 2]]; }

            Parallel.For(0, rz, z =>
            {
                for (int y = 0; y < ry; y++)
                for (int x = 0; x < rx; x++)
                {
                    Vector3 p = origin + new Vector3((x + 0.5f) / rx * size.x, (y + 0.5f) / ry * size.y, (z + 0.5f) / rz * size.z);
                    float best = float.MaxValue;
                    for (int t = 0; t < triCount; t++)
                    {
                        float d = SqrDistPointTriangle(p, a[t], bb[t], c[t]);
                        if (d < best) best = d;
                    }
                    dist[(z * ry + y) * rx + x] = Mathf.Sqrt(best);
                }
            });

            // Sign by ray parity through a temporary collider (back faces included), majority of 14 directions (the six
            // axis directions and the eight body diagonals). Three axes were not enough: next to the thumb two of them
            // ran along a seam of the generated mesh and whole voxel columns came out "inside" (a slab). The winding
            // number was tried and rejected: this mesh's triangle orientation is inconsistent, so it is noisy at the skin.
            var go = new GameObject("PalmSdfBakeCollider");
            go.hideFlags = HideFlags.HideAndDontSave;
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            bool previousBackfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            var hits = new RaycastHit[64];
            int inside = 0;
            try
            {
                float far = size.magnitude * 2f + 1f;
                var dirs = new System.Collections.Generic.List<Vector3>();
                foreach (var ax in new[] { Vector3.right, Vector3.up, Vector3.forward }) { dirs.Add(ax); dirs.Add(-ax); }
                for (int sx = -1; sx <= 1; sx += 2) for (int sy = -1; sy <= 1; sy += 2) for (int sz = -1; sz <= 1; sz += 2) dirs.Add(new Vector3(sx, sy, sz).normalized);
                int needed = dirs.Count / 2 + 1;
                for (int z = 0; z < rz; z++)
                for (int y = 0; y < ry; y++)
                for (int x = 0; x < rx; x++)
                {
                    Vector3 p = origin + new Vector3((x + 0.5f) / rx * size.x, (y + 0.5f) / ry * size.y, (z + 0.5f) / rz * size.z);
                    int votes = 0;
                    for (int k = 0; k < dirs.Count; k++)
                    {
                        int n = 0;
                        int count = Physics.RaycastNonAlloc(p, dirs[k], hits, far, ~0, QueryTriggerInteraction.Collide);
                        for (int h = 0; h < count; h++) if (hits[h].collider == collider) n++;
                        if ((n & 1) == 1) votes++;
                    }
                    if (votes >= needed) { dist[(z * ry + y) * rx + x] *= -1f; inside++; }
                }
            }
            finally
            {
                Physics.queriesHitBackfaces = previousBackfaces;
                UnityEngine.Object.DestroyImmediate(go);
            }

            // Stored normalised in R8: 0.5 at the surface, ±SdfRange/2 at 0 and 1 (0.14 units per step); kept readable for checks.
            float range = SdfRange(margin);
            var tex = new Texture3D(rx, ry, rz, TextureFormat.R8, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var bytes = new byte[dist.Length];
            for (int i = 0; i < dist.Length; i++) bytes[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(dist[i] / range + 0.5f) * 255f);
            tex.SetPixelData(bytes, 0);
            tex.Apply(false, false);
            tex.name = Path.GetFileNameWithoutExtension(outputAssetPath);
            AssetDatabase.CreateAsset(tex, outputAssetPath);
            AssetDatabase.SaveAssets();
            return outputAssetPath + " | grid " + rx + "x" + ry + "x" + rz + " | origin " + origin.ToString("F2") + " size " + size.ToString("F2") + " | inside voxels " + inside + "/" + dist.Length;
        }

        // Total encoded distance span in object units for a bake with this margin; the material's _SdfRange.
        public static float SdfRange(float margin) => 2f * (margin + 6f);

        // Ericson, Real-Time Collision Detection 5.1.5.
        static float SqrDistPointTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return ap.sqrMagnitude;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return bp.sqrMagnitude;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) { float t = d1 / (d1 - d3); return (p - (a + ab * t)).sqrMagnitude; }
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return cp.sqrMagnitude;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) { float t = d2 / (d2 - d6); return (p - (a + ac * t)).sqrMagnitude; }
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f) { float t = (d4 - d3) / ((d4 - d3) + (d5 - d6)); return (p - (b + (c - b) * t)).sqrMagnitude; }
            float denom = 1f / (va + vb + vc);
            float vv = vb * denom, ww = vc * denom;
            return (p - (a + ab * vv + ac * ww)).sqrMagnitude;
        }

        public static string BakeNoise3D(string outputAssetPath, int size, int octaves, int seed)
        {
            var rng = new System.Random(seed);
            var data = new float[size * size * size];
            float amplitude = 1f, total = 0f;
            int period = 8;
            for (int o = 0; o < octaves && period <= size; o++)
            {
                var lattice = new float[period * period * period];
                for (int i = 0; i < lattice.Length; i++) lattice[i] = (float)rng.NextDouble();
                float cell = (float)size / period;
                for (int z = 0; z < size; z++)
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x / cell, fy = y / cell, fz = z / cell;
                    int x0 = (int)fx, y0 = (int)fy, z0 = (int)fz;
                    float tx = Smooth(fx - x0), ty = Smooth(fy - y0), tz = Smooth(fz - z0);
                    float n = 0f;
                    for (int dz = 0; dz < 2; dz++)
                    for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                    {
                        float wgt = (dx == 1 ? tx : 1f - tx) * (dy == 1 ? ty : 1f - ty) * (dz == 1 ? tz : 1f - tz);
                        n += wgt * lattice[(((z0 + dz) % period) * period + ((y0 + dy) % period)) * period + ((x0 + dx) % period)];
                    }
                    data[(z * size + y) * size + x] += n * amplitude;
                }
                total += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }
            var tex = new Texture3D(size, size, size, TextureFormat.R8, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            var colors = new Color[data.Length];
            for (int i = 0; i < data.Length; i++) { float n = data[i] / total; colors[i] = new Color(n, n, n, 1f); }
            tex.SetPixels(colors);
            tex.Apply(false, true);
            tex.name = Path.GetFileNameWithoutExtension(outputAssetPath);
            AssetDatabase.CreateAsset(tex, outputAssetPath);
            AssetDatabase.SaveAssets();
            return outputAssetPath + " | " + size + "^3, " + octaves + " octaves";
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
