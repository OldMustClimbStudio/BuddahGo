using FishNet.Object;
using UnityEngine;

public abstract class SkillAction : ScriptableObject
{
    [Header("Identity")]
    public string skillId = "push";
    public string displayName = "Push";

    [Header("Balance")]
    [Min(0f)] public float cooldownSeconds = 1f;

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
