using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// PLACEHOLDER 缘起 visuals for the artist to replace (the SkillVfxDatabase ids such as frost_lock_ground_vfx /
/// frost_lock_block_vfx are not wired yet). A translucent primitive with no collider that holds, fades out and then
/// destroys itself and the material it created. Presentation only: it never touches game state.
/// Works in URP (Universal Render Pipeline/Unlit, transparent) and falls back to Unlit/Color, then Sprites/Default.
/// </summary>
[DisallowMultipleComponent]
public sealed class YuanqiPlaceholderVisual : MonoBehaviour
{
    private static readonly string[] ShaderFallbacks = { "Universal Render Pipeline/Unlit", "Unlit/Color", "Sprites/Default" };
    private const string UrpUnlitShaderName = "Universal Render Pipeline/Unlit";
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Shader _cachedShader;

    private Material _material;
    private Color _color;
    private float _holdSeconds;
    private float _fadeSeconds;
    private float _age;

    /// <summary>A flat disc lying on the ground (world space, does not follow anything).</summary>
    public static YuanqiPlaceholderVisual SpawnGroundDisc(string objectName, Vector3 groundPoint, float radius, float thickness,
        Color color, float holdSeconds, float fadeSeconds)
    {
        YuanqiPlaceholderVisual visual = Create(PrimitiveType.Cylinder, objectName, color, holdSeconds, fadeSeconds);
        float height = Mathf.Max(0.001f, thickness);
        // Unity's cylinder is 1 unit wide and 2 units tall.
        visual.transform.SetPositionAndRotation(groundPoint + Vector3.up * (height * 0.5f), Quaternion.identity);
        visual.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        return visual;
    }

    /// <summary>A box around world bounds, parented to <paramref name="follow"/> so it stays with that object.</summary>
    public static YuanqiPlaceholderVisual SpawnBlock(string objectName, Transform follow, Bounds worldBounds,
        Color color, float holdSeconds, float fadeSeconds)
    {
        YuanqiPlaceholderVisual visual = Create(PrimitiveType.Cube, objectName, color, holdSeconds, fadeSeconds);
        visual.transform.SetPositionAndRotation(worldBounds.center, follow != null ? Quaternion.Euler(0f, follow.eulerAngles.y, 0f) : Quaternion.identity);
        visual.transform.localScale = worldBounds.size;
        if (follow != null)
            visual.transform.SetParent(follow, worldPositionStays: true);
        return visual;
    }

    private static YuanqiPlaceholderVisual Create(PrimitiveType primitive, string objectName, Color color, float holdSeconds, float fadeSeconds)
    {
        GameObject go = GameObject.CreatePrimitive(primitive);
        go.name = objectName;

        // Visual only: drop the primitive's collider right away.
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }

        YuanqiPlaceholderVisual visual = go.AddComponent<YuanqiPlaceholderVisual>();
        visual._color = color;
        visual._holdSeconds = Mathf.Max(0f, holdSeconds);
        visual._fadeSeconds = Mathf.Max(0f, fadeSeconds);
        visual._material = CreateTranslucentMaterial(color);

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (visual._material != null)
                renderer.sharedMaterial = visual._material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        return visual;
    }

    private static Material CreateTranslucentMaterial(Color color)
    {
        Shader shader = ResolveShader();
        if (shader == null)
        {
            Debug.LogWarning("[Yuanqi][Placeholder] No placeholder shader found; using the primitive's default material.");
            return null;
        }

        var material = new Material(shader) { name = "YuanqiPlaceholder (runtime)" };
        if (shader.name == UrpUnlitShaderName)
        {
            // URP Unlit as Transparent / Alpha blend (what the material inspector would set).
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        ApplyColor(material, color);
        return material;
    }

    private static Shader ResolveShader()
    {
        if (_cachedShader != null)
            return _cachedShader;
        for (int i = 0; i < ShaderFallbacks.Length && _cachedShader == null; i++)
            _cachedShader = Shader.Find(ShaderFallbacks[i]);
        return _cachedShader;
    }

    private static void ApplyColor(Material material, Color color)
    {
        if (material == null)
            return;
        if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
        if (material.HasProperty(ColorId)) material.SetColor(ColorId, color);
    }

    private void Update()
    {
        _age += Time.deltaTime;
        if (_age >= _holdSeconds + _fadeSeconds)
        {
            Destroy(gameObject);
            return;
        }
        if (_age <= _holdSeconds || _fadeSeconds <= 0f)
            return;

        float remaining01 = 1f - (_age - _holdSeconds) / _fadeSeconds;
        Color faded = _color;
        faded.a *= Mathf.Clamp01(remaining01);
        ApplyColor(_material, faded);
    }

    private void OnDestroy()
    {
        if (_material != null)
            Destroy(_material);
        _material = null;
    }
}
