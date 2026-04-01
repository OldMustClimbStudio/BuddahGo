using System;
using System.Collections.Generic;

public enum ProjectConfigValidationSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

[Serializable]
public class ProjectConfigValidationMessage
{
    public ProjectConfigValidationSeverity severity;
    public string context = string.Empty;
    public string message = string.Empty;
}

public static class ProjectConfigValidator
{
    public static List<ProjectConfigValidationMessage> Validate(ProjectConfigDatabase database)
    {
        List<ProjectConfigValidationMessage> results = new List<ProjectConfigValidationMessage>();
        if (database == null)
        {
            results.Add(Create(ProjectConfigValidationSeverity.Error, "ProjectConfigDatabase", "Database reference is null."));
            return results;
        }

        ValidateSkillDefinitions(database, results);
        ValidateSkillBalances(database, results);
        ValidateEffectParams(database, results);
        ValidateSelectionRules(database, results);
        ValidateDefaultLoadouts(database, results);
        ValidateGlobalRules(database, results);
        return results;
    }

    private static void ValidateSkillDefinitions(ProjectConfigDatabase database, List<ProjectConfigValidationMessage> results)
    {
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < database.skillDefinitions.Count; i++)
        {
            SkillDefinitionRecord record = database.skillDefinitions[i];
            string context = "SkillDefinition[" + i + "]";
            string skillId = NormalizeId(record != null ? record.skillId : string.Empty);
            if (string.IsNullOrWhiteSpace(skillId))
            {
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "Missing skillId."));
                continue;
            }

            if (!seen.Add(skillId))
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "Duplicate skillId '" + skillId + "'."));

            if (string.IsNullOrWhiteSpace(record.behaviorType))
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "Missing behaviorType for skill '" + skillId + "'."));
        }
    }

    private static void ValidateSkillBalances(ProjectConfigDatabase database, List<ProjectConfigValidationMessage> results)
    {
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> knownSkillIds = BuildDefinitionIdSet(database);

        for (int i = 0; i < database.skillBalances.Count; i++)
        {
            SkillBalanceRecord record = database.skillBalances[i];
            string context = "SkillBalance[" + i + "]";
            string skillId = NormalizeId(record != null ? record.skillId : string.Empty);
            if (string.IsNullOrWhiteSpace(skillId))
            {
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "Missing skillId."));
                continue;
            }

            if (!seen.Add(skillId))
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "Duplicate balance record for skillId '" + skillId + "'."));

            if (!knownSkillIds.Contains(skillId))
                results.Add(Create(ProjectConfigValidationSeverity.Warning, context, "Balance record references missing SkillDefinition '" + skillId + "'."));

            string antiSkillId = NormalizeId(record.antiSkillId);
            if (!string.IsNullOrWhiteSpace(antiSkillId) && !knownSkillIds.Contains(antiSkillId))
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "antiSkillId '" + antiSkillId + "' does not exist."));
        }
    }

    private static void ValidateEffectParams(ProjectConfigDatabase database, List<ProjectConfigValidationMessage> results)
    {
        for (int i = 0; i < database.skillEffectParams.Count; i++)
        {
            SkillEffectParamRecord record = database.skillEffectParams[i];
            string context = "SkillEffectParam[" + i + "]";
            if (string.IsNullOrWhiteSpace(record != null ? record.skillId : string.Empty))
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "Missing skillId."));

            if (string.IsNullOrWhiteSpace(record != null ? record.effectType : string.Empty))
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "Missing effectType."));

            if (string.IsNullOrWhiteSpace(record != null ? record.paramKey : string.Empty))
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "Missing paramKey."));

            if (record != null && record.paramValue == null)
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "paramValue is null."));
        }
    }

    private static void ValidateSelectionRules(ProjectConfigDatabase database, List<ProjectConfigValidationMessage> results)
    {
        HashSet<string> knownSkillIds = BuildDefinitionIdSet(database);
        Dictionary<string, bool> duplicatePolicyByPoolContext = new Dictionary<string, bool>(StringComparer.Ordinal);

        for (int i = 0; i < database.skillSelectionRules.Count; i++)
        {
            SkillSelectionRuleRecord record = database.skillSelectionRules[i];
            string context = "SkillSelectionRule[" + i + "]";
            string skillId = NormalizeId(record != null ? record.skillId : string.Empty);
            if (string.IsNullOrWhiteSpace(skillId))
            {
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "Missing skillId."));
                continue;
            }

            if (!knownSkillIds.Contains(skillId))
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "Selection rule references missing skillId '" + skillId + "'."));

            string duplicatePolicyKey = NormalizeId(record.ruleSetId) + "|" + NormalizeId(record.modeTag) + "|" + NormalizeId(record.mapTag) + "|" + record.poolType;
            if (duplicatePolicyByPoolContext.TryGetValue(duplicatePolicyKey, out bool existingAllowDuplicate))
            {
                if (existingAllowDuplicate != record.allowDuplicate)
                {
                    results.Add(Create(
                        ProjectConfigValidationSeverity.Warning,
                        context,
                        "Conflicting allowDuplicate policy under context '" + duplicatePolicyKey + "'."));
                }
            }
            else
            {
                duplicatePolicyByPoolContext.Add(duplicatePolicyKey, record.allowDuplicate);
            }
        }

        SelectionRuleRepository repository = new SelectionRuleRepository(database);
        HashSet<string> ruleSetIds = repository.GetKnownRuleSetIds();
        foreach (string ruleSetId in ruleSetIds)
        {
            List<string> selectableSkills = repository.GetSkillIdsForPool(ruleSetId, database.DefaultModeTag, string.Empty, SkillSelectionPoolType.Selectable);
            if (selectableSkills.Count == 0)
            {
                results.Add(Create(
                    ProjectConfigValidationSeverity.Warning,
                    "SkillSelectionRule[" + ruleSetId + "]",
                    "Selectable pool is empty for ruleset '" + ruleSetId + "'."));
            }

            List<string> fallbackSkills = repository.GetSkillIdsForPool(ruleSetId, database.DefaultModeTag, string.Empty, SkillSelectionPoolType.Fallback);
            if (fallbackSkills.Count == 0)
            {
                results.Add(Create(
                    ProjectConfigValidationSeverity.Warning,
                    "SkillSelectionRule[" + ruleSetId + "]",
                    "Fallback pool is empty for ruleset '" + ruleSetId + "'."));
            }
        }
    }

    private static void ValidateDefaultLoadouts(ProjectConfigDatabase database, List<ProjectConfigValidationMessage> results)
    {
        HashSet<string> knownSkillIds = BuildDefinitionIdSet(database);
        HashSet<string> contextKeys = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < database.defaultLoadouts.Count; i++)
        {
            DefaultLoadoutRecord record = database.defaultLoadouts[i];
            string context = "DefaultLoadout[" + i + "]";
            if (record == null)
            {
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "Record is null."));
                continue;
            }

            string contextKey = NormalizeId(record.ruleSetId) + "|" + NormalizeId(record.modeTag);
            if (!contextKeys.Add(contextKey))
                results.Add(Create(ProjectConfigValidationSeverity.Warning, context, "Duplicate default loadout context '" + contextKey + "'."));

            string[] slots = record.ToSlots();
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                string skillId = NormalizeId(slots[slotIndex]);
                if (string.IsNullOrWhiteSpace(skillId))
                    continue;

                if (!knownSkillIds.Contains(skillId))
                {
                    results.Add(Create(
                        ProjectConfigValidationSeverity.Error,
                        context,
                        "Default loadout references missing skillId '" + skillId + "' in slot " + slotIndex + "."));
                }
            }
        }
    }

    private static void ValidateGlobalRules(ProjectConfigDatabase database, List<ProjectConfigValidationMessage> results)
    {
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < database.globalRules.Count; i++)
        {
            GlobalRuleRecord record = database.globalRules[i];
            string context = "GlobalRule[" + i + "]";
            if (string.IsNullOrWhiteSpace(record != null ? record.ruleKey : string.Empty))
            {
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "Missing ruleKey."));
                continue;
            }

            string compositeKey = NormalizeId(record.ruleSetId) + "|" + NormalizeId(record.ruleKey);
            if (!seen.Add(compositeKey))
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "Duplicate global rule key '" + compositeKey + "'."));
        }
    }

    private static HashSet<string> BuildDefinitionIdSet(ProjectConfigDatabase database)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < database.skillDefinitions.Count; i++)
        {
            SkillDefinitionRecord record = database.skillDefinitions[i];
            string skillId = NormalizeId(record != null ? record.skillId : string.Empty);
            if (!string.IsNullOrWhiteSpace(skillId))
                ids.Add(skillId);
        }

        return ids;
    }

    private static ProjectConfigValidationMessage Create(ProjectConfigValidationSeverity severity, string context, string message)
    {
        return new ProjectConfigValidationMessage
        {
            severity = severity,
            context = context ?? string.Empty,
            message = message ?? string.Empty
        };
    }

    private static string NormalizeId(string value)
    {
        return (value ?? string.Empty).Trim();
    }
}
