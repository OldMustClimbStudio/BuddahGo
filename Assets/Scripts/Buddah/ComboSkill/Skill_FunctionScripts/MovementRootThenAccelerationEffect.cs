using UnityEngine;

/// <summary>
/// Local (owner-client) movement effect: force root for a short time, then apply acceleration buff.
/// Intended for anti/backfire behavior.
/// </summary>
public class MovementRootThenAccelerationEffect : MonoBehaviour
{
    private BuddahMovement _move;
    private Rigidbody _rb;

    private float _savedForwardForce;
    private float _savedMaxSpeed;
    private bool _hasSavedMoveStats;

    private float _rootTimeLeft;
    private float _extraForwardForce;
    private float _extraMaxSpeed;
    private float _accelDurationSeconds;
    private bool _rooting;
    private bool _rootFlagApplied;

    public void ApplyOrRestart(BuddahMovement move, float rootDurationSeconds, float extraForwardForce, float extraMaxSpeed, float accelDurationSeconds)
    {
        _move = move;
        if (_move == null)
        {
            Debug.LogWarning("[MovementRootThenAccelerationEffect] BuddahMovement missing.");
            Destroy(this);
            return;
        }

        if (_rb == null)
            _rb = GetComponent<Rigidbody>();

        if (!_hasSavedMoveStats)
        {
            _savedForwardForce = _move.forwardForce;
            _savedMaxSpeed = _move.maxSpeed;
            _hasSavedMoveStats = true;
        }

        _rootTimeLeft = Mathf.Max(0.01f, rootDurationSeconds);
        _extraForwardForce = extraForwardForce;
        _extraMaxSpeed = extraMaxSpeed;
        _accelDurationSeconds = Mathf.Max(0.05f, accelDurationSeconds);
        _rooting = true;
        SetRootFlag(true);

        ForceRootNow();
    }

    private void Update()
    {
        if (_move == null)
        {
            SetRootFlag(false);
            Destroy(this);
            return;
        }

        if (!_rooting)
            return;

        _rootTimeLeft -= Time.deltaTime;
        ForceRootNow();

        if (_rootTimeLeft > 0f)
            return;

        EndRootAndApplyAcceleration();
        Destroy(this);
    }

    private void ForceRootNow()
    {
        if (_move != null)
        {
            _move.forwardForce = 0f;
            _move.maxSpeed = 0f;
        }

        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }

    private void EndRootAndApplyAcceleration()
    {
        if (_move == null)
            return;

        if (_hasSavedMoveStats)
        {
            _move.forwardForce = _savedForwardForce;
            _move.maxSpeed = _savedMaxSpeed;
        }

        SetRootFlag(false);

        var accel = GetComponent<MovementAccelerationEffect>();
        if (accel == null)
            accel = gameObject.AddComponent<MovementAccelerationEffect>();

        accel.ApplyOrRefresh(_move, _extraForwardForce, _extraMaxSpeed, _accelDurationSeconds);

        _rooting = false;
    }

    private void OnDisable()
    {
        if (_rooting && _move != null && _hasSavedMoveStats)
        {
            _move.forwardForce = _savedForwardForce;
            _move.maxSpeed = _savedMaxSpeed;
        }

        SetRootFlag(false);
    }

    private void SetRootFlag(bool rooted)
    {
        if (_move == null)
            return;

        if (rooted)
        {
            if (_rootFlagApplied)
                return;

            _move.SetSkillRooted(true);
            _rootFlagApplied = true;
            return;
        }

        if (!_rootFlagApplied)
            return;

        _move.SetSkillRooted(false);
        _rootFlagApplied = false;
    }
}
