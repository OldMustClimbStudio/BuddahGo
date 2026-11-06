using UnityEngine;

/// <summary>
/// 禅足冰封's ice block on a frozen racer (players, AI, training dummies), every peer (Docs/vfx/yuanqi-frost-lock.md).
/// The moment <see cref="RacerSkillPresentationState.FrozenChanged"/> reports a freeze it plays the
/// <see cref="VfxId"/> slot on this racer through its <see cref="SkillVfxReplicator"/> (local, no RPC) for the freeze
/// seconds left; the slot follows the racer's visual root and scales with its body, and melts at the end of that
/// window. A freeze that ends early (reset, respawn) ends the slot then. Added next to the presentation state by
/// SkillExecutor; never touches game state.
/// </summary>
[DisallowMultipleComponent]
public sealed class FrozenIceShell : MonoBehaviour
{
    /// <summary>The ice block slot (VfxRegistry / SkillVfxDatabase). Yuanqi_FrostLock lists it with its own slots.</summary>
    public const string VfxId = "frost_lock_hit";

    // The thaw is reported RacerSkillPresentationState.releaseDelaySeconds (0.1 s) after the motor; a report that comes
    // sooner than the window's end minus this is an early end.
    private const float EarlyEndSlackSeconds = 0.05f;

    private RacerSkillPresentationState _state;
    private SkillVfxReplicator _vfx;
    private float _endsAt;

    internal static void Attach(GameObject racer, RacerSkillPresentationState state)
    {
        if (racer == null || state == null)
            return;
        if (!racer.TryGetComponent(out FrozenIceShell shell))
            shell = racer.AddComponent<FrozenIceShell>();
        shell.Bind(state);
    }

    private void Bind(RacerSkillPresentationState state)
    {
        if (_state == state)
            return;
        if (_state != null)
            _state.FrozenChanged -= OnFrozenChanged;
        _state = state;
        if (isActiveAndEnabled)
            _state.FrozenChanged += OnFrozenChanged;
    }

    private void OnEnable()
    {
        if (_state != null)
        {
            _state.FrozenChanged -= OnFrozenChanged;
            _state.FrozenChanged += OnFrozenChanged;
        }
    }

    private void OnDisable()
    {
        if (_state != null)
            _state.FrozenChanged -= OnFrozenChanged;
    }

    private void OnFrozenChanged(bool frozen)
    {
        if (_vfx == null && !TryGetComponent(out _vfx))
            return;
        if (frozen)
        {
            float seconds = _state != null ? _state.FreezeSecondsLeft : 0f;
            if (seconds <= 0f)
                return;
            _vfx.PlaySlotLocal(VfxId, seconds);
            _endsAt = Time.time + seconds;
        }
        else if (_endsAt > 0f && Time.time < _endsAt - EarlyEndSlackSeconds)
        {
            _vfx.StopSlotLocal(VfxId);
            _endsAt = 0f;
        }
    }
}
