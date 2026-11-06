using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 占位，等手部朝向指引设计 (placeholder until the hand-direction guide is designed).
/// Owner-only, local-only marker on the mouse-aim ground point: a flat ring with a cross, drawn in world space where
/// the cursor ray meets the aim plane (<see cref="BuddahHandControl.TryGetAimGroundPoint"/>), so the player sees where
/// the hands are aiming. Hidden when there is no aim point (not in the race yet, a UI modal owns the input, no mouse
/// or camera) and, by default, inside the mouse-aim dead radius; still shown while frozen (用户确认, 2026-10-31).
/// Nothing is networked or predicted. Added at runtime to the RaceMap HUD by <see cref="RaceMapUIBootstrap"/> (A6, no
/// prefab edit); the marker itself is a separate scene object because a world-space mesh must not live under the canvas.
/// The future hand-direction guide replaces <see cref="BuildVisual"/> and <see cref="LateUpdate"/>'s drawing; the
/// read side (LocalOwner, TryGetAimGroundPoint, IsInsideMouseAimDeadRadius) stays.
/// </summary>
[DisallowMultipleComponent]
public class AimGroundIndicator : MonoBehaviour
{
    // 占位，等手部朝向指引设计: every value below is a placeholder default, not a design number.
    [Tooltip("Placeholder: radius of the ring's centre line (metres).")]
    [SerializeField, Min(0.05f)] private float ringRadius = 1f;
    [Tooltip("Placeholder: width of the ring and the cross bars (metres).")]
    [SerializeField, Min(0.005f)] private float lineWidth = 0.15f;
    [Tooltip("Placeholder: how far each cross bar reaches from the centre (metres). 0 = ring only.")]
    [SerializeField, Min(0f)] private float crossHalfLength = 0.35f;
    [Tooltip("Placeholder: ring smoothness.")]
    [SerializeField, Range(8, 128)] private int ringSegments = 48;
    [Tooltip("Placeholder: marker tint (Sprites/Default, alpha blended).")]
    [SerializeField] private Color color = new Color(1f, 0.85f, 0.3f, 0.9f);
    [Tooltip("Placeholder: lift above the aim plane (metres) so the marker does not z-fight a ground at that height.")]
    [SerializeField] private float heightOffset = 0.05f;
    [Tooltip("Hide while the cursor is inside the mouse-aim dead radius, where the hands hold their target (用户确认: hide).")]
    [SerializeField] private bool hideInsideDeadRadius = true;

    private const string ShaderName = "Sprites/Default"; // in GraphicsSettings' Always Included Shaders, so builds have it

    private GameObject _visual;
    private Mesh _mesh;
    private Material _material;
    private bool _shaderMissingReported;

    private void LateUpdate()
    {
        BuddahHandControl hands = BuddahHandControl.LocalOwner;
        bool hasPoint = false;
        bool insideDeadRadius = false;
        Vector3 groundPoint = default;
        if (hands != null && hands.isActiveAndEnabled)
        {
            hasPoint = hands.TryGetAimGroundPoint(out groundPoint);
            insideDeadRadius = hasPoint && hands.IsInsideMouseAimDeadRadius(groundPoint);
        }

        if (!AimGroundIndicatorMath.TryGetDrawPosition(hasPoint, groundPoint, insideDeadRadius, hideInsideDeadRadius,
                heightOffset, out Vector3 drawPosition))
        {
            SetVisible(false);
            return;
        }

        if (!EnsureVisual())
            return;
        if (_material.color != color)
            _material.color = color; // tint can be tuned live in the inspector; the shape is built once
        _visual.transform.SetPositionAndRotation(drawPosition, Quaternion.identity);
        SetVisible(true);
    }

    private void OnDisable()
    {
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (_visual != null)
            Destroy(_visual);
        if (_mesh != null)
            Destroy(_mesh);
        if (_material != null)
            Destroy(_material);
    }

    private void SetVisible(bool visible)
    {
        if (_visual != null && _visual.activeSelf != visible)
            _visual.SetActive(visible);
    }

    // Lazy: created the first time there is something to show, and again if a scene unload destroyed it.
    private bool EnsureVisual()
    {
        if (_visual != null)
            return true;
        if (_material == null)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                if (!_shaderMissingReported)
                    Debug.LogWarning($"[AimGroundIndicator] Shader '{ShaderName}' not found; the aim marker is not drawn.");
                _shaderMissingReported = true;
                return false;
            }
            _material = new Material(shader) { name = "AimGroundIndicator (placeholder)", color = color };
        }
        if (_mesh == null)
            _mesh = BuildMesh();
        _visual = BuildVisual(_mesh, _material);
        return true;
    }

    private Mesh BuildMesh()
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        AimGroundIndicatorMath.BuildRingAndCross(ringRadius, lineWidth, crossHalfLength, ringSegments, vertices, triangles);

        var colors = new Color[vertices.Count];
        var normals = new Vector3[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            colors[i] = Color.white; // tint comes from the material
            normals[i] = Vector3.up;
        }

        var mesh = new Mesh { name = "AimGroundIndicator (placeholder)" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.colors = colors;
        mesh.normals = normals;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static GameObject BuildVisual(Mesh mesh, Material material)
    {
        var visual = new GameObject("AimGroundIndicator (placeholder)");
        visual.SetActive(false);
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        var meshRenderer = visual.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return visual;
    }
}
