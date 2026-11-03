using UnityEngine;

[CreateAssetMenu(fileName = "Skill_Acceleration", menuName = "Skills/Actions/Normal/Acceleration")]
public class Skill_Acceleration : SkillAction
{
    [Header("Acceleration Buff")]
    [Min(0f)] public float extraForwardForce = 10f;
    [Min(0f)] public float extraMaxSpeed = 3f;
    [Min(0.1f)] public float durationSeconds = 10f;
    [Tooltip("VFX lifetime. 0 = follow durationSeconds.")]
    [Min(0f)] public float vfxDurationSeconds = 0f;
    [SerializeField] public string vfxId = "accel_release"; // Release slot in SkillVfxDatabase (Docs/vfx/slots.md).
    [Header("Feel (Optional)")]
    [SerializeField] private string observersFeelEventId = string.Empty;
    [SerializeField] private string observersFeelStopEventId = string.Empty;
    [Header("Camera")]
    [SerializeField] private float localCasterBuffFovBoost = 6f;
    [SerializeField] private float localCasterBuffFovRampInSeconds = 0.15f;
    [SerializeField] private float localCasterBuffFovSettleSeconds = 0.25f;
    [SerializeField] private AnimationCurve localCasterBuffFovRampInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve localCasterBuffFovSettleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        caster.ApplyAccelerationToOwner(extraForwardForce, extraMaxSpeed, durationSeconds);
        float actualVfxDuration = ResolveVfxDuration(vfxDurationSeconds, durationSeconds);

        // Existing loadouts without a Feel binding still use the authored, timed VFX.
        if (string.IsNullOrWhiteSpace(observersFeelEventId))
        {
            var vfx = caster.GetComponent<SkillVfxReplicator>();
            if (vfx != null)
                vfx.PlayVfxAll(vfxId, actualVfxDuration);
        }

        GameLog.Verbose($"[Skill_Acceleration][Server] Apply +{extraForwardForce} force, +{extraMaxSpeed} maxSpeed for {durationSeconds}s, vfx={actualVfxDuration}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        GameLog.Verbose($"[Skill_Acceleration][Observers] '{skillId}' triggered (slot {slotIndex})");
        float actualVfxDuration = ResolveVfxDuration(vfxDurationSeconds, durationSeconds);
        PlayObserverFeel(caster, observersFeelEventId, observersFeelStopEventId, actualVfxDuration);

        // Authored Feel bindings take precedence over the timed VFX fallback.
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex, bool isAnti, bool localIsCaster)
    {
        ExecuteObservers(caster, slotIndex);

        if (!localIsCaster || caster == null)
            return;

        if (localCasterBuffFovBoost <= 0f)
            return;

        caster.PlayCameraFovBoostLocal(localCasterBuffFovBoost, localCasterBuffFovRampInSeconds, durationSeconds, localCasterBuffFovSettleSeconds, localCasterBuffFovRampInCurve, localCasterBuffFovSettleCurve);
    }

    // Played only while observersFeelEventId is empty (see ExecuteServer).
    public override void CollectVfxSlots(System.Collections.Generic.List<VfxSlotTiming> into)
    {
        if (string.IsNullOrWhiteSpace(observersFeelEventId))
            AddVfxSlot(into, vfxId, vfxDurationSeconds, durationSeconds, "Skill.effectDurationSeconds → durationSeconds");
    }
}
