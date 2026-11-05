using System;
using System.Collections.Generic;
using System.Linq;

namespace BuddahGo.Rendering.Settings
{
    // The graphics-settings spec read from the design tables (Graphics.xlsx: GraphicsPreset, GraphicsOption,
    // GraphicsOptionLevel). Options list their levels low to high; each preset names one default level per option.
    // An empty catalog (no table rows) means the settings layer does nothing and the game keeps today's behaviour.
    public sealed class GraphicsSettingsCatalog
    {
        public sealed class Level
        {
            public string Id;
            public string LabelKey;
            public int SortOrder;
            public GraphicsOptionLevelRecord Values;
        }

        public sealed class Option
        {
            public string Id;
            public string LabelKey;
            public string GroupId;
            public int SortOrder;
            public bool RestartRequired;
            public IReadOnlyList<Level> Levels;

            public Level Find(string levelId) => Levels.FirstOrDefault(l => l.Id == levelId);
        }

        public sealed class Preset
        {
            public string Id;
            public string LabelKey;
            public string QualityLevel;
            public int SortOrder;
            public bool IsDefault;
            internal readonly Dictionary<string, string> Defaults = new Dictionary<string, string>(StringComparer.Ordinal);

            public string DefaultLevel(string optionId) => Defaults.TryGetValue(optionId, out var level) ? level : null;
        }

        public const string CustomLabelKey = "settings.graphics.preset.custom";

        public IReadOnlyList<Option> Options { get; }
        public IReadOnlyList<Preset> Presets { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool IsEmpty => Options.Count == 0 || Presets.Count == 0;

        GraphicsSettingsCatalog(List<Option> options, List<Preset> presets, List<string> errors)
        {
            Options = options;
            Presets = presets;
            Errors = errors;
        }

        public Option FindOption(string optionId) => Options.FirstOrDefault(o => o.Id == optionId);

        /// <summary>Level id of the <c>resolution</c> option for a display size, e.g. "1920x1080".</summary>
        public static string ResolutionLevelId(int width, int height) => width + "x" + height;

        /// <summary>
        /// Returns a catalog whose <c>resolution</c> option lists, after the table's own levels (<c>native</c>), one
        /// level per distinct display size, largest first. The table cannot know the player's display, so the menu gets
        /// these at startup from <c>Screen.resolutions</c>; the level id carries the size and the applier parses it. The
        /// table levels keep their label keys; a generated level has none and shows its id. A saved choice naming a
        /// size the display no longer offers is dropped like any stale level.
        /// </summary>
        public GraphicsSettingsCatalog WithDisplayResolutions(IEnumerable<(int width, int height)> resolutions)
        {
            var option = FindOption(GraphicsPipelineMapper.Resolution);
            if (option == null || resolutions == null)
                return this;
            var sizes = resolutions.Where(r => r.width > 0 && r.height > 0).Distinct()
                .OrderByDescending(r => (long)r.width * r.height).ThenByDescending(r => r.width).ToList();
            var levels = option.Levels.ToList();
            foreach (var size in sizes)
            {
                string id = ResolutionLevelId(size.width, size.height);
                if (levels.Any(l => l.Id == id))
                    continue;
                levels.Add(new Level
                {
                    Id = id,
                    LabelKey = string.Empty,
                    SortOrder = levels.Count,
                    Values = new GraphicsOptionLevelRecord { optionId = option.Id, levelId = id, sortOrder = levels.Count, labelKey = string.Empty },
                });
            }
            var options = Options.Select(o => o == option
                ? new Option { Id = o.Id, LabelKey = o.LabelKey, GroupId = o.GroupId, SortOrder = o.SortOrder, RestartRequired = o.RestartRequired, Levels = levels }
                : o).ToList();
            return new GraphicsSettingsCatalog(options, Presets.ToList(), Errors.ToList());
        }
        public Preset FindPreset(string presetId) => Presets.FirstOrDefault(p => p.Id == presetId);
        public Preset DefaultPreset => Presets.FirstOrDefault(p => p.IsDefault) ?? Presets.LastOrDefault();

        public static GraphicsSettingsCatalog FromDatabase(ProjectConfigDatabase db)
        {
            return db == null
                ? FromRecords(null, null, null)
                : FromRecords(db.graphicsPresets, db.graphicsOptions, db.graphicsOptionLevels);
        }

        public static GraphicsSettingsCatalog FromRecords(IEnumerable<GraphicsPresetRecord> presetRows,
            IEnumerable<GraphicsOptionRecord> optionRows, IEnumerable<GraphicsOptionLevelRecord> levelRows)
        {
            var errors = new List<string>();
            var levelsByOption = (levelRows ?? Enumerable.Empty<GraphicsOptionLevelRecord>())
                .Where(r => r != null && !string.IsNullOrEmpty(r.optionId))
                .GroupBy(r => r.optionId)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.sortOrder).ToList());

            var options = new List<Option>();
            foreach (var row in (optionRows ?? Enumerable.Empty<GraphicsOptionRecord>()).Where(r => r != null && !string.IsNullOrEmpty(r.optionId)).OrderBy(r => r.sortOrder))
            {
                if (options.Any(o => o.Id == row.optionId))
                {
                    errors.Add("duplicate option " + row.optionId);
                    continue;
                }
                levelsByOption.TryGetValue(row.optionId, out var levelList);
                var levels = (levelList ?? new List<GraphicsOptionLevelRecord>()).Select(l => new Level
                {
                    Id = l.levelId,
                    LabelKey = l.labelKey,
                    SortOrder = l.sortOrder,
                    Values = l,
                }).ToList();
                if (levels.Count == 0)
                    errors.Add("option " + row.optionId + " has no levels");
                if (levels.Select(l => l.Id).Distinct().Count() != levels.Count)
                    errors.Add("option " + row.optionId + " has duplicate level ids");
                options.Add(new Option
                {
                    Id = row.optionId,
                    LabelKey = row.labelKey,
                    GroupId = row.groupId,
                    SortOrder = row.sortOrder,
                    RestartRequired = row.restartRequired,
                    Levels = levels,
                });
            }
            foreach (var optionId in levelsByOption.Keys.Where(id => options.All(o => o.Id != id)))
                errors.Add("levels for unknown option " + optionId);

            var presets = new List<Preset>();
            foreach (var row in (presetRows ?? Enumerable.Empty<GraphicsPresetRecord>()).Where(r => r != null && !string.IsNullOrEmpty(r.presetId)).OrderBy(r => r.sortOrder))
            {
                var preset = new Preset
                {
                    Id = row.presetId,
                    LabelKey = row.labelKey,
                    QualityLevel = row.qualityLevel,
                    SortOrder = row.sortOrder,
                    IsDefault = row.isDefault,
                };
                foreach (var option in options)
                {
                    var defaults = option.Levels.Where(l => l.Values.defaultFor != null && l.Values.defaultFor.Contains(preset.Id)).ToList();
                    if (defaults.Count == 1)
                        preset.Defaults[option.Id] = defaults[0].Id;
                    else
                    {
                        errors.Add("preset " + preset.Id + " has " + defaults.Count + " default levels for " + option.Id);
                        if (option.Levels.Count > 0)
                            preset.Defaults[option.Id] = (defaults.Count > 0 ? defaults[0] : option.Levels[option.Levels.Count - 1]).Id;
                    }
                }
                presets.Add(preset);
            }
            if (presets.Count(p => p.IsDefault) > 1)
                errors.Add("more than one default preset");
            return new GraphicsSettingsCatalog(options, presets, errors);
        }
    }
}
