using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Minimal bridge from design-table records to existing components: overlay record fields onto
/// serialized fields of the same (or mapped) name, plus a few lookups (current track, current mode,
/// text keys, global rules). Components keep their prefab/scene values as fallback when a table row
/// is missing, so behaviour never changes unless a row exists.
/// </summary>
public static class DesignTableOverlay
{
    private const BindingFlags FieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static ProjectConfigDatabase Db => ProjectConfigRuntime.EnsureInitialized() ? ProjectConfigRuntime.Database : null;

    public static T Single<T>(List<T> list, Func<T, bool> predicate) where T : class
    {
        if (list == null)
            return null;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && (predicate == null || predicate(list[i])))
                return list[i];
        }
        return null;
    }

    public static string Text(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key))
            return fallback;
        return ProjectConfigRuntime.TryGetTextRepository(out LocalizedTextRepository texts) && texts.TryGet(key, out string value) ? value : fallback;
    }

    public static float GlobalFloat(string ruleKey, float fallback)
    {
        return ProjectConfigRuntime.TryGetGlobalRuleRepository(out GlobalRuleRepository rules) && rules.TryGetFloat(ruleKey, out float value) ? value : fallback;
    }

    public static int GlobalInt(string ruleKey, int fallback)
    {
        return Mathf.RoundToInt(GlobalFloat(ruleKey, fallback));
    }

    /// <summary>Track row whose sceneName matches the active scene; falls back to the first Track row.</summary>
    public static string CurrentTrackId()
    {
        ProjectConfigDatabase db = Db;
        if (db == null || db.tracks == null || db.tracks.Count == 0)
            return string.Empty;
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        TrackRecord match = Single(db.tracks, t => string.Equals(t.sceneName, scene, StringComparison.Ordinal));
        return (match ?? db.tracks[0]).trackId;
    }

    /// <summary>"Online", "Solo" or "Practice" from the active match rules.</summary>
    public static string CurrentModeId()
    {
        BuddahGo.Match.IMatchRules rules = BuddahGo.Match.MatchRules.Current;
        if (rules == null || !rules.IsSolo)
            return "Online";
        return rules.EndOnHumanFinish ? "Practice" : "Solo";
    }

    public static ModeRuleRecord ModeRule(string modeId)
    {
        ProjectConfigDatabase db = Db;
        return db == null ? null : Single(db.modeRules, m => m.modeId == modeId);
    }

    public static ModeRuleRecord CurrentModeRule() => ModeRule(CurrentModeId());

    /// <summary>
    /// Copy every public field of <paramref name="record"/> onto a field of <paramref name="target"/> with the same
    /// name, or the mapped name given as "recordField=targetField". Fields without a counterpart are skipped.
    /// Returns the number of fields written.
    /// </summary>
    public static int Apply(object target, object record, params string[] map)
    {
        if (target == null || record == null)
            return 0;
        Dictionary<string, string> rename = null;
        if (map != null && map.Length > 0)
        {
            rename = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string entry in map)
            {
                int eq = entry.IndexOf('=');
                if (eq > 0)
                    rename[entry.Substring(0, eq)] = entry.Substring(eq + 1);
            }
        }

        int written = 0;
        foreach (FieldInfo source in record.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            string name = source.Name;
            if (rename != null && rename.TryGetValue(name, out string mapped))
                name = mapped;
            FieldInfo destination = FindField(target.GetType(), name);
            if (destination == null)
                continue;
            object value = source.GetValue(record);
            if (TryCoerce(value, destination.FieldType, out object coerced))
            {
                destination.SetValue(target, coerced);
                written++;
            }
        }
        return written;
    }

    /// <summary>Set one field by name from its cell text (used for SkillEffectParam rows).</summary>
    public static bool TrySetField(object target, string fieldName, string rawValue)
    {
        if (target == null || string.IsNullOrEmpty(fieldName))
            return false;
        FieldInfo field = FindField(target.GetType(), fieldName);
        if (field == null)
            return false;
        if (!TryParse(rawValue, field.FieldType, out object value))
            return false;
        field.SetValue(target, value);
        return true;
    }

    public static FieldInfo FindField(Type type, string name)
    {
        while (type != null && type != typeof(object))
        {
            FieldInfo field = type.GetField(name, FieldFlags | BindingFlags.DeclaredOnly);
            if (field != null)
                return field;
            type = type.BaseType;
        }
        return null;
    }

    public static bool TryParse(string raw, Type type, out object value)
    {
        raw = (raw ?? string.Empty).Trim();
        value = null;
        try
        {
            if (type == typeof(string)) { value = raw; return true; }
            if (type == typeof(float)) { value = float.Parse(raw, CultureInfo.InvariantCulture); return true; }
            if (type == typeof(int)) { value = (int)Math.Round(double.Parse(raw, CultureInfo.InvariantCulture)); return true; }
            if (type == typeof(bool))
            {
                string l = raw.ToLowerInvariant();
                value = l == "true" || l == "1" || l == "是" || l == "yes";
                return l == "true" || l == "false" || l == "1" || l == "0" || l == "是" || l == "否" || l == "yes" || l == "no" || l.Length == 0;
            }
            if (type == typeof(Vector3)) { float[] p = Parts(raw, 3); value = new Vector3(p[0], p[1], p[2]); return true; }
            if (type == typeof(Vector2)) { float[] p = Parts(raw, 2); value = new Vector2(p[0], p[1]); return true; }
            if (type == typeof(Color)) { float[] p = Parts(raw, 4); value = new Color(p[0], p[1], p[2], p[3]); return true; }
            if (type.IsEnum)
            {
                value = int.TryParse(raw, out int iv) ? Enum.ToObject(type, iv) : Enum.Parse(type, raw, true);
                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }
        return false;
    }

    private static float[] Parts(string raw, int n)
    {
        string[] split = raw.Split(',');
        float[] parts = new float[n];
        for (int i = 0; i < n && i < split.Length; i++)
            parts[i] = float.Parse(split[i].Trim(), CultureInfo.InvariantCulture);
        return parts;
    }

    public static bool TryCoerce(object value, Type target, out object result)
    {
        result = null;
        if (value == null)
            return false;
        Type source = value.GetType();
        if (target.IsAssignableFrom(source)) { result = value; return true; }
        if (target == typeof(float) && value is int i) { result = (float)i; return true; }
        if (target == typeof(int) && value is float f) { result = Mathf.RoundToInt(f); return true; }
        if (target.IsEnum && value is string s)
            return TryParse(s, target, out result);
        if (value is IList list)
        {
            if (target.IsArray)
            {
                Type elem = target.GetElementType();
                Array array = Array.CreateInstance(elem, list.Count);
                for (int k = 0; k < list.Count; k++)
                {
                    if (!TryCoerce(list[k], elem, out object item))
                        return false;
                    array.SetValue(item, k);
                }
                result = array;
                return true;
            }
            if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elem = target.GetGenericArguments()[0];
                IList outList = (IList)Activator.CreateInstance(target);
                for (int k = 0; k < list.Count; k++)
                {
                    if (!TryCoerce(list[k], elem, out object item))
                        return false;
                    outList.Add(item);
                }
                result = outList;
                return true;
            }
        }
        return false;
    }
}
