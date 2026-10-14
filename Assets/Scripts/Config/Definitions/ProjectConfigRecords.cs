using System;
using System.Collections.Generic;
using UnityEngine;

public static class ProjectConfigConstants
{
    public const string DefaultRuleSetId = "default";
    public const string DefaultModeTag = "default";
}

public enum SkillSelectionPoolType
{
    Selectable = 0,
    Fallback = 1,
    Default = 2,
    Hidden = 3,
    TestOnly = 4
}

// Skill identity, timing and obsession live in the generated SkillRecord (Skill.xlsx / Skill);
// per-family effect numbers in SkillEffect<Family>Record and PushRecord (ProjectConfigRecords.Generated.cs).

[Serializable]
public class SkillSelectionRuleRecord
{
    public string ruleSetId = ProjectConfigConstants.DefaultRuleSetId;
    public string skillId = string.Empty;
    public bool isEnabled = true;
    public bool allowDuplicate = false;
    [Min(0f)] public float weight = 1f;
    public string modeTag = string.Empty;
    public string mapTag = string.Empty;
    public SkillSelectionPoolType poolType = SkillSelectionPoolType.Selectable;
}

[Serializable]
public class DefaultLoadoutRecord
{
    public string loadoutId = string.Empty;
    public string slot0SkillId = string.Empty;
    public string slot1SkillId = string.Empty;
    public string slot2SkillId = string.Empty;
    public string ruleSetId = ProjectConfigConstants.DefaultRuleSetId;
    public string modeTag = ProjectConfigConstants.DefaultModeTag;

    public string[] ToSlots()
    {
        return new[]
        {
            slot0SkillId ?? string.Empty,
            slot1SkillId ?? string.Empty,
            slot2SkillId ?? string.Empty
        };
    }
}

[Serializable]
public class GlobalRuleRecord
{
    public string ruleKey = string.Empty;
    public string ruleValue = string.Empty;
    public string ruleSetId = ProjectConfigConstants.DefaultRuleSetId;
    [TextArea] public string description = string.Empty;
}
