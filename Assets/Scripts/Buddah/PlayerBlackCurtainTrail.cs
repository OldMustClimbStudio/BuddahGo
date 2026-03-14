using UnityEngine;
using System.Collections;

public class PlayerBlackCurtainTrail : MonoBehaviour
{
    [Header("Trail Refs")]
    [SerializeField] private TrailRenderer[] trailRenderers;

    [SerializeField] private Color fallbackColor = Color.cyan;
    [SerializeField] private float fallbackWidth = 0.5f;
    [SerializeField] private float fallbackTime = 0.6f;
    [SerializeField] private float fadeOutMultiplier = 1f;

    private float[] _defaultTrailTimes;
    private bool _isBlackCurtainActive;
    private float _activeStartedAt;
    private float _activeDuration;
    private float _fadeOutDuration;

    private void Awake()
    {
        if (trailRenderers == null || trailRenderers.Length == 0)
            trailRenderers = GetComponentsInChildren<TrailRenderer>(true);

        EnsureTrailSetup();
        SetBlackCurtainVisible(false);
    }

    private void Update()
    {
        if (!_isBlackCurtainActive || trailRenderers == null)
            return;

        float elapsed = Mathf.Max(0f, Time.time - _activeStartedAt);
        float requiredTime = Mathf.Max(fallbackTime, elapsed + _fadeOutDuration);
        ApplyTrailTime(requiredTime);
    }

    public void SetBlackCurtainVisible(bool visible)
    {
        SetBlackCurtainVisible(visible, 0f);
    }

    public void SetBlackCurtainVisible(bool visible, float activeDuration)
    {
        if (trailRenderers == null)
            return;

        StopAllCoroutines();

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trail = trailRenderers[i];
            if (trail == null)
                continue;

            if (visible)
            {
                trail.enabled = true;
                trail.Clear();
                trail.emitting = true;
                trail.time = Mathf.Max(GetDefaultTrailTime(i), Mathf.Max(fallbackTime, activeDuration + GetFadeOutDuration()));
            }
            else
            {
                trail.emitting = false;
            }
        }

        _isBlackCurtainActive = visible;
        _activeStartedAt = Time.time;
        _activeDuration = Mathf.Max(0f, activeDuration);
        _fadeOutDuration = GetFadeOutDuration();

        if (!visible)
            StartCoroutine(FinishFadeOut());
    }

    public void ClearTrail()
    {
        if (trailRenderers == null)
            return;

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trail = trailRenderers[i];
            if (trail == null)
                continue;

            trail.Clear();
        }
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
}
