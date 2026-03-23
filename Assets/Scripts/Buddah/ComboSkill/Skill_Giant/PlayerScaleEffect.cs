using UnityEngine;

public class PlayerScaleEffect : MonoBehaviour
{
    private const float MinScaleMultiplier = 0.1f;

    private BuddahMovement _move;
    private Vector3 _baseLocalScale;
    private float _baseTurnTorque;
    private float _targetScaleMultiplier = 1f;
    private float _appliedScaleMultiplier = 1f;
    private float _transitionStartMultiplier = 1f;
    private float _transitionTargetMultiplier = 1f;
    private float _transitionDuration;
    private float _transitionElapsed;
    private float _timeLeft;
    private float _enterDuration;
    private float _restoreDuration;
    private bool _initialized;

    public float CurrentScaleMultiplier => _initialized ? _appliedScaleMultiplier : 1f;

    public void ApplyOrRefresh(float scaleMultiplier, float durationSeconds, float enterDurationSeconds, float restoreDurationSeconds)
    {
        float targetMultiplier = Mathf.Max(MinScaleMultiplier, scaleMultiplier);
        if (!_initialized)
        {
            _move = GetComponent<BuddahMovement>();
            _baseLocalScale = transform.localScale;
            _baseTurnTorque = _move != null ? _move.turnTorque : 0f;
            _appliedScaleMultiplier = 1f;
            _initialized = true;
        }

        _enterDuration = Mathf.Max(0f, enterDurationSeconds);
        _restoreDuration = Mathf.Max(0f, restoreDurationSeconds);
        _targetScaleMultiplier = targetMultiplier;
        _timeLeft = Mathf.Max(_timeLeft, Mathf.Max(0.05f, durationSeconds));
        StartTransition(_targetScaleMultiplier, _enterDuration);
    }

    private void Update()
    {
        if (!_initialized)
            return;

        if (_timeLeft > 0f)
        {
            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0f)
            {
                _timeLeft = 0f;
                StartTransition(1f, _restoreDuration);
            }
        }

        UpdateTransition(Time.deltaTime);

        if (_timeLeft > 0f)
            return;

        if (!Mathf.Approximately(_appliedScaleMultiplier, 1f))
            return;

        Restore(immediateOnly: true);
        Destroy(this);
    }

    private void UpdateTransition(float deltaTime)
    {
        if (!_initialized)
            return;

        if (Mathf.Approximately(_appliedScaleMultiplier, _transitionTargetMultiplier))
            return;

        if (_transitionDuration <= 0f)
        {
            _appliedScaleMultiplier = _transitionTargetMultiplier;
            ApplyNow();
            return;
        }

        _transitionElapsed += deltaTime;
        float t = Mathf.Clamp01(_transitionElapsed / _transitionDuration);
        t = t * t * (3f - 2f * t);
        _appliedScaleMultiplier = Mathf.Lerp(_transitionStartMultiplier, _transitionTargetMultiplier, t);
        ApplyNow();
    }

    private void StartTransition(float targetMultiplier, float durationSeconds)
    {
        _transitionStartMultiplier = _appliedScaleMultiplier;
        _transitionTargetMultiplier = Mathf.Max(MinScaleMultiplier, targetMultiplier);
        _transitionDuration = Mathf.Max(0f, durationSeconds);
        _transitionElapsed = 0f;

        if (_transitionDuration <= 0f)
        {
            _appliedScaleMultiplier = _transitionTargetMultiplier;
            ApplyNow();
        }
    }

    private void ApplyNow()
    {
        transform.localScale = _baseLocalScale * _appliedScaleMultiplier;
        if (_move != null)
            _move.turnTorque = _baseTurnTorque * _appliedScaleMultiplier;
        Physics.SyncTransforms();
    }

    private void Restore(bool immediateOnly)
    {
        if (!_initialized)
            return;

        if (!immediateOnly)
        {
            StartTransition(1f, _restoreDuration);
            return;
        }

        _appliedScaleMultiplier = 1f;
        transform.localScale = _baseLocalScale;
        if (_move != null)
            _move.turnTorque = _baseTurnTorque;
        Physics.SyncTransforms();
    }

    private void OnDisable()
    {
        Restore(immediateOnly: true);
    }
}
