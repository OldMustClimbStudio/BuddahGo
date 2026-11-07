using NewBuddah.PredictionV2.Core;
using UnityEngine;

/// <summary>
/// 法相金身's handle (server). CurrentScale is the live body scale: the server copy of PlayerScaleEffect (smooth grow /
/// shrink; present on a host) or, without it, the motor's step scale. Consume ends the motor scale on the server and
/// the owner (SkillExecutor.EndScaleForOwner) and runs the normal shrink on every peer.
/// </summary>
internal sealed class GiantRuntimeHandle : IScaleRuntimeHandle
{
    private readonly SkillExecutor _caster;
    private readonly float _untilTime;
    private PlayerScaleEffect _scaleEffect;
    private BuddahPredictedMotor _motor;
    private bool _consumed;

    internal GiantRuntimeHandle(SkillExecutor caster, string skillId, float durationSeconds)
    {
        _caster = caster;
        SkillId = skillId;
        _untilTime = Time.time + Mathf.Max(0f, durationSeconds);
    }

    public string SkillId { get; }

    public bool IsActive => !_consumed && _caster != null && Time.time < _untilTime;

    public float CurrentScale
    {
        get
        {
            if (_caster == null)
                return 1f;

            // The visual effect is added and destroyed per cast, so the cache is refreshed when it is gone.
            if (_scaleEffect == null)
                _scaleEffect = _caster.GetComponent<PlayerScaleEffect>();
            if (_scaleEffect != null)
                return Mathf.Max(PlayerScaleEffect.MinScaleMultiplier, _scaleEffect.CurrentScaleMultiplier);

            if (_motor == null)
                _motor = _caster.GetComponent<BuddahPredictedMotor>();
            return _motor != null ? Mathf.Max(PlayerScaleEffect.MinScaleMultiplier, _motor.CurrentScaleMultiplier) : 1f;
        }
    }

    public void Consume(string reason)
    {
        if (_consumed)
            return;
        _consumed = true;
        if (_caster == null || !_caster.IsServerInitialized)
            return;

        _caster.EndScaleForOwner();
        _caster.PlaySkillConsumedObservers(SkillId, _caster.ResolveActiveSlot(SkillId));
        GameLog.Verbose($"[Skill_Giant][Server] '{SkillId}' consumed on {_caster.name} ({reason}): scale ended early.");
    }
}
