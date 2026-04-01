using System;
using System.Collections.Generic;

public class SelectionRuleRepository
{
    private readonly string _defaultRuleSetId;
    private readonly string _defaultModeTag;
    private readonly List<SkillSelectionRuleRecord> _rules = new List<SkillSelectionRuleRecord>();
    private readonly Dictionary<string, List<string>> _skillIdsByPoolContext = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    private readonly Dictionary<string, DefaultLoadoutRecord> _defaultLoadoutByContext = new Dictionary<string, DefaultLoadoutRecord>(StringComparer.Ordinal);

    public SelectionRuleRepository(ProjectConfigDatabase database)
    {
        _defaultRuleSetId = database != null ? database.DefaultRuleSetId : ProjectConfigConstants.DefaultRuleSetId;
        _defaultModeTag = database != null ? database.DefaultModeTag : ProjectConfigConstants.DefaultModeTag;

        if (database == null)
            return;

        BuildRules(database.skillSelectionRules);
        BuildDefaultLoadouts(database.defaultLoadouts);
    }

    public bool HasAnyRules => _rules.Count > 0;

    public List<string> GetSkillIdsForPool(string ruleSetId, string modeTag, string mapTag, SkillSelectionPoolType poolType)
    {
        string key = BuildPoolContextKey(ruleSetId, modeTag, mapTag, poolType);
        if (_skillIdsByPoolContext.TryGetValue(key, out List<string> cached))
            return new List<string>(cached);

        string resolvedRuleSetId = ResolveRuleSetId(ruleSetId);
        string resolvedModeTag = ResolveModeTag(modeTag);
        string resolvedMapTag = ResolveTag(mapTag);
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        List<string> results = new List<string>();

        for (int i = 0; i < _rules.Count; i++)
        {
            SkillSelectionRuleRecord record = _rules[i];
            if (!RuleMatchesContext(record, resolvedRuleSetId, resolvedModeTag, resolvedMapTag, poolType))
                continue;

            string skillId = NormalizeId(record.skillId);
            if (seen.Add(skillId))
                results.Add(skillId);
        }

        _skillIdsByPoolContext[key] = new List<string>(results);
        return results;
    }

    public bool TryGetAllowDuplicateForPool(string ruleSetId, string modeTag, string mapTag, SkillSelectionPoolType poolType, out bool allowDuplicate)
    {
        allowDuplicate = false;
        string resolvedRuleSetId = ResolveRuleSetId(ruleSetId);
        string resolvedModeTag = ResolveModeTag(modeTag);
        string resolvedMapTag = ResolveTag(mapTag);

        for (int i = 0; i < _rules.Count; i++)
        {
            SkillSelectionRuleRecord record = _rules[i];
            if (!RuleMatchesContext(record, resolvedRuleSetId, resolvedModeTag, resolvedMapTag, poolType))
                continue;

            allowDuplicate = record.allowDuplicate;
            return true;
        }

        return false;
    }

    public bool TryGetDefaultLoadout(string ruleSetId, string modeTag, out DefaultLoadoutRecord record)
    {
        string resolvedRuleSetId = ResolveRuleSetId(ruleSetId);
        string resolvedModeTag = ResolveModeTag(modeTag);

        if (_defaultLoadoutByContext.TryGetValue(BuildDefaultLoadoutKey(resolvedRuleSetId, resolvedModeTag), out record))
            return true;

        if (_defaultLoadoutByContext.TryGetValue(BuildDefaultLoadoutKey(resolvedRuleSetId, string.Empty), out record))
            return true;

        if (_defaultLoadoutByContext.TryGetValue(BuildDefaultLoadoutKey(_defaultRuleSetId, _defaultModeTag), out record))
            return true;

        record = null;
        return false;
    }

    public HashSet<string> GetKnownRuleSetIds()
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        ids.Add(_defaultRuleSetId);

        for (int i = 0; i < _rules.Count; i++)
            ids.Add(ResolveRuleSetId(_rules[i].ruleSetId));

        return ids;
    }

    private void BuildRules(List<SkillSelectionRuleRecord> records)
    {
        if (records == null)
            return;

        for (int i = 0; i < records.Count; i++)
        {
            SkillSelectionRuleRecord record = records[i];
            if (record == null || string.IsNullOrWhiteSpace(record.skillId))
                continue;

            _rules.Add(record);
        }
    }

    private void BuildDefaultLoadouts(List<DefaultLoadoutRecord> records)
    {
        if (records == null)
            return;

        for (int i = 0; i < records.Count; i++)
        {
            DefaultLoadoutRecord record = records[i];
            if (record == null)
                continue;

            string contextKey = BuildDefaultLoadoutKey(record.ruleSetId, record.modeTag);
            if (!_defaultLoadoutByContext.ContainsKey(contextKey))
                _defaultLoadoutByContext.Add(contextKey, record);
        }
    }

    private bool RuleMatchesContext(SkillSelectionRuleRecord record, string ruleSetId, string modeTag, string mapTag, SkillSelectionPoolType poolType)
    {
        if (record == null || !record.isEnabled || record.poolType != poolType)
            return false;

        if (!string.Equals(ResolveRuleSetId(record.ruleSetId), ruleSetId, StringComparison.Ordinal))
            return false;

        if (!ContextTagMatches(record.modeTag, modeTag))
            return false;

        if (!ContextTagMatches(record.mapTag, mapTag))
            return false;

        return true;
    }

    private static bool ContextTagMatches(string ruleValue, string contextValue)
    {
        string normalizedRuleValue = ResolveTag(ruleValue);
        if (string.IsNullOrWhiteSpace(normalizedRuleValue))
            return true;

        return string.Equals(normalizedRuleValue, ResolveTag(contextValue), StringComparison.Ordinal);
    }

    private string ResolveRuleSetId(string ruleSetId)
    {
        return string.IsNullOrWhiteSpace(ruleSetId) ? _defaultRuleSetId : ruleSetId.Trim();
    }

    private string ResolveModeTag(string modeTag)
    {
        return string.IsNullOrWhiteSpace(modeTag) ? _defaultModeTag : modeTag.Trim();
    }

    private static string ResolveTag(string value)
    {
        return (value ?? string.Empty).Trim();
    }

    private string BuildPoolContextKey(string ruleSetId, string modeTag, string mapTag, SkillSelectionPoolType poolType)
    {
        return ResolveRuleSetId(ruleSetId) + "|" + ResolveModeTag(modeTag) + "|" + ResolveTag(mapTag) + "|" + poolType;
    }

    private string BuildDefaultLoadoutKey(string ruleSetId, string modeTag)
    {
        return ResolveRuleSetId(ruleSetId) + "|" + ResolveTag(modeTag);
    }

    private static string NormalizeId(string value)
    {
        return (value ?? string.Empty).Trim();
    }
}
