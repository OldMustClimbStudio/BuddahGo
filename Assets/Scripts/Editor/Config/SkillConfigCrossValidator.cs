#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Reports drift between SkillAction / 缘起 assets and the design tables without applying values or changing runtime repositories.</summary>
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

            // Hidden rows are never offered or cast, so they need no action.
            foreach (var pair in repository.Skills)
                if (!actions.ContainsKey(pair.Key) && !IsHidden(pair.Value))
                    Add(results, ProjectConfigValidationSeverity.Warning, path,
                        "Config skill '" + pair.Key + "' has no action in this SkillDatabase.");

            foreach (var pair in actions)
            {
                string id = pair.Key;
                SkillAction action = pair.Value;
                string context = path + " -> " + AssetDatabase.GetAssetPath(action);
                if (repository.TryGet(id, out SkillRecord row))
                {
                    Compare(results, context, "cooldownSeconds", action.cooldownSeconds, row.cooldownSeconds);
                    // Anti rows carry no obsession gain (a backfire adds none), so only normal skills are compared.
                    if (!string.IsNullOrEmpty(row.antiSkillId) || !string.IsNullOrWhiteSpace(action.antiSkillId))
                        Compare(results, context, "obsessionGain", action.obsessionGain, row.obsessionGain);
                    if (!string.Equals((action.antiSkillId ?? string.Empty).Trim(), (row.antiSkillId ?? string.Empty).Trim(), StringComparison.Ordinal))
                        Add(results, ProjectConfigValidationSeverity.Warning, context,
                            "antiSkillId differs: action='" + action.antiSkillId + "', config='" + row.antiSkillId + "'. Config takes priority when nonempty.");
                    string durationField = SkillDatabase.EffectDurationField(action);
                    if (durationField != null && row.effectDurationSeconds > 0f)
                        CompareField(results, context, action, durationField, row.effectDurationSeconds);
                }
                else
                    Add(results, ProjectConfigValidationSeverity.Warning, context, "Action id '" + id + "' has no Skill row; runtime uses SkillAction balance values.");

                // The variant a backfire of this skill runs (SkillExecutor.ResolveAntiSkillId order: config, SkillAction, "<id>_anti").
                if (!id.EndsWith("_anti", StringComparison.Ordinal))
                {
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
                        Add(results, ProjectConfigValidationSeverity.Warning, context, "Resolved anti id is absent; a backfire would run no variant.");
                }

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

                // SkillEffect<Family> rows: every column with a same-name serialized field on this action is compared.
                CompareFamily(results, context, action, id, config.skillEffectAccelerations);
                CompareFamily(results, context, action, id, config.skillEffectSlowTraps);
                CompareFamily(results, context, action, id, config.skillEffectBlackCurtains);
                CompareFamily(results, context, action, id, config.skillEffectGiants);
                // Skill/Push charged rows use the explicit mapping of SkillDatabase.
                if (id == "push_projectile_hands")
                    CompareMapped(results, context, action, Find(config.pushes, "charged"), SkillDatabase.PushChargedMap);
                else if (id == "push_projectile_hands_anti")
                    CompareMapped(results, context, action, Find(config.pushes, "charged_anti"), SkillDatabase.PushChargedAntiMap);
            }

            CompareYuanqis(results, path, database, config);
        }
        return results;
    }

    // Skill/Yuanqi rows (SkillDatabase.YuanqiRowMap) and YuanqiEffect<Pair> rows against the 缘起 assets of this database.
    private static void CompareYuanqis(List<ProjectConfigValidationMessage> results, string path, SkillDatabase database, ProjectConfigDatabase config)
    {
        var assets = new Dictionary<string, YuanqiAction>(StringComparer.Ordinal);
        if (database.yuanqis != null)
        {
            foreach (YuanqiAction yuanqi in database.yuanqis)
            {
                string id = yuanqi != null ? (yuanqi.yuanqiId ?? string.Empty).Trim() : string.Empty;
                if (id.Length > 0 && !assets.ContainsKey(id))
                    assets.Add(id, yuanqi);
            }
        }

        var rows = new HashSet<string>(StringComparer.Ordinal);
        if (config.yuanqis != null)
        {
            foreach (YuanqiRecord row in config.yuanqis)
            {
                string id = row != null ? (row.yuanqiId ?? string.Empty).Trim() : string.Empty;
                if (id.Length == 0)
                    continue;
                rows.Add(id);
                if (!assets.TryGetValue(id, out YuanqiAction yuanqi))
                {
                    Add(results, ProjectConfigValidationSeverity.Warning, path, "Config yuanqi '" + id + "' has no 缘起 asset in this SkillDatabase.");
                    continue;
                }
                string context = path + " -> " + AssetDatabase.GetAssetPath(yuanqi);
                foreach (string field in SkillDatabase.YuanqiRowMap)
                    CompareText(results, context, yuanqi, field, typeof(YuanqiRecord).GetField(field)?.GetValue(row));
            }
        }

        foreach (KeyValuePair<string, YuanqiAction> pair in assets)
        {
            string context = path + " -> " + AssetDatabase.GetAssetPath(pair.Value);
            if (!rows.Contains(pair.Key))
                Add(results, ProjectConfigValidationSeverity.Warning, context, "Yuanqi id '" + pair.Key + "' has no Skill/Yuanqi row; runtime uses the asset values.");
            CompareYuanqiFamily(results, context, pair.Value, pair.Key, config.yuanqiEffectFrostLocks);
            CompareYuanqiFamily(results, context, pair.Value, pair.Key, config.yuanqiEffectGiantCharges);
        }
    }

    private static void CompareYuanqiFamily(List<ProjectConfigValidationMessage> results, string context, YuanqiAction yuanqi, string id, IList rows)
    {
        if (rows == null)
            return;
        foreach (object row in rows)
        {
            FieldInfo idField = row != null ? row.GetType().GetField("yuanqiId", BindingFlags.Public | BindingFlags.Instance) : null;
            if (idField == null || (idField.GetValue(row) as string ?? string.Empty).Trim() != id)
                continue;
            Type expected = SkillDatabase.YuanqiFamilyActionType(row.GetType());
            if (expected == null || !expected.IsInstanceOfType(yuanqi))
            {
                Add(results, ProjectConfigValidationSeverity.Warning, context, row.GetType().Name + " row '" + id + "' targets " + yuanqi.GetType().Name + "; SkillDatabase skips it.");
                continue;
            }
            foreach (FieldInfo field in row.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.Name == "yuanqiId")
                    continue;
                object value = field.GetValue(row);
                if (value is float f)
                    CompareField(results, context, yuanqi, field.Name, f);
                else if (value is int n)
                    CompareField(results, context, yuanqi, field.Name, n);
                else
                    CompareText(results, context, yuanqi, field.Name, value);
            }
        }
    }

    // Bool / enum / string fields: the asset's serialized value against the table cell (enum by member name, case-insensitive).
    private static void CompareText(List<ProjectConfigValidationMessage> results, string context, UnityEngine.Object asset, string fieldName, object configured)
    {
        using var serialized = new SerializedObject(asset);
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property == null || configured == null)
            return;
        string actual;
        switch (property.propertyType)
        {
            case SerializedPropertyType.Boolean: actual = property.boolValue ? "true" : "false"; break;
            case SerializedPropertyType.Enum: actual = property.enumValueIndex >= 0 && property.enumValueIndex < property.enumNames.Length ? property.enumNames[property.enumValueIndex] : property.intValue.ToString(CultureInfo.InvariantCulture); break;
            case SerializedPropertyType.String: actual = property.stringValue ?? string.Empty; break;
            default: return;
        }
        string expected = configured is bool b ? (b ? "true" : "false") : (configured.ToString() ?? string.Empty).Trim();
        if (!string.Equals(actual.Trim(), expected, StringComparison.OrdinalIgnoreCase))
            Add(results, ProjectConfigValidationSeverity.Warning, context,
                fieldName + " differs: asset=" + actual + ", config=" + expected + ". Report only; the table wins in play.");
    }

    private static bool IsHidden(SkillRecord row)
    {
        return row != null && string.Equals((row.unlockState ?? string.Empty).Trim(), "hidden", StringComparison.OrdinalIgnoreCase);
    }

    private static PushRecord Find(List<PushRecord> rows, string pushId)
    {
        return rows == null ? null : rows.Find(r => r != null && r.pushId == pushId);
    }

    private static void CompareFamily(List<ProjectConfigValidationMessage> results, string context, SkillAction action, string id, IList rows)
    {
        if (rows == null)
            return;
        foreach (object row in rows)
        {
            if (row == null)
                continue;
            FieldInfo idField = row.GetType().GetField("skillId", BindingFlags.Public | BindingFlags.Instance);
            if (idField == null || (idField.GetValue(row) as string ?? string.Empty).Trim() != id)
                continue;
            foreach (FieldInfo field in row.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.Name == "skillId")
                    continue;
                object value = field.GetValue(row);
                if (value is float f)
                    CompareField(results, context, action, field.Name, f);
                else if (value is int n)
                    CompareField(results, context, action, field.Name, n);
            }
        }
    }

    private static void CompareMapped(List<ProjectConfigValidationMessage> results, string context, SkillAction action, PushRecord row, string[] map)
    {
        if (row == null)
            return;
        foreach (string entry in map)
        {
            int eq = entry.IndexOf('=');
            string source = eq > 0 ? entry.Substring(0, eq) : entry;
            string target = eq > 0 ? entry.Substring(eq + 1) : entry;
            FieldInfo field = typeof(PushRecord).GetField(source, BindingFlags.Public | BindingFlags.Instance);
            object value = field != null ? field.GetValue(row) : null;
            if (value is float f)
                CompareField(results, context, action, target, f, "Push." + source);
            else if (value is int n)
                CompareField(results, context, action, target, n, "Push." + source);
        }
    }

    /// <summary>Compare a serialized numeric field of the action with the table value; blank numeric cells (0) on a field the class lacks are skipped by FindProperty.</summary>
    private static void CompareField(List<ProjectConfigValidationMessage> results, string context, UnityEngine.Object action, string fieldName, float configured, string label = null)
    {
        using var serializedAction = new SerializedObject(action);
        SerializedProperty property = serializedAction.FindProperty(fieldName);
        if (property == null)
            return;
        if (property.propertyType == SerializedPropertyType.Float)
            Compare(results, context, label ?? fieldName, property.floatValue, configured);
        else if (property.propertyType == SerializedPropertyType.Integer)
            Compare(results, context, label ?? fieldName, property.intValue, configured);
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
