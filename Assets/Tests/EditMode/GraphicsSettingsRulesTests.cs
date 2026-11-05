using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BuddahGo.Rendering;
using BuddahGo.Rendering.Settings;
using BuddahGo.VFX;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuddahGo.Tests
{
    // Docs/rendering/graphics-settings.md, "Restart and availability": restart flags match the play-mode check, the
    // restart notice lists only restart options changed since startup, and options grey out when another makes them moot.
    public class GraphicsSettingsRulesTests
    {
        const string DatabasePath = "Assets/Data/Config/ProjectConfigDatabase_Main.asset";

        static GraphicsSettingsCatalog TableCatalog() =>
            GraphicsSettingsCatalog.FromDatabase(AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(DatabasePath));

        // Options verified on 2026-11-05 to need a restart (play mode: each option stepped through its levels and
        // checked on the frame after Apply; see the doc's restart column). None today: all apply live.
        static readonly string[] RestartOptions = { };

        [Test]
        public void RestartFlagsMatchThePlayModeCheck()
        {
            var flagged = TableCatalog().Options.Where(o => o.RestartRequired).Select(o => o.Id);
            Assert.That(flagged, Is.EquivalentTo(RestartOptions));
        }

        [Test]
        public void OnlyRestartOptionsChangedSinceStartupNeedARestart()
        {
            var presets = new List<GraphicsPresetRecord> { new GraphicsPresetRecord { presetId = "high", qualityLevel = "High Fidelity", isDefault = true } };
            var options = new List<GraphicsOptionRecord>
            {
                new GraphicsOptionRecord { optionId = "texture_quality", sortOrder = 0, restartRequired = true },
                new GraphicsOptionRecord { optionId = "vsync", sortOrder = 1 },
            };
            var levels = new List<GraphicsOptionLevelRecord>
            {
                new GraphicsOptionLevelRecord { optionId = "texture_quality", levelId = "half", sortOrder = 0 },
                new GraphicsOptionLevelRecord { optionId = "texture_quality", levelId = "full", sortOrder = 1, defaultFor = new List<string> { "high" } },
                new GraphicsOptionLevelRecord { optionId = "vsync", levelId = "off", sortOrder = 0 },
                new GraphicsOptionLevelRecord { optionId = "vsync", levelId = "on", sortOrder = 1, defaultFor = new List<string> { "high" } },
            };
            var catalog = GraphicsSettingsCatalog.FromRecords(presets, options, levels);
            var model = new GraphicsSettingsModel(catalog);
            string startup = model.ToJson();
            model.Set("vsync", "off");
            Assert.That(GraphicsSettingsService.RestartDifferences(catalog, startup, model.ToJson()), Is.Empty, "a live option");
            model.Set("texture_quality", "half");
            Assert.That(GraphicsSettingsService.RestartDifferences(catalog, startup, model.ToJson()), Is.EqualTo(new[] { "texture_quality" }));
            model.Set("texture_quality", "full");
            Assert.That(GraphicsSettingsService.RestartDifferences(catalog, startup, model.ToJson()), Is.Empty, "back to the startup level");
        }

        [Test]
        public void OptionsGreyOutWhenAnotherOptionMakesThemMoot()
        {
            var catalog = TableCatalog();
            GraphicsOptionLevelRecord L(string option, string level) => catalog.FindOption(option).Find(level).Values;
            var levels = new Dictionary<string, GraphicsOptionLevelRecord>
            {
                ["render_scale"] = L("render_scale", "p100"),
                ["anti_aliasing"] = L("anti_aliasing", "fxaa"),
                ["vsync"] = L("vsync", "on"),
            };
            EveryCase.Check(new[]
            {
                (option: "upscaler", inactive: true),
                (option: "sharpening", inactive: true),
                (option: "frame_rate_cap", inactive: true),
                (option: "shadow_quality", inactive: false),
            }, c => Assert.That(GraphicsOptionDependencies.InactiveBecause(levels, c.option) != null, Is.EqualTo(c.inactive)), c => c.option);
            levels["render_scale"] = L("render_scale", "p77");
            levels["anti_aliasing"] = L("anti_aliasing", "taa");
            levels["vsync"] = L("vsync", "off");
            foreach (string option in new[] { "upscaler", "sharpening", "frame_rate_cap" })
                Assert.That(GraphicsOptionDependencies.InactiveBecause(levels, option), Is.Null, option);
        }
    }
}
