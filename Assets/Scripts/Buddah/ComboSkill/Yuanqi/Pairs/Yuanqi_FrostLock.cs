using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

/// <summary>
/// #2 禅足冰封 (acceleration + slowtrap, Instant, shipped row consumes both). The moment the two overlap, every racer
/// other than the caster within the radius (horizontal) of the caster — AI and training dummies included — is frozen
/// for freezeSeconds. The radius is radiusMeters × the caster's palm multiplier at the trigger (PlayerScaleEffect /
/// PalmSizeRule, the same sample 如来神掌 and #1 use: ×2 at full 金身 → 24 m, ×0.5 at full 金身反噬, linear in
/// between, ×1 otherwise; 已定 2026-11-07, "技能联动") (ApplyFreezeToOwner also interrupts a confirming cast and refunds that slot). Effect first, then
/// the skills the row's consumeA / consumeB name are consumed (yuanqi-architecture §4.2 / §4.6).
/// Presentation (Docs/vfx/yuanqi-frost-lock.md): the slot vfxId (the frozen ground, radius R) on the caster from
/// PlayObservers, its ground sized by radius / radiusMeters (SkillVfxLayer area scale), and the ice block slot FrozenIceShell.VfxId on each frozen racer from its own
/// RacerSkillPresentationState.FrozenChanged (its Animator holds while frozen). Every peer gets the payload (centre,
/// radius, seconds N, frozen target ids) through SkillExecutor.YuanqiObserved.
/// </summary>
[CreateAssetMenu(fileName = "Yuanqi_FrostLock", menuName = "Skills/Yuanqi/Frost Lock")]
public class Yuanqi_FrostLock : YuanqiAction
{
    [Header("Frost Lock (table Skill/YuanqiEffectFrostLock; these values are the fallback without a row)")]
    [Tooltip("N: freeze seconds applied to every target.")]
    [Min(0f)] public float freezeSeconds = 1.5f;
    [Tooltip("R: horizontal radius around the caster at palm multiplier 1, metres (scaled by the caster's palm multiplier).")]
    [Min(0f)] public float radiusMeters = 12f;
    [Tooltip("Practice training dummies are frozen too.")]
    public bool affectsTrainingDummies = true;

    [NonSerialized] private readonly List<SkillExecutor> _targets = new List<SkillExecutor>();

    public override void OnTriggerServer(YuanqiContext ctx)
    {
        SkillExecutor caster = ctx != null ? ctx.Caster : null;
        if (caster == null)
            return;

        Vector3 center = caster.transform.position;
        float radius = RadiusFor(radiusMeters, PalmMultiplierOf(caster));
        YuanqiTargeting.CollectOthersWithin(caster, radius, affectsTrainingDummies, _targets);

        var targetIds = new List<int>(_targets.Count);
        for (int i = 0; i < _targets.Count; i++)
        {
            SkillExecutor target = _targets[i];
            target.ApplyFreezeToOwner(freezeSeconds);
            // The registry records one "yuanqi-trigger" combat event per id listed here.
            NetworkObject targetObject = target.NetworkObject;
            if (targetObject != null)
                targetIds.Add(targetObject.ObjectId);
        }
        int frozen = _targets.Count;
        _targets.Clear();

        ctx.Presentation = new YuanqiPresentationPayload
        {
            Center = center,
            Radius = radius,
            Seconds = freezeSeconds,
            TargetObjectIds = targetIds.ToArray(),
        };

        // Effect first, then consume what the table names (shipped: both); if consuming throws, the freeze has landed.
        ctx.ConsumeConfigured("yuanqi:" + yuanqiId);

        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[Yuanqi][FrostLock][Server] '{caster.name}' froze {frozen} racer(s) within {radius:0.#} m (R {radiusMeters:0.#} m) for {freezeSeconds:0.##}s; consume {skillA}={consumeA}, {skillB}={consumeB}.");
    }

    // Every peer: the frozen ground slot on the caster for the freeze (laid once, left in place). The ice blocks on the
    // frozen racers come from their own FrozenChanged (FrozenIceShell).
    public override void PlayObservers(SkillExecutor caster, YuanqiPresentationPayload payload, bool localIsCaster)
    {
        // The ground is authored at R; it grows with the radius the server used (payload), never with the body itself.
        float areaScale = radiusMeters > 0f && payload.Radius > 0f ? payload.Radius / radiusMeters : 1f;
        PlayVfxSlotLocal(caster, payload.Seconds > 0f ? payload.Seconds : freezeSeconds, areaScale);
    }

    /// <summary>The freeze radius for base radius R at this palm multiplier (PalmSizeRule; 1 without a scale skill).</summary>
    internal static float RadiusFor(float radiusMeters, float palmMultiplier)
        => radiusMeters * (palmMultiplier > 0f && !float.IsNaN(palmMultiplier) ? palmMultiplier : 1f);

    private static float PalmMultiplierOf(SkillExecutor caster)
    {
        PlayerScaleEffect scale = caster.GetComponent<PlayerScaleEffect>();
        return scale != null ? scale.CurrentPalmSizeMultiplier : 1f;
    }

    public override void CollectVfxSlots(List<SkillAction.VfxSlotTiming> into)
    {
        AddVfxSlot(into, freezeSeconds, "Skill/YuanqiEffectFrostLock.freezeSeconds");
        // Played by each frozen racer (FrozenIceShell) for the freeze seconds it has left: N at the trigger.
        into.Add(new SkillAction.VfxSlotTiming { vfxId = FrozenIceShell.VfxId, seconds = freezeSeconds, source = "Skill/YuanqiEffectFrostLock.freezeSeconds" });
    }
}
