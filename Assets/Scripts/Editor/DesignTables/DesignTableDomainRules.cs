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
            GraphicsSettingsAreComplete(set, issues);
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
