using System;
using System.Collections.Generic;
using UnityEngine;

public static class ProjectConfigConstants
{
    public const string DefaultRuleSetId = "default";
    public const string DefaultModeTag = "default";
    public const string GlobalRuleComboInputWindowSeconds = "combo_input_window_seconds";
    public const string GlobalRulePregameCountdownSeconds = "pregame_countdown_seconds";
    public const string GlobalRuleRaceLapCount = "race_lap_count";
    public const string GlobalRuleResultSceneCountdownSeconds = "result_scene_countdown_seconds";
    public const string GlobalRuleFinishWindowSeconds = "finish_window_seconds";
    public const string GlobalRuleCatchupRecoveryA = "catchup_recovery_a";
    public const string GlobalRuleObsessionRecoveryBaseline = "obsession_recovery_baseline";
}

public enum SkillSelectionPoolType
{
    Selectable = 0,
    Fallback = 1,
    Default = 2,
    Hidden = 3,
    TestOnly = 4
}

[Serializable]
public class SkillDefinitionRecord
{
    public string skillId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    public string behaviorType = string.Empty;
    public string iconKey = string.Empty;
    public bool isSelectable = true;
    public int sortOrder = 0;
    public List<string> tags = new List<string>();
    public string unlockState = "unlocked";
}

[Serializable]
public class SkillBalanceRecord
{
    public string skillId = string.Empty;
    [Min(0f)] public float cooldownSeconds = 0f;
    [Min(0f)] public float castLockSeconds = 0f;
    [Min(0f)] public float obsessionGain = 0f;
    public string antiSkillId = string.Empty;
}

[Serializable]
public class SkillEffectParamRecord
{
    public string skillId = string.Empty;
    public string effectType = string.Empty;
    public string paramKey = string.Empty;
    public string paramValue = string.Empty;
}

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
