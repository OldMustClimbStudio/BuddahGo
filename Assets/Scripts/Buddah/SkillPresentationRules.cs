using UnityEngine;

/// <summary>
/// Start/end edge of a boolean skill state as the presentation layer sees it (freeze, burst, backfire penalty).
/// The start is reported on the first active sample; the end only after the state stayed inactive for the release
/// delay, so a one- or two-tick flicker (a stale reconcile snapshot undoing a fresh owner command) raises nothing.
/// Pure and unit-tested; the owners poll it once per frame.
/// </summary>
internal sealed class PresentationFlag
{
    private float _inactiveSince = float.NaN;

    /// <summary>The state last reported to listeners.</summary>
    public bool Reported { get; private set; }

    /// <summary>No release delay: every change is reported at once.</summary>
    public bool Update(bool active) => Update(active, 0f, 0f);

    /// <summary>Feeds this frame's raw state. Returns true when <see cref="Reported"/> changed (raise the event).</summary>
    public bool Update(bool active, float now, float releaseDelaySeconds)
    {
        if (active)
        {
            _inactiveSince = float.NaN;
            if (Reported)
                return false;
            Reported = true;
            return true;
        }

        if (!Reported)
            return false;
        if (float.IsNaN(_inactiveSince))
            _inactiveSince = now;
        if (now - _inactiveSince < Mathf.Max(0f, releaseDelaySeconds))
            return false;

        _inactiveSince = float.NaN;
        Reported = false;
        return true;
    }

    /// <summary>Forgets the state (object stops). Returns true when it was reported active, i.e. an end is owed.</summary>
    public bool Reset()
    {
        bool wasReported = Reported;
        Reported = false;
        _inactiveSince = float.NaN;
        return wasReported;
    }
}

/// <summary>
/// Animator playback hold of a frozen racer: Begin stores the current speed and returns 0, End returns the stored
/// speed. Idempotent both ways, so re-entering a freeze never stores the held 0. Pure and unit-tested.
/// </summary>
internal sealed class AnimatorSpeedHold
{
    public bool IsHolding { get; private set; }

    /// <summary>The speed to restore when the hold ends.</summary>
    public float HeldSpeed { get; private set; } = 1f;

    /// <summary>Returns the speed to write while holding (0).</summary>
    public float Begin(float currentSpeed)
    {
        if (!IsHolding)
        {
            HeldSpeed = currentSpeed;
            IsHolding = true;
        }
        return 0f;
    }

    /// <summary>Returns the speed to write: the stored one after a hold, else <paramref name="currentSpeed"/> unchanged.</summary>
    public float End(float currentSpeed)
    {
        if (!IsHolding)
            return currentSpeed;
        IsHolding = false;
        return HeldSpeed;
    }
}
