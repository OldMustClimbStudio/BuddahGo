using UnityEngine;
using NewBuddah.PredictionV2.Bootstrap;

public class PlayerScaleEffect : MonoBehaviour
{
    private const float MinScaleMultiplier = 0.1f;

    private BuddahMovement _move;
    private Rigidbody _rb;
    private Vector3 _baseLocalScale;
    private float _baseTurnTorque;
    private float _baseForwardForce;
    private float _baseMass;
    private float _movementMassMultiplier = 1f;
    private float _movementForwardForceMultiplier = 1f;
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
    private BuddahPredictionBootstrap _predictionBootstrap;

    public float CurrentScaleMultiplier => _initialized ? _appliedScaleMultiplier : 1f;

    public void ApplyOrRefresh(float scaleMultiplier, float durationSeconds, float enterDurationSeconds, float restoreDurationSeconds, float massMultiplier = 1f, float forwardForceMultiplier = 1f)
    {
        float targetMultiplier = Mathf.Max(MinScaleMultiplier, scaleMultiplier);
        if (!_initialized)
        {
            _move = GetComponent<BuddahMovement>();
            _rb = GetComponent<Rigidbody>();
            _predictionBootstrap = GetComponent<BuddahPredictionBootstrap>() ?? GetComponentInParent<BuddahPredictionBootstrap>();
            _baseLocalScale = transform.localScale;
            _baseTurnTorque = _move != null ? _move.turnTorque : 0f;
            _baseForwardForce = _move != null ? _move.forwardForce : 0f;
            _baseMass = _rb != null ? Mathf.Max(0.0001f, _rb.mass) : 1f;
            _appliedScaleMultiplier = 1f;
            _initialized = true;
        }

        _movementMassMultiplier = Mathf.Max(0.1f, massMultiplier);
        _movementForwardForceMultiplier = Mathf.Max(0.1f, forwardForceMultiplier);
        _enterDuration = Mathf.Max(0f, enterDurationSeconds);
        _restoreDuration = Mathf.Max(0f, restoreDurationSeconds);
        _targetScaleMultiplier = targetMultiplier;
        _timeLeft = Mathf.Max(_timeLeft, Mathf.Max(0.05f, durationSeconds));
        StartTransition(_targetScaleMultiplier, _enterDuration);
    }

    public void CancelAndRestore()
    {
        Restore(immediateOnly: true);
        _timeLeft = 0f;
        Destroy(this);
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
        if (!IsPredictionModeActive())
        {
            if (_move != null)
            {
                _move.turnTorque = _baseTurnTorque * _appliedScaleMultiplier;
                _move.forwardForce = _baseForwardForce * _movementForwardForceMultiplier;
            }

            if (_rb != null)
                _rb.mass = _baseMass * _movementMassMultiplier;
        }
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
        if (!IsPredictionModeActive())
        {
            if (_move != null)
            {
                _move.turnTorque = _baseTurnTorque;
                _move.forwardForce = _baseForwardForce;
            }

            if (_rb != null)
                _rb.mass = _baseMass;
        }
        _movementMassMultiplier = 1f;
        _movementForwardForceMultiplier = 1f;
        Physics.SyncTransforms();
    }

    private bool IsPredictionModeActive()
    {
        if (_predictionBootstrap == null)
            _predictionBootstrap = GetComponent<BuddahPredictionBootstrap>() ?? GetComponentInParent<BuddahPredictionBootstrap>();

        return _predictionBootstrap != null && _predictionBootstrap.IsPredictionModeActive();
    }

    private void OnDisable()
    {
        Restore(immediateOnly: true);
    }
}
