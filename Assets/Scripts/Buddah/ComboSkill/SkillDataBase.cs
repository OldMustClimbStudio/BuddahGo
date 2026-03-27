using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Skills/Databases/Skill Database")]
public class SkillDatabase : ScriptableObject
{
    [Header("Normal Skills")]
    public List<SkillAction> normalSkills = new();

    [Header("Anti Skills")]
    public List<SkillAction> antiSkills = new();

    [Header("Legacy / Unsorted (Optional)")]
    [Tooltip("Backward-compatible list. Runtime will merge this list with Normal Skills and Anti Skills.")]
    public List<SkillAction> skills = new();

    private Dictionary<string, SkillAction> _byId;

    private void OnEnable()
    {
        RebuildLookup();
    }

    public bool TryGet(string skillId, out SkillAction skill)
    {
        if (_byId == null)
            RebuildLookup();

        return _byId.TryGetValue(skillId, out skill);
    }

    public List<SkillAction> GetSelectableNormalSkills()
    {
        if (_byId == null)
            RebuildLookup();

        List<SkillAction> results = new List<SkillAction>();
        HashSet<string> antiSkillIds = BuildAntiSkillIdSet();
        HashSet<string> seen = new HashSet<string>();

        AddSelectableSkillsFromList(normalSkills, antiSkillIds, seen, results, "normalSkills");
        AddSelectableSkillsFromList(skills, antiSkillIds, seen, results, "skills");
        return results;
    }

    private void RebuildLookup()
    {
        _byId = new Dictionary<string, SkillAction>();
        AddListToLookup(normalSkills, "normalSkills");
        AddListToLookup(antiSkills, "antiSkills");
        AddListToLookup(skills, "skills");
    }

    private void AddListToLookup(List<SkillAction> list, string listName)
    {
        if (list == null)
            return;

        foreach (SkillAction skill in list)
        {
            if (!TryGetValidSkillId(skill, out string skillId))
                continue;

            if (_byId.ContainsKey(skillId))
            {
                Debug.LogWarning($"[SkillDatabase] Duplicate skillId '{skillId}' in database (list={listName}).");
                continue;
            }

            _byId.Add(skillId, skill);
        }
    }

    private void AddSelectableSkillsFromList(
        List<SkillAction> list,
        HashSet<string> antiSkillIds,
        HashSet<string> seen,
        List<SkillAction> results,
        string listName)
    {
        if (list == null)
            return;

        foreach (SkillAction skill in list)
        {
            if (!TryGetValidSkillId(skill, out string skillId))
                continue;

            if (antiSkillIds.Contains(skillId))
                continue;

            if (!seen.Add(skillId))
            {
                Debug.LogWarning($"[SkillDatabase] Duplicate selectable skillId '{skillId}' skipped (list={listName}).");
                continue;
            }

            results.Add(skill);
        }
    }

    private HashSet<string> BuildAntiSkillIdSet()
    {
        HashSet<string> antiSkillIds = new HashSet<string>();
        if (antiSkills == null)
            return antiSkillIds;

        foreach (SkillAction skill in antiSkills)
        {
            if (TryGetValidSkillId(skill, out string skillId))
                antiSkillIds.Add(skillId);
        }

        return antiSkillIds;
    }

    private static bool TryGetValidSkillId(SkillAction skill, out string skillId)
    {
        skillId = skill != null ? (skill.skillId ?? string.Empty).Trim() : string.Empty;
        return !string.IsNullOrWhiteSpace(skillId);
    }
}
