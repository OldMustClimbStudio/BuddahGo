using System;
using UnityEngine;

/// <summary>
/// #1 金身巨掌 (giant + push_projectile_hands, Sustained, consumes nothing). While both are active the charged
/// projectile's impulse follows the body scale, sampled together with its size (which already follows the scale);
/// no cap, no own parameters. Either skill ending turns it off (yuanqi-architecture §5.2).
/// </summary>
[CreateAssetMenu(fileName = "Yuanqi_GiantPalm", menuName = "Skills/Yuanqi/Giant Palm")]
public class Yuanqi_GiantPalm : YuanqiAction
{
    public override void OnTriggerServer(YuanqiContext ctx)
    {
        SkillExecutor caster = ctx != null ? ctx.Caster : null;
        if (caster == null)
            return;

        if (!SetImpulseFollowsScale(caster, true))
            return;

        ctx.Presentation = new YuanqiPresentationPayload
        {
            Center = caster.transform.position,
            Radius = 0f,
            Seconds = 0f,
            TargetObjectIds = Array.Empty<int>(),
        };

        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[Yuanqi][GiantPalm][Server] '{caster.name}' palm impulse now follows body scale.");
    }

    public override void OnEndServer(YuanqiContext ctx)
    {
        SkillExecutor caster = ctx != null ? ctx.Caster : null;
        if (caster == null)
            return;

        if (SetImpulseFollowsScale(caster, false) && NetDebug.EnableVerboseLog)
            GameLog.Verbose($"[Yuanqi][GiantPalm][Server] '{caster.name}' palm impulse back to normal.");
    }

    private static bool SetImpulseFollowsScale(SkillExecutor caster, bool follows)
    {
        IHandsImpulseScale hands = caster.GetComponent<IHandsImpulseScale>();
        if (hands == null)
        {
            Debug.LogWarning($"[Yuanqi][GiantPalm] '{caster.name}' has no IHandsImpulseScale (BuddahHandControl).");
            return false;
        }
        hands.ImpulseFollowsScale = follows;
        return true;
    }
}
