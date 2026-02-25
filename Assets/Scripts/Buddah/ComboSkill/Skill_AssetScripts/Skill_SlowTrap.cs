using UnityEngine;

[CreateAssetMenu(fileName = "Skill_SlowTrap", menuName = "Skills/Actions/Normal/Slow Trap")]
public class Skill_SlowTrap : SkillAction
{
    [Header("Trap Zone")]
    [Tooltip("Prefab with one or more Collider components set as IsTrigger (will be parented to caster at runtime).")]
    public GameObject triggerColliderPrefab;
    [Tooltip("Optional debug-only visual prefab (no gameplay logic). Spawned as a child of caster so you can see the trap follow the player.")]
    public GameObject debugReferencePrefab;
    [SerializeField] public Vector3 debugReferenceLocalOffset = Vector3.zero;
    [SerializeField] public Vector3 debugReferenceLocalEuler = Vector3.zero;
    [Tooltip("Multiplier applied on top of the debug reference prefab's authored local scale. (1,1,1) keeps prefab size.)")]
    [SerializeField] public Vector3 debugReferenceLocalScale = Vector3.one;
    [Min(0.1f)] public float zoneDurationSeconds = 5f;
    [Min(0f)] public float perTargetReapplyCooldown = 0.75f;
    public bool affectCaster = false;
    public LayerMask targetLayers = ~0;

    [Header("Slow Debuff")]
    [Tooltip("How much forwardForce to REMOVE (positive value).")]
    [Min(0f)] public float slowForwardForce = 6f;
    [Tooltip("How much maxSpeed to REMOVE (positive value).")]
    [Min(0f)] public float slowMaxSpeed = 2f;
    [Min(0.05f)] public float slowDurationSeconds = 1.25f;

    [Header("VFX (Optional)")]
    [Min(0f)] public float vfxStopPlayingBeforeEndSeconds = 0f;
    [SerializeField] public string vfxId = "slow_trap_vfx";
    [SerializeField] public Vector3 vfxLocalOffset = Vector3.zero;
    [SerializeField] public Vector3 vfxLocalEuler = Vector3.zero;

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        if (caster == null)
            return;

        var zone = caster.GetComponent<MovementSlowTrapZoneEffect>();
        if (zone == null)
            zone = caster.gameObject.AddComponent<MovementSlowTrapZoneEffect>();

        zone.ApplyOrRefresh(
            caster,
            zoneDurationSeconds,
            slowForwardForce,
            slowMaxSpeed,
            slowDurationSeconds,
            perTargetReapplyCooldown,
            affectCaster,
            targetLayers,
            triggerColliderPrefab,
            debugReferencePrefab,
            debugReferenceLocalOffset,
            debugReferenceLocalEuler,
            debugReferenceLocalScale);

        var vfx = caster.GetComponent<SkillVfxReplicator>();
        if (vfx != null)
            vfx.PlayVfxAll(vfxId, zoneDurationSeconds, vfxLocalOffset, vfxLocalEuler, vfxStopPlayingBeforeEndSeconds);

        Debug.Log($"[Skill_SlowTrap][Server] TriggerTrap zoneDuration={zoneDurationSeconds}s, slow=({slowForwardForce},{slowMaxSpeed}) for {slowDurationSeconds}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        Debug.Log($"[Skill_SlowTrap][Observers] '{skillId}' triggered (slot {slotIndex})");
        // VFX replication is sent from server in ExecuteServer().
    }
}
