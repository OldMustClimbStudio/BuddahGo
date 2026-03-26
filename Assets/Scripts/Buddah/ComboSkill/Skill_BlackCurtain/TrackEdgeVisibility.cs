using UnityEngine;

public class TrackEdgeVisibility : MonoBehaviour
{
    [Header("Material Ref")]
    [Tooltip("Direct material reference used to control track edge visibility.")]
    [SerializeField] private Material edgeMaterial;
    [Tooltip("Optional renderer targets. If empty, all child renderers will be driven.")]
    [SerializeField] private Renderer[] targetRenderers;
    [Tooltip("Optional fallback when edgeMaterial is not assigned.")]
    [SerializeField] private string fallbackMaterialName = "SG_MapEdge";
    [Tooltip("Optional fallback when material is looked up by shader name.")]
    [SerializeField] private string fallbackShaderName = "Shader Graphs/SG_MapEdge";

    [Header("Shader Control")]
    [Tooltip("Float property on the edge shader. 0 = hidden, 1 = visible.")]
    [SerializeField] private string visibilityProperty = "_EdgeVisible";
    [SerializeField] private float hiddenValue = 0f;
    [SerializeField] private float visibleValue = 1f;

    private Material _resolvedMaterial;
    private int _visibilityPropertyId;
    private MaterialPropertyBlock _propertyBlock;

    private void Awake()
    {
        CachePropertyId();
        _resolvedMaterial = ResolveMaterial(edgeMaterial, fallbackMaterialName, fallbackShaderName);
        CacheRenderers();
        ApplyVisibility(hiddenValue);
    }

    public void SetVisible(bool visible)
    {
        SetVisibility(visible ? 1f : 0f);
    }

    public void SetVisibility(float normalizedVisibility)
    {
        CachePropertyId();

        if (_resolvedMaterial == null)
            _resolvedMaterial = ResolveMaterial(edgeMaterial, fallbackMaterialName, fallbackShaderName);

        if (string.IsNullOrWhiteSpace(visibilityProperty))
        {
            Debug.LogWarning("[TrackEdgeVisibility] No visibility property configured.");
            return;
        }

        float t = Mathf.Clamp01(normalizedVisibility);
        float targetValue = Mathf.Lerp(hiddenValue, visibleValue, t);

        if (_resolvedMaterial != null)
        {
            if (_resolvedMaterial.HasProperty(_visibilityPropertyId))
            {
                _resolvedMaterial.SetFloat(_visibilityPropertyId, targetValue);
            }
            else
            {
                Debug.LogWarning($"[TrackEdgeVisibility] Material '{_resolvedMaterial.name}' does not expose '{visibilityProperty}'.");
            }
        }
        else
        {
            Debug.LogWarning("[TrackEdgeVisibility] No edge material found. Falling back to renderer property blocks only.");
        }

        ApplyVisibility(targetValue);
        Debug.Log($"[TrackEdgeVisibility] SetVisibility t={t:0.###}, targetValue={targetValue:0.###}, material='{_resolvedMaterial?.name ?? "property-block-only"}', shader='{_resolvedMaterial?.shader?.name ?? "unknown"}'");
    }

    private void CachePropertyId()
    {
        if (string.IsNullOrWhiteSpace(visibilityProperty))
            return;

        _visibilityPropertyId = Shader.PropertyToID(visibilityProperty);
    }

    private void CacheRenderers()
    {
        if (targetRenderers != null && targetRenderers.Length > 0)
            return;

        targetRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void ApplyVisibility(float value)
    {
        CacheRenderers();

        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer renderer = targetRenderers[i];
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(_visibilityPropertyId, value);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private static Material ResolveMaterial(Material directReference, string fallbackMaterialName, string fallbackShaderName)
    {
        if (directReference != null)
            return directReference;

        Material[] loadedMaterials = Resources.FindObjectsOfTypeAll<Material>();

        if (!string.IsNullOrWhiteSpace(fallbackMaterialName))
        {
            for (int i = 0; i < loadedMaterials.Length; i++)
            {
                Material material = loadedMaterials[i];
                if (material != null && material.name == fallbackMaterialName)
                    return material;
            }
        }

        if (!string.IsNullOrWhiteSpace(fallbackShaderName))
        {
            for (int i = 0; i < loadedMaterials.Length; i++)
            {
                Material material = loadedMaterials[i];
                if (material != null && material.shader != null && material.shader.name == fallbackShaderName)
                    return material;
            }
        }

        return null;
    }
}
