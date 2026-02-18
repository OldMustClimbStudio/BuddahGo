using UnityEngine;

[CreateAssetMenu(menuName = "Skills/Actions/Debug Log Skill")]
public class Skill_DebugLog : SkillAction
{
    public override void ExecuteServer(SkillExecutor caster, int slotIndex)
    {
        Debug.Log($"[Skill_DebugLog][Server] skillId='{skillId}' display='{displayName}' slot={slotIndex} caster={caster.name}");
    }

    public override void ExecuteObservers(SkillExecutor caster, int slotIndex)
    {
        Debug.Log($"[Skill_DebugLog][Observers] skillId='{skillId}' slot={slotIndex}");
    }
}
