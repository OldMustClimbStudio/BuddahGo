#if UNITY_EDITOR
using System.Collections.Generic;

namespace BuddahGo.DesignTables
{
    /// <summary>Cross-sheet rules that the type grammar cannot express. Keep each rule small and named.</summary>
    public static class DesignTableDomainRules
    {
        public static void Validate(DesignTableSet set, List<Issue> issues)
        {
            CheckpointCountMatchesTrack(set, issues);
            RosterMatchesPersonalities(set, issues);
            KeyMaxBelowComboWindow(set, issues);
            SkillRowsAreConsistent(set, issues);
            DefaultLoadoutUsesSelectableSkills(set, issues);
        }

        private static void CheckpointCountMatchesTrack(DesignTableSet set, List<Issue> issues)
        {
            // Match/Track carries requiredCheckpointCount (v2); Movement/LapRule did before the move.
            DesignSheet lap = set.Find("Match", "Track");
            if (lap == null || lap.FindColumn("requiredCheckpointCount") == null)
                lap = set.Find("Movement", "LapRule");
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
            // The AI's key-hold maximum must stay inside the player's combo input window.
            float keyMax = -1f, window = -1f;
            DesignSheet tuning = set.Find("AI", "SkillTuning");
            if (tuning != null && tuning.FindColumn("keyMaxSeconds") != null && tuning.Rows.Count > 0)
                keyMax = ValueConverter.ToFloat(tuning.Cell(tuning.Rows[0], tuning.FindColumn("keyMaxSeconds")));
            else
            {
                DesignSheet global = set.Find("AI", "SkillGlobal");
                if (global != null)
                {
                    ColumnSpec k = global.FindColumn("key"), v = global.FindColumn("value");
                    foreach (DataRow row in global.Rows)
                        if (global.Cell(row, k).Trim() == "KeyMaxSeconds") keyMax = ValueConverter.ToFloat(global.Cell(row, v));
                }
            }
            DesignSheet combo = set.Find("Skill", "ComboInput");
            if (combo != null && combo.FindColumn("inputWindowSeconds") != null && combo.Rows.Count > 0)
                window = ValueConverter.ToFloat(combo.Cell(combo.Rows[0], combo.FindColumn("inputWindowSeconds")));
            if (keyMax >= 0f && window >= 0f && keyMax >= window)
                issues.Add(new Issue { Severity = IssueSeverity.Error, Location = "AI.xlsx/SkillTuning", Message = "keyMaxSeconds " + keyMax + " must be below Skill/ComboInput.inputWindowSeconds " + window });
        }

        private static void SkillRowsAreConsistent(DesignTableSet set, List<Issue> issues)
        {
            DesignSheet skill = set.Find("Skill", "Skill");
            if (skill == null) return;
            ColumnSpec id = skill.FindColumn("skillId"), anti = skill.FindColumn("antiSkillId"), gain = skill.FindColumn("obsessionGain");
            if (id == null || anti == null) return;
            HashSet<string> antiIds = new HashSet<string>();
            foreach (DataRow row in skill.Rows)
            {
                string a = skill.Cell(row, anti).Trim();
                string sid = skill.Cell(row, id).Trim();
                if (!string.IsNullOrEmpty(a)) antiIds.Add(a);
                else if (!sid.EndsWith("_anti"))
                    issues.Add(new Issue { Severity = IssueSeverity.Warning, Location = skill.Location(row, anti), Message = "normal skill '" + sid + "' has no antiSkillId" });
            }
            if (gain == null) return;
            // Backfire adds the requested skill's obsessionGain (SkillExecutor.QueueCast); the anti row's value is never read.
            foreach (DataRow row in skill.Rows)
            {
                string sid = skill.Cell(row, id).Trim();
                string g = skill.Cell(row, gain).Trim();
                if (antiIds.Contains(sid) && !string.IsNullOrEmpty(g) && ValueConverter.ToFloat(g) != 0f)
                    issues.Add(new Issue { Severity = IssueSeverity.Warning, Location = skill.Location(row, gain), Message = "obsessionGain on anti skill '" + sid + "' is ignored (backfire uses the requested skill's gain); leave it blank" });
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
