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

        ValidateSkills(database, results);
        ValidateSelectionRules(database, results);
        ValidateDefaultLoadouts(database, results);
        ValidateGlobalRules(database, results);
        return results;
    }

    private static void ValidateSkills(ProjectConfigDatabase database, List<ProjectConfigValidationMessage> results)
    {
        // A backfire fails the requested cast (unified penalty), so the table has no *_anti rows or antiSkillId column any more.
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < database.skills.Count; i++)
        {
            SkillRecord record = database.skills[i];
            string context = "Skill[" + i + "]";
            string skillId = NormalizeId(record != null ? record.skillId : string.Empty);
            if (string.IsNullOrWhiteSpace(skillId))
            {
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "Missing skillId."));
                continue;
            }

            if (!seen.Add(skillId))
                results.Add(Create(ProjectConfigValidationSeverity.Error, context, "Duplicate skillId '" + skillId + "'."));
        }
    }

    private static void ValidateSelectionRules(ProjectConfigDatabase database, List<ProjectConfigValidationMessage> results)
    {
        HashSet<string> knownSkillIds = BuildSkillIdSet(database);
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
        HashSet<string> knownSkillIds = BuildSkillIdSet(database);
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

    private static HashSet<string> BuildSkillIdSet(ProjectConfigDatabase database)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < database.skills.Count; i++)
        {
            SkillRecord record = database.skills[i];
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
