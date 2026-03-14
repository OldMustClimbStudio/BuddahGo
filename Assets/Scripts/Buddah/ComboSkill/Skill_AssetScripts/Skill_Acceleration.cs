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
    [Header("Feel (Optional)")]
    [SerializeField] private string observersFeelEventId = "acceleration_shared";
    [SerializeField] private string observersFeelStopEventId = string.Empty;

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        caster.ApplyAccelerationToOwner(extraForwardForce, extraMaxSpeed, durationSeconds);
        float actualVfxDuration = vfxDurationSeconds > 0f ? vfxDurationSeconds : durationSeconds;

        Debug.Log($"[Skill_Acceleration][Server] Apply +{extraForwardForce} force, +{extraMaxSpeed} maxSpeed for {durationSeconds}s, vfx={actualVfxDuration}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        Debug.Log($"[Skill_Acceleration][Observers] '{skillId}' triggered (slot {slotIndex})");
        float actualVfxDuration = vfxDurationSeconds > 0f ? vfxDurationSeconds : durationSeconds;
        caster.PlayFeelLocalTimed(observersFeelEventId, observersFeelStopEventId, actualVfxDuration, $"{skillId}_observers");

        // World-facing VFX now runs through Feel on each observer.
    }
}
