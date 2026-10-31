#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;

namespace BuddahGo.DesignTables
{
    /// <summary>Cross-sheet rules that the type grammar cannot express. Keep each rule small and named.</summary>
    public static class DesignTableDomainRules
    {
        public static void Validate(DesignTableSet set, List<Issue> issues)
        {
            CheckpointCountMatchesTrack(set, issues);
            RosterMatchesPersonalities(set, issues);
            DefaultLoadoutUsesSelectableSkills(set, issues);
            YuanqiRowsAreFilled(set, issues);
            YuanqiPairsAreValid(set, issues);
            YuanqiAvoidsPairwiseTriangles(set, issues);
            BackfireAndSurchargeCellsAreFilled(set, issues);
            TrainingDummyCellsAreFilled(set, issues);
            GraphicsSettingsAreComplete(set, issues);
        }

        /// <summary>Error for each blank cell of <paramref name="vars"/> in the row (columns the sheet lacks are skipped).</summary>
        private static void RequireFilled(DesignSheet sheet, DataRow row, List<Issue> issues, string why, params string[] vars)
        {
            foreach (string var in vars)
            {
                ColumnSpec column = sheet.FindColumn(var);
                if (column != null && string.IsNullOrWhiteSpace(sheet.Cell(row, column)))
                    issues.Add(new Issue { Severity = IssueSeverity.Error, Location = sheet.Location(row, column), Message = "'" + var + "' must be filled: " + why });
            }
        }

        // A blank bool imports as false (a blank isEnabled silently disables the pair), a blank enum leaves the SO value
        // and a blank skill imports as "" (YuanqiCatalog then drops the pair; the enum and ref checks skip blanks).
        private static void YuanqiRowsAreFilled(DesignTableSet set, List<Issue> issues)
        {
            DesignSheet yuanqi = set.Find("Skill", "Yuanqi");
            if (yuanqi == null) return;
            foreach (DataRow row in yuanqi.Rows)
                RequireFilled(yuanqi, row, issues, "a blank skill drops the pair, a blank bool reads false and a blank enum keeps the asset value",
                    "skillA", "skillB", "consumeA", "consumeB", "mode", "retriggerPolicy", "isEnabled");
        }

        /// <summary>
        /// True when the cell imports as <paramref name="member"/>: the same parse as DesignTableOverlay.TryParse (an int
        /// is the enum value, decision 29; otherwise the name, ignoring case).
        /// </summary>
        private static bool CellIsEnumMember<TEnum>(string cell, TEnum member) where TEnum : struct, Enum
        {
            string raw = (cell ?? string.Empty).Trim();
            if (raw.Length == 0)
                return false;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                return value == Convert.ToInt32(member, CultureInfo.InvariantCulture);
            return Enum.TryParse(raw, true, out TEnum parsed) && parsed.Equals(member);
        }

        // yuanqi-architecture 3.6: two different skills, each unordered pair once, a Sustained pair consumes nothing.
        private static void YuanqiPairsAreValid(DesignTableSet set, List<Issue> issues)
        {
            DesignSheet yuanqi = set.Find("Skill", "Yuanqi");
            if (yuanqi == null) return;
            ColumnSpec id = yuanqi.FindColumn("yuanqiId"), a = yuanqi.FindColumn("skillA"), b = yuanqi.FindColumn("skillB");
            ColumnSpec mode = yuanqi.FindColumn("mode"), consumeA = yuanqi.FindColumn("consumeA"), consumeB = yuanqi.FindColumn("consumeB");
            if (id == null || a == null || b == null) return;
            Dictionary<string, string> pairs = new Dictionary<string, string>();
            foreach (DataRow row in yuanqi.Rows)
            {
                string yid = yuanqi.Cell(row, id).Trim(), sa = yuanqi.Cell(row, a).Trim(), sb = yuanqi.Cell(row, b).Trim();
                if (sa.Length > 0 && sa == sb)
                    issues.Add(new Issue { Severity = IssueSeverity.Error, Location = yuanqi.Location(row, b), Message = "缘起 '" + yid + "' pairs '" + sa + "' with itself; skillA and skillB must differ" });
                else if (sa.Length > 0 && sb.Length > 0)
                {
                    string pair = string.CompareOrdinal(sa, sb) < 0 ? sa + "+" + sb : sb + "+" + sa;
                    if (pairs.TryGetValue(pair, out string first))
                        issues.Add(new Issue { Severity = IssueSeverity.Error, Location = yuanqi.Location(row, id), Message = "缘起 '" + yid + "' repeats the pair " + pair + " of '" + first + "'; each unordered pair appears once" });
                    else
                        pairs.Add(pair, yid);
                }

                // By name or by value ("1"): both import as Sustained.
                if (mode == null || !CellIsEnumMember(yuanqi.Cell(row, mode), YuanqiMode.Sustained)) continue;
                foreach (ColumnSpec consume in new[] { consumeA, consumeB })
                {
                    if (consume != null && ValueConverter.TryBool(yuanqi.Cell(row, consume), out bool consumes) && consumes)
                        issues.Add(new Issue { Severity = IssueSeverity.Error, Location = yuanqi.Location(row, consume), Message = "Sustained 缘起 '" + yid + "' cannot consume a skill; set " + consume.Var + " to false" });
                }
            }
        }

