#if UNITY_EDITOR
using System.Collections.Generic;

namespace BuddahGo.DesignTables
{
    /// <summary>Cross-sheet rules that the type grammar cannot express. Keep each rule small and named.</summary>
    public static class DesignTableDomainRules
    {
        public static void Validate(DesignTableSet set, List<Issue> issues)
        {
            CheckpointCountMatchesLapRule(set, issues);
            RosterMatchesPersonalities(set, issues);
            KeyMaxBelowComboWindow(set, issues);
            AntiSkillsHaveBalanceRows(set, issues);
            DefaultLoadoutUsesSelectableSkills(set, issues);
        }

        private static void CheckpointCountMatchesLapRule(DesignTableSet set, List<Issue> issues)
        {
            DesignSheet lap = set.Find("Movement", "LapRule");
            DesignSheet cps = set.Find("Match", "TrackCheckpoint");
            if (lap == null || cps == null) return;
            ColumnSpec trackCol = lap.FindColumn("trackId");
            ColumnSpec countCol = lap.FindColumn("requiredCheckpointCount");
            ColumnSpec cpTrack = cps.FindColumn("trackId");
            if (trackCol == null || countCol == null || cpTrack == null) return;
            foreach (DataRow row in lap.Rows)
            {
                string track = lap.Cell(row, trackCol).Trim();
                int required = ValueConverter.ToInt(lap.Cell(row, countCol));
                int actual = 0;
                foreach (DataRow cp in cps.Rows)
                    if (cps.Cell(cp, cpTrack).Trim() == track) actual++;
                if (required != actual)
                    issues.Add(new Issue { Severity = IssueSeverity.Error, Location = lap.Location(row, countCol), Message = "requiredCheckpointCount " + required + " but TrackCheckpoint has " + actual + " rows for track '" + track + "'" });
            }
        }

        private static void RosterMatchesPersonalities(DesignTableSet set, List<Issue> issues)
        {
            DesignSheet roster = set.Find("AI", "Roster");
            DesignSheet personalities = set.Find("AI", "Personality");
            if (roster == null || personalities == null) return;
            if (roster.Rows.Count != personalities.Rows.Count)
                issues.Add(new Issue { Severity = IssueSeverity.Error, Location = "AI.xlsx/Roster", Message = "Roster has " + roster.Rows.Count + " slots but Personality has " + personalities.Rows.Count + " rows; AISkillCatalog requires them to match RacerId.MaxAI (5)" });
        }

        private static void KeyMaxBelowComboWindow(DesignTableSet set, List<Issue> issues)
        {
            DesignSheet global = set.Find("AI", "SkillGlobal");
            DesignSheet rules = set.Find("Global", "GlobalRule");
            if (global == null || rules == null) return;
            float keyMax = -1f, window = -1f;
            ColumnSpec k = global.FindColumn("key"), v = global.FindColumn("value");
            foreach (DataRow row in global.Rows)
                if (global.Cell(row, k).Trim() == "KeyMaxSeconds") keyMax = ValueConverter.ToFloat(global.Cell(row, v));
            ColumnSpec rk = rules.FindColumn("ruleKey"), rv = rules.FindColumn("ruleValue");
            foreach (DataRow row in rules.Rows)
                if (rules.Cell(row, rk).Trim() == "combo_input_window_seconds") window = ValueConverter.ToFloat(rules.Cell(row, rv));
            if (keyMax >= 0f && window >= 0f && keyMax >= window)
                issues.Add(new Issue { Severity = IssueSeverity.Error, Location = "AI.xlsx/SkillGlobal", Message = "KeyMaxSeconds " + keyMax + " must be below combo_input_window_seconds " + window });
        }

        private static void AntiSkillsHaveBalanceRows(DesignTableSet set, List<Issue> issues)
        {
            DesignSheet balance = set.Find("Skill", "SkillBalance");
            if (balance == null) return;
            ColumnSpec id = balance.FindColumn("skillId"), anti = balance.FindColumn("antiSkillId");
            HashSet<string> ids = set.KeyValues(balance, "skillId");
            foreach (DataRow row in balance.Rows)
            {
                string a = balance.Cell(row, anti).Trim();
                if (!string.IsNullOrEmpty(a) && !ids.Contains(a))
                    issues.Add(new Issue { Severity = IssueSeverity.Error, Location = balance.Location(row, anti), Message = "antiSkillId '" + a + "' has no SkillBalance row" });
                string sid = balance.Cell(row, id).Trim();
                if (string.IsNullOrEmpty(a) && !sid.Contains("anti"))
                    issues.Add(new Issue { Severity = IssueSeverity.Warning, Location = balance.Location(row, anti), Message = "normal skill '" + sid + "' has no antiSkillId" });
            }
        }

        private static void DefaultLoadoutUsesSelectableSkills(DesignTableSet set, List<Issue> issues)
        {
            DesignSheet loadout = set.Find("Skill", "DefaultLoadout");
            DesignSheet rules = set.Find("Skill", "SkillSelectionRule");
            if (loadout == null || rules == null) return;
            HashSet<string> selectable = new HashSet<string>();
            ColumnSpec rs = rules.FindColumn("skillId"), rp = rules.FindColumn("poolType");
            foreach (DataRow row in rules.Rows)
                if (rules.Cell(row, rp).Trim() == "Selectable") selectable.Add(rules.Cell(row, rs).Trim());
            foreach (DataRow row in loadout.Rows)
            {
                foreach (string slot in new[] { "slot0SkillId", "slot1SkillId", "slot2SkillId" })
                {
                    ColumnSpec c = loadout.FindColumn(slot);
                    string v = loadout.Cell(row, c).Trim();
                    if (!string.IsNullOrEmpty(v) && !selectable.Contains(v))
                        issues.Add(new Issue { Severity = IssueSeverity.Warning, Location = loadout.Location(row, c), Message = "'" + v + "' is not in the Selectable pool" });
                }
            }
        }
    }
}
#endif
