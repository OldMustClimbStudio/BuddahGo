/// <summary>
/// 技能兼容接口：a skill's running effect as a 缘起 sees it. Created by the skill itself (SkillAction.CreateRuntimeHandle),
/// server-side, and stored on the caster's SkillStateBoard entry. A 缘起 touches skills only through this interface
/// (and the small rewrite interfaces below), never through Skill_* classes or their components.
/// </summary>
public interface ISkillRuntimeHandle
{
    string SkillId { get; }

    /// <summary>The underlying effect is still running (informational; the board window decides "生效中").</summary>
    bool IsActive { get; }

    /// <summary>
    /// Server: ends the remaining effect and its presentation (server, owner and observers). Idempotent.
    /// Consumed skills refund neither cooldown nor obsession, so nothing here touches them.
    /// </summary>
    void Consume(string reason);
}

/// <summary>法相金身: the live body scale while the skill runs (>= 0.1).</summary>
public interface IScaleRuntimeHandle : ISkillRuntimeHandle
{
    float CurrentScale { get; }
}

/// <summary>
/// 如来神掌 rewrite (#1 金身巨掌). Implemented by BuddahHandControl, server-side: while true, the charged-projectile
/// impulse is multiplied by the same body-scale sample that already sizes the projectile.
/// </summary>
public interface IHandsImpulseScale
{
    bool ImpulseFollowsScale { get; set; }
}
