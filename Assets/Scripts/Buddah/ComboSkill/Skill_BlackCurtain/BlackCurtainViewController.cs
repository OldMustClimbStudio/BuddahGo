using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

public class BlackCurtainViewController : MonoBehaviour
{
    private const BindingFlags InstancePrivateBindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    [Header("Renderer Indices")]
    [Tooltip("Renderer index used when no black curtain effect is active. Set to -1 to use the camera's pipeline default renderer.")]
    [SerializeField] private int defaultRendererIndex = -1;
    [Tooltip("Renderer index used during black curtain to hide volumetric lights. Set to -1 to keep the current renderer.")]
    [SerializeField] private int noVolumetricLightRendererIndex = -1;
    [Tooltip("If disabled, black curtain keeps using the current URP camera renderer.")]
    [SerializeField] private bool controlCameraRenderer = false;

    [Header("Player Visibility")]
    [SerializeField] private bool hideOtherPlayersForVictim = true;
    [Header("Track Edge")]
    [SerializeField] private string trackEdgeTag = "TrackEdge";
    [SerializeField] private float trackEdgeFadeInDelay = 0.5f;
    [SerializeField] private string trackEdgeVisibilityProperty = "_EdgeVisible";

    [Header("Volumes")]
    [SerializeField] private Volume outdoorVolume;
    [SerializeField] private string outdoorVolumeObjectName = "Outdoor Volume";
    [SerializeField] private bool disableOutdoorVolumeDuringBlackCurtain = true;
    [SerializeField] private Volume blackCurtainActiveVolume;
    [SerializeField] private string blackCurtainActiveVolumeObjectName = "BlackCurtain Active Volume";

    private UniversalAdditionalCameraData _cameraData;
    private BlackCurtainScreenEffect _screenEffect;
    private int _cachedDefaultRendererIndex = -1;
    private int _lastAppliedRendererIndex = int.MinValue;
    private float _activeUntil;
    private float _noVolumetricUntil;
    private float _restoreOtherPlayersAt;
    private float _trackEdgeFadeInStartAt;
    private float _trackEdgeFadeInEndAt;
    private float _trackEdgeFadeOutStartAt;
    private float _trackEdgeFadeOutEndAt;
    private float _disableOutdoorVolumeAt;
    private float _enableOutdoorVolumeAt;
    private bool _canSeeEdge = true;
    private bool _trackEdgeFadeActive;
    private int _playToken;
    private bool _cachedOutdoorVolumeEnabled;
    private bool _hasCachedOutdoorVolumeEnabled;
    private bool _cachedBlackCurtainActiveVolumeEnabled;
    private bool _hasCachedBlackCurtainActiveVolumeEnabled;
    private MaterialPropertyBlock _trackEdgePropertyBlock;
    private readonly Dictionary<Renderer, bool> _hiddenPlayerRenderers = new Dictionary<Renderer, bool>();
    private readonly Dictionary<GameObject, bool> _hiddenTrackEdgeObjects = new Dictionary<GameObject, bool>();

    private void Awake()
    {
        _cameraData = GetComponent<UniversalAdditionalCameraData>();
        _screenEffect = GetComponent<BlackCurtainScreenEffect>();
        if (_screenEffect == null)
            _screenEffect = gameObject.AddComponent<BlackCurtainScreenEffect>();

        ResolveOutdoorVolume();
        CacheDefaultRendererIndex();
        ResetState();
    }

    private void OnEnable()
    {
        ResolveOutdoorVolume();
        CacheDefaultRendererIndex();
        ResetState();
    }

    private void OnDisable()
    {
        ResetState();
    }

    private void Update()
    {
        bool wasActive = IsBlackCurtainActive;

        if (_activeUntil > 0f && Time.time >= _activeUntil)
            _activeUntil = 0f;

        if (_restoreOtherPlayersAt > 0f && Time.time >= _restoreOtherPlayersAt)
        {
            _restoreOtherPlayersAt = 0f;
            SetOtherPlayersVisible(true);
        }

        UpdateOutdoorVolumeWindow();

        UpdateTrackEdgeFade();

        if (wasActive && !IsBlackCurtainActive)
        {
            SetTrailsVisible(false, 0f);
            SetOtherPlayersVisible(true);
            SetTrackEdgesVisible(false, false);
            SetOutdoorVolumeEnabled(true);
            SetBlackCurtainActiveVolumeEnabled(false);
        }

        ApplyCurrentRenderer();
    }

