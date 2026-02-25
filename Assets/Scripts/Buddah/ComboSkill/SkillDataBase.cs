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
        _byId = new Dictionary<string, SkillAction>();
        AddListToLookup(normalSkills, "normalSkills");
        AddListToLookup(antiSkills, "antiSkills");
        AddListToLookup(skills, "skills");
    }

    public bool TryGet(string skillId, out SkillAction skill)
    {
        if (_byId == null) OnEnable();
        return _byId.TryGetValue(skillId, out skill);
    }

    private void AddListToLookup(List<SkillAction> list, string listName)
    {
        if (list == null)
            return;

        foreach (var s in list)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.skillId))
                continue;

            if (_byId.ContainsKey(s.skillId))
            {
                Debug.LogWarning($"[SkillDatabase] Duplicate skillId '{s.skillId}' in database (list={listName}).");
                continue;
            }

            _byId.Add(s.skillId, s);
        }
    }
}