        // Design rule: avoid three skills that trigger each other pairwise. A Warning, not an Error: the designer decides.
        private static void YuanqiAvoidsPairwiseTriangles(DesignTableSet set, List<Issue> issues)
        {
            DesignSheet yuanqi = set.Find("Skill", "Yuanqi");
            if (yuanqi == null) return;
            ColumnSpec a = yuanqi.FindColumn("skillA"), b = yuanqi.FindColumn("skillB"), enabled = yuanqi.FindColumn("isEnabled");
            if (a == null || b == null) return;
            Dictionary<string, HashSet<string>> partners = new Dictionary<string, HashSet<string>>();
            foreach (DataRow row in yuanqi.Rows)
            {
                string sa = yuanqi.Cell(row, a).Trim(), sb = yuanqi.Cell(row, b).Trim();
                bool on = enabled == null || (ValueConverter.TryBool(yuanqi.Cell(row, enabled), out bool e) && e);
                if (!on || sa.Length == 0 || sb.Length == 0 || sa == sb) continue;
                foreach (string[] edge in new[] { new[] { sa, sb }, new[] { sb, sa } })
                {
                    if (!partners.TryGetValue(edge[0], out HashSet<string> set0))
                        partners[edge[0]] = set0 = new HashSet<string>();
                    set0.Add(edge[1]);
                }
            }
            List<string> skills = new List<string>(partners.Keys);
            skills.Sort(string.CompareOrdinal);
            for (int i = 0; i < skills.Count; i++)
                for (int j = i + 1; j < skills.Count; j++)
                    for (int k = j + 1; k < skills.Count; k++)
                        if (partners[skills[i]].Contains(skills[j]) && partners[skills[j]].Contains(skills[k]) && partners[skills[k]].Contains(skills[i]))
                            issues.Add(new Issue { Severity = IssueSeverity.Warning, Location = "Skill.xlsx/Yuanqi", Message = "skills " + skills[i] + ", " + skills[j] + " and " + skills[k] + " all trigger each other pairwise; the design avoids such triangles" });
        }

        // Blank numeric cells import as 0 and the #range check skips them: a blank multiplier or keep ratio would zero the effect.
        private static void BackfireAndSurchargeCellsAreFilled(DesignTableSet set, List<Issue> issues)
        {
            DesignSheet obsession = set.Find("Skill", "Obsession");
            if (obsession == null) return;
            foreach (DataRow row in obsession.Rows)
                RequireFilled(obsession, row, issues, "a blank cell imports as 0 and the range check skips it",
                    "repeatWindowSeconds", "repeatGainMultiplier", "backfireLockSeconds", "backfireMaxSpeedMultiplier", "backfireEndKeepRatio");
        }

