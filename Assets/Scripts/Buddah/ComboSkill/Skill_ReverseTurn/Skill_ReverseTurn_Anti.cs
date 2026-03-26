using UnityEngine;

/// <summary>
/// Anti (Backfire) variant for Reverse Turn:
/// invert the caster's own turn input for a duration.
/// </summary>
[CreateAssetMenu(fileName = "Skill_ReverseTurnTrap_Anti", menuName = "Skills/Actions/Anti/Reverse Turn Anti")]
public class Skill_ReverseTurn_Anti : SkillAction
{
    [Header("Self Invert Turn Debuff")]
    [Tooltip("Duration that the caster's own horizontal turn input (A/D) is inverted.")]
    [Min(0.05f)] public float invertDurationSeconds = 1.25f;

    [Header("VFX (Optional)")]
    [Tooltip("VFX lifetime. 0 = follow invertDurationSeconds.")]
    [Min(0f)] public float vfxDurationSeconds = 0f;
    [Min(0f)] public float vfxStopPlayingBeforeEndSeconds = 0f;
    [SerializeField] public string vfxId = "reverse_turn_trap_anti_vfx";
    [SerializeField] public Vector3 vfxLocalOffset = Vector3.zero;
    [SerializeField] public Vector3 vfxLocalEuler = Vector3.zero;
    [Header("Feel (Optional)")]
    [SerializeField] private string observersFeelEventId = string.Empty;
    [SerializeField] private string observersFeelStopEventId = string.Empty;

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        if (caster == null)
            return;

        caster.ApplyInvertTurnInputToOwner(invertDurationSeconds);
        float actualVfxDuration = vfxDurationSeconds > 0f ? vfxDurationSeconds : invertDurationSeconds;

        Debug.Log($"[Skill_ReverseTurnTrap_Anti][Server] Invert self turn input for {invertDurationSeconds}s, vfx={actualVfxDuration}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        Debug.Log($"[Skill_ReverseTurnTrap_Anti][Observers] '{skillId}' triggered (slot {slotIndex})");
        float actualVfxDuration = vfxDurationSeconds > 0f ? vfxDurationSeconds : invertDurationSeconds;
        caster.PlayFeelLocalTimed(observersFeelEventId, observersFeelStopEventId, actualVfxDuration, $"{skillId}_observers");
    }
}
