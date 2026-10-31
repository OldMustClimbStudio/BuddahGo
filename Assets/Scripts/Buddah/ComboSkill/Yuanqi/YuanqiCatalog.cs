using System;
using System.Collections.Generic;

/// <summary>
/// Which 缘起 a loadout can trigger ("开局即知"). Pure: the server registry and the HUD (loadout screen, from the synced
/// SkillLoadout.SlotSkillIds + the local SkillDatabase) use the same answer.
/// </summary>
public static class YuanqiCatalog
{
    /// <summary>
    /// The enabled pairs whose two skills are both in <paramref name="skillIds"/>, in <paramref name="all"/> order.
    /// Pairs are unordered. Skipped: null entries, empty ids, a pair of one skill with itself, and a yuanqiId already
    /// matched (the first match wins). Ids compare ordinally after trimming.
    /// </summary>
    public static List<YuanqiAction> ForLoadout(IEnumerable<YuanqiAction> all, IEnumerable<string> skillIds)
    {
        var results = new List<YuanqiAction>();
        if (all == null || skillIds == null)
            return results;

        var loadout = new HashSet<string>(StringComparer.Ordinal);
        foreach (string skillId in skillIds)
        {
            string id = Normalize(skillId);
            if (id.Length > 0)
                loadout.Add(id);
        }
        if (loadout.Count < 2)
            return results;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (YuanqiAction action in all)
        {
            if (action == null || !action.isEnabled)
                continue;
            string yuanqiId = Normalize(action.yuanqiId);
            string a = Normalize(action.skillA);
            string b = Normalize(action.skillB);
            if (yuanqiId.Length == 0 || a.Length == 0 || b.Length == 0 || string.Equals(a, b, StringComparison.Ordinal))
                continue;
            if (!loadout.Contains(a) || !loadout.Contains(b) || !seen.Add(yuanqiId))
                continue;
            results.Add(action);
        }
        return results;
    }

    private static string Normalize(string id) => (id ?? string.Empty).Trim();
}
