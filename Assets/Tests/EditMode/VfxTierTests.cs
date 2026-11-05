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
    // Docs/rendering/graphics-settings.md, "VFX tiers": the vfx_quality table is complete, keeps gameplay effects
    // readable, thins detail particles and scales the ink smoke within its box.
    public class VfxTierTests
    {
        const string DatabasePath = "Assets/Data/Config/ProjectConfigDatabase_Main.asset";

        static GraphicsSettingsCatalog TableCatalog() =>
            GraphicsSettingsCatalog.FromDatabase(AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(DatabasePath));

        [Test]
        public void VfxTierTableIsCompleteAndKeepsGameplayEffectsReadable()
        {
            var option = TableCatalog().FindOption(GraphicsPipelineMapper.VfxQuality);
            Assert.That(option, Is.Not.Null);
            var tiers = new Dictionary<VfxQualityTier, VfxTierSettings>();
            foreach (var level in option.Levels)
            {
                Assert.That(System.Enum.TryParse(level.Values.vfxTier, out VfxQualityTier tier), Is.True, level.Id);
                tiers[tier] = GraphicsSettingsApplier.VfxTierFrom(level.Values);
            }
            Assert.That(tiers.Keys, Is.EquivalentTo(new[] { VfxQualityTier.Low, VfxQualityTier.Medium, VfxQualityTier.High }));
            EveryCase.Parts(
                ("gameplay layers are kept at nearly full emission on every tier", () =>
                {
                    foreach (var kv in tiers)
                    {
                        Assert.That(kv.Value.GameplayEmissionScale, Is.GreaterThanOrEqualTo(0.7f), kv.Key.ToString());
                        Assert.That(SkillVfxRules.Keeps(kv.Value, 3, true, SkillVfxFunction.Smoke), Is.True, kv.Key.ToString());
                        Assert.That(SkillVfxRules.TrailSeconds(kv.Value, true, 0.25f), Is.EqualTo(0.25f), kv.Key.ToString());
                    }
                }),
                ("tiers grow from Low to High", () =>
                {
                    var low = tiers[VfxQualityTier.Low];
                    var medium = tiers[VfxQualityTier.Medium];
                    var high = tiers[VfxQualityTier.High];
                    Assert.That(new[] { low.KeepPriority, medium.KeepPriority, high.KeepPriority }, Is.Ordered);
                    Assert.That(new[] { low.EmissionScale, medium.EmissionScale, high.EmissionScale }, Is.Ordered);
                    Assert.That(new[] { low.TrailScale, medium.TrailScale, high.TrailScale }, Is.Ordered);
                    Assert.That(new[] { low.InkFluidBudget, medium.InkFluidBudget, high.InkFluidBudget }, Is.Ordered);
                    Assert.That(new[] { high.InkFluidCellScale, medium.InkFluidCellScale, low.InkFluidCellScale }, Is.Ordered);
                    Assert.That(high.KeepPriority, Is.EqualTo(3));
                    Assert.That(high.EmissionScale, Is.EqualTo(1f));
                    Assert.That(high.InkFluidCellScale, Is.EqualTo(1f));
                }),
                ("the layer rules match the pre-table fallback", () =>
                {
                    // The table took over the rules SkillVfxRules had in code; without tables the fallback still applies.
                    foreach (var kv in tiers)
                    {
                        var fallback = VfxTierSettings.Fallback(kv.Key);
                        Assert.That(kv.Value.KeepPriority, Is.EqualTo(fallback.KeepPriority), kv.Key.ToString());
                        Assert.That(kv.Value.EmissionScale, Is.EqualTo(fallback.EmissionScale).Within(1e-4f), kv.Key.ToString());
                        Assert.That(kv.Value.RealtimeLights, Is.EqualTo(fallback.RealtimeLights), kv.Key.ToString());
                    }
                }),
                ("the high tier keeps the prefab's ink grid and budget", () =>
                {
                    var high = tiers[VfxQualityTier.High];
                    InkFluidVolume.TierGrid(new Vector3Int(80, 80, 192), 2f, high.InkFluidCellScale, out var grid, out float cell);
                    Assert.That(grid, Is.EqualTo(new Vector3Int(80, 80, 192)));
                    Assert.That(cell, Is.EqualTo(2f));
                    Assert.That(high.InkFluidBudget, Is.EqualTo(8), "VFX_Palm_Bullet's simulationBudget");
                }));
        }

        [Test]
        public void DetailSystemsThinOutWhileGameplayShapesStay()
        {
            Assert.That(SkillVfxRules.ParticlesPerCycle(1f, 0f, 1f), Is.LessThanOrEqualTo(SkillVfxRules.DetailParticleThreshold), "one big sprite");
            Assert.That(SkillVfxRules.ParticlesPerCycle(3f, 0f, 1f), Is.LessThanOrEqualTo(SkillVfxRules.DetailParticleThreshold), "the pack's 1-3 sprites");
            Assert.That(SkillVfxRules.ParticlesPerCycle(0f, 30f, 1f), Is.GreaterThan(SkillVfxRules.DetailParticleThreshold), "sparks");
            var low = VfxTierSettings.Fallback(VfxQualityTier.Low);
            low.GameplayEmissionScale = 1f;
            Assert.That(SkillVfxRules.EmissionScale(low, true), Is.EqualTo(1f), "gameplay shape");
            Assert.That(SkillVfxRules.DetailEmissionScale(low), Is.EqualTo(0.4f).Within(1e-5f), "detail on any layer");

            // Every slot prefab with particles keeps at least one shape system (or a scaling zone's area), so Low still
            // draws each effect's outline at full count.
            var registry = AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(DatabasePath);
            var failures = new List<string>();
            int withParticles = 0;
            foreach (var row in registry.vfxRegistries)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(row.prefabPath);
                if (prefab == null)
                    continue;
                var systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
                if (systems.Length == 0)
                    continue;
                withParticles++;
                var areas = new HashSet<ParticleSystem>(prefab.GetComponentsInChildren<SkillVfxLayer>(true)
                    .Where(l => l.areaSystems != null).SelectMany(l => l.areaSystems).Where(a => a != null));
                if (!systems.Any(ps => areas.Contains(ps) || !SkillVfxRules.IsDetailSystem(ps)))
                    failures.Add(row.vfxId + " has only detail systems");
            }
            Assert.That(withParticles, Is.GreaterThan(0));
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void InkGridKeepsItsBoxAndThreadGroupMultiples()
        {
            InkFluidVolume.TierGrid(new Vector3Int(80, 80, 192), 2f, 1.5f, out var grid, out float cell);
            Assert.That(grid, Is.EqualTo(new Vector3Int(56, 56, 128)), "the measured cell-3 grid");
            Assert.That(cell, Is.EqualTo(3f));
            InkFluidVolume.TierGrid(new Vector3Int(80, 80, 192), 2f, 1.25f, out grid, out cell);
            Assert.That(grid.x % 8, Is.Zero);
            Assert.That(grid.z % 4, Is.Zero);
            Assert.That(grid.z * cell, Is.GreaterThanOrEqualTo(192 * 2f), "the box never shrinks");
            Assert.That(InkFluidVolume.TierBudget(new VfxTierSettings { InkFluidBudget = -1 }), Is.EqualTo(int.MaxValue));
            Assert.That(InkFluidVolume.TierBudget(new VfxTierSettings { InkFluidBudget = 2 }), Is.EqualTo(2));
        }
    }
}
