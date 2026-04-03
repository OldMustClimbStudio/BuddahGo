using System;
using System.Collections.Generic;
using System.Globalization;

public class GlobalRuleRepository
{
    private readonly string _defaultRuleSetId;
    private readonly Dictionary<string, GlobalRuleRecord> _ruleByCompositeKey = new Dictionary<string, GlobalRuleRecord>(StringComparer.Ordinal);

    public GlobalRuleRepository(ProjectConfigDatabase database)
    {
        _defaultRuleSetId = database != null ? database.DefaultRuleSetId : ProjectConfigConstants.DefaultRuleSetId;
        if (database == null || database.globalRules == null)
            return;

        for (int i = 0; i < database.globalRules.Count; i++)
        {
            GlobalRuleRecord record = database.globalRules[i];
            if (record == null || string.IsNullOrWhiteSpace(record.ruleKey))
                continue;

            string compositeKey = BuildCompositeKey(record.ruleSetId, record.ruleKey);
            if (!_ruleByCompositeKey.ContainsKey(compositeKey))
                _ruleByCompositeKey.Add(compositeKey, record);
        }
    }

    public bool TryGetRule(string ruleKey, out GlobalRuleRecord record)
    {
        return TryGetRule(null, ruleKey, out record);
    }

    public bool TryGetRule(string ruleSetId, string ruleKey, out GlobalRuleRecord record)
    {
        string compositeKey = BuildCompositeKey(ruleSetId, ruleKey);
        if (_ruleByCompositeKey.TryGetValue(compositeKey, out record))
            return true;

        compositeKey = BuildCompositeKey(_defaultRuleSetId, ruleKey);
        return _ruleByCompositeKey.TryGetValue(compositeKey, out record);
    }

    public bool TryGetFloat(string ruleKey, out float value)
    {
        return TryGetFloat(null, ruleKey, out value);
    }

    public bool TryGetFloat(string ruleSetId, string ruleKey, out float value)
    {
        value = 0f;
        if (!TryGetRule(ruleSetId, ruleKey, out GlobalRuleRecord record))
            return false;

        return float.TryParse(record.ruleValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public string GetString(string ruleKey, string fallbackValue)
    {
        if (TryGetRule(ruleKey, out GlobalRuleRecord record) && !string.IsNullOrWhiteSpace(record.ruleValue))
            return record.ruleValue;

        return fallbackValue;
    }

    private string BuildCompositeKey(string ruleSetId, string ruleKey)
    {
        string resolvedRuleSetId = string.IsNullOrWhiteSpace(ruleSetId) ? _defaultRuleSetId : ruleSetId.Trim();
        return resolvedRuleSetId + "|" + (ruleKey ?? string.Empty).Trim();
    }
}
