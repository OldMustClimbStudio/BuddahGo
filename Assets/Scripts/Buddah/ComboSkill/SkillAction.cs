using FishNet.Object;
using UnityEngine;
using System;

public abstract class SkillAction : ScriptableObject
{
    [Header("Identity")]
    public string skillId = "push";
    public string displayName = "Push";

    [Header("Balance")]
    [Min(0f)] public float cooldownSeconds = 1f;
    [Min(0f)] public float castLockSeconds = 0.25f;


    [Header("Obsession")]
    [Min(0f)] public float obsessionGain = 0f;

    public float ObsessionGain => obsessionGain;

    protected static float ResolveVfxDuration(float configuredSeconds, float fallbackSeconds)
    {
        return configuredSeconds > 0f ? configuredSeconds : fallbackSeconds;
    }

    protected void PlayObserverFeel(SkillExecutor caster, string startEventId, string stopEventId, float durationSeconds)
    {
        caster.PlayFeelLocalTimed(startEventId, stopEventId, durationSeconds, $"{skillId}_observers");
    }

    /// <summary>
    /// Server-authoritative skill execution (spawn hitbox, apply forces, etc).
    /// </summary>
    public abstract void ExecuteServer(SkillExecutor caster, int slotIndex);

    /// <summary>
    /// Runs on all observers (including owner). Use for VFX/SFX/anim/log.
    /// Keep it cosmetic; game state should be decided on server.
    /// </summary>
    public virtual void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        // optional
    }

    /// <summary>
    /// Observer-side execution with network context. Default behavior keeps older skills working.
    /// </summary>
    public virtual void ExecuteObservers(SkillExecutor caster, int slotIndex, bool localIsCaster)
    {
        ExecuteObservers(caster, slotIndex);
    }

    /// <summary>
    /// Runs only on the owning client for local feel (camera/UI/input feedback).
    /// </summary>
    public virtual void ExecuteLocal(SkillExecutor caster, int slotIndex)
    {
        // optional
    }

    // ---- 缘起 (skill state board) ----

    /// <summary>
    /// Server: when this execution counts as "生效中" on the caster's SkillStateBoard. Default: from execution, for the
    /// table Skill.effectDurationSeconds (SO primary duration without tables); 0 registers nothing.
    /// </summary>
    public virtual SkillActiveWindow GetActiveWindow(SkillExecutor caster)
    {
        return new SkillActiveWindow(0f, caster != null ? caster.ResolveEffectDurationSeconds(skillId) : 0f);
    }

    /// <summary>
    /// Server, right after ExecuteServer: the handle a 缘起 uses to consume (or rewrite) this execution's running effect.
    /// Null = the skill cannot be consumed.
    /// </summary>
    public virtual ISkillRuntimeHandle CreateRuntimeHandle(SkillExecutor caster) => null;

    /// <summary>
    /// Every peer (host included): presentation of an early end after a 缘起 consumed the skill (trail, FOV, VFX...).
    /// Presentation only; the server already ended the effect itself.
    /// </summary>
    public virtual void ExecuteConsumedObservers(SkillExecutor caster, int slotIndex, bool localIsCaster)
    {
        // optional
    }
}
