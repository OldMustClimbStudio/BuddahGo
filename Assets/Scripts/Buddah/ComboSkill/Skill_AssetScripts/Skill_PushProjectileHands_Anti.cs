using UnityEngine;

[CreateAssetMenu(fileName = "Skill_PushProjectileHands_Anti", menuName = "Skills/Actions/Anti/Projectile Push Hands Anti")]
public class Skill_PushProjectileHands_Anti : SkillAction
{
    [Header("Immediate Reverse Projectile Burst")]
    [Min(1)] public int projectileCount = 3;
    [Min(0f)] public float projectileSpacing = 2f;
    [Min(0f)] public float additionalForwardSpawnOffset = 2f;
    [Min(0f)] public float additionalHeightSpawnOffset = 0f;
    [Min(0.1f)] public float projectileSpeed = 18f;
    [Min(0.05f)] public float projectileLifetimeSeconds = 1.2f;
    [Min(0f)] public float projectileImpulseStrength = 12f;
    [SerializeField] public Vector3 projectileColliderSize = new Vector3(1.25f, 1.1f, 1.8f);

    [Header("Feel (Optional)")]
    [SerializeField] private string observersFeelEventId = string.Empty;
    [SerializeField] private string observersFeelStopEventId = string.Empty;

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

        handControl.FireProjectileBurstImmediate(
            projectileCount,
            projectileSpacing,
            projectileSpeed,
            projectileLifetimeSeconds,
            projectileImpulseStrength,
            projectileColliderSize,
            reverseDirection: true,
            allowSelfHit: true,
            additionalForwardSpawnOffset: additionalForwardSpawnOffset,
            additionalHeightSpawnOffset: additionalHeightSpawnOffset);

        Debug.Log($"[Skill_PushProjectileHands_Anti][Server] Fired reverse burst count={projectileCount}, spacing={projectileSpacing}, extraSpawn=({additionalForwardSpawnOffset},{additionalHeightSpawnOffset}), speed={projectileSpeed}");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        if (caster == null)
            return;

        float feelDuration = Mathf.Max(0.05f, projectileLifetimeSeconds);
        caster.PlayFeelLocalTimed(observersFeelEventId, observersFeelStopEventId, feelDuration, $"{skillId}_observers");
    }
}
