using UnityEngine;

/// <summary>
/// Anti (Backfire) variant for Slow Trap:
/// force the caster to be rooted briefly, then grant acceleration.
/// </summary>
[CreateAssetMenu(fileName = "Skill_SlowTrap_Anti", menuName = "Skills/Actions/Anti/Slow Trap Anti")]
public class Skill_SlowTrap_Anti : SkillAction
{
    [Header("Root (Self)")]
    [Min(0.05f)] public float rootDurationSeconds = 1f;

    [Header("Acceleration After Root")]
    [Min(0f)] public float extraForwardForce = 10f;
    [Min(0f)] public float extraMaxSpeed = 3f;
    [Min(0.1f)] public float accelerationDurationSeconds = 4f;

    [Header("VFX")]
    [Min(0f)] public float vfxStopPlayingBeforeEndSeconds = 0f;
    [SerializeField] public string vfxId = "slow_trap_anti_vfx";
    [SerializeField] public Vector3 vfxLocalOffset = Vector3.zero;
    [SerializeField] public Vector3 vfxLocalEuler = Vector3.zero;

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        if (caster == null)
            return;

        caster.ApplyRootThenAccelerationToOwner(
            rootDurationSeconds,
            extraForwardForce,
            extraMaxSpeed,
            accelerationDurationSeconds);

        var vfx = caster.GetComponent<SkillVfxReplicator>();
        if (vfx != null)
            vfx.PlayVfxAll(vfxId, rootDurationSeconds + accelerationDurationSeconds, vfxLocalOffset, vfxLocalEuler, vfxStopPlayingBeforeEndSeconds);

        Debug.Log($"[Skill_SlowTrap_Anti][Server] Root self {rootDurationSeconds}s, then accel +{extraForwardForce}/+{extraMaxSpeed} for {accelerationDurationSeconds}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        Debug.Log($"[Skill_SlowTrap_Anti][Observers] '{skillId}' triggered (slot {slotIndex})");
        // VFX replication is sent from server in ExecuteServer().
    }
}
