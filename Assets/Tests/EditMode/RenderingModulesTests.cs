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
    // Docs/rendering/graphics-settings.md, "Modules": every quality level renders the gameplay passes (the 一念光明
    // curtain covers the map, the passes after it draw what a player may still see), and post volumes hold what their
    // Feel shakers drive.
    public class RenderingModulesTests
    {
        const string DatabasePath = "Assets/Data/Config/ProjectConfigDatabase_Main.asset";

        static GraphicsSettingsCatalog TableCatalog() =>
            GraphicsSettingsCatalog.FromDatabase(AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(DatabasePath));

        [Test]
        public void EveryQualityLevelRendersTheGameplayPasses()
        {
            Assert.That(QualitySettings.names.Length, Is.GreaterThanOrEqualTo(3));
            var problems = RenderingModules.CheckQualityLevels();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void RendererContractReportsWhatIsMissing()
        {
            var data = ScriptableObject.CreateInstance<UnityEngine.Rendering.Universal.UniversalRendererData>();
            try
            {
                var problems = RenderingModules.Violations(data);
                foreach (string feature in RenderingModules.GameplayFeatures)
                    Assert.That(problems, Has.Some.Contains("missing " + feature));
                Assert.That(RenderingModules.Violations(null), Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void PostVolumesHoldWhatTheirFeelShakersDrive()
        {
            var shakers = new Dictionary<string, string>
            {
                ["MMWhiteBalanceShaker_URP"] = "WhiteBalance",
                ["MMBloomShaker_URP"] = "Bloom",
                ["MMColorAdjustmentsShaker_URP"] = "ColorAdjustments",
                ["MMVignetteShaker_URP"] = "Vignette",
                ["MMChromaticAberrationShaker_URP"] = "ChromaticAberration",
                ["MMLensDistortionShaker_URP"] = "LensDistortion",
                ["MMChannelMixerShaker_URP"] = "ChannelMixer",
                ["MMDepthOfFieldShaker_URP"] = "DepthOfField",
                ["MMMotionBlurShaker_URP"] = "MotionBlur",
                ["MMPaniniProjectionShaker_URP"] = "PaniniProjection",
            };
            var byGuid = new Dictionary<string, string>();
            foreach (var kv in shakers)
                foreach (string guid in AssetDatabase.FindAssets(kv.Key + " t:MonoScript"))
                    if (Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) == kv.Key)
                        byGuid[guid] = kv.Value;
            Assert.That(byGuid, Is.Not.Empty, "Feel's URP shakers");

            var block = new Regex(@"--- !u!114 &\d+\s*\nMonoBehaviour:(?<body>.*?)(?=\n--- |\z)", RegexOptions.Singleline);
            var gameObject = new Regex(@"m_GameObject: \{fileID: (\d+)\}");
            var script = new Regex(@"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})");
            var profile = new Regex(@"sharedProfile: \{fileID: \d+, guid: ([0-9a-f]{32})");
            int checkedShakers = 0;
            var failures = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                string text = File.ReadAllText(scene.path);
                var profiles = new Dictionary<string, string>();
                var needs = new List<(string go, string component)>();
                foreach (System.Text.RegularExpressions.Match m in block.Matches(text))
                {
                    string body = m.Groups["body"].Value;
                    var go = gameObject.Match(body);
                    var s = script.Match(body);
                    if (!go.Success || !s.Success)
                        continue;
                    var p = profile.Match(body);
                    if (p.Success)
                        profiles[go.Groups[1].Value] = p.Groups[1].Value;
                    if (byGuid.TryGetValue(s.Groups[1].Value, out string component))
                        needs.Add((go.Groups[1].Value, component));
                }
                foreach (var (go, component) in needs)
                {
                    checkedShakers++;
                    if (!profiles.TryGetValue(go, out string profileGuid))
                    {
                        failures.Add(scene.path + ": a " + component + " shaker has no Volume profile on its object");
                        continue;
                    }
                    var asset = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(profileGuid));
                    if (asset == null || !asset.components.Any(c => c != null && c.GetType().Name == component))
                        failures.Add(scene.path + ": " + (asset != null ? asset.name : profileGuid) + " has no " + component + " for its shaker");
                }
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
            Assert.That(checkedShakers, Is.GreaterThan(0), "RaceMap's post volumes carry Feel shakers");
        }
    }
}
