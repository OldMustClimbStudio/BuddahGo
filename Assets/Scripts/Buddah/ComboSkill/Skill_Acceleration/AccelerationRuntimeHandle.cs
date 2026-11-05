using UnityEngine;

/// <summary>
/// 神足通's consume handle (server). Consume ends the acceleration buff on the server and the owner
/// (SkillExecutor.EndAccelerationForOwner) and plays the early-end presentation on every peer (trail fade, back VFX,
/// owner FOV settle). Prediction path: only a positive buff is ended. When an enemy slow has overwritten the shared
/// slot after the cast, the consume leaves that slow running to the later of the two deadlines (open question for the
/// user). Legacy fallback (prediction off): CancelAndRestore drops the merged effect, slow included.
/// </summary>
internal sealed class AccelerationRuntimeHandle : ISkillRuntimeHandle
{
    private readonly SkillExecutor _caster;
    private readonly float _untilTime;
    private bool _consumed;

    internal AccelerationRuntimeHandle(SkillExecutor caster, string skillId, float durationSeconds)
    {
        _caster = caster;
        SkillId = skillId;
        _untilTime = Time.time + Mathf.Max(0f, durationSeconds);
    }

    public string SkillId { get; }

    public bool IsActive => !_consumed && _caster != null && Time.time < _untilTime;

    public void Consume(string reason)
    {
        if (_consumed)
            return;
        _consumed = true;
        if (_caster == null || !_caster.IsServerInitialized)
            return;

        _caster.EndAccelerationForOwner();
        _caster.PlaySkillConsumedObservers(SkillId, _caster.ResolveActiveSlot(SkillId));
        GameLog.Verbose($"[Skill_Acceleration][Server] '{SkillId}' consumed on {_caster.name} ({reason}): acceleration ended early.");
    }
}
