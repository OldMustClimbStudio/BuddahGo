using UnityEngine;

[CreateAssetMenu(fileName = "Skill_Giant", menuName = "Skills/Actions/Normal/Giant")]
public class Skill_Giant : SkillAction
{
    private const string DefaultAntiSkillId = "giant_anti";

    [Header("Scale Buff")]
    [Min(1f)] public float scaleMultiplier = 5f;
    [Min(0.1f)] public float durationSeconds = 6f;
    [Min(0f)] public float growDurationSeconds = 0.35f;
    [Min(0f)] public float shrinkDurationSeconds = 0.35f;
    [Min(0.1f)] public float massMultiplier = 1f;
    [Min(0.1f)] public float forwardForceMultiplier = 1f;

    [Header("Feel (Optional)")]
    [SerializeField] private string observersFeelEventId = string.Empty;
    [SerializeField] private string observersFeelStopEventId = string.Empty;

    private void OnEnable()
    {
        EnsureAntiSkillId();
    }

    private void OnValidate()
    {
        EnsureAntiSkillId();
    }

    private void EnsureAntiSkillId()
    {
        if (string.IsNullOrWhiteSpace(antiSkillId))
            antiSkillId = DefaultAntiSkillId;
    }

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        caster.ApplyScaleToOwner(
            scaleMultiplier,
            durationSeconds,
            growDurationSeconds,
            shrinkDurationSeconds,
            massMultiplier,
            forwardForceMultiplier);
        GameLog.Verbose($"[Skill_Giant][Server] Apply x{scaleMultiplier:0.##} scale for {durationSeconds:0.##}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex, bool localIsCaster)
    {
        ScaleSkillPresentation.ApplyScaleEffect(caster, scaleMultiplier, durationSeconds, growDurationSeconds, shrinkDurationSeconds);

        PlayObserverFeel(caster, observersFeelEventId, observersFeelStopEventId, durationSeconds);
        GameLog.Verbose($"[Skill_Giant][Observers] '{skillId}' triggered (slot {slotIndex})");
    }

    public override void ExecuteLocal(SkillExecutor caster, int slotIndex)
    {
        if (caster == null || !caster.IsOwner)
            return;

        ScaleSkillPresentation.ResetLocalCamera(caster, GetType().Name);
    }

    public override ISkillRuntimeHandle CreateRuntimeHandle(SkillExecutor caster)
    {
        return caster != null ? new GiantRuntimeHandle(caster, skillId, durationSeconds) : null;
    }

    // 缘起 consumed the giant form: every peer runs the normal shrink now (the motor scale already ended).
    public override void ExecuteConsumedObservers(SkillExecutor caster, int slotIndex, bool localIsCaster)
    {
        if (caster == null)
            return;

        var effect = caster.GetComponent<PlayerScaleEffect>();
        if (effect != null)
            effect.EndEarly();

        GameLog.Verbose($"[Skill_Giant][Observers] '{skillId}' consumed (slot {slotIndex}), shrinking early.");
    }
}
