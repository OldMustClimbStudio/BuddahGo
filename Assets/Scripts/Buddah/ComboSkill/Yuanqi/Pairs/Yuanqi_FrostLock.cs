using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

/// <summary>
/// #2 禅足冰封 (acceleration + slowtrap, Instant, shipped row consumes both). The moment the two overlap, every racer
/// other than the caster within radiusMeters (horizontal) of the caster — AI and training dummies included — is frozen
/// for freezeSeconds (ApplyFreezeToOwner also interrupts a confirming cast and refunds that slot). Effect first, then
/// the skills the row's consumeA / consumeB name are consumed (yuanqi-architecture §4.2 / §4.6).
/// Presentation is an interface only (no art yet): every peer gets the payload (centre, radius R, seconds N, frozen
/// target ids) through SkillExecutor.YuanqiObserved, and each frozen racer raises
/// RacerSkillPresentationState.FrozenChanged (its Animator holds while frozen).
/// </summary>
[CreateAssetMenu(fileName = "Yuanqi_FrostLock", menuName = "Skills/Yuanqi/Frost Lock")]
public class Yuanqi_FrostLock : YuanqiAction
{
    [Header("Frost Lock (table Skill/YuanqiEffectFrostLock; these values are the fallback without a row)")]
    [Tooltip("N: freeze seconds applied to every target.")]
    [Min(0f)] public float freezeSeconds = 1.5f;
    [Tooltip("R: horizontal radius around the caster, metres.")]
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
        YuanqiTargeting.CollectOthersWithin(caster, radiusMeters, affectsTrainingDummies, _targets);

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
            Radius = radiusMeters,
            Seconds = freezeSeconds,
            TargetObjectIds = targetIds.ToArray(),
        };

        // Effect first, then consume what the table names (shipped: both); if consuming throws, the freeze has landed.
        ctx.ConsumeConfigured("yuanqi:" + yuanqiId);

        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[Yuanqi][FrostLock][Server] '{caster.name}' froze {frozen} racer(s) within {radiusMeters:0.#} m for {freezeSeconds:0.##}s; consume {skillA}={consumeA}, {skillB}={consumeB}.");
    }
}