    public bool IsBlackCurtainActive => _activeUntil > Time.time;

    public void Play(
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
        string activeProperty,
        string centerProperty,
        bool canSeeEdge,
        Vector2 center)
    {
        if (_screenEffect == null)
            return;

        _screenEffect.ApplyOrRefresh(
            fullscreenMaterial,
            fallbackMaterialName,
            fallbackShaderName,
            expandDuration,
            holdDuration,
            fadeOutDuration,
            maxOpacity,
            progressProperty,
            opacityProperty,
            elapsedTimeProperty,
            activeProperty,
            centerProperty,
            center);

        float totalDuration = Mathf.Max(0.01f, expandDuration) + Mathf.Max(0f, holdDuration) + Mathf.Max(0f, fadeOutDuration);
        float endsAt = Time.time + totalDuration;
        float fadeInEndsAt = Time.time + Mathf.Max(0f, expandDuration);
        float fadeOutStartsAt = fadeInEndsAt + Mathf.Max(0f, holdDuration);
        _activeUntil = Mathf.Max(_activeUntil, endsAt);
        // Keep volumetric lights hidden until the screen effect has fully faded out.
        _noVolumetricUntil = Mathf.Max(_noVolumetricUntil, endsAt);
        _canSeeEdge = canSeeEdge;
        if (canSeeEdge)
            BeginTrackEdgeFade(expandDuration, holdDuration, fadeOutDuration);
        else
            SetTrackEdgesVisible(false, true);
        SetTrailsVisible(!canSeeEdge, totalDuration);
        // Disable Outdoor Volume only for the local observer who can see track edges.
        // This avoids extra scene clutter while black curtain is active.
        if (disableOutdoorVolumeDuringBlackCurtain && canSeeEdge)
        {
            ScheduleOutdoorVolumeWindow(fadeInEndsAt, fadeOutStartsAt);
        }
        else
        {
            _disableOutdoorVolumeAt = 0f;
            _enableOutdoorVolumeAt = 0f;
            SetOutdoorVolumeEnabled(true);
        }
        SetOtherPlayersVisible(canSeeEdge || !hideOtherPlayersForVictim);
        if (!canSeeEdge && hideOtherPlayersForVictim)
            _restoreOtherPlayersAt = fadeOutDuration > 0f ? Time.time + Mathf.Max(0.01f, expandDuration) + Mathf.Max(0f, holdDuration) : endsAt;
        else
            _restoreOtherPlayersAt = 0f;
        ApplyCurrentRenderer();

        _playToken++;
        StopAllCoroutines();
        StartCoroutine(StopAfterDuration(totalDuration, _playToken));
    }

    private void ApplyCurrentRenderer()
    {
        if (_cameraData == null)
            return;

        if (!controlCameraRenderer)
            return;

        int targetRenderer = ResolveDefaultRendererIndex();

        if (_noVolumetricUntil > Time.time && noVolumetricLightRendererIndex >= 0)
            targetRenderer = noVolumetricLightRendererIndex;

        if (targetRenderer < 0 || targetRenderer == _lastAppliedRendererIndex)
            return;

        _cameraData.SetRenderer(targetRenderer);
        _lastAppliedRendererIndex = targetRenderer;
    }

    private void CacheDefaultRendererIndex()
    {
        _cachedDefaultRendererIndex = ReadCurrentRendererIndex();
        _lastAppliedRendererIndex = int.MinValue;
    }

    private int ResolveDefaultRendererIndex()
    {
        if (defaultRendererIndex >= 0)
            return defaultRendererIndex;

        return _cachedDefaultRendererIndex;
    }

