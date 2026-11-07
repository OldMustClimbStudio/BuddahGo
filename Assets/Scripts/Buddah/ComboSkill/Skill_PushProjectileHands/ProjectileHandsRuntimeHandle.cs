/// <summary>
/// 如来神掌's consume handle (server). Consume ends the charged-push buff on the server
/// (BuddahHandControl.EndProjectilePushModeServer: pushes already in windup still fire) and clears the local mode on
/// every peer. The #1 impulse rewrite is not part of the handle: BuddahHandControl implements IHandsImpulseScale.
/// </summary>
internal sealed class ProjectileHandsRuntimeHandle : ISkillRuntimeHandle
{
    private readonly SkillExecutor _caster;
    private readonly BuddahHandControl _hands;
    private bool _consumed;

    internal ProjectileHandsRuntimeHandle(SkillExecutor caster, BuddahHandControl hands, string skillId)
    {
        _caster = caster;
        _hands = hands;
        SkillId = skillId;
    }

    public string SkillId { get; }

    public bool IsActive => !_consumed && _hands != null && _hands.ProjectileBuffSecondsLeft > 0f;

    public void Consume(string reason)
    {
        if (_consumed)
            return;
        _consumed = true;
        if (_caster == null || !_caster.IsServerInitialized)
            return;

        if (_hands != null)
            _hands.EndProjectilePushModeServer();
        _caster.PlaySkillConsumedObservers(SkillId, _caster.ResolveActiveSlot(SkillId));
        GameLog.Verbose($"[Skill_PushProjectileHands][Server] '{SkillId}' consumed on {_caster.name} ({reason}): charged push buff ended early.");
    }
}
