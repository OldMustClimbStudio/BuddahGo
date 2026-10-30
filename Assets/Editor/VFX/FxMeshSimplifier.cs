using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuddahGo.VFX.EditorTools
{
    // Quadric-error edge-collapse simplifier (Garland & Heckbert), iterative-threshold variant: every pass collapses
    // the edges whose error is below a rising threshold, so no priority queue is needed. Positions only: the FX body
    // shaders need no UVs, and normals are recalculated afterwards. The project has no Blender, so VFX meshes are
    // reduced here (Docs/vfx/README.md section 8: body mesh <= 3000 triangles).
    internal static class FxMeshSimplifier
    {
        struct SymMat
        {
            public double m0, m1, m2, m3, m4, m5, m6, m7, m8, m9;

            public SymMat(double a, double b, double c, double d)
            {
                m0 = a * a; m1 = a * b; m2 = a * c; m3 = a * d;
                m4 = b * b; m5 = b * c; m6 = b * d;
                m7 = c * c; m8 = c * d;
                m9 = d * d;
            }

            public static SymMat operator +(SymMat a, SymMat b)
            {
                return new SymMat
                {
                    m0 = a.m0 + b.m0, m1 = a.m1 + b.m1, m2 = a.m2 + b.m2, m3 = a.m3 + b.m3, m4 = a.m4 + b.m4,
                    m5 = a.m5 + b.m5, m6 = a.m6 + b.m6, m7 = a.m7 + b.m7, m8 = a.m8 + b.m8, m9 = a.m9 + b.m9,
                };
            }

            public double this[int i]
            {
                get
                {
                    switch (i)
                    {
                        case 0: return m0; case 1: return m1; case 2: return m2; case 3: return m3; case 4: return m4;
                        case 5: return m5; case 6: return m6; case 7: return m7; case 8: return m8; default: return m9;
                    }
                }
            }

            public double Det(int a11, int a12, int a13, int a21, int a22, int a23, int a31, int a32, int a33)
            {
                return this[a11] * this[a22] * this[a33] + this[a13] * this[a21] * this[a32] + this[a12] * this[a23] * this[a31]
                     - this[a13] * this[a22] * this[a31] - this[a11] * this[a23] * this[a32] - this[a12] * this[a21] * this[a33];
            }
        }

        class Tri
        {
            public readonly int[] v = new int[3];
            public readonly double[] err = new double[4];
            public bool deleted, dirty;
            public Vector3d n;
        }

        class Vert
        {
            public Vector3d p;
            public SymMat q;
            public int tstart, tcount;
            public bool border;
        }

        struct Ref
        {
            public int tid, tvertex;
        }

        struct Vector3d
        {
            public double x, y, z;
            public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
            public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z);
            public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);
            public static Vector3d operator *(Vector3d a, double s) => new Vector3d(a.x * s, a.y * s, a.z * s);
            public static double Dot(Vector3d a, Vector3d b) => a.x * b.x + a.y * b.y + a.z * b.z;
            public static Vector3d Cross(Vector3d a, Vector3d b) => new Vector3d(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
            public Vector3d Normalized
            {
                get
                {
                    double l = Math.Sqrt(x * x + y * y + z * z);
                    return l > 1e-20 ? new Vector3d(x / l, y / l, z / l) : new Vector3d(0, 0, 0);
                }
            }
        }

        // Welds vertices closer than weldDistance (UV/normal seams would otherwise count as borders and never collapse).
        internal static void Weld(Vector3[] positions, int[] triangles, float weldDistance, out Vector3[] weldedPositions, out int[] weldedTriangles)
        {
            var map = new Dictionary<Vector3Int, int>();
            var outPositions = new List<Vector3>();
            var remap = new int[positions.Length];
            float inv = 1f / Mathf.Max(1e-9f, weldDistance);
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i];
                var key = new Vector3Int(Mathf.RoundToInt(p.x * inv), Mathf.RoundToInt(p.y * inv), Mathf.RoundToInt(p.z * inv));
                if (!map.TryGetValue(key, out int index))
                {
                    index = outPositions.Count;
                    outPositions.Add(p);
                    map.Add(key, index);
                }
                remap[i] = index;
            }

            var outTriangles = new List<int>(triangles.Length);
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = remap[triangles[t]], b = remap[triangles[t + 1]], c = remap[triangles[t + 2]];
                if (a == b || b == c || a == c)
                    continue;
                outTriangles.Add(a); outTriangles.Add(b); outTriangles.Add(c);
            }
            weldedPositions = outPositions.ToArray();
            weldedTriangles = outTriangles.ToArray();
        }

        // Positions should be roughly unit-sized (the error threshold is absolute).
        internal static void Simplify(Vector3[] positions, int[] triangles, int targetTriangles, double aggressiveness,
            out Vector3[] outPositions, out int[] outTriangles)
        {
            var verts = new List<Vert>(positions.Length);
            for (int i = 0; i < positions.Length; i++)
                verts.Add(new Vert { p = new Vector3d(positions[i].x, positions[i].y, positions[i].z) });
            var tris = new List<Tri>(triangles.Length / 3);
            for (int t = 0; t < triangles.Length; t += 3)
            {
                var tri = new Tri();
                tri.v[0] = triangles[t]; tri.v[1] = triangles[t + 1]; tri.v[2] = triangles[t + 2];
                tris.Add(tri);
            }
            var refs = new List<Ref>();

            int deletedTriangles = 0;
            int triangleCount = tris.Count;
            var deleted0 = new List<bool>();
            var deleted1 = new List<bool>();

            for (int iteration = 0; iteration < 100; iteration++)
            {
                if (triangleCount - deletedTriangles <= targetTriangles)
                    break;
                if (iteration % 5 == 0)
                {
                    UpdateMesh(iteration, verts, tris, refs);
                    triangleCount = tris.Count;
                    deletedTriangles = 0;
                }

                for (int i = 0; i < tris.Count; i++)
                    tris[i].dirty = false;

                double threshold = 0.000000001 * Math.Pow(iteration + 3, aggressiveness);

                for (int i = 0; i < tris.Count; i++)
                {
                    Tri t = tris[i];
                    if (t.err[3] > threshold || t.deleted || t.dirty)
                        continue;

                    for (int j = 0; j < 3; j++)
                    {
                        if (t.err[j] >= threshold)
                            continue;
                        int i0 = t.v[j];
                        int i1 = t.v[(j + 1) % 3];
                        Vert v0 = verts[i0];
                        Vert v1 = verts[i1];
                        if (v0.border != v1.border)
                            continue;

                        CalculateError(verts, i0, i1, out Vector3d p);
                        Resize(deleted0, v0.tcount);
                        Resize(deleted1, v1.tcount);
                        if (Flipped(p, i1, v0, verts, tris, refs, deleted0))
                            continue;
                        if (Flipped(p, i0, v1, verts, tris, refs, deleted1))
                            continue;

                        v0.p = p;
                        v0.q = v1.q + v0.q;
                        int tstart = refs.Count;
                        UpdateTriangles(i0, v0, deleted0, verts, tris, refs, ref deletedTriangles);
                        UpdateTriangles(i0, v1, deleted1, verts, tris, refs, ref deletedTriangles);
                        int tcount = refs.Count - tstart;
                        if (tcount <= v0.tcount)
                        {
                            for (int k = 0; k < tcount; k++)
                                refs[v0.tstart + k] = refs[tstart + k];
                        }
                        else
                        {
                            v0.tstart = tstart;
                        }
                        v0.tcount = tcount;
                        break;
                    }
                    if (triangleCount - deletedTriangles <= targetTriangles)
                        break;
                }
            }

            // Compact.
            var used = new int[verts.Count];
            for (int i = 0; i < used.Length; i++)
                used[i] = -1;
            var positionsOut = new List<Vector3>();
            var trianglesOut = new List<int>();
            for (int i = 0; i < tris.Count; i++)
            {
                Tri t = tris[i];
                if (t.deleted)
                    continue;
                for (int j = 0; j < 3; j++)
                {
                    int vi = t.v[j];
                    if (used[vi] < 0)
                    {
                        used[vi] = positionsOut.Count;
                        Vector3d p = verts[vi].p;
                        positionsOut.Add(new Vector3((float)p.x, (float)p.y, (float)p.z));
                    }
                    trianglesOut.Add(used[vi]);
                }
            }
            outPositions = positionsOut.ToArray();
            outTriangles = trianglesOut.ToArray();
        }

        static void Resize(List<bool> list, int size)
        {
            while (list.Count < size) list.Add(false);
        }

        static bool Flipped(Vector3d p, int i1, Vert v0, List<Vert> verts, List<Tri> tris, List<Ref> refs, List<bool> deleted)
        {
            for (int k = 0; k < v0.tcount; k++)
            {
                Ref r = refs[v0.tstart + k];
                Tri t = tris[r.tid];
                if (t.deleted)
                    continue;
                int s = r.tvertex;
                int id1 = t.v[(s + 1) % 3];
                int id2 = t.v[(s + 2) % 3];
                if (id1 == i1 || id2 == i1)
                {
                    deleted[k] = true;
                    continue;
                }
                Vector3d d1 = (verts[id1].p - p).Normalized;
                Vector3d d2 = (verts[id2].p - p).Normalized;
                if (Math.Abs(Vector3d.Dot(d1, d2)) > 0.999)
                    return true;
                Vector3d n = Vector3d.Cross(d1, d2).Normalized;
                deleted[k] = false;
                if (Vector3d.Dot(n, t.n) < 0.2)
                    return true;
            }
            return false;
        }

        static void UpdateTriangles(int i0, Vert v, List<bool> deleted, List<Vert> verts, List<Tri> tris, List<Ref> refs, ref int deletedTriangles)
        {
            for (int k = 0; k < v.tcount; k++)
            {
                Ref r = refs[v.tstart + k];
                Tri t = tris[r.tid];
                if (t.deleted)
                    continue;
                if (deleted[k])
                {
                    t.deleted = true;
                    deletedTriangles++;
                    continue;
                }
                t.v[r.tvertex] = i0;
                t.dirty = true;
                t.err[0] = CalculateError(verts, t.v[0], t.v[1], out _);
                t.err[1] = CalculateError(verts, t.v[1], t.v[2], out _);
                t.err[2] = CalculateError(verts, t.v[2], t.v[0], out _);
                t.err[3] = Math.Min(t.err[0], Math.Min(t.err[1], t.err[2]));
                refs.Add(r);
            }
        }

        static void UpdateMesh(int iteration, List<Vert> verts, List<Tri> tris, List<Ref> refs)
        {
            if (iteration > 0)
                tris.RemoveAll(t => t.deleted);

            if (iteration == 0)
            {
                for (int i = 0; i < verts.Count; i++)
                    verts[i].q = new SymMat();
                for (int i = 0; i < tris.Count; i++)
                {
                    Tri t = tris[i];
                    Vector3d p0 = verts[t.v[0]].p, p1 = verts[t.v[1]].p, p2 = verts[t.v[2]].p;
                    Vector3d n = Vector3d.Cross(p1 - p0, p2 - p0).Normalized;
                    t.n = n;
                    var q = new SymMat(n.x, n.y, n.z, -Vector3d.Dot(n, p0));
                    for (int j = 0; j < 3; j++)
                        verts[t.v[j]].q = verts[t.v[j]].q + q;
                }
                for (int i = 0; i < tris.Count; i++)
                {
                    Tri t = tris[i];
                    for (int j = 0; j < 3; j++)
                        t.err[j] = CalculateError(verts, t.v[j], t.v[(j + 1) % 3], out _);
                    t.err[3] = Math.Min(t.err[0], Math.Min(t.err[1], t.err[2]));
                }
            }

            for (int i = 0; i < verts.Count; i++)
            {
                verts[i].tstart = 0;
                verts[i].tcount = 0;
            }
            for (int i = 0; i < tris.Count; i++)
                for (int j = 0; j < 3; j++)
                    verts[tris[i].v[j]].tcount++;
            int tstart = 0;
            for (int i = 0; i < verts.Count; i++)
            {
                verts[i].tstart = tstart;
                tstart += verts[i].tcount;
                verts[i].tcount = 0;
            }
            refs.Clear();
            for (int i = 0; i < tstart; i++)
                refs.Add(default);
            for (int i = 0; i < tris.Count; i++)
            {
                Tri t = tris[i];
                for (int j = 0; j < 3; j++)
                {
                    Vert v = verts[t.v[j]];
                    refs[v.tstart + v.tcount] = new Ref { tid = i, tvertex = j };
                    v.tcount++;
                }
            }

            if (iteration == 0)
            {
                // A vertex on an edge used by a single triangle is a border vertex.
                var vcount = new List<int>();
                var vids = new List<int>();
                for (int i = 0; i < verts.Count; i++)
                    verts[i].border = false;
                for (int i = 0; i < verts.Count; i++)
                {
                    Vert v = verts[i];
                    vcount.Clear();
                    vids.Clear();
                    for (int j = 0; j < v.tcount; j++)
                    {
                        Tri t = tris[refs[v.tstart + j].tid];
                        for (int k = 0; k < 3; k++)
                        {
                            int id = t.v[k];
                            int ofs = vids.IndexOf(id);
                            if (ofs < 0)
                            {
                                vcount.Add(1);
                                vids.Add(id);
                            }
                            else
                            {
                                vcount[ofs]++;
                            }
                        }
                    }
                    for (int j = 0; j < vcount.Count; j++)
                    {
                        if (vcount[j] == 1)
                            verts[vids[j]].border = true;
                    }
                }
            }
        }

        static double VertexError(SymMat q, double x, double y, double z)
        {
            return q.m0 * x * x + 2 * q.m1 * x * y + 2 * q.m2 * x * z + 2 * q.m3 * x + q.m4 * y * y
                 + 2 * q.m5 * y * z + 2 * q.m6 * y + q.m7 * z * z + 2 * q.m8 * z + q.m9;
        }

        static double CalculateError(List<Vert> verts, int idV1, int idV2, out Vector3d result)
        {
            SymMat q = verts[idV1].q + verts[idV2].q;
            bool border = verts[idV1].border && verts[idV2].border;
            double det = q.Det(0, 1, 2, 1, 4, 5, 2, 5, 7);
            if (Math.Abs(det) > 1e-30 && !border)
            {
                result = new Vector3d(
                    -1 / det * q.Det(1, 2, 3, 4, 5, 6, 5, 7, 8),
                    1 / det * q.Det(0, 2, 3, 1, 5, 6, 2, 7, 8),
                    -1 / det * q.Det(0, 1, 3, 1, 4, 6, 2, 5, 8));
                return VertexError(q, result.x, result.y, result.z);
            }

            Vector3d p1 = verts[idV1].p, p2 = verts[idV2].p, p3 = (p1 + p2) * 0.5;
            double e1 = VertexError(q, p1.x, p1.y, p1.z);
            double e2 = VertexError(q, p2.x, p2.y, p2.z);
            double e3 = VertexError(q, p3.x, p3.y, p3.z);
            double error = Math.Min(e1, Math.Min(e2, e3));
            result = error == e1 ? p1 : error == e2 ? p2 : p3;
            return error;
        }
    }
}
