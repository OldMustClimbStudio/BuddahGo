using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BuddahGo.Rendering.Settings;
using NUnit.Framework;
using UnityEditor;

namespace BuddahGo.Tests
{
    // Graphics.xlsx, functionality and readability: every value column of GraphicsOptionLevel belongs to exactly one
    // option (whose runtime reads it), each level fills only its option's columns, and the option rows are in the
    // settings panel's order. A new column without an owner here fails, so a column nobody reads cannot slip in.
    public class GraphicsTableTests
    {
        const string DatabasePath = "Assets/Data/Config/ProjectConfigDatabase_Main.asset";

        // Option -> the GraphicsOptionLevelRecord columns its reader uses (GraphicsPipelineMapper, GraphicsSettingsApplier,
        // GraphicsSettingsApplier.VfxTierFrom). Keep in step with "Adding an option" in Docs/rendering/graphics-settings.md.
        static readonly Dictionary<string, string[]> Columns = new Dictionary<string, string[]>
        {
            ["render_scale"] = new[] { "renderScale" },
            ["upscaler"] = new[] { "upscalingFilter" },
            ["anti_aliasing"] = new[] { "msaaSamples", "postAntialiasing" },
            ["sharpening"] = new[] { "taaSharpening" },
            ["shadow_quality"] = new[] { "shadowsEnabled", "shadowDistance", "shadowCascadeCount", "shadowCascadeSplitsMeters", "shadowResolution", "softShadows" },
            ["texture_quality"] = new[] { "textureMipmapLimit" },
            ["anisotropic_filtering"] = new[] { "anisotropicFiltering" },
            ["reflections"] = new[] { "planarScale" },
            ["ambient_occlusion"] = new[] { "ssaoEnabled", "ssaoDownsample", "ssaoNormals", "ssaoSamples" },
            ["vfx_quality"] = new[] { "vfxTier", "vfxKeepPriority", "vfxEmissionScale", "vfxGameplayEmissionScale", "vfxRealtimeLights", "vfxTrailScale", "vfxInkBudget", "vfxInkCellScale" },
            ["frame_rate_cap"] = new[] { "frameRate" },
            ["window_mode"] = new[] { "fullScreenMode" },
            ["resolution"] = new string[0],
            ["vsync"] = new[] { "vSyncCount" },
        };

        static readonly string[] KeyColumns = { "optionId", "levelId", "sortOrder", "labelKey", "defaultFor" };
        static readonly string[] Groups = { "display", "quality", "effects", "performance" };

        static ProjectConfigDatabase Db() => AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(DatabasePath);

        static bool IsBlank(object value)
        {
            switch (value)
            {
                case null: return true;
                case string s: return s.Length == 0;
                case bool b: return !b;
                case int i: return i == 0;
                case float f: return f == 0f;
                case IList list: return list.Count == 0;
                default: return false;
            }
        }

        [Test]
        public void EveryLevelColumnHasExactlyOneOwningOption()
        {
            var fields = typeof(GraphicsOptionLevelRecord).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).Except(KeyColumns).ToList();
            var owned = Columns.SelectMany(kv => kv.Value).ToList();
            Assert.That(owned, Is.Unique, "a column owned by two options");
            Assert.That(fields.Except(owned), Is.Empty, "columns no option reads");
            Assert.That(owned.Except(fields), Is.Empty, "owned columns missing from the table");
            Assert.That(TableOptions().Select(o => o.optionId), Is.EquivalentTo(Columns.Keys));
        }

        [Test]
        public void EachLevelFillsOnlyItsOptionsColumns()
        {
            var fields = typeof(GraphicsOptionLevelRecord).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => !KeyColumns.Contains(f.Name)).ToList();
            EveryCase.Check(Db().graphicsOptionLevels, level =>
            {
                Assert.That(Columns.TryGetValue(level.optionId, out string[] own), Is.True, "unknown option");
                foreach (var field in fields)
                    if (!own.Contains(field.Name))
                        Assert.That(IsBlank(field.GetValue(level)), Is.True, field.Name + " belongs to another option");
            }, level => level.optionId + "/" + level.levelId);
        }

        [Test]
        public void OptionRowsFollowThePanelOrder()
        {
            var options = TableOptions();
            Assert.That(options.All(o => Groups.Contains(o.groupId)), Is.True, "unknown groupId");
            // The database keeps the sheet's row order; the panel orders by group, then sortOrder.
            var panel = options.OrderBy(o => System.Array.IndexOf(Groups, o.groupId)).ThenBy(o => o.sortOrder).Select(o => o.optionId).ToList();
            Assert.That(options.Select(o => o.optionId), Is.EqualTo(panel), "rows out of the panel's order");
            foreach (var group in options.GroupBy(o => o.groupId))
                Assert.That(group.Select(o => o.sortOrder), Is.Unique, group.Key + " has a repeated sortOrder");
            var vsync = options.First(o => o.optionId == "vsync");
            var cap = options.First(o => o.optionId == "frame_rate_cap");
            Assert.That(vsync.sortOrder, Is.LessThan(cap.sortOrder), "the frame cap depends on VSync, so it comes after it");
        }

        static List<GraphicsOptionRecord> TableOptions() => Db().graphicsOptions;
    }
}
