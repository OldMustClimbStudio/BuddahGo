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

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        caster.ApplyScaleToOwner(scaleMultiplier, durationSeconds, growDurationSeconds, shrinkDurationSeconds);
        Debug.Log($"[Skill_Giant][Server] Apply x{scaleMultiplier:0.##} scale for {durationSeconds:0.##}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex, bool isAnti, bool localIsCaster)
    {
        if (!localIsCaster)
            ApplyScaleEffect(caster);

        caster.PlayFeelLocalTimed(observersFeelEventId, observersFeelStopEventId, durationSeconds, $"{skillId}_observers");
        Debug.Log($"[Skill_Giant][Observers] '{skillId}' triggered (slot {slotIndex})");
    }

    private void ApplyScaleEffect(SkillExecutor caster)
    {
        if (caster == null)
            return;

        var effect = caster.GetComponent<PlayerScaleEffect>();
        if (effect == null)
            effect = caster.gameObject.AddComponent<PlayerScaleEffect>();

        effect.ApplyOrRefresh(scaleMultiplier, durationSeconds, growDurationSeconds, shrinkDurationSeconds);
    }

    private void EnsureAntiSkillId()
    {
        if (string.IsNullOrWhiteSpace(antiSkillId))
            antiSkillId = DefaultAntiSkillId;
    }
}
