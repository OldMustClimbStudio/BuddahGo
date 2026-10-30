using UnityEngine;

[CreateAssetMenu(fileName = "Skill_ReverseTurn", menuName = "Skills/Actions/Normal/Reverse Turn")]
public class Skill_ReverseTurn : SkillAction
{
    [Header("Global Invert Turn Debuff")]
    [Tooltip("Duration that other players' horizontal turn input (A/D) is inverted.")]
    [Min(0.05f)] public float invertDurationSeconds = 1.25f;

    [Header("VFX (Optional)")]
    [Tooltip("VFX lifetime. 0 = follow invertDurationSeconds.")]
    [Min(0f)] public float vfxDurationSeconds = 0f;
    [Min(0f)] public float vfxStopPlayingBeforeEndSeconds = 0f;
    [Tooltip("Release slot played on the caster. Empty = none.")]
    [SerializeField] public string vfxId = "reverse_release";
    [SerializeField] public Vector3 vfxLocalOffset = Vector3.zero;
    [SerializeField] public Vector3 vfxLocalEuler = Vector3.zero;
    [Tooltip("Hit slot played on every player whose turning is reversed (Docs/vfx/README.md section 4). Empty = none.")]
    [SerializeField] public string hitVfxId = "reverse_hit";
    [Header("Feel (Optional)")]
    [SerializeField] private string observersFeelEventId = string.Empty;
    [SerializeField] private string observersFeelStopEventId = string.Empty;

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

        float actualVfxDuration = ResolveVfxDuration(vfxDurationSeconds, invertDurationSeconds);
        if (!string.IsNullOrWhiteSpace(hitVfxId))
        {
            for (int i = 0; i < allExecutors.Length; i++)
            {
                SkillExecutor target = allExecutors[i];
                if (target == null || target == caster || !target.IsServerInitialized)
                    continue;
                var targetVfx = target.GetComponent<SkillVfxReplicator>();
                if (targetVfx != null)
                    targetVfx.PlayVfxAll(hitVfxId, actualVfxDuration, Vector3.zero, Vector3.zero, 0f);
            }
        }

        GameLog.Verbose($"[Skill_ReverseTurnTrap][Server] InvertTurnAllOthers duration={invertDurationSeconds}s, targets={appliedCount}, vfx={actualVfxDuration}s");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        GameLog.Verbose($"[Skill_ReverseTurnTrap][Observers] '{skillId}' triggered (slot {slotIndex})");
        float actualVfxDuration = ResolveVfxDuration(vfxDurationSeconds, invertDurationSeconds);
        PlaySlotOnCaster(caster, vfxId, actualVfxDuration);
        PlayObserverFeel(caster, observersFeelEventId, observersFeelStopEventId, actualVfxDuration);
    }

    // The release on the caster and the hit on every reversed player share the reversal length.
    public override void CollectVfxSlots(System.Collections.Generic.List<VfxSlotTiming> into)
    {
        AddVfxSlot(into, vfxId, vfxDurationSeconds, invertDurationSeconds, "Skill.effectDurationSeconds → invertDurationSeconds");
        AddVfxSlot(into, hitVfxId, vfxDurationSeconds, invertDurationSeconds, "Skill.effectDurationSeconds → invertDurationSeconds (on each reversed player)");
    }
}
