using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 占位，等禅足冰封正式特效 (placeholder until the frost-lock VFX is designed): procedural meshes and the shared
/// material of <see cref="FrostGroundPlaceholder"/> (#2's frozen ground) and <see cref="FrozenIceShellPlaceholder"/>
/// (the ice block on a frozen racer). White and grey only (Docs/vfx/README §2: ink / gold / white; no new colour),
/// no art assets, unlit (Sprites/Default is in GraphicsSettings' Always Included Shaders), shading baked into vertex
/// colours. The real effect replaces both from the same hooks: Yuanqi_FrostLock.PlayObservers and
/// RacerSkillPresentationState.FrozenChanged.
/// </summary>
public static class FrostPlaceholder
{
    /// <summary>Off = neither placeholder draws (the real VFX, or a before/after capture, turns them off).</summary>
    public static bool Enabled = true;

    private const string ShaderName = "Sprites/Default";
    private static Material _material;
    private static bool _shaderMissingReported;

    /// <summary>A new material instance (each placeholder fades its own alpha); null when the shader is missing.</summary>
    internal static Material NewMaterial(string name)
    {
        if (_material == null)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                if (!_shaderMissingReported)
                    Debug.LogWarning($"[FrostPlaceholder] Shader '{ShaderName}' not found; the frost placeholders are not drawn.");
                _shaderMissingReported = true;
                return null;
            }
            _material = new Material(shader) { name = "FrostPlaceholder (shared)" };
        }
        return new Material(_material) { name = name, color = Color.white };
    }

    internal static GameObject NewVisual(string name, Mesh mesh, Material material)
    {
        var visual = new GameObject(name);
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        var meshRenderer = visual.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return visual;
    }

    internal static Mesh ToMesh(string name, List<Vector3> vertices, List<Color> colors, List<int> triangles)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        var normals = new Vector3[vertices.Count];
        for (int i = 0; i < normals.Length; i++)
            normals[i] = Vector3.up;
        mesh.normals = normals;
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---- frozen ground: unit disc (radius 1, y = 0), scaled to R by the caller ---------------------------------------

    private const int DiscSegments = 64;
    private const float RimInner = 0.9f;
    private const int CrackCount = 11;
    private static readonly Color DiscFill = new Color(0.92f, 0.94f, 0.96f, 0.42f);
    private static readonly Color DiscRim = new Color(1f, 1f, 1f, 0.75f);
    private static readonly Color Crack = new Color(1f, 1f, 1f, 0.6f);

    /// <summary>Filled disc, a brighter rim band, and thin crack wedges from near the centre to the rim (fixed pattern).</summary>
    internal static void BuildGroundDisc(List<Vector3> vertices, List<Color> colors, List<int> triangles)
    {
        int centre = Add(vertices, colors, Vector3.zero, DiscFill);
        int ringStart = vertices.Count;
        for (int i = 0; i < DiscSegments; i++)
            Add(vertices, colors, OnCircle(i * 360f / DiscSegments, RimInner), DiscFill);
        for (int i = 0; i < DiscSegments; i++)
            AddTriangle(triangles, centre, ringStart + (i + 1) % DiscSegments, ringStart + i);

        for (int i = 0; i < DiscSegments; i++)
        {
            float a0 = i * 360f / DiscSegments, a1 = (i + 1) * 360f / DiscSegments;
            AddQuad(vertices, colors, triangles, OnCircle(a0, RimInner), OnCircle(a1, RimInner), OnCircle(a1, 1f), OnCircle(a0, 1f), DiscRim);
        }

        for (int i = 0; i < CrackCount; i++)
        {
            // Uneven spacing and lengths read as cracks rather than spokes; fixed, so every peer draws the same.
            float angle = i * 360f / CrackCount + (i * 37 % 17) - 8f;
            float inner = 0.12f + (i * 13 % 7) * 0.02f;
            float outer = 0.72f + (i * 29 % 11) * 0.025f;
            float halfWidthDeg = 0.9f + (i % 3) * 0.35f;
            AddQuad(vertices, colors, triangles,
                OnCircle(angle - halfWidthDeg * 0.3f, inner), OnCircle(angle + halfWidthDeg * 0.3f, inner),
                OnCircle(angle + halfWidthDeg, outer), OnCircle(angle - halfWidthDeg, outer), Crack);
        }
    }

    // ---- ice block: unit cube (-0.5..0.5) with face shading and bright edges, scaled to the body by the caller -------

    private const float EdgeThickness = 0.035f;
    private static readonly Color FaceTop = new Color(1f, 1f, 1f, 0.38f);
    private static readonly Color FaceSideA = new Color(0.88f, 0.9f, 0.93f, 0.3f);
    private static readonly Color FaceSideB = new Color(0.8f, 0.83f, 0.86f, 0.3f);
    private static readonly Color FaceBottom = new Color(0.72f, 0.74f, 0.77f, 0.3f);
    private static readonly Color Edge = new Color(1f, 1f, 1f, 0.7f);

    internal static void BuildIceBlock(List<Vector3> vertices, List<Color> colors, List<int> triangles)
    {
        AddBox(vertices, colors, triangles, Vector3.zero, Vector3.one, FaceTop, FaceSideA, FaceSideB, FaceBottom);
        // Twelve edge bars, a little outside the faces so they are not hidden by them.
        float e = EdgeThickness, h = 0.5f;
        for (int axis = 0; axis < 3; axis++)
        {
            for (int s1 = -1; s1 <= 1; s1 += 2)
            {
                for (int s2 = -1; s2 <= 1; s2 += 2)
                {
                    Vector3 centre = Vector3.zero, size = Vector3.one * e;
                    centre[(axis + 1) % 3] = s1 * h;
                    centre[(axis + 2) % 3] = s2 * h;
                    size[axis] = 1f + e;
                    AddBox(vertices, colors, triangles, centre, size, Edge, Edge, Edge, Edge);
                }
            }
        }
    }

    private static void AddBox(List<Vector3> v, List<Color> c, List<int> t, Vector3 centre, Vector3 size,
        Color top, Color sideA, Color sideB, Color bottom)
    {
        Vector3 h = size * 0.5f;
        Vector3 P(float x, float y, float z) => centre + Vector3.Scale(h, new Vector3(x, y, z));
        AddQuad(v, c, t, P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), top);          // +y
        AddQuad(v, c, t, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), bottom);   // -y
        AddQuad(v, c, t, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), sideA);        // +z
        AddQuad(v, c, t, P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), sideA);    // -z
        AddQuad(v, c, t, P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), sideB);        // +x
        AddQuad(v, c, t, P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), sideB);    // -x
    }

    private static Vector3 OnCircle(float degrees, float radius)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(r) * radius, 0f, Mathf.Sin(r) * radius);
    }

    private static int Add(List<Vector3> v, List<Color> c, Vector3 p, Color color)
    {
        v.Add(p);
        c.Add(color);
        return v.Count - 1;
    }

    // Sprites/Default does not cull, so winding only matters for consistency.
    private static void AddQuad(List<Vector3> v, List<Color> c, List<int> t, Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color color)
    {
        int i = Add(v, c, a, color);
        Add(v, c, b, color);
        Add(v, c, d, color);
        Add(v, c, e, color);
        AddTriangle(t, i, i + 1, i + 2);
        AddTriangle(t, i, i + 2, i + 3);
    }

    private static void AddTriangle(List<int> t, int a, int b, int c)
    {
        t.Add(a);
        t.Add(b);
        t.Add(c);
    }
}
