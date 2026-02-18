using UnityEngine;

[CreateAssetMenu(fileName = "Skill_Acceleration", menuName = "Skills/Actions/Acceleration")]
public class Skill_Acceleration : SkillAction
{
    [Header("Acceleration Buff")]
    [Min(0f)] public float extraForwardForce = 10f;
    [Min(0f)] public float extraMaxSpeed = 3f;
    [Min(0.1f)] public float durationSeconds = 10f;
    [Min(0f)] public float vfxStopPlayingBeforeEndSeconds = 0f;
    [SerializeField] public string vfxId = "accel_back_vfx"; // 你需要在SkillVfxReplicator里设置这个ID对应的VFX预制体
    [SerializeField] public Vector3 vfxLocalOffset = Vector3.back * 2f;
    [SerializeField] public Vector3 vfxLocalEuler = new Vector3(0f, 180f, 0f);

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        caster.ApplyAccelerationToOwner(extraForwardForce, extraMaxSpeed, durationSeconds);
        Debug.Log($"[Skill_Acceleration][Server] Apply +{extraForwardForce} force, +{extraMaxSpeed} maxSpeed for {durationSeconds}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        Debug.Log($"[Skill_Acceleration][Observers] '{skillId}' triggered (slot {slotIndex})");

        var vfx = caster.GetComponent<SkillVfxReplicator>();
        if (vfx != null)
            vfx.PlayVfxLocal(vfxId, durationSeconds, vfxLocalOffset, vfxLocalEuler, vfxStopPlayingBeforeEndSeconds);
    }
}

