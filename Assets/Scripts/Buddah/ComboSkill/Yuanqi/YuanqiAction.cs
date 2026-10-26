using UnityEngine;

/// <summary>
/// 缘起模块: one pair's effect, a ScriptableObject like SkillAction (one Yuanqi_* class + one .asset per pair, listed in
/// SkillDatabase.yuanqis). YuanqiRegistry instantiates a per-racer copy for each enabled pair fully inside the loadout.
/// A pair never references a Skill_* class or another Yuanqi_*: it touches skills only through ctx (ISkillRuntimeHandle),
/// movement only through SkillExecutor.ApplyXToOwner, impulses only through BuddahPredictionRouter.RouteImpulse and
/// visuals only through ctx.Presentation → PlayObservers.
/// </summary>
public abstract class YuanqiAction : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("snake_case id, e.g. frost_lock. Observers resolve the presentation by it.")]
    public string yuanqiId;
    public string displayName;

    [Header("Pair (unordered)")]
    [Tooltip("ctx.EntryA always belongs to skillA, whichever skill was cast first.")]
    public string skillA;
    public string skillB;
    [Tooltip("Applied by the registry after OnTriggerServer (idempotent if the pair already consumed it).")]
    public bool consumeA;
    public bool consumeB;

    [Header("Rules")]
    public YuanqiMode mode = YuanqiMode.Instant;
    public YuanqiRetrigger retriggerPolicy = YuanqiRetrigger.OncePerOverlap;
    public bool isEnabled = true;

    /// <summary>Server: both skills just overlapped. Set ctx.Presentation for observers; consume in the pair's own order.</summary>
    public abstract void OnTriggerServer(YuanqiContext ctx);

    /// <summary>Server, Sustained only: either entry ended (expiry, consume or reset). Undo every rewrite.</summary>
    public virtual void OnEndServer(YuanqiContext ctx)
    {
    }

    /// <summary>Every peer (host included), presentation only. Never writes game state.</summary>
    public virtual void PlayObservers(SkillExecutor caster, YuanqiPresentationPayload payload, bool localIsCaster)
    {
    }
}
