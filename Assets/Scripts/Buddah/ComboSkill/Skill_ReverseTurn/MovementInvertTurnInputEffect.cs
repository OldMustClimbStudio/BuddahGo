using UnityEngine;

/// <summary>
/// Local (owner-client) movement effect: invert horizontal turn input (A/D) for a duration.
/// </summary>
public class MovementInvertTurnInputEffect : MonoBehaviour
{
    private BuddahMovement _move;
    private float _savedTurnInputMultiplier;
    private bool _initialized;
    private float _timeLeft;

    public void ApplyOrRefresh(BuddahMovement move, float durationSeconds)
    {
        _move = move;
        if (_move == null)
        {
            Debug.LogWarning("[MovementInvertTurnInputEffect] BuddahMovement missing.");
            Destroy(this);
            return;
        }

        if (!_initialized)
        {
            _savedTurnInputMultiplier = _move.turnInputMultiplier;
            _initialized = true;
        }

        _timeLeft = Mathf.Max(_timeLeft, Mathf.Max(0.05f, durationSeconds));
        ApplyNow();
    }

    private void Update()
    {
        if (!_initialized || _move == null)
        {
            Destroy(this);
            return;
        }

        _timeLeft -= Time.deltaTime;
        if (_timeLeft <= 0f)
        {
            Restore();
            Destroy(this);
        }
    }

    private void ApplyNow()
    {
        if (_move == null)
            return;

        float magnitude = Mathf.Max(0.0001f, Mathf.Abs(_savedTurnInputMultiplier));
        _move.turnInputMultiplier = -magnitude;
    }

    private void Restore()
    {
        if (_move == null)
            return;

        _move.turnInputMultiplier = _savedTurnInputMultiplier;
    }

    private void OnDisable()
    {
        if (_initialized && _move != null)
            Restore();
    }
}