    private int ReadCurrentRendererIndex()
    {
        if (_cameraData == null)
            return -1;

        FieldInfo rendererIndexField = typeof(UniversalAdditionalCameraData).GetField("m_RendererIndex", InstancePrivateBindingFlags);
        if (rendererIndexField == null)
            return -1;

        object rawValue = rendererIndexField.GetValue(_cameraData);
        return rawValue is int rendererIndex ? rendererIndex : -1;
    }

    private void ResetState()
    {
        _activeUntil = 0f;
        _noVolumetricUntil = 0f;
        _restoreOtherPlayersAt = 0f;
        _trackEdgeFadeInStartAt = 0f;
        _trackEdgeFadeInEndAt = 0f;
        _trackEdgeFadeOutStartAt = 0f;
        _trackEdgeFadeOutEndAt = 0f;
        _disableOutdoorVolumeAt = 0f;
        _enableOutdoorVolumeAt = 0f;
        _canSeeEdge = true;
        _trackEdgeFadeActive = false;
        _playToken++;
        StopAllCoroutines();
        SetTrailsVisible(false, 0f);
        SetOtherPlayersVisible(true);
        SetTrackEdgesVisible(false, false);
        SetOutdoorVolumeEnabled(true);
        SetBlackCurtainActiveVolumeEnabled(false);
        ApplyCurrentRenderer();
    }

    private IEnumerator StopAfterDuration(float duration, int token)
    {
        yield return new WaitForSeconds(duration);

        if (token != _playToken)
            yield break;

        ResetState();
    }

