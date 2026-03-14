using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;

public class BlackCurtainViewController : MonoBehaviour
{
    [Header("Renderer Indices")]
    [Tooltip("Renderer index used when no black curtain effect is active.")]
    [SerializeField] private int defaultRendererIndex = 0;
    [Tooltip("Renderer index used during black curtain when the local player should NOT see edge outlines. Set to -1 to keep the current renderer.")]
    [SerializeField] private int noEdgeRendererIndex = -1;
    [Tooltip("Renderer index used during black curtain when the local player SHOULD see edge outlines. Set to -1 to keep the current renderer.")]
    [SerializeField] private int edgeRendererIndex = -1;
    [Tooltip("If disabled, black curtain only toggles edge shader visibility and does not switch URP camera renderers.")]
    [SerializeField] private bool controlCameraRenderer = false;

    [Header("Track Edge")]
    [SerializeField] private bool controlTrackEdgesByShader = true;

    [Header("Player Visibility")]
    [SerializeField] private bool hideOtherPlayersForVictim = true;

    private Camera _camera;
    private UniversalAdditionalCameraData _cameraData;
    private BlackCurtainScreenEffect _screenEffect;
    private float _activeUntil;
    private float _edgeUntil;
    private float _edgeFadeStartAt;
    private float _edgeFadeEndAt;
    private bool _edgeWasShownThisPlay;
    private float _restoreOtherPlayersAt;
    private int _playToken;
    private readonly Dictionary<Renderer, bool> _hiddenPlayerRenderers = new Dictionary<Renderer, bool>();

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _cameraData = GetComponent<UniversalAdditionalCameraData>();
        _screenEffect = GetComponent<BlackCurtainScreenEffect>();
        if (_screenEffect == null)
            _screenEffect = gameObject.AddComponent<BlackCurtainScreenEffect>();

        ResetState();
    }

    private void OnEnable()
    {
        ResetState();
    }

    private void OnDisable()
    {
        ResetState();
    }

    private void Update()
    {
        bool wasActive = IsBlackCurtainActive;

        if (_edgeUntil > 0f && Time.time >= _edgeUntil)
            _edgeUntil = 0f;

        if (_activeUntil > 0f && Time.time >= _activeUntil)
            _activeUntil = 0f;

        if (_restoreOtherPlayersAt > 0f && Time.time >= _restoreOtherPlayersAt)
        {
            _restoreOtherPlayersAt = 0f;
            SetOtherPlayersVisible(true);
        }

        UpdateTrackEdgeFade();

        if (wasActive && !IsBlackCurtainActive)
        {
            SetTrailsVisible(false, 0f);
            SetOtherPlayersVisible(true);
        }

        ApplyCurrentRenderer();
    }

    public bool IsBlackCurtainActive => _activeUntil > Time.time;

    public void PlayWithoutEdge(
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
        Vector2 center)
    {
        PlayInternal(
            showEdge: false,
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
    }

    public void PlayWithEdge(
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
        Vector2 center)
    {
        PlayInternal(
            showEdge: true,
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
    }

    private void PlayInternal(
        bool showEdge,
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
        Vector2 center)
    {
        if (_screenEffect == null)
            return;

        Debug.Log($"[BlackCurtainViewController] PlayInternal showEdge={showEdge}, camera={name}, controlTrackEdgesByShader={controlTrackEdgesByShader}");

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
        _activeUntil = Mathf.Max(_activeUntil, endsAt);
        _edgeWasShownThisPlay = showEdge;
        _edgeFadeStartAt = Time.time + Mathf.Max(0.01f, expandDuration) + Mathf.Max(0f, holdDuration);
        _edgeFadeEndAt = endsAt;

        if (showEdge)
            _edgeUntil = Mathf.Max(_edgeUntil, endsAt);
        else
            _edgeUntil = 0f;

        bool localIsVictim = !showEdge;
        SetTrailsVisible(localIsVictim, totalDuration);
        SetTrackEdgesVisible(showEdge);
        SetOtherPlayersVisible(!localIsVictim || !hideOtherPlayersForVictim);
        if (localIsVictim && hideOtherPlayersForVictim)
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

        int targetRenderer = defaultRendererIndex;

        if (IsBlackCurtainActive)
        {
            if (_edgeUntil > Time.time && edgeRendererIndex >= 0)
                targetRenderer = edgeRendererIndex;
            else if (noEdgeRendererIndex >= 0)
                targetRenderer = noEdgeRendererIndex;
        }

        Debug.Log($"[BlackCurtainViewController] ApplyCurrentRenderer active={IsBlackCurtainActive}, edgeActive={_edgeUntil > Time.time}, defaultRendererIndex={defaultRendererIndex}, edgeRendererIndex={edgeRendererIndex}, noEdgeRendererIndex={noEdgeRendererIndex}, targetRenderer={targetRenderer}, camera={name}");
        _cameraData.SetRenderer(targetRenderer);
    }

    private void ResetState()
    {
        _activeUntil = 0f;
        _edgeUntil = 0f;
        _edgeFadeStartAt = 0f;
        _edgeFadeEndAt = 0f;
        _edgeWasShownThisPlay = false;
        _restoreOtherPlayersAt = 0f;
        _playToken++;
        StopAllCoroutines();
        SetTrailsVisible(false, 0f);
        SetTrackEdgesVisible(false);
        SetOtherPlayersVisible(true);
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

    private void SetTrackEdgesVisible(bool visible)
    {
        if (!controlTrackEdgesByShader)
        {
            Debug.Log($"[BlackCurtainViewController] Skip SetTrackEdgesVisible because controlTrackEdgesByShader is disabled on {name}.");
            return;
        }

        TrackEdgeVisibility[] edges = FindObjectsByType<TrackEdgeVisibility>(FindObjectsSortMode.None);
        Debug.Log($"[BlackCurtainViewController] SetTrackEdgesVisible visible={visible}, edgeCount={edges.Length}, camera={name}");
        for (int i = 0; i < edges.Length; i++)
        {
            if (edges[i] != null)
                edges[i].SetVisible(visible);
        }
    }

    private void SetTrackEdgesVisibilityAmount(float visibility)
    {
        if (!controlTrackEdgesByShader)
            return;

        TrackEdgeVisibility[] edges = FindObjectsByType<TrackEdgeVisibility>(FindObjectsSortMode.None);
        for (int i = 0; i < edges.Length; i++)
        {
            if (edges[i] != null)
                edges[i].SetVisibility(visibility);
        }
    }

    private void UpdateTrackEdgeFade()
    {
        if (!_edgeWasShownThisPlay || _edgeFadeEndAt <= 0f)
            return;

        if (Time.time < _edgeFadeStartAt)
            return;

        if (_edgeFadeEndAt <= _edgeFadeStartAt)
        {
            SetTrackEdgesVisibilityAmount(0f);
            _edgeWasShownThisPlay = false;
            return;
        }

        float t = Mathf.InverseLerp(_edgeFadeStartAt, _edgeFadeEndAt, Time.time);
        float visibility = 1f - t;
        SetTrackEdgesVisibilityAmount(visibility);

        if (t >= 1f)
            _edgeWasShownThisPlay = false;
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
