using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ProjectConfigDatabase", menuName = "Config/Project Config Database")]
public class ProjectConfigDatabase : ScriptableObject
{
    [Header("Defaults")]
    [SerializeField] private string _defaultRuleSetId = ProjectConfigConstants.DefaultRuleSetId;
    [SerializeField] private string _defaultModeTag = ProjectConfigConstants.DefaultModeTag;

    [Header("Skills")]
    public List<SkillDefinitionRecord> skillDefinitions = new List<SkillDefinitionRecord>();
    public List<SkillBalanceRecord> skillBalances = new List<SkillBalanceRecord>();
    public List<SkillEffectParamRecord> skillEffectParams = new List<SkillEffectParamRecord>();
    public List<SkillSelectionRuleRecord> skillSelectionRules = new List<SkillSelectionRuleRecord>();
    public List<DefaultLoadoutRecord> defaultLoadouts = new List<DefaultLoadoutRecord>();

    [Header("Global Rules")]
    public List<GlobalRuleRecord> globalRules = new List<GlobalRuleRecord>();

    public string DefaultRuleSetId
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_defaultRuleSetId))
                return ProjectConfigConstants.DefaultRuleSetId;

            return _defaultRuleSetId.Trim();
        }
    }

    public string DefaultModeTag
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_defaultModeTag))
                return ProjectConfigConstants.DefaultModeTag;

            return _defaultModeTag.Trim();
        }
    }
}
