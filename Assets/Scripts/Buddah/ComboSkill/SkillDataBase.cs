using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Skills/Skill Database")]
public class SkillDatabase : ScriptableObject
{
    public List<SkillAction> skills = new();

    private Dictionary<string, SkillAction> _byId;

    private void OnEnable()
    {
        _byId = new Dictionary<string, SkillAction>();
        foreach (var s in skills)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.skillId))
                continue;

            if (_byId.ContainsKey(s.skillId))
            {
                Debug.LogWarning($"[SkillDatabase] Duplicate skillId '{s.skillId}' in database.");
                continue;
            }

            _byId.Add(s.skillId, s);
        }
    }

    public bool TryGet(string skillId, out SkillAction skill)
    {
        if (_byId == null) OnEnable();
        return _byId.TryGetValue(skillId, out skill);
    }
}
