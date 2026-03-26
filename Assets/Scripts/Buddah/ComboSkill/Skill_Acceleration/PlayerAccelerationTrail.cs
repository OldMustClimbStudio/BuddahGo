using System.Collections;
using UnityEngine;

public class PlayerAccelerationTrail : MonoBehaviour
{
    [Header("Trail Refs")]
    [SerializeField] private TrailRenderer[] trailRenderers;

    [SerializeField] private Color fallbackColor = new Color(1f, 0.7f, 0f, 1f);
    [SerializeField] private float fallbackWidth = 0.5f;
    [SerializeField] private float fallbackTime = 0.6f;
    [SerializeField] private float fadeOutMultiplier = 1f;

    private float[] _defaultTrailTimes;
    private float _activeUntilTime;
    private Coroutine _fadeOutRoutine;
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
        if (trailRenderers == null || trailRenderers.Length == 0)
            return;

        if (Time.time < _activeUntilTime)
        {
            EnsureTrailVisible();
            return;
        }

        if (_isTrailVisible && _fadeOutRoutine == null)
            StartFadeOut();
    }

    public void ShowForDuration(float durationSeconds)
    {
        if (trailRenderers == null || trailRenderers.Length == 0 || durationSeconds <= 0f)
            return;

        _activeUntilTime = Mathf.Max(_activeUntilTime, Time.time + durationSeconds);
        EnsureTrailVisible();
    }

    public void ClearTrail()
    {
        _activeUntilTime = 0f;

        if (_fadeOutRoutine != null)
        {
            StopCoroutine(_fadeOutRoutine);
            _fadeOutRoutine = null;
        }

        if (trailRenderers == null)
            return;

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            TrailRenderer trail = trailRenderers[i];
            if (trail == null)
                continue;

            trail.Clear();
            trail.emitting = false;
            trail.enabled = false;
            trail.time = GetDefaultTrailTime(i);
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
        yield return new WaitForSeconds(GetFadeOutDuration());

        if (trailRenderers == null)
        {
            _fadeOutRoutine = null;
            yield break;
        }

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
