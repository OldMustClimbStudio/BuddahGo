using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBlackCurtainTrail : MonoBehaviour
{
    private const string BlackCurtainRequestId = "blackcurtain";

    [Header("Trail Refs")]
    [SerializeField] private TrailRenderer[] trailRenderers;

    [SerializeField] private Color fallbackColor = Color.cyan;
    [SerializeField] private float fallbackWidth = 0.5f;
    [SerializeField] private float fallbackTime = 0.6f;
    [SerializeField] private float fadeOutMultiplier = 1f;

    private readonly Dictionary<string, float> _activeRequestExpiryById = new();
    private float[] _defaultTrailTimes;
    private Coroutine _fadeOutRoutine;
    private Coroutine _teleportRebaseRoutine;
    private bool _isTrailVisible;

    private void Awake()
    {
        if (trailRenderers == null || trailRenderers.Length == 0)
            trailRenderers = GetComponentsInChildren<TrailRenderer>(true);

        EnsureTrailSetup();
        ClearTrail();
    }

    private void Update()
    {
        if (trailRenderers == null)
            return;

        CleanupExpiredRequests();

        if (_activeRequestExpiryById.Count > 0)
        {
            EnsureTrailVisible();
            ApplyTrailTime(GetRequiredActiveTrailTime());
            return;
        }

        if (_isTrailVisible && _fadeOutRoutine == null)
            StartFadeOut();
    }

    public void SetBlackCurtainVisible(bool visible)
    {
        SetBlackCurtainVisible(visible, 0f);
    }

    public void SetBlackCurtainVisible(bool visible, float activeDuration)
    {
        SetVisibilityRequest(BlackCurtainRequestId, visible, activeDuration);
    }

    public void NotifyTeleportRebase()
    {
        if (trailRenderers == null || trailRenderers.Length == 0)
            return;

        if (_teleportRebaseRoutine != null)
            StopCoroutine(_teleportRebaseRoutine);

        _teleportRebaseRoutine = StartCoroutine(TeleportRebaseCoroutine());
    }

    private IEnumerator TeleportRebaseCoroutine()
    {
        CleanupExpiredRequests();
        bool shouldRestoreVisible = _activeRequestExpiryById.Count > 0;

        DisableEmissionAndClearGeometry();
        yield return null;
        DisableEmissionAndClearGeometry();

        CleanupExpiredRequests();
        if (shouldRestoreVisible && _activeRequestExpiryById.Count > 0)
        {
            EnsureTrailVisible();
            ApplyTrailTime(GetRequiredActiveTrailTime());
        }

        _teleportRebaseRoutine = null;
    }

    private void SetVisibilityRequest(string requestId, bool visible, float activeDuration)
    {
        if (trailRenderers == null)
            return;

        if (string.IsNullOrWhiteSpace(requestId))
            return;

        if (visible)
        {
            float expiryTime = activeDuration > 0f
                ? Time.time + activeDuration
                : float.PositiveInfinity;
            _activeRequestExpiryById[requestId] = expiryTime;
            EnsureTrailVisible();
            ApplyTrailTime(GetRequiredActiveTrailTime());
            return;
        }

        _activeRequestExpiryById.Remove(requestId);

        if (_activeRequestExpiryById.Count == 0)
            StartFadeOut();
    }

    public void ClearTrail()
    {
        if (trailRenderers == null)
            return;

        _activeRequestExpiryById.Clear();

        if (_fadeOutRoutine != null)
        {
            StopCoroutine(_fadeOutRoutine);
            _fadeOutRoutine = null;
        }

        if (_teleportRebaseRoutine != null)
        {
            StopCoroutine(_teleportRebaseRoutine);
            _teleportRebaseRoutine = null;
        }

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trail = trailRenderers[i];
            if (trail == null)
                continue;

            trail.Clear();
            trail.emitting = false;
            trail.enabled = false;
        }

        _isTrailVisible = false;
    }

    private void EnsureTrailSetup()
    {
        if (trailRenderers == null)
            return;

        _defaultTrailTimes = new float[trailRenderers.Length];

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trail = trailRenderers[i];
            if (trail == null)
                continue;

            if (trail.time <= 0f)
                trail.time = fallbackTime;

            _defaultTrailTimes[i] = trail.time;

            if (trail.widthMultiplier <= 0f)
                trail.widthMultiplier = fallbackWidth;

            if (trail.minVertexDistance <= 0f)
                trail.minVertexDistance = 0.05f;

            ApplyDefaultGradient(trail);
        }
    }

    private void ApplyDefaultGradient(TrailRenderer trail)
    {
        GradientColorKey[] colorKeys = trail.colorGradient.colorKeys;
        GradientAlphaKey[] alphaKeys = trail.colorGradient.alphaKeys;
        bool hasColor = colorKeys != null && colorKeys.Length > 0;
        bool hasAlpha = alphaKeys != null && alphaKeys.Length > 0;

        if (hasColor && hasAlpha)
            return;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(fallbackColor, 0f),
                new GradientColorKey(fallbackColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });

        trail.colorGradient = gradient;
    }

    private void EnsureTrailVisible()
    {
        if (_fadeOutRoutine != null)
        {
            StopCoroutine(_fadeOutRoutine);
            _fadeOutRoutine = null;
        }

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trail = trailRenderers[i];
            if (trail == null)
                continue;

            if (!_isTrailVisible)
                trail.Clear();

            trail.enabled = true;
            trail.emitting = true;
        }

        _isTrailVisible = true;
    }

    private void StartFadeOut()
    {
        if (!_isTrailVisible)
            return;

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trail = trailRenderers[i];
            if (trail == null)
                continue;

            trail.emitting = false;
        }

        if (_fadeOutRoutine != null)
            StopCoroutine(_fadeOutRoutine);

        _fadeOutRoutine = StartCoroutine(FinishFadeOut());
        _isTrailVisible = false;
    }

    private IEnumerator FinishFadeOut()
    {
        float waitTime = GetFadeOutDuration();
        yield return new WaitForSeconds(waitTime);

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trail = trailRenderers[i];
            if (trail == null)
                continue;

            trail.Clear();
            trail.enabled = false;
            trail.time = GetDefaultTrailTime(i);
        }

        _fadeOutRoutine = null;
    }

    private void ApplyTrailTime(float time)
    {
        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trail = trailRenderers[i];
            if (trail == null)
                continue;

            trail.time = Mathf.Max(GetDefaultTrailTime(i), time);
        }
    }

    private float GetFadeOutDuration()
    {
        float baseTime = fallbackTime;
        if (_defaultTrailTimes != null)
        {
            for (int i = 0; i < _defaultTrailTimes.Length; i++)
                baseTime = Mathf.Max(baseTime, _defaultTrailTimes[i]);
        }

        return Mathf.Max(0.01f, baseTime * Mathf.Max(0f, fadeOutMultiplier));
    }

    private float GetDefaultTrailTime(int index)
    {
        if (_defaultTrailTimes == null || index < 0 || index >= _defaultTrailTimes.Length)
            return fallbackTime;

        return Mathf.Max(fallbackTime, _defaultTrailTimes[index]);
    }

    private void CleanupExpiredRequests()
    {
        if (_activeRequestExpiryById.Count == 0)
            return;

        List<string> expiredRequestIds = null;
        foreach (KeyValuePair<string, float> request in _activeRequestExpiryById)
        {
            if (float.IsPositiveInfinity(request.Value) || request.Value > Time.time)
                continue;

            expiredRequestIds ??= new List<string>();
            expiredRequestIds.Add(request.Key);
        }

        if (expiredRequestIds == null)
            return;

        for (int i = 0; i < expiredRequestIds.Count; i++)
            _activeRequestExpiryById.Remove(expiredRequestIds[i]);
    }

    private float GetRequiredActiveTrailTime()
    {
        float longestRemainingDuration = 0f;
        bool hasInfiniteRequest = false;

        foreach (float expiryTime in _activeRequestExpiryById.Values)
        {
            if (float.IsPositiveInfinity(expiryTime))
            {
                hasInfiniteRequest = true;
                continue;
            }

            longestRemainingDuration = Mathf.Max(longestRemainingDuration, expiryTime - Time.time);
        }

        if (hasInfiniteRequest)
            return Mathf.Max(fallbackTime, GetFadeOutDuration());

        return Mathf.Max(fallbackTime, longestRemainingDuration + GetFadeOutDuration());
    }

    private void DisableEmissionAndClearGeometry()
    {
        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trail = trailRenderers[i];
            if (trail == null)
                continue;

            trail.emitting = false;
            trail.Clear();
        }
    }
}
