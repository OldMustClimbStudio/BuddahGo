using System;
using System.Collections.Generic;

/// <summary>Lookup for UI/Text rows exported from the design tables. Keys are "domain.entity.field".</summary>
public class LocalizedTextRepository
{
    private readonly Dictionary<string, LocalizedTextRecord> _byKey = new Dictionary<string, LocalizedTextRecord>(StringComparer.Ordinal);

    public LocalizedTextRepository(ProjectConfigDatabase database)
    {
        if (database == null || database.localizedTexts == null)
            return;

        for (int i = 0; i < database.localizedTexts.Count; i++)
        {
            LocalizedTextRecord record = database.localizedTexts[i];
            if (record == null || string.IsNullOrWhiteSpace(record.key))
                continue;
            if (!_byKey.ContainsKey(record.key))
                _byKey.Add(record.key, record);
        }
    }

    public int Count => _byKey.Count;

    public bool TryGet(string key, out string text)
    {
        text = null;
        if (string.IsNullOrEmpty(key) || !_byKey.TryGetValue(key, out LocalizedTextRecord record))
            return false;
        text = record.zh;
        return true;
    }

    /// <summary>Returns the zh text, or the key itself when missing so a wrong key is visible in game.</summary>
    public string Get(string key)
    {
        return TryGet(key, out string text) ? text : key;
    }
}
