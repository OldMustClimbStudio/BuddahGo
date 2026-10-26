using System;
using UnityEngine;

/// <summary>
/// #4 金足破阵 (acceleration + giant, Instant, consumes acceleration only). 神足通 stops accelerating and the giant
/// dashes instead: the speed cap jumps to cap + burstExtraMaxSpeed and eases back over burstSeconds (motor Burst
/// command). During the burst the giant's body hits each other racer once with impactImpulseMultiplier x the basic
/// push impulse, sideways from the caster (Q9). 法相金身 keeps running to its own end (yuanqi-architecture §5.1).
/// </summary>
[CreateAssetMenu(fileName = "Yuanqi_GiantCharge", menuName = "Skills/Yuanqi/Giant Charge")]
public class Yuanqi_GiantCharge : YuanqiAction
{
    [Header("Giant Charge (phase A placeholders; phase B: YuanqiEffectGiantCharge)")]
    [Tooltip("Extra max speed on top of the base cap at the burst start; it decays to 0 with ease-out (motor rule).")]
    [Min(0f)] public float burstExtraMaxSpeed = 60f;
    [Tooltip("Seconds for the extra cap to decay back to the base cap. The body impact is armed for the same time.")]
    [Min(0f)] public float burstSeconds = 0.8f;
    [Tooltip("M: impact impulse = M x Push.basic.impulseStrength (BuddahHandControl.BasicPushImpulseStrength).")]
    [Min(0f)] public float impactImpulseMultiplier = 3f;

    public override void OnTriggerServer(YuanqiContext ctx)
    {
        SkillExecutor caster = ctx != null ? ctx.Caster : null;
        if (caster == null)
            return;

        // Consume 神足通 first: the burst then launches from the base cap, not base + acceleration. Exception (open
        // question): an enemy slow that overwrote the shared acceleration slot after the cast is not ended by the
        // consume, so the burst then starts from the slowed cap.
        ctx.ConsumeA("yuanqi:" + yuanqiId);
        caster.ApplyBurstToOwner(burstExtraMaxSpeed, burstSeconds);

        float impulse = ResolveImpactImpulse(caster);
        BuddahBodyImpact impact = caster.GetComponent<BuddahBodyImpact>();
        if (impact == null)
            impact = caster.gameObject.AddComponent<BuddahBodyImpact>();
        impact.Arm(impulse, burstSeconds);

        ctx.Presentation = new YuanqiPresentationPayload
        {
            Center = caster.transform.position,
            Radius = 0f,
            Seconds = burstSeconds,
            TargetObjectIds = Array.Empty<int>(),
        };

        if (NetDebug.EnableVerboseLog) GameLog.Verbose($"[Yuanqi][GiantCharge][Server] '{caster.name}' burst +{burstExtraMaxSpeed:0.#} for {burstSeconds:0.##}s, impact impulse {impulse:0.#}; consumed {skillA}.");
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
