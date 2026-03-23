using UnityEngine;

[CreateAssetMenu(fileName = "Skill_Giant_Anti", menuName = "Skills/Actions/Anti/Giant Anti")]
public class Skill_Giant_Anti : SkillAction
{
    [Header("Shrink Debuff")]
    [Range(0.1f, 0.99f)] public float scaleMultiplier = 0.5f;
    [Min(0.1f)] public float durationSeconds = 4f;
    [Min(0f)] public float shrinkDurationSeconds = 0.25f;
    [Min(0f)] public float restoreDurationSeconds = 0.25f;

    [Header("Feel (Optional)")]
    [SerializeField] private string observersFeelEventId = string.Empty;
    [SerializeField] private string observersFeelStopEventId = string.Empty;

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        caster.ApplyScaleToOwner(scaleMultiplier, durationSeconds, shrinkDurationSeconds, restoreDurationSeconds);
        Debug.Log($"[Skill_Giant_Anti][Server] Apply x{scaleMultiplier:0.##} scale for {durationSeconds:0.##}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex, bool isAnti, bool localIsCaster)
    {
        if (!localIsCaster)
            ApplyScaleEffect(caster);

        caster.PlayFeelLocalTimed(observersFeelEventId, observersFeelStopEventId, durationSeconds, $"{skillId}_observers");
        Debug.Log($"[Skill_Giant_Anti][Observers] '{skillId}' triggered (slot {slotIndex})");
    }

    private void ApplyScaleEffect(SkillExecutor caster)
    {
        if (caster == null)
            return;

        var effect = caster.GetComponent<PlayerScaleEffect>();
        if (effect == null)
            effect = caster.gameObject.AddComponent<PlayerScaleEffect>();

        effect.ApplyOrRefresh(scaleMultiplier, durationSeconds, shrinkDurationSeconds, restoreDurationSeconds);
    }
}
