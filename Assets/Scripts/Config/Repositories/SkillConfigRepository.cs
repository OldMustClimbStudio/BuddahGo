using System;
using System.Collections.Generic;
using System.Globalization;

public class SkillConfigRepository
{
    private readonly Dictionary<string, SkillDefinitionRecord> _definitionById = new Dictionary<string, SkillDefinitionRecord>(StringComparer.Ordinal);
    private readonly Dictionary<string, SkillBalanceRecord> _balanceById = new Dictionary<string, SkillBalanceRecord>(StringComparer.Ordinal);
    private readonly Dictionary<string, List<SkillEffectParamRecord>> _effectParamsBySkillId = new Dictionary<string, List<SkillEffectParamRecord>>(StringComparer.Ordinal);
    private readonly Dictionary<string, SkillEffectParamRecord> _effectParamByCompositeKey = new Dictionary<string, SkillEffectParamRecord>(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _skillIdsByTag = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _skillIdsByBehaviorType = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    public SkillConfigRepository(ProjectConfigDatabase database)
    {
        if (database == null)
            return;

        BuildDefinitions(database.skillDefinitions);
        BuildBalances(database.skillBalances);
        BuildEffectParams(database.skillEffectParams);
    }

    public IReadOnlyDictionary<string, SkillDefinitionRecord> Definitions => _definitionById;
    public IReadOnlyDictionary<string, SkillBalanceRecord> Balances => _balanceById;

    public bool TryGetDefinition(string skillId, out SkillDefinitionRecord record)
    {
        return _definitionById.TryGetValue(NormalizeId(skillId), out record);
    }

    public bool TryGetBalance(string skillId, out SkillBalanceRecord record)
    {
        return _balanceById.TryGetValue(NormalizeId(skillId), out record);
    }

    public string GetDisplayName(string skillId, string fallbackValue)
    {
        if (TryGetDefinition(skillId, out SkillDefinitionRecord record) && !string.IsNullOrWhiteSpace(record.displayName))
            return record.displayName;

        return string.IsNullOrWhiteSpace(fallbackValue) ? NormalizeId(skillId) : fallbackValue;
    }

    public string GetDescription(string skillId, string fallbackValue = "")
    {
        if (TryGetDefinition(skillId, out SkillDefinitionRecord record) && !string.IsNullOrWhiteSpace(record.description))
            return record.description;

        return fallbackValue ?? string.Empty;
    }

    public string GetIconKey(string skillId, string fallbackValue = "")
    {
        if (TryGetDefinition(skillId, out SkillDefinitionRecord record) && !string.IsNullOrWhiteSpace(record.iconKey))
            return record.iconKey;

        return fallbackValue ?? string.Empty;
    }

    public string GetBehaviorType(string skillId, string fallbackValue = "")
    {
        if (TryGetDefinition(skillId, out SkillDefinitionRecord record) && !string.IsNullOrWhiteSpace(record.behaviorType))
            return record.behaviorType;

        return fallbackValue ?? string.Empty;
    }

    public int GetSortOrder(string skillId, int fallbackValue = 0)
    {
        if (TryGetDefinition(skillId, out SkillDefinitionRecord record))
            return record.sortOrder;

        return fallbackValue;
    }

    public bool TryGetCooldown(string skillId, out float value)
    {
        value = 0f;
        if (!TryGetBalance(skillId, out SkillBalanceRecord record))
            return false;

        value = record.cooldownSeconds;
        return true;
    }

    public bool TryGetCastLock(string skillId, out float value)
    {
        value = 0f;
        if (!TryGetBalance(skillId, out SkillBalanceRecord record))
            return false;

        value = record.castLockSeconds;
        return true;
    }

    public bool TryGetObsessionGain(string skillId, out float value)
    {
        value = 0f;
        if (!TryGetBalance(skillId, out SkillBalanceRecord record))
            return false;

        value = record.obsessionGain;
        return true;
    }

    public bool TryGetAntiSkillId(string skillId, out string antiSkillId)
    {
        antiSkillId = string.Empty;
        if (!TryGetBalance(skillId, out SkillBalanceRecord record) || string.IsNullOrWhiteSpace(record.antiSkillId))
            return false;

        antiSkillId = record.antiSkillId.Trim();
        return true;
    }

    public List<SkillEffectParamRecord> GetAllParams(string skillId)
    {
        if (_effectParamsBySkillId.TryGetValue(NormalizeId(skillId), out List<SkillEffectParamRecord> records))
            return new List<SkillEffectParamRecord>(records);

        return new List<SkillEffectParamRecord>();
    }

    public bool TryGetFloat(string skillId, string effectType, string paramKey, out float value)
    {
        value = 0f;
        if (!TryGetParam(skillId, effectType, paramKey, out SkillEffectParamRecord record))
            return false;

        return float.TryParse(record.paramValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public float GetFloat(string skillId, string effectType, string paramKey, float defaultValue)
    {
        return TryGetFloat(skillId, effectType, paramKey, out float value) ? value : defaultValue;
    }

    public bool TryGetParam(string skillId, string effectType, string paramKey, out SkillEffectParamRecord record)
    {
        return _effectParamByCompositeKey.TryGetValue(BuildEffectKey(skillId, effectType, paramKey), out record);
    }

    public List<string> GetSkillIdsByTag(string tag)
    {
        if (_skillIdsByTag.TryGetValue((tag ?? string.Empty).Trim(), out HashSet<string> ids))
            return new List<string>(ids);

        return new List<string>();
    }

    public List<string> GetSkillIdsByBehaviorType(string behaviorType)
    {
        if (_skillIdsByBehaviorType.TryGetValue((behaviorType ?? string.Empty).Trim(), out HashSet<string> ids))
            return new List<string>(ids);

        return new List<string>();
    }

    private void BuildDefinitions(List<SkillDefinitionRecord> records)
    {
        if (records == null)
            return;

        for (int i = 0; i < records.Count; i++)
        {
            SkillDefinitionRecord record = records[i];
            string skillId = NormalizeId(record != null ? record.skillId : string.Empty);
            if (string.IsNullOrWhiteSpace(skillId) || _definitionById.ContainsKey(skillId))
                continue;

            _definitionById.Add(skillId, record);
            CacheBehaviorType(skillId, record.behaviorType);
            CacheTags(skillId, record.tags);
        }
    }

    private void BuildBalances(List<SkillBalanceRecord> records)
    {
        if (records == null)
            return;

        for (int i = 0; i < records.Count; i++)
        {
            SkillBalanceRecord record = records[i];
            string skillId = NormalizeId(record != null ? record.skillId : string.Empty);
            if (string.IsNullOrWhiteSpace(skillId) || _balanceById.ContainsKey(skillId))
                continue;

            _balanceById.Add(skillId, record);
        }
    }

    private void BuildEffectParams(List<SkillEffectParamRecord> records)
    {
        if (records == null)
            return;

        for (int i = 0; i < records.Count; i++)
        {
            SkillEffectParamRecord record = records[i];
            string skillId = NormalizeId(record != null ? record.skillId : string.Empty);
            if (string.IsNullOrWhiteSpace(skillId))
                continue;

            if (!_effectParamsBySkillId.TryGetValue(skillId, out List<SkillEffectParamRecord> list))
            {
                list = new List<SkillEffectParamRecord>();
                _effectParamsBySkillId.Add(skillId, list);
            }

            list.Add(record);

            string compositeKey = BuildEffectKey(skillId, record.effectType, record.paramKey);
            if (!_effectParamByCompositeKey.ContainsKey(compositeKey))
                _effectParamByCompositeKey.Add(compositeKey, record);
        }
    }

    private void CacheBehaviorType(string skillId, string behaviorType)
    {
        string normalizedBehaviorType = (behaviorType ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedBehaviorType))
            return;

        if (!_skillIdsByBehaviorType.TryGetValue(normalizedBehaviorType, out HashSet<string> ids))
        {
            ids = new HashSet<string>(StringComparer.Ordinal);
            _skillIdsByBehaviorType.Add(normalizedBehaviorType, ids);
        }

        ids.Add(skillId);
    }

    private void CacheTags(string skillId, List<string> tags)
    {
        if (tags == null)
            return;

        for (int i = 0; i < tags.Count; i++)
        {
            string tag = (tags[i] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(tag))
                continue;

            if (!_skillIdsByTag.TryGetValue(tag, out HashSet<string> ids))
            {
                ids = new HashSet<string>(StringComparer.Ordinal);
                _skillIdsByTag.Add(tag, ids);
            }

            ids.Add(skillId);
        }
    }

    private static string NormalizeId(string value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static string BuildEffectKey(string skillId, string effectType, string paramKey)
    {
        return NormalizeId(skillId) + "|" + NormalizeId(effectType) + "|" + NormalizeId(paramKey);
    }
}
