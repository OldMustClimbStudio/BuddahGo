using System;
using UnityEngine;

/// <summary>
/// #4 金足破阵 (acceleration + giant, Instant, shipped row consumes acceleration only, before the burst). 神足通 stops accelerating and the giant
/// dashes instead: the speed cap jumps to cap + burstExtraMaxSpeed and eases back over burstSeconds (motor Burst
/// command). During the burst the giant's body hits each other racer once with impactImpulseMultiplier x the basic
/// push impulse, sideways from the caster (Q9). 法相金身 keeps running to its own end (yuanqi-architecture §5.1).
/// Presentation (interfaces, no art): the trigger payload carries the dash direction (SkillExecutor.YuanqiObserved),
/// each hit raises SkillExecutor.YuanqiImpactObserved, the burst raises RacerSkillPresentationState.BurstChanged;
/// the caster's own camera gets a short FOV kick.
/// </summary>
[CreateAssetMenu(fileName = "Yuanqi_GiantCharge", menuName = "Skills/Yuanqi/Giant Charge")]
public class Yuanqi_GiantCharge : YuanqiAction
{
    private const float FlatDirectionMinSqrMagnitude = 0.0001f;

    [Header("Giant Charge (table Skill/YuanqiEffectGiantCharge; these values are the fallback without a row)")]
    [Tooltip("Extra max speed on top of the base cap at the burst start; it decays to 0 with ease-out (motor rule).")]
    [Min(0f)] public float burstExtraMaxSpeed = 60f;
    [Tooltip("Seconds for the extra cap to decay back to the base cap. The body impact is armed for the same time.")]
    [Min(0f)] public float burstSeconds = 0.8f;
    [Tooltip("M: impact impulse = M x Push.basic.impulseStrength (BuddahHandControl.BasicPushImpulseStrength).")]
    [Min(0f)] public float impactImpulseMultiplier = 3f;

    [Header("Caster camera (no art)")]
    [Tooltip("FOV kick in degrees on the caster's own camera when the dash starts (instant, then it settles). 0 = off.")]
    [Min(0f)] public float casterFovKickDegrees = 8f;
    [Tooltip("Seconds the FOV kick is held before it settles.")]
    [Min(0f)] public float casterFovKickHoldSeconds = 0.15f;
    [Tooltip("Seconds the FOV kick takes to settle back to 0.")]
    [Min(0f)] public float casterFovKickSettleSeconds = 0.65f;

    public override void OnTriggerServer(YuanqiContext ctx)
    {
        SkillExecutor caster = ctx != null ? ctx.Caster : null;
        if (caster == null)
            return;

        // Consume first, then burst: with the shipped row (神足通 consumed, 法相金身 kept) the burst launches from the
        // base cap, not base + acceleration. The row's consumeA / consumeB decide what is consumed, in either skill
        // order. Exception (open question): an enemy slow that overwrote the shared acceleration slot after the cast is
        // not ended by the consume, so the burst then starts from the slowed cap.
        ctx.ConsumeConfigured("yuanqi:" + yuanqiId);
        caster.ApplyBurstToOwner(burstExtraMaxSpeed, burstSeconds);

        float impulse = ResolveImpactImpulse(caster);
        BuddahBodyImpact impact = caster.GetComponent<BuddahBodyImpact>();
        if (impact == null)
            impact = caster.gameObject.AddComponent<BuddahBodyImpact>();
        impact.Arm(impulse, burstSeconds, yuanqiId);

        ctx.Presentation = new YuanqiPresentationPayload
        {
            Center = caster.transform.position,
            Radius = 0f,
            Seconds = burstSeconds,
            TargetObjectIds = Array.Empty<int>(),
            // The motor launches the dash along the flat facing (BuddahPredictedMotor.LaunchBodyForBurst).
            Direction = FlatForward(caster.transform),
        };

        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[Yuanqi][GiantCharge][Server] '{caster.name}' burst +{burstExtraMaxSpeed:0.#} for {burstSeconds:0.##}s, impact impulse {impulse:0.#}; consume {skillA}={consumeA}, {skillB}={consumeB}.");
    }

    // Every peer. Only the caster's own camera reacts; observers get the payload through SkillExecutor.YuanqiObserved.
    public override void PlayObservers(SkillExecutor caster, YuanqiPresentationPayload payload, bool localIsCaster)
    {
        if (caster == null || !localIsCaster || casterFovKickDegrees <= 0f)
            return;
        // The kick starts from ~0 in both cast orders, so it always widens the view: when 法相金身 completes the pair,
        // its own CastObserversRpc arrived just before this RPC and reset the owner camera (Skill_Giant.ExecuteLocal →
        // PlayerCamera.ResetRuntimeEffects), dropping a ramped 神足通 boost; when 神足通 completes it, its boost is still
        // at the start of its ramp-in. Instant (no ramp-in) so the dash reads at once.
        caster.PlayCameraFovBoostLocal(casterFovKickDegrees, 0f, casterFovKickHoldSeconds, casterFovKickSettleSeconds, slotTag: yuanqiId);
    }

    private static Vector3 FlatForward(Transform transform)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude < FlatDirectionMinSqrMagnitude ? Vector3.forward : forward.normalized;
    }

    private float ResolveImpactImpulse(SkillExecutor caster)
    {
        BuddahHandControl hands = caster.GetComponent<BuddahHandControl>();
        if (hands == null)
        {
            Debug.LogWarning($"[Yuanqi][GiantCharge] '{caster.name}' has no BuddahHandControl; the body impact stays unarmed.");
            return 0f;
        }
        return impactImpulseMultiplier * hands.BasicPushImpulseStrength;
    }
}
