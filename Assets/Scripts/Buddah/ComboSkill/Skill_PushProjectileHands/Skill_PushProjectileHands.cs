using UnityEngine;

[CreateAssetMenu(fileName = "Skill_PushProjectileHands", menuName = "Skills/Actions/Normal/Projectile Push Hands")]
public class Skill_PushProjectileHands : SkillAction
{
    [Header("Projectile Push Buff")]
    [Min(0.1f)] public float buffDurationSeconds = 8f;
    [Header("Charged Projectile")]
    [Min(0f)] public float buildUpSeconds = 0.5f;
    [Min(0f)] public float chargedPushCooldownSeconds = 0.6f;
    [Min(0f)] public float delayedPushActionSeconds = 0f;
    [Tooltip("Extra forward offset added on top of the normal projectile spawn point.")]
    [Min(0f)] public float projectileForwardOffset = 0f;
    [Tooltip("Extra height offset added on top of the normal projectile spawn point.")]
    [Min(0f)] public float projectileHeightOffset = 0f;
    [Min(0.1f)] public float projectileSpeed = 18f;
    [Min(0.05f)] public float projectileLifetimeSeconds = 1.2f;
    [Min(0f)] public float projectileImpulseStrength = 12f;
    [SerializeField] public Vector3 projectileColliderSize = new Vector3(1.25f, 1.1f, 1.8f);
    [SerializeField] private bool ignoreSolidWorld = false;
    [SerializeField] private GameObject chargedProjectileVfxPrefab;
    [SerializeField] private string chargedProjectileProgressProperty = "Progress";
    [SerializeField] private GameObject chargedProjectileLaunchEffectPrefab;

    [Header("Feel (Optional)")]
    [SerializeField] private string observersFeelEventId = string.Empty;
    [SerializeField] private string observersFeelStopEventId = string.Empty;
    [Header("Camera")]
    [SerializeField] private float localCasterBuffFovBoost = 8f;
    [SerializeField] private float localCasterBuffFovRampInSeconds = 0.15f;
    [SerializeField] private float localCasterBuffFovSettleSeconds = 0.2f;
    [SerializeField] private AnimationCurve localCasterBuffFovRampInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve localCasterBuffFovSettleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

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
            buildUpSeconds,
            projectileSpeed,
            projectileLifetimeSeconds,
            projectileImpulseStrength,
            projectileColliderSize,
            projectileForwardOffset,
            projectileHeightOffset,
            chargedProjectileVfxPrefab,
            chargedProjectileProgressProperty,
            chargedPushCooldownSeconds,
            true,
            ignoreSolidWorld);

        Debug.Log($"[Skill_PushProjectileHands][Server] Enabled charged projectile push buff for {buffDurationSeconds}s, buildup={buildUpSeconds}s, offset=({projectileForwardOffset},{projectileHeightOffset}), speed={projectileSpeed}, lifetime={projectileLifetimeSeconds}, impulse={projectileImpulseStrength}");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        if (caster == null)
            return;

        BuddahHandControl handControl = caster.GetComponent<BuddahHandControl>();
        if (handControl != null)
        {
            handControl.ConfigureProjectilePushModeLocal(
                buffDurationSeconds,
                chargedProjectileVfxPrefab,
                chargedProjectileProgressProperty,
                chargedProjectileLaunchEffectPrefab,
                projectileForwardOffset,
                projectileHeightOffset,
                chargedPushCooldownSeconds,
                true,
                delayedPushActionSeconds);
        }

        caster.PlayFeelLocalTimed(observersFeelEventId, observersFeelStopEventId, buffDurationSeconds, $"{skillId}_observers");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex, bool isAnti, bool localIsCaster)
    {
        ExecuteObservers(caster, slotIndex);

        if (!localIsCaster || caster == null)
            return;

        if (localCasterBuffFovBoost <= 0f)
            return;

        caster.PlayCameraFovBoostLocal(localCasterBuffFovBoost, localCasterBuffFovRampInSeconds, buffDurationSeconds, localCasterBuffFovSettleSeconds, localCasterBuffFovRampInCurve, localCasterBuffFovSettleCurve);
    }
}
