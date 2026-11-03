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

    protected static float ResolveVfxDuration(float configuredSeconds, float fallbackSeconds)
    {
        return configuredSeconds > 0f ? configuredSeconds : fallbackSeconds;
    }

    // Plays a slot effect (Docs/vfx/README.md section 4) on the caster on this client. Skills call it from
    // ExecuteObservers, so every client plays it without another RPC. Empty id = none.
    protected static void PlaySlotOnCaster(SkillExecutor caster, string vfxId, float durationSeconds)
    {
        if (caster == null || string.IsNullOrWhiteSpace(vfxId))
            return;
        var vfx = caster.GetComponent<SkillVfxReplicator>();
        if (vfx != null)
            vfx.PlaySlotLocal(vfxId, durationSeconds);
    }

    // One effect this skill plays and the length it passes (Docs/vfx/README.md section 4, "时长同步"). Skills build
    // their playback and this list from the same properties, so the editor's timing window shows what the race plays.
    // In play mode the lengths already carry the design tables (SkillDatabase applies them to the SOs).
    public struct VfxSlotTiming
    {
        public string vfxId;
        public float seconds;
        // Where the length comes from, in table / field words.
        public string source;
        // Absolute vfxDurationSeconds on the SO: the effect no longer follows the table's gameplay length.
        public bool overridden;
    }

    // Effects this skill plays with their lengths at the current values. Empty ids are left out.
    public virtual void CollectVfxSlots(System.Collections.Generic.List<VfxSlotTiming> into)
    {
    }

    protected static void AddVfxSlot(System.Collections.Generic.List<VfxSlotTiming> into, string vfxId,
        float configuredSeconds, float gameplaySeconds, string source)
    {
        if (string.IsNullOrWhiteSpace(vfxId))
            return;
        into.Add(new VfxSlotTiming
        {
            vfxId = vfxId,
            seconds = ResolveVfxDuration(configuredSeconds, gameplaySeconds),
            source = source,
            overridden = configuredSeconds > 0f,
        });
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
    public virtual void ExecuteObservers(SkillExecutor caster, int slotIndex, bool isAnti, bool localIsCaster)
    {
        ExecuteObservers(caster, slotIndex);
    }

    /// <summary>
    /// Runs only on the owning client for local feel (camera/UI/input feedback).
    /// </summary>
    public virtual void ExecuteLocal(SkillExecutor caster, int slotIndex, bool isAnti)
    {
        // optional
    }
}
