using UnityEngine;

public class MovementAccelerationEffect : MonoBehaviour
{
    private BuddahMovement _move;

    private float _baseForwardForce;
    private float _baseMaxSpeed;

    private float _extraForwardForce;
    private float _extraMaxSpeed;
    private float _timeLeft;
    private bool _initialized;

    public void ApplyOrRefresh(BuddahMovement move, float extraForwardForce, float extraMaxSpeed, float durationSeconds)
    {
        _move = move;
        if (_move == null)
        {
            Debug.LogWarning("[MovementAccelerationEffect] BuddahMovement missing.");
            Destroy(this);
            return;
        }

        if (!_initialized)
        {
            _baseForwardForce = _move.forwardForce;
            _baseMaxSpeed = _move.maxSpeed;
            _initialized = true;
        }

        // 刷新策略：取“绝对值更强”的那个（支持负数 = 减速）
        if (Mathf.Abs(extraForwardForce) > Mathf.Abs(_extraForwardForce))
            _extraForwardForce = extraForwardForce;

        if (Mathf.Abs(extraMaxSpeed) > Mathf.Abs(_extraMaxSpeed))
            _extraMaxSpeed = extraMaxSpeed;

        // 持续时间仍然取更长
        _timeLeft = Mathf.Max(_timeLeft, durationSeconds);

        ApplyNow();

        Debug.Log($"[AccelEffect] ON: +{_extraForwardForce} forwardForce, +{_extraMaxSpeed} maxSpeed, {_timeLeft:0.00}s");
    }

    private void Update()
    {
        if (!_initialized || _move == null) return;

        _timeLeft -= Time.deltaTime;
        if (_timeLeft <= 0f)
        {
            Restore();
            Debug.Log("[AccelEffect] OFF: restored");
            Destroy(this);
        }
    }

    private void ApplyNow()
    {
        _move.forwardForce = _baseForwardForce + _extraForwardForce;
        _move.maxSpeed = _baseMaxSpeed + _extraMaxSpeed;
    }

    private void Restore()
    {
        _move.forwardForce = _baseForwardForce;
        _move.maxSpeed = _baseMaxSpeed;
    }

    private void OnDisable()
    {
        if (_initialized && _move != null)
            Restore();
    }
}
