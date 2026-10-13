using System;
using System.Collections.Generic;

/// <summary>Lookup over Skill.xlsx / Skill rows (identity, cooldown / lock / cast delay, obsession gain, anti id).</summary>
public class SkillConfigRepository
{
    private readonly Dictionary<string, SkillRecord> _byId = new Dictionary<string, SkillRecord>(StringComparer.Ordinal);

    public SkillConfigRepository(ProjectConfigDatabase database)
    {
        if (database == null || database.skills == null)
            return;

        for (int i = 0; i < database.skills.Count; i++)
        {
            SkillRecord record = database.skills[i];
            string skillId = NormalizeId(record != null ? record.skillId : string.Empty);
            if (string.IsNullOrWhiteSpace(skillId) || _byId.ContainsKey(skillId))
                continue;

            _byId.Add(skillId, record);
            if (record.castConfirmDelaySeconds > MaxCastConfirmDelaySeconds)
                MaxCastConfirmDelaySeconds = record.castConfirmDelaySeconds;
        }
    }

    public IReadOnlyDictionary<string, SkillRecord> Skills => _byId;

    /// <summary>Largest castConfirmDelaySeconds over all rows (0 without rows); used for timing estimates.</summary>
    public float MaxCastConfirmDelaySeconds { get; private set; }

    public bool TryGet(string skillId, out SkillRecord record)
    {
        return _byId.TryGetValue(NormalizeId(skillId), out record);
    }

    /// <summary>Display text for the skill (UI/Text via displayNameKey); the fallback when there is no row or key.</summary>
    public string GetDisplayName(string skillId, string fallbackValue)
    {
        if (TryGet(skillId, out SkillRecord record) && !string.IsNullOrWhiteSpace(record.displayNameKey))
            return DesignTableOverlay.Text(record.displayNameKey, string.IsNullOrWhiteSpace(fallbackValue) ? NormalizeId(skillId) : fallbackValue);

        return string.IsNullOrWhiteSpace(fallbackValue) ? NormalizeId(skillId) : fallbackValue;
    }

    public string GetDescription(string skillId, string fallbackValue = "")
    {
        if (TryGet(skillId, out SkillRecord record) && !string.IsNullOrWhiteSpace(record.descriptionKey))
            return DesignTableOverlay.Text(record.descriptionKey, fallbackValue ?? string.Empty);

        return fallbackValue ?? string.Empty;
    }

    public int GetSortOrder(string skillId, int fallbackValue = 0)
    {
        return TryGet(skillId, out SkillRecord record) ? record.sortOrder : fallbackValue;
    }

    public bool TryGetCooldown(string skillId, out float value)
    {
        value = 0f;
        if (!TryGet(skillId, out SkillRecord record))
            return false;

        value = record.cooldownSeconds;
        return true;
    }

    public bool TryGetCastLock(string skillId, out float value)
    {
        value = 0f;
        if (!TryGet(skillId, out SkillRecord record))
            return false;

        value = record.castLockSeconds;
        return true;
    }

    /// <summary>Cast confirmation delay of the executed skill (normal or anti row). False when the row has no positive value.</summary>
    public bool TryGetCastConfirmDelay(string skillId, out float value)
    {
        value = 0f;
        if (!TryGet(skillId, out SkillRecord record) || record.castConfirmDelaySeconds <= 0f)
            return false;

        value = record.castConfirmDelaySeconds;
        return true;
    }

    public bool TryGetObsessionGain(string skillId, out float value)
    {
        value = 0f;
        if (!TryGet(skillId, out SkillRecord record))
            return false;

        value = record.obsessionGain;
        return true;
    }

    public bool TryGetAntiSkillId(string skillId, out string antiSkillId)
    {
        antiSkillId = string.Empty;
        if (!TryGet(skillId, out SkillRecord record) || string.IsNullOrWhiteSpace(record.antiSkillId))
            return false;

        antiSkillId = record.antiSkillId.Trim();
        return true;
    }

    private static string NormalizeId(string value)
    {
        return (value ?? string.Empty).Trim();
    }
}
