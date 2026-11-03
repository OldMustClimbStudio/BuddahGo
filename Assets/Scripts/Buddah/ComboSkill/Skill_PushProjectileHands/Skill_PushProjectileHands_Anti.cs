using UnityEngine;

[CreateAssetMenu(fileName = "Skill_PushProjectileHands_Anti", menuName = "Skills/Actions/Anti/Projectile Push Hands Anti")]
public class Skill_PushProjectileHands_Anti : SkillAction
{
    [Header("Immediate Reverse Projectile Burst")]
    [Min(0f)] public float buildUpSeconds = 0.3f;
    [Min(1)] public int projectileCount = 3;
    [Min(0f)] public float projectileSpacing = 2f;
    [Min(0f)] public float additionalForwardSpawnOffset = 2f;
    [Min(0f)] public float additionalHeightSpawnOffset = 0f;
    [Tooltip("Peak flight speed (Skill/Push charged_anti `speed`); the shape below scales it over the flight.")]
    [Min(0.1f)] public float projectileSpeed = 18f;
    [Min(0.05f)] public float projectileLifetimeSeconds = 1.2f;
    [Tooltip("Flight speed as a fraction of projectileSpeed over normalised flight time, same shape as the normal palm (Docs/vfx/push-projectile-hands.md, 飞行速度).")]
    public AnimationCurve projectileSpeedCurve = ChargedProjectileFlight.DefaultSpeedCurve();
    [Min(0f)] public float projectileImpulseStrength = 12f;
    [SerializeField] public Vector3 projectileColliderSize = new Vector3(1.25f, 1.1f, 1.8f);

    [Header("Hit Spin")]
    [Tooltip("One-time signed torque impulse applied on hit. Positive spins right, negative spins left.")]
    public float hitTurnTorqueImpulse = 12f;

    [Header("VFX (vfx_id row of Skill.xlsx / VfxRegistry, prefab in SkillVfxDatabase)")]
    [Tooltip("Red fire animation, one per volley (its layers are burst-shared).")]
    [SerializeField] private string vfxId = "palm_release_anti";
    [Tooltip("Red ink palm on every bullet of the volley.")]
    [SerializeField] private string handVfxId = "palm_bullet_anti";

    [Header("Feel (Optional)")]
    [SerializeField] private string observersFeelEventId = string.Empty;
    [SerializeField] private string observersFeelStopEventId = string.Empty;

    public string VfxId => vfxId;
    public string HandVfxId => handVfxId;

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        if (caster == null)
            return;

        BuddahHandControl handControl = caster.GetComponent<BuddahHandControl>();
        if (handControl == null)
        {
            Debug.LogWarning("[Skill_PushProjectileHands_Anti][Server] Missing BuddahHandControl.");
            return;
        }

        handControl.FireChargedProjectileBurstReplicated(this);

        GameLog.Verbose($"[Skill_PushProjectileHands_Anti][Server] Fired reverse burst count={projectileCount}, spacing={projectileSpacing}, extraSpawn=({additionalForwardSpawnOffset},{additionalHeightSpawnOffset}), speed={projectileSpeed}, hitTurnTorqueImpulse={hitTurnTorqueImpulse}");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        if (caster == null)
            return;

        float feelDuration = Mathf.Max(0.05f, buildUpSeconds + projectileLifetimeSeconds);
        PlayObserverFeel(caster, observersFeelEventId, observersFeelStopEventId, feelDuration);
    }

    // Every bullet of the volley runs Chg over buildUpSeconds and Prj over projectileLifetimeSeconds, as the normal palm.
    public override void CollectVfxSlots(System.Collections.Generic.List<VfxSlotTiming> into)
    {
        float shotSeconds = buildUpSeconds + projectileLifetimeSeconds;
        const string shotSource = "Skill/Push charged_anti windupSeconds + lifetimeSeconds → buildUpSeconds + projectileLifetimeSeconds";
        AddVfxSlot(into, vfxId, 0f, shotSeconds, shotSource);
        AddVfxSlot(into, handVfxId, 0f, shotSeconds, shotSource);
    }
}
