using UnityEngine;

/// <summary>
/// Anti (Backfire) variant of Acceleration: applies a temporary slow to the caster.
/// This is a standalone SkillAction so you can give it its own VFX, duration, and tuning.
///
/// Requires SkillExecutor.ApplyAccelerationToOwner(...) and MovementAccelerationEffect to support negative values.
/// </summary>
[CreateAssetMenu(fileName = "Skill_Acceleration_Anti", menuName = "Skills/Actions/Anti/Acceleration Anti")]
public class Skill_Acceleration_Anti : SkillAction
{
    [Header("Slow (Anti) Debuff")]
    [Tooltip("How much forwardForce to REMOVE while slowed (positive number).")]
    [Min(0f)] public float slowForwardForce = 10f;

    [Tooltip("How much maxSpeed to REMOVE while slowed (positive number).")]
    [Min(0f)] public float slowMaxSpeed = 3f;

    [Min(0.1f)] public float durationSeconds = 3f;

    [Header("VFX")]
    [Min(0f)] public float vfxStopPlayingBeforeEndSeconds = 0f;
    [SerializeField] public string vfxId = "accel_anti_vfx"; // Configure matching VFX id in SkillVfxReplicator.
    [SerializeField] public Vector3 vfxLocalOffset = Vector3.back * 2f;
    [SerializeField] public Vector3 vfxLocalEuler = new Vector3(0f, 180f, 0f);

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        // Convert to negative deltas.
        float extraForwardForce = -Mathf.Abs(slowForwardForce);
        float extraMaxSpeed = -Mathf.Abs(slowMaxSpeed);

        caster.ApplyAccelerationToOwner(extraForwardForce, extraMaxSpeed, durationSeconds);

        var vfx = caster.GetComponent<SkillVfxReplicator>();
        if (vfx != null)
            vfx.PlayVfxAll(vfxId, durationSeconds, vfxLocalOffset, vfxLocalEuler, vfxStopPlayingBeforeEndSeconds);

        Debug.Log($"[Skill_Acceleration_Anti][Server] Apply {extraForwardForce} force, {extraMaxSpeed} maxSpeed for {durationSeconds}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        Debug.Log($"[Skill_Acceleration_Anti][Observers] '{skillId}' triggered (slot {slotIndex})");
        // VFX replication is sent from server in ExecuteServer().
    }
}
