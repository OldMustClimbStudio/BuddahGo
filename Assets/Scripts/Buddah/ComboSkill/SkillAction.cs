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

    // 反噬技能相关参数放在这里
    [Header("Anti (Backfire) Variant")]
    [Tooltip("If set, casting this skill may instead cast the Anti variant based on ObsessionFigure.")]
    public string antiSkillId = string.Empty;
    public bool HasAnti => !string.IsNullOrWhiteSpace(antiSkillId);

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
}
