#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

/// <summary>Reports cross-asset drift without applying values or changing runtime repositories.</summary>
public static class SkillConfigCrossValidator
{
    public static List<ProjectConfigValidationMessage> Validate(ProjectConfigDatabase config)
    {
        var results = new List<ProjectConfigValidationMessage>();
        if (config == null)
            return results;

        var repository = new SkillConfigRepository(config);
        HashSet<string> vfxIds = CollectVfxIds();
        foreach (string guid in AssetDatabase.FindAssets("t:SkillDatabase"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillDatabase database = AssetDatabase.LoadAssetAtPath<SkillDatabase>(path);
            if (database == null)
                continue;
            using var serializedDatabase = new SerializedObject(database);
            var configured = serializedDatabase.FindProperty("_projectConfigDatabase").objectReferenceValue;
            if (configured != null && configured != config)
                continue;
            if (configured == null)
                Add(results, ProjectConfigValidationSeverity.Info, path,
                    "No explicit config reference: this comparison is a candidate; runtime fallback may use a different database.");

            var actions = new Dictionary<string, SkillAction>(StringComparer.Ordinal);
            Collect(database.normalSkills, actions, path, results);
            Collect(database.antiSkills, actions, path, results);
            Collect(database.skills, actions, path, results);

            foreach (string id in repository.Definitions.Keys)
                if (!actions.ContainsKey(id))
                    Add(results, ProjectConfigValidationSeverity.Warning, path,
                        "Config skill '" + id + "' has no action in this SkillDatabase.");

            foreach (var pair in actions)
            {
                string id = pair.Key;
                SkillAction action = pair.Value;
                string context = path + " -> " + AssetDatabase.GetAssetPath(action);
                if (!repository.TryGetDefinition(id, out _))
                    Add(results, ProjectConfigValidationSeverity.Warning, context, "Action id '" + id + "' has no config definition.");
                if (repository.TryGetBalance(id, out SkillBalanceRecord balance))
                {
                    Compare(results, context, "cooldownSeconds", action.cooldownSeconds, balance.cooldownSeconds);
                    Compare(results, context, "castLockSeconds", action.castLockSeconds, balance.castLockSeconds);
                    Compare(results, context, "obsessionGain", action.obsessionGain, balance.obsessionGain);
                    if (!string.Equals((action.antiSkillId ?? string.Empty).Trim(), (balance.antiSkillId ?? string.Empty).Trim(), StringComparison.Ordinal))
                        Add(results, ProjectConfigValidationSeverity.Warning, context,
                            "antiSkillId differs: action='" + action.antiSkillId + "', config='" + balance.antiSkillId + "'. Config takes priority when nonempty.");
                }
                else
                    Add(results, ProjectConfigValidationSeverity.Info, context, "No balance row; runtime uses SkillAction balance values.");

                string anti = string.Empty;
                string layer = "none";
                if (repository.TryGetAntiSkillId(id, out string configuredAnti))
                {
                    anti = configuredAnti;
                    layer = "config";
                }
                else if (!string.IsNullOrWhiteSpace(action.antiSkillId))
                {
                    anti = action.antiSkillId.Trim();
                    layer = "SkillAction";
                }
                else if (actions.ContainsKey(id + "_anti"))
                {
                    anti = id + "_anti";
                    layer = "inferred suffix";
                }
                Add(results, ProjectConfigValidationSeverity.Info, context, "Anti resolution: " + layer + " -> '" + anti + "'.");
                if (anti.Length > 0 && !actions.ContainsKey(anti))
                    Add(results, ProjectConfigValidationSeverity.Warning, context, "Resolved anti id is absent; runtime would fall back to the normal skill.");

                using var serializedAction = new SerializedObject(action);
                SerializedProperty vfx = serializedAction.FindProperty("vfxId");
                if (vfx != null && vfx.propertyType == SerializedPropertyType.String)
                {
                    if (string.IsNullOrWhiteSpace(vfx.stringValue))
                        Add(results, ProjectConfigValidationSeverity.Info, context, "Optional vfxId is empty; no library effect is configured.");
                    else if (!vfxIds.Contains(vfx.stringValue))
                        Add(results, ProjectConfigValidationSeverity.Warning, context,
                            "Declared vfxId '" + vfx.stringValue + "' has no valid library prefab. Check whether this action actually uses the field.");
                }
                if (config.skillEffectParams == null)
                    continue;
                foreach (SkillEffectParamRecord parameter in config.skillEffectParams)
                {
                    if (parameter == null || !string.Equals((parameter.skillId ?? string.Empty).Trim(), id, StringComparison.Ordinal))
                        continue;
                    SerializedProperty property = serializedAction.FindProperty((parameter.paramKey ?? string.Empty).Trim());
                    if (property == null)
                    {
                        Add(results, ProjectConfigValidationSeverity.Warning, context,
                            "Effect parameter '" + parameter.effectType + "/" + parameter.paramKey + "' has no same-name serialized field; no alias was assumed.");
                        continue;
                    }
                    if (property.propertyType == SerializedPropertyType.Float || property.propertyType == SerializedPropertyType.Integer)
                    {
                        if (float.TryParse(parameter.paramValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float configuredNumber))
                            Compare(results, context, parameter.paramKey,
                                property.propertyType == SerializedPropertyType.Float ? property.floatValue : property.intValue, configuredNumber);
                        else
                            Add(results, ProjectConfigValidationSeverity.Warning, context, "Nonnumeric config value for '" + parameter.paramKey + "'.");
                    }
                    else if (property.propertyType == SerializedPropertyType.String && property.stringValue != parameter.paramValue)
                        Add(results, ProjectConfigValidationSeverity.Warning, context, "String effect parameter differs: " + parameter.paramKey + ".");
                }
            }
        }
        return results;
    }

    private static void Collect(List<SkillAction> source, Dictionary<string, SkillAction> actions,
        string context, List<ProjectConfigValidationMessage> results)
    {
        if (source == null)
            return;
        foreach (SkillAction action in source)
        {
            if (action == null)
                continue;
            string id = (action.skillId ?? string.Empty).Trim();
            if (id.Length == 0)
            {
                Add(results, ProjectConfigValidationSeverity.Warning, context, "SkillAction has an empty id: " + action.name);
                continue;
            }
            if (actions.ContainsKey(id))
                Add(results, ProjectConfigValidationSeverity.Warning, context, "Duplicate action id '" + id + "'; runtime keeps the first entry.");
            else
                actions.Add(id, action);
        }
    }

    private static HashSet<string> CollectVfxIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string guid in AssetDatabase.FindAssets("t:SkillVfxDatabase"))
        {
            var database = AssetDatabase.LoadAssetAtPath<SkillVfxDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            if (database == null || database.entries == null)
                continue;
            foreach (var entry in database.entries)
                if (entry != null && entry.prefab != null && !string.IsNullOrWhiteSpace(entry.vfxId))
                    ids.Add(entry.vfxId);
        }
        return ids;
    }

    private static void Compare(List<ProjectConfigValidationMessage> results, string context, string field, float action, float config)
    {
        if (!action.Equals(config))
            Add(results, ProjectConfigValidationSeverity.Warning, context,
                field + " differs: action=" + action.ToString("R", CultureInfo.InvariantCulture)
                + ", config=" + config.ToString("R", CultureInfo.InvariantCulture) + ". Report only; choose the source under D5.");
    }

    private static void Add(List<ProjectConfigValidationMessage> results, ProjectConfigValidationSeverity severity, string context, string message)
    {
        results.Add(new ProjectConfigValidationMessage { severity = severity, context = context, message = message });
    }
}
#endif
