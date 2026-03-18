using UnityEngine;

[CreateAssetMenu(fileName = "Skill_PushProjectileHands", menuName = "Skills/Actions/Normal/Projectile Push Hands")]
public class Skill_PushProjectileHands : SkillAction
{
    [Header("Projectile Push Buff")]
    [Min(0.1f)] public float buffDurationSeconds = 8f;
    [Tooltip("Extra forward offset added on top of the normal push-hitbox spawn point.")]
    [Min(0f)] public float projectileForwardOffset = 0f;
    [Tooltip("Extra height offset added on top of the normal push-hitbox spawn point.")]
    [Min(0f)] public float projectileHeightOffset = 0f;
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
            Debug.LogWarning("[Skill_PushProjectileHands][Server] Missing BuddahHandControl.");
            return;
        }

        handControl.ActivateProjectilePushMode(
            buffDurationSeconds,
            projectileSpeed,
            projectileLifetimeSeconds,
            projectileImpulseStrength,
            projectileColliderSize,
            projectileForwardOffset,
            projectileHeightOffset);

        Debug.Log($"[Skill_PushProjectileHands][Server] Enabled projectile push for {buffDurationSeconds}s, offset=({projectileForwardOffset},{projectileHeightOffset}), speed={projectileSpeed}, lifetime={projectileLifetimeSeconds}, impulse={projectileImpulseStrength}");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        if (caster == null)
            return;

        caster.PlayFeelLocalTimed(observersFeelEventId, observersFeelStopEventId, buffDurationSeconds, $"{skillId}_observers");
    }
}
