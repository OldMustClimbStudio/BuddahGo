using UnityEngine;

public class TrackEdgeVisibility : MonoBehaviour
{
    [Header("Material Ref")]
    [Tooltip("Direct material reference used to control track edge visibility.")]
    [SerializeField] private Material edgeMaterial;
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

    private void Awake()
    {
        CachePropertyId();
        _resolvedMaterial = ResolveMaterial(edgeMaterial, fallbackMaterialName, fallbackShaderName);
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

        if (_resolvedMaterial == null)
        {
            Debug.LogWarning("[TrackEdgeVisibility] No edge material found.");
            return;
        }

        if (string.IsNullOrWhiteSpace(visibilityProperty) || !_resolvedMaterial.HasProperty(_visibilityPropertyId))
        {
            Debug.LogWarning($"[TrackEdgeVisibility] Material '{_resolvedMaterial.name}' does not expose '{visibilityProperty}'.");
            return;
        }

        float t = Mathf.Clamp01(normalizedVisibility);
        float targetValue = Mathf.Lerp(hiddenValue, visibleValue, t);
        _resolvedMaterial.SetFloat(_visibilityPropertyId, targetValue);
        Debug.Log($"[TrackEdgeVisibility] SetVisibility t={t:0.###}, targetValue={targetValue:0.###}, material='{_resolvedMaterial.name}', shader='{_resolvedMaterial.shader?.name ?? "null"}'");
    }

    private void CachePropertyId()
    {
        if (string.IsNullOrWhiteSpace(visibilityProperty))
            return;

        _visibilityPropertyId = Shader.PropertyToID(visibilityProperty);
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
