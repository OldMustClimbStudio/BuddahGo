using System;
using NewBuddah.PredictionV2.Core;
using UnityEngine;

/// <summary>
/// Presentation surface of a racer's skill-driven motor states, on every peer (server, owner, observers) and for
/// every racer (players, AI, training dummies). Plain MonoBehaviour added at runtime by SkillExecutor.OnStartNetwork
/// (no new NetworkBehaviour on the prefab). It only reads state, never writes game state.
///
/// Polled once per frame from the predicted motor. Freeze and burst follow the motor deadlines in this peer's clock:
/// the server and the owner run them, observers get them (and the resolved ComputedStats) from every reconcile
/// snapshot, so an observer whose snapshots pause still ends on time. Starts are reported at once; ends after
/// <see cref="releaseDelaySeconds"/> so a one- or two-tick flicker raises nothing.
///
/// 动作层 (no art): while frozen the character Animator holds (speed 0) and gets its previous speed back after.
/// Artists attach VFX to <see cref="FrozenChanged"/> / <see cref="BurstChanged"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class RacerSkillPresentationState : MonoBehaviour
{
    [Tooltip("An end (unfreeze, burst over) is reported only after the state stayed off this long, so a stale reconcile snapshot does not make the events flicker.")]
    [SerializeField, Min(0f)] private float releaseDelaySeconds = 0.1f;

    [Tooltip("Hold the character Animator (speed 0) while the racer is frozen.")]
    [SerializeField] private bool holdAnimatorWhileFrozen = true;

    /// <summary>True when the racer becomes frozen (禁锢), false when it thaws. Every peer.</summary>
    public event Action<bool> FrozenChanged;

    /// <summary>True when a burst (金足破阵 dash) starts, false when it ends. Every peer. The direction is the racer's flat facing.</summary>
    public event Action<bool> BurstChanged;

    /// <summary>The reported freeze state (matches the last <see cref="FrozenChanged"/>).</summary>
    public bool IsFrozen => _frozen.Reported;

    /// <summary>Seconds of freeze left in this peer's clock; 0 when not frozen.</summary>
    public float FreezeSecondsLeft => _motor != null ? _motor.FreezeSecondsLeft : 0f;

    /// <summary>The reported burst state (matches the last <see cref="BurstChanged"/>).</summary>
    public bool IsBurstActive => _burst.Reported;

    /// <summary>Seconds of burst left in this peer's clock; 0 when no burst runs.</summary>
    public float BurstSecondsLeft => _motor != null ? _motor.BurstSecondsLeft : 0f;

    private readonly PresentationFlag _frozen = new PresentationFlag();
    private readonly PresentationFlag _burst = new PresentationFlag();
    private readonly AnimatorSpeedHold _animatorHold = new AnimatorSpeedHold();
    private BuddahPredictedMotor _motor;
    private Animator _animator;
    // The Animator the hold was written to (a rebind must release it on that one).
    private Animator _heldAnimator;

    /// <summary>Gets the racer's presentation state, adding it when missing (it then binds itself lazily).</summary>
    public static RacerSkillPresentationState GetOrAdd(GameObject racer)
    {
        if (racer == null)
            return null;
        if (!racer.TryGetComponent(out RacerSkillPresentationState state))
            state = racer.AddComponent<RacerSkillPresentationState>();
        return state;
    }

    /// <summary>Idempotent. SkillExecutor passes its motor and character Animator; null ones are resolved here.</summary>
    internal void Bind(BuddahPredictedMotor motor, Animator animator)
    {
        if (motor != null)
            _motor = motor;
        if (animator != null && animator != _animator)
        {
            ReleaseAnimatorHold();
            _animator = animator;
        }
        ResolveRefs();
    }

    /// <summary>The network object stops on this peer: releases the hold and reports the owed ends.</summary>
    internal void Unbind()
    {
        ReleaseAnimatorHold();
        if (_frozen.Reset())
            Raise(FrozenChanged, false, "frozen");
        if (_burst.Reset())
            Raise(BurstChanged, false, "burst");
    }

    private void ResolveRefs()
    {
        if (_motor == null)
            _motor = GetComponent<BuddahPredictedMotor>();
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>(true);
    }

    private void Update()
    {
        ResolveRefs();
        if (_motor == null)
            return;

        float now = Time.time;
        bool frozen = _motor.IsFrozen;
        // Mirrors ComputedStats.IsBurstActive: the dash only runs while the body is neither rooted nor frozen.
        bool burst = _motor.BurstSecondsLeft > 0f && !frozen && !_motor.CurrentComputedStats.IsRooted;

        if (_frozen.Update(frozen, now, releaseDelaySeconds))
            Raise(FrozenChanged, _frozen.Reported, "frozen");
        if (_burst.Update(burst, now, releaseDelaySeconds))
            Raise(BurstChanged, _burst.Reported, "burst");

        // Re-applied every frame (idempotent), so a re-enabled component holds again while still frozen.
        if (holdAnimatorWhileFrozen && _frozen.Reported)
            HoldAnimator();
        else
            ReleaseAnimatorHold();
    }

    private void HoldAnimator()
    {
        if (_animator == null)
            return;
        if (_heldAnimator != null && _heldAnimator != _animator)
            ReleaseAnimatorHold();
        _animator.speed = _animatorHold.Begin(_animator.speed);
        _heldAnimator = _animator;
    }

    private void ReleaseAnimatorHold()
    {
        if (!_animatorHold.IsHolding)
            return;
        float restored = _animatorHold.End(0f);
        if (_heldAnimator != null)
            _heldAnimator.speed = restored;
        _heldAnimator = null;
    }

    private void Raise(Action<bool> handler, bool value, string what)
    {
        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[SkillPresentation] '{name}' {what}={value}");
        if (handler == null)
            return;
        // A failing presentation listener must not stop the other one or the Animator hold.
        try { handler(value); }
        catch (Exception exception) { Debug.LogException(exception, this); }
    }

    private void OnDisable()
    {
        ReleaseAnimatorHold();
    }

    private void OnDestroy()
    {
        ReleaseAnimatorHold();
    }
}
