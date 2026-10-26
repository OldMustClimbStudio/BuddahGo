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
    [Min(0f)] public float vfxStopPlayingBeforeEndSeconds = 0f;
    [SerializeField] public string vfxId = "accel_back_vfx"; // Configure matching VFX id in SkillVfxReplicator.
    [SerializeField] public Vector3 vfxLocalOffset = Vector3.back * 2f;
    [SerializeField] public Vector3 vfxLocalEuler = new Vector3(0f, 180f, 0f);
    [Tooltip("缘起 consumed the skill: seconds the stopped back VFX lingers (particles fade) before it is destroyed.")]
    [Min(0f)] public float consumedVfxLingerSeconds = 1f;
    [Header("Feel (Optional)")]
    [SerializeField] private string observersFeelEventId = "acceleration_shared";
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
                vfx.PlayVfxAll(vfxId, actualVfxDuration, vfxLocalOffset, vfxLocalEuler, vfxStopPlayingBeforeEndSeconds);
        }

        GameLog.Verbose($"[Skill_Acceleration][Server] Apply +{extraForwardForce} force, +{extraMaxSpeed} maxSpeed for {durationSeconds}s, vfx={actualVfxDuration}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        GameLog.Verbose($"[Skill_Acceleration][Observers] '{skillId}' triggered (slot {slotIndex})");
        float actualVfxDuration = ResolveVfxDuration(vfxDurationSeconds, durationSeconds);
        PlayObserverFeel(caster, observersFeelEventId, observersFeelStopEventId, actualVfxDuration);
        caster.ShowAccelerationTrailLocal(durationSeconds);

        // Authored Feel bindings take precedence over the timed VFX fallback.
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex, bool localIsCaster)
    {
        ExecuteObservers(caster, slotIndex);

        if (!localIsCaster || caster == null)
            return;

        if (localCasterBuffFovBoost <= 0f)
            return;

        // Tagged with the skill id so a 缘起 consume settles the boost only while this skill still owns the FOV slot.
        caster.PlayCameraFovBoostLocal(localCasterBuffFovBoost, localCasterBuffFovRampInSeconds, durationSeconds, localCasterBuffFovSettleSeconds, localCasterBuffFovRampInCurve, localCasterBuffFovSettleCurve, skillId);
    }

    public override ISkillRuntimeHandle CreateRuntimeHandle(SkillExecutor caster)
    {
        return caster != null ? new AccelerationRuntimeHandle(caster, skillId, durationSeconds) : null;
    }

    // 缘起 consumed the buff: the trail fades out now, the back VFX stops, the owner's FOV boost settles early.
    public override void ExecuteConsumedObservers(SkillExecutor caster, int slotIndex, bool localIsCaster)
    {
        if (caster == null)
            return;

        caster.EndAccelerationTrailEarlyLocal();

        var vfx = caster.GetComponent<SkillVfxReplicator>();
        if (vfx != null)
            vfx.StopVfxLocal(vfxId, consumedVfxLingerSeconds);

        if (localIsCaster)
            caster.SettleCameraFovBoostEarlyLocal(skillId, localCasterBuffFovSettleSeconds, localCasterBuffFovSettleCurve);

        GameLog.Verbose($"[Skill_Acceleration][Observers] '{skillId}' consumed (slot {slotIndex}), localIsCaster={localIsCaster}");
    }
}
