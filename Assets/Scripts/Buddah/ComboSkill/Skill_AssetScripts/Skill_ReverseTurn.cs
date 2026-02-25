using UnityEngine;

[CreateAssetMenu(fileName = "Skill_ReverseTurn", menuName = "Skills/Actions/Normal/Reverse Turn")]
public class Skill_ReverseTurn : SkillAction
{
    [Header("Global Invert Turn Debuff")]
    [Tooltip("Duration that other players' horizontal turn input (A/D) is inverted.")]
    [Min(0.05f)] public float invertDurationSeconds = 1.25f;

    [Header("VFX (Optional)")]
    [Min(0f)] public float vfxStopPlayingBeforeEndSeconds = 0f;
    [SerializeField] public string vfxId = "reverse_turn_trap_vfx";
    [SerializeField] public Vector3 vfxLocalOffset = Vector3.zero;
    [SerializeField] public Vector3 vfxLocalEuler = Vector3.zero;

    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        if (caster == null)
            return;

        SkillExecutor[] allExecutors = FindObjectsByType<SkillExecutor>(FindObjectsSortMode.None);
        int appliedCount = 0;
        for (int i = 0; i < allExecutors.Length; i++)
        {
            SkillExecutor target = allExecutors[i];
            if (target == null || target == caster)
                continue;

            if (!target.IsServerInitialized)
                continue;

            target.ApplyInvertTurnInputToOwner(invertDurationSeconds);
            appliedCount++;
        }

        var vfx = caster.GetComponent<SkillVfxReplicator>();
        if (vfx != null)
            vfx.PlayVfxAll(vfxId, invertDurationSeconds, vfxLocalOffset, vfxLocalEuler, vfxStopPlayingBeforeEndSeconds);

        Debug.Log($"[Skill_ReverseTurnTrap][Server] InvertTurnAllOthers duration={invertDurationSeconds}s, targets={appliedCount}");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        Debug.Log($"[Skill_ReverseTurnTrap][Observers] '{skillId}' triggered (slot {slotIndex})");
    }
}