    private static void SetTrailsVisible(bool visible, float activeDuration)
    {
        PlayerBlackCurtainTrail[] trails = FindObjectsByType<PlayerBlackCurtainTrail>(FindObjectsSortMode.None);
        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] != null)
                trails[i].SetBlackCurtainVisible(visible, activeDuration);
        }
    }

    private void ResolveOutdoorVolume()
    {
        if (outdoorVolume != null)
            return;

        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
        {
            Volume volume = volumes[i];
            if (volume == null)
                continue;

            if (volume.gameObject.name == outdoorVolumeObjectName)
            {
                outdoorVolume = volume;
                return;
            }
        }
    }

    private void ResolveBlackCurtainActiveVolume()
    {
        if (blackCurtainActiveVolume != null)
            return;

        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
        {
            Volume volume = volumes[i];
            if (volume == null)
                continue;

            if (volume.gameObject.name == blackCurtainActiveVolumeObjectName)
            {
                blackCurtainActiveVolume = volume;
                return;
            }
        }
    }

    private void SetOutdoorVolumeEnabled(bool enabled)
    {
        ResolveOutdoorVolume();
        if (outdoorVolume == null)
            return;

        if (!enabled)
        {
            if (!_hasCachedOutdoorVolumeEnabled)
            {
                _cachedOutdoorVolumeEnabled = outdoorVolume.enabled;
                _hasCachedOutdoorVolumeEnabled = true;
            }

            outdoorVolume.enabled = false;
            return;
        }

        if (!_hasCachedOutdoorVolumeEnabled)
            return;

        outdoorVolume.enabled = _cachedOutdoorVolumeEnabled;
        _hasCachedOutdoorVolumeEnabled = false;
    }

    private void SetBlackCurtainActiveVolumeEnabled(bool enabled)
    {
        ResolveBlackCurtainActiveVolume();
        if (blackCurtainActiveVolume == null)
            return;

        if (enabled)
        {
            if (!_hasCachedBlackCurtainActiveVolumeEnabled)
            {
                _cachedBlackCurtainActiveVolumeEnabled = blackCurtainActiveVolume.enabled;
                _hasCachedBlackCurtainActiveVolumeEnabled = true;
            }

            blackCurtainActiveVolume.enabled = true;
            return;
        }

        if (!_hasCachedBlackCurtainActiveVolumeEnabled)
            return;

        blackCurtainActiveVolume.enabled = _cachedBlackCurtainActiveVolumeEnabled;
        _hasCachedBlackCurtainActiveVolumeEnabled = false;
    }

    private void ScheduleOutdoorVolumeWindow(float disableAt, float enableAt)
    {
        SetOutdoorVolumeEnabled(true);
        SetBlackCurtainActiveVolumeEnabled(false);
        _disableOutdoorVolumeAt = 0f;
        _enableOutdoorVolumeAt = 0f;

        if (enableAt <= disableAt)
            return;

        _disableOutdoorVolumeAt = disableAt;
        _enableOutdoorVolumeAt = enableAt;
        UpdateOutdoorVolumeWindow();
    }

    private void UpdateOutdoorVolumeWindow()
    {
        if (_enableOutdoorVolumeAt > 0f && Time.time >= _enableOutdoorVolumeAt)
        {
            _disableOutdoorVolumeAt = 0f;
            _enableOutdoorVolumeAt = 0f;
            SetOutdoorVolumeEnabled(true);
            SetBlackCurtainActiveVolumeEnabled(false);
            return;
        }

        if (_disableOutdoorVolumeAt > 0f && Time.time >= _disableOutdoorVolumeAt)
        {
            SetOutdoorVolumeEnabled(false);
            SetBlackCurtainActiveVolumeEnabled(true);
        }
    }

    private void SetTrackEdgesVisible(bool visible, bool hideTaggedObjects)
    {
        RestoreTrackEdgeObjects();

        _trackEdgeFadeActive = false;
        SetTrackEdgesVisibilityAmount(visible ? 1f : 0f);

        if (!visible && hideTaggedObjects)
            HideTaggedTrackEdgeObjects();
    }

    private void BeginTrackEdgeFade(float expandDuration, float holdDuration, float fadeOutDuration)
    {
        RestoreTrackEdgeObjects();

        float now = Time.time;
        float fadeInDelay = Mathf.Max(0f, trackEdgeFadeInDelay);
        _trackEdgeFadeInStartAt = now + fadeInDelay;
        _trackEdgeFadeInEndAt = _trackEdgeFadeInStartAt + Mathf.Max(0f, expandDuration - fadeInDelay);
        _trackEdgeFadeOutStartAt = now + Mathf.Max(0.01f, expandDuration) + Mathf.Max(0f, holdDuration);
        _trackEdgeFadeOutEndAt = _trackEdgeFadeOutStartAt + Mathf.Max(0f, fadeOutDuration);
        _trackEdgeFadeActive = true;

        if (_trackEdgeFadeInEndAt > _trackEdgeFadeInStartAt)
            SetTrackEdgesVisibilityAmount(0f);
        else
            SetTrackEdgesVisibilityAmount(1f);
    }

    private void UpdateTrackEdgeFade()
    {
        if (!_trackEdgeFadeActive || !_canSeeEdge)
            return;

        float now = Time.time;
        float visibility = 1f;

        if (_trackEdgeFadeInEndAt > _trackEdgeFadeInStartAt && now < _trackEdgeFadeInEndAt)
        {
            visibility = Mathf.InverseLerp(_trackEdgeFadeInStartAt, _trackEdgeFadeInEndAt, now);
        }
        else if (_trackEdgeFadeOutEndAt > _trackEdgeFadeOutStartAt && now >= _trackEdgeFadeOutStartAt)
        {
            visibility = 1f - Mathf.InverseLerp(_trackEdgeFadeOutStartAt, _trackEdgeFadeOutEndAt, now);
            if (now >= _trackEdgeFadeOutEndAt)
                _trackEdgeFadeActive = false;
        }

        SetTrackEdgesVisibilityAmount(visibility);
    }

    private void SetTrackEdgesVisibilityAmount(float visibility)
    {
        TrackEdgeVisibility[] edges = FindObjectsByType<TrackEdgeVisibility>(FindObjectsSortMode.None);
        for (int i = 0; i < edges.Length; i++)
        {
            TrackEdgeVisibility edge = edges[i];
            if (edge == null)
                continue;

            edge.SetVisibility(visibility);
        }

        ApplyTrackEdgeVisibilityToTaggedRenderers(visibility);
    }

    private void ApplyTrackEdgeVisibilityToTaggedRenderers(float visibility)
    {
        if (string.IsNullOrWhiteSpace(trackEdgeTag) || string.IsNullOrWhiteSpace(trackEdgeVisibilityProperty))
            return;

        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(trackEdgeTag);
        if (taggedObjects == null || taggedObjects.Length == 0)
            return;

        if (_trackEdgePropertyBlock == null)
            _trackEdgePropertyBlock = new MaterialPropertyBlock();

        int visibilityPropertyId = Shader.PropertyToID(trackEdgeVisibilityProperty);
        float clampedVisibility = Mathf.Clamp01(visibility);

        for (int i = 0; i < taggedObjects.Length; i++)
        {
            GameObject taggedObject = taggedObjects[i];
            if (taggedObject == null)
                continue;

            Renderer[] renderers = taggedObject.GetComponentsInChildren<Renderer>(true);
            for (int j = 0; j < renderers.Length; j++)
            {
                Renderer renderer = renderers[j];
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(_trackEdgePropertyBlock);
                _trackEdgePropertyBlock.SetFloat(visibilityPropertyId, clampedVisibility);
                renderer.SetPropertyBlock(_trackEdgePropertyBlock);
            }
        }
    }

    private void RestoreTrackEdgeObjects()
    {
        if (_hiddenTrackEdgeObjects.Count == 0)
            return;

        foreach (KeyValuePair<GameObject, bool> pair in _hiddenTrackEdgeObjects)
        {
            if (pair.Key != null)
                pair.Key.SetActive(pair.Value);
        }

        _hiddenTrackEdgeObjects.Clear();
    }

    private void HideTaggedTrackEdgeObjects()
    {
        if (_hiddenTrackEdgeObjects.Count > 0 || string.IsNullOrWhiteSpace(trackEdgeTag))
            return;

        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(trackEdgeTag);
        for (int i = 0; i < taggedObjects.Length; i++)
        {
            GameObject taggedObject = taggedObjects[i];
            if (taggedObject == null || _hiddenTrackEdgeObjects.ContainsKey(taggedObject))
                continue;

            _hiddenTrackEdgeObjects.Add(taggedObject, taggedObject.activeSelf);
            taggedObject.SetActive(false);
        }
    }

    private void SetOtherPlayersVisible(bool visible)
    {
        if (visible)
        {
            RestoreOtherPlayerRenderers();
            return;
        }

        HideOtherPlayerRenderers();
    }

    private void HideOtherPlayerRenderers()
    {
        if (_hiddenPlayerRenderers.Count > 0)
            return;

        SkillExecutor localExecutor = FindLocalExecutor();
        SkillExecutor[] executors = FindObjectsByType<SkillExecutor>(FindObjectsSortMode.None);
        for (int i = 0; i < executors.Length; i++)
        {
            SkillExecutor executor = executors[i];
            if (executor == null || executor == localExecutor)
                continue;

            Renderer[] renderers = executor.GetComponentsInChildren<Renderer>(true);
            for (int j = 0; j < renderers.Length; j++)
            {
                Renderer renderer = renderers[j];
                if (renderer == null || _hiddenPlayerRenderers.ContainsKey(renderer))
                    continue;

                // Keep all trail renderers visible during black curtain.
                if (renderer is TrailRenderer)
                    continue;

                _hiddenPlayerRenderers.Add(renderer, renderer.enabled);
                renderer.enabled = false;
            }
        }
    }

    private void RestoreOtherPlayerRenderers()
    {
        if (_hiddenPlayerRenderers.Count == 0)
            return;

        foreach (KeyValuePair<Renderer, bool> pair in _hiddenPlayerRenderers)
        {
            if (pair.Key != null)
                pair.Key.enabled = pair.Value;
        }

        _hiddenPlayerRenderers.Clear();
    }

    private static SkillExecutor FindLocalExecutor()
    {
        SkillExecutor[] executors = FindObjectsByType<SkillExecutor>(FindObjectsSortMode.None);
        for (int i = 0; i < executors.Length; i++)
        {
            if (executors[i] != null && executors[i].IsOwner)
                return executors[i];
        }

        return null;
    }
}
