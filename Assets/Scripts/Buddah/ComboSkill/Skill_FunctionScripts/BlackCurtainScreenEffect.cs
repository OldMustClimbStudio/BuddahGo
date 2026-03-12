using UnityEngine;

public class BlackCurtainScreenEffect : MonoBehaviour
{
    private Material _fullscreenMaterial;

    private float _expandDuration;
    private float _holdDuration;
    private float _fadeOutDuration;
    private float _maxOpacity;
    private float _startedAt = -1f;
    private bool _isPlaying;

    private string _progressProperty = "_Expansion";
    private string _opacityProperty = "_Opacity";
    private string _elapsedTimeProperty = "_ElapsedTime";
    private string _activeProperty = "_EffectActive";

    public void ApplyOrRefresh(
        Material fullscreenMaterial,
        string fallbackMaterialName,
        string fallbackShaderName,
        float expandDuration,
        float holdDuration,
        float fadeOutDuration,
        float maxOpacity,
        string progressProperty,
        string opacityProperty,
        string elapsedTimeProperty,
        string activeProperty)
    {
        _fullscreenMaterial = ResolveMaterial(fullscreenMaterial, fallbackMaterialName, fallbackShaderName);
        if (_fullscreenMaterial == null)
        {
            Debug.LogWarning("[BlackCurtainScreenEffect] No fullscreen material found.");
            return;
        }

        _expandDuration = Mathf.Max(0.01f, expandDuration);
        _holdDuration = Mathf.Max(0f, holdDuration);
        _fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        _maxOpacity = Mathf.Clamp01(maxOpacity);

        _progressProperty = string.IsNullOrWhiteSpace(progressProperty) ? "_Expansion" : progressProperty;
        _opacityProperty = string.IsNullOrWhiteSpace(opacityProperty) ? "_Opacity" : opacityProperty;
        _elapsedTimeProperty = string.IsNullOrWhiteSpace(elapsedTimeProperty) ? "_ElapsedTime" : elapsedTimeProperty;
        _activeProperty = string.IsNullOrWhiteSpace(activeProperty) ? "_EffectActive" : activeProperty;

        _startedAt = Time.time;
        _isPlaying = true;
        ApplyMaterialProperties(0f, _maxOpacity, 0f, true);
    }

    private void Awake()
    {
        DisableEffect();
    }

    private void Update()
    {
        if (!_isPlaying || _fullscreenMaterial == null)
            return;

        float elapsed = Mathf.Max(0f, Time.time - _startedAt);
        float totalDuration = _expandDuration + _holdDuration + _fadeOutDuration;

        float progress = Mathf.Clamp01(elapsed / _expandDuration);
        float opacity = _maxOpacity;

        if (_fadeOutDuration > 0f && elapsed > (_expandDuration + _holdDuration))
        {
            float fadeElapsed = elapsed - (_expandDuration + _holdDuration);
            float fadeT = Mathf.Clamp01(fadeElapsed / _fadeOutDuration);
            opacity = Mathf.Lerp(_maxOpacity, 0f, fadeT);
        }

        bool active = elapsed < totalDuration;
        ApplyMaterialProperties(progress, opacity, elapsed, active);

        if (!active)
        {
            _isPlaying = false;
            DisableEffect();
        }
    }

    private void OnDisable()
    {
        DisableEffect();
    }

    private void OnDestroy()
    {
        DisableEffect();
    }

    private void DisableEffect()
    {
        if (_fullscreenMaterial == null)
            return;

        ApplyMaterialProperties(0f, 0f, 0f, false);
    }

    private void ApplyMaterialProperties(float progress, float opacity, float elapsed, bool active)
    {
        TrySetFloat(_progressProperty, progress);
        TrySetFloat(_opacityProperty, opacity);
        TrySetFloat(_elapsedTimeProperty, elapsed);
        TrySetFloat(_activeProperty, active ? 1f : 0f);
    }

    private void TrySetFloat(string propertyName, float value)
    {
        if (_fullscreenMaterial == null || string.IsNullOrWhiteSpace(propertyName) || !_fullscreenMaterial.HasProperty(propertyName))
            return;

        _fullscreenMaterial.SetFloat(propertyName, value);
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
