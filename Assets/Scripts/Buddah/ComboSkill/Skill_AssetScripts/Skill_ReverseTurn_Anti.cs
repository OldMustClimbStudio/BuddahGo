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
    [Min(0f)] public float vfxStopPlayingBeforeEndSeconds = 0f;
    [SerializeField] public string vfxId = "reverse_turn_trap_anti_vfx";
    [SerializeField] public Vector3 vfxLocalOffset = Vector3.zero;
    [SerializeField] public Vector3 vfxLocalEuler = Vector3.zero;

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        if (caster == null)
            return;

        caster.ApplyInvertTurnInputToOwner(invertDurationSeconds);

        var vfx = caster.GetComponent<SkillVfxReplicator>();
        if (vfx != null)
            vfx.PlayVfxAll(vfxId, invertDurationSeconds, vfxLocalOffset, vfxLocalEuler, vfxStopPlayingBeforeEndSeconds);

        Debug.Log($"[Skill_ReverseTurnTrap_Anti][Server] Invert self turn input for {invertDurationSeconds}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        Debug.Log($"[Skill_ReverseTurnTrap_Anti][Observers] '{skillId}' triggered (slot {slotIndex})");
    }
}