        // Rows that spawn training dummies need every placement cell (a blank spacing stacks them all on one spot).
        private static void TrainingDummyCellsAreFilled(DesignTableSet set, List<Issue> issues)
        {
            DesignSheet mode = set.Find("Match", "ModeRule");
            ColumnSpec count = mode != null ? mode.FindColumn("trainingDummyCount") : null;
            if (count == null) return;
            foreach (DataRow row in mode.Rows)
            {
                if (ValueConverter.ToInt(mode.Cell(row, count)) <= 0) continue;
                RequireFilled(mode, row, issues, "this row spawns training dummies (trainingDummyCount > 0)",
                    "trainingDummySpacingMeters", "trainingDummyStartOffsetMeters", "trainingDummyLateralOffsetMeters", "trainingDummyResetDelaySeconds");
            }
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

        private static void GraphicsSettingsAreComplete(DesignTableSet set, List<Issue> issues)
        {
            // Graphics/*: every preset needs exactly one default level per option, and level values must be ones the
            // runtime (GraphicsPipelineMapper / GraphicsSettingsApplier) understands.
            DesignSheet presets = set.Find("Graphics", "GraphicsPreset");
            DesignSheet options = set.Find("Graphics", "GraphicsOption");
            DesignSheet levels = set.Find("Graphics", "GraphicsOptionLevel");
            if (presets == null || options == null || levels == null) return;
            ColumnSpec presetId = presets.FindColumn("presetId"), isDefault = presets.FindColumn("isDefault");
            ColumnSpec optionId = options.FindColumn("optionId");
            ColumnSpec lOption = levels.FindColumn("optionId"), lDefault = levels.FindColumn("defaultFor");
            if (presetId == null || optionId == null || lOption == null || lDefault == null) return;

            int defaults = 0;
            if (isDefault != null)
                foreach (DataRow row in presets.Rows)
                    if (ValueConverter.TryBool(presets.Cell(row, isDefault), out bool d) && d) defaults++;
            if (defaults != 1)
                issues.Add(new Issue { Severity = IssueSeverity.Error, Location = "Graphics.xlsx/GraphicsPreset", Message = "exactly one preset must have isDefault = true (found " + defaults + ")" });

            foreach (DataRow optionRow in options.Rows)
            {
                string option = options.Cell(optionRow, optionId).Trim();
                foreach (DataRow presetRow in presets.Rows)
                {
                    string preset = presets.Cell(presetRow, presetId).Trim();
                    int count = 0;
                    foreach (DataRow levelRow in levels.Rows)
                    {
                        if (levels.Cell(levelRow, lOption).Trim() != option) continue;
                        foreach (string p in levels.Cell(levelRow, lDefault).Split('|'))
                            if (p.Trim() == preset) count++;
                    }
                    if (count != 1)
                        issues.Add(new Issue { Severity = IssueSeverity.Error, Location = "Graphics.xlsx/GraphicsOptionLevel", Message = "preset '" + preset + "' has " + count + " default levels for option '" + option + "' (needs exactly 1)" });
                }
            }

            // FrameRateLock reads Global/GlobalRule target_frame_rate; the settings layer replaces it at startup with the
            // preset's default frame_rate_cap level, so the two must agree.
            int globalFps = 0;
            DesignSheet rules = set.Find("Global", "GlobalRule");
            ColumnSpec ruleKey = rules?.FindColumn("ruleKey"), ruleValue = rules?.FindColumn("ruleValue");
            if (ruleKey != null && ruleValue != null)
                foreach (DataRow row in rules.Rows)
                    if (rules.Cell(row, ruleKey).Trim() == "target_frame_rate") globalFps = ValueConverter.ToInt(rules.Cell(row, ruleValue));

            foreach (DataRow row in levels.Rows)
            {
                string option = levels.Cell(row, lOption).Trim();
                string Value(string var) { ColumnSpec c = levels.FindColumn(var); return c == null ? string.Empty : levels.Cell(row, c).Trim(); }
                void Check(string var, bool ok, string message)
                {
                    if (ok) return;
                    ColumnSpec c = levels.FindColumn(var);
                    issues.Add(new Issue { Severity = IssueSeverity.Error, Location = c == null ? levels.Workbook + ".xlsx/" + levels.Name + " (no " + var + " column)" : levels.Location(row, c), Message = message });
                }
                bool OneOf(string value, params string[] allowed) => System.Array.IndexOf(allowed, value) >= 0;
                switch (option)
                {
                    case "upscaler":
                        Check("upscalingFilter", OneOf(Value("upscalingFilter"), "Auto", "Linear", "Point", "FSR"), "upscalingFilter must be Auto, Linear, Point or FSR");
                        break;
                    case "anti_aliasing":
                        int msaa = ValueConverter.ToInt(Value("msaaSamples"));
                        string post = Value("postAntialiasing");
                        Check("msaaSamples", OneOf(msaa.ToString(), "1", "2", "4", "8"), "msaaSamples must be 1, 2, 4 or 8");
                        Check("postAntialiasing", OneOf(post, "None", "FXAA", "TAA"), "postAntialiasing must be None, FXAA or TAA (SMAA crawls on RaceMap's sand)");
                        Check("postAntialiasing", msaa <= 1 || post != "TAA", "TAA cannot run on an MSAA level (URP disables TAA with MSAA)");
                        break;
                    case "shadow_quality":
                        if (!ValueConverter.TryBool(Value("shadowsEnabled"), out bool on) || !on) break;
                        int cascades = ValueConverter.ToInt(Value("shadowCascadeCount"));
                        string splits = Value("shadowCascadeSplitsMeters");
                        int splitCount = string.IsNullOrEmpty(splits) ? 0 : splits.Split('|').Length;
                        Check("shadowCascadeSplitsMeters", splitCount == cascades - 1, "needs " + (cascades - 1) + " cascade splits for " + cascades + " cascades");
                        float distance = ValueConverter.ToFloat(Value("shadowDistance")), previous = 0f;
                        bool ascending = true;
                        if (splitCount > 0)
                            foreach (string split in splits.Split('|'))
                            {
                                float metres = ValueConverter.ToFloat(split.Trim());
                                ascending &= metres > previous && metres < distance;
                                previous = metres;
                            }
                        Check("shadowCascadeSplitsMeters", ascending, "cascade splits must increase and stay below shadowDistance (" + distance + " m)");
                        Check("shadowResolution", OneOf(Value("shadowResolution"), "256", "512", "1024", "2048", "4096"), "shadowResolution must be 256-4096 (power of two)");
                        break;
                    case "reflections":
                        float planar = ValueConverter.ToFloat(Value("planarScale"));
                        Check("planarScale", planar >= 0f && planar <= 1f, "planarScale must be 0 (off) to 1");
                        break;
                    case "ambient_occlusion":
                        if (ValueConverter.TryBool(Value("ssaoEnabled"), out bool ao) && ao)
                            Check("ssaoSamples", OneOf(Value("ssaoSamples"), "Low", "Medium", "High"), "ssaoSamples must be Low, Medium or High");
                        break;
                    case "anisotropic_filtering":
                        Check("anisotropicFiltering", OneOf(Value("anisotropicFiltering"), "Disable", "Enable", "ForceEnable"), "anisotropicFiltering must be Disable, Enable or ForceEnable");
                        break;
                    case "frame_rate_cap":
                        int fps = ValueConverter.ToInt(Value("frameRate"));
                        Check("frameRate", fps > 0 && fps % 60 == 0, "frameRate must be a multiple of the 60 Hz network tick (FrameRateLock)");
                        Check("frameRate", globalFps <= 0 || string.IsNullOrEmpty(Value("defaultFor")) || fps == globalFps, "a preset's default frameRate (" + fps + ") must equal Global/GlobalRule target_frame_rate (" + globalFps + ")");
                        break;
                    case "window_mode":
                        Check("fullScreenMode", OneOf(Value("fullScreenMode"), "ExclusiveFullScreen", "FullScreenWindow", "MaximizedWindow", "Windowed"), "fullScreenMode must be ExclusiveFullScreen, FullScreenWindow, MaximizedWindow or Windowed");
                        break;
                    case "vsync":
                        Check("vSyncCount", OneOf(Value("vSyncCount"), "0", "1", "2"), "vSyncCount must be 0, 1 or 2");
                        break;
                    case "vfx_quality":
                        Check("vfxTier", OneOf(Value("vfxTier"), "Low", "Medium", "High"), "vfxTier must be Low, Medium or High");
                        break;
                }
            }
        }
    }
}
#endif
