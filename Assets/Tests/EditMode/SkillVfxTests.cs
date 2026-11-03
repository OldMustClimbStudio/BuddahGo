using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BuddahGo.Rendering;
using BuddahGo.VFX;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.Tests
{
    // Docs/vfx/README.md sections 4, 6 and 8: phase-controller rules, the 如来神掌 prefabs against the layer sheet,
    // naming and the per-cast budget.
    public class SkillVfxTests
    {
        const string BufPrefab = "Assets/VFX/如来神掌/Buff/VFX_Palm_Buff.prefab";
        const string MainAsset = "Assets/Data/Config/ProjectConfigDatabase_Main.asset";
        static readonly Vector3 Reference = new Vector3(60f, 90f, 20f);

        [Test]
        public void TierKeepsPrioritiesAndNeverDropsGameplayInformation()
        {
            var cases = new[]
            {
                (tier: VfxQualityTier.High, priority: 3, gameplayInfo: false, function: SkillVfxFunction.Sparks, expected: true),
                (tier: VfxQualityTier.Medium, priority: 3, gameplayInfo: false, function: SkillVfxFunction.Sparks, expected: false),
                (tier: VfxQualityTier.Medium, priority: 2, gameplayInfo: false, function: SkillVfxFunction.Smoke, expected: true),
                (tier: VfxQualityTier.Low, priority: 2, gameplayInfo: false, function: SkillVfxFunction.Smoke, expected: false),
                (tier: VfxQualityTier.Low, priority: 1, gameplayInfo: false, function: SkillVfxFunction.Flash, expected: true),
                (tier: VfxQualityTier.Low, priority: 3, gameplayInfo: true, function: SkillVfxFunction.Smoke, expected: true),
                (tier: VfxQualityTier.Low, priority: 1, gameplayInfo: false, function: SkillVfxFunction.Light, expected: false),
                (tier: VfxQualityTier.High, priority: 3, gameplayInfo: false, function: SkillVfxFunction.Light, expected: true),
            };
            EveryCase.Check(cases, c =>
                Assert.That(SkillVfxRules.Keeps(c.tier, c.priority, c.gameplayInfo, c.function), Is.EqualTo(c.expected)));
        }

        [Test]
        public void RulesConvertWindowsAlphaAndScale()
        {
            EveryCase.Parts(
                ("window units convert from gameplay and table values", () =>
                {
                    SkillVfxVisualValues visual = SkillVfxVisualValues.Defaults;
                    visual.impFrames = 4;
                    visual.lightSeconds = 0.3f;
                    Assert.That(SkillVfxRules.ToSeconds(0.5f, SkillVfxWindowUnit.PhaseFraction, 2f, visual), Is.EqualTo(1f).Within(1e-5f));
                    Assert.That(SkillVfxRules.ToSeconds(1f, SkillVfxWindowUnit.ImpFrames, 0f, visual), Is.EqualTo(4f / 60f).Within(1e-5f));
                    Assert.That(SkillVfxRules.ToSeconds(1f, SkillVfxWindowUnit.LightSeconds, 0f, visual), Is.EqualTo(0.3f).Within(1e-5f));
                    Assert.That(SkillVfxRules.ToSeconds(0.35f, SkillVfxWindowUnit.Seconds, 9f, visual), Is.EqualTo(0.35f));
                }),
                ("the window alpha fades inside the window only", () =>
                {
                    Assert.That(SkillVfxRules.WindowAlpha(-0.1f, 0f, 1f, 0.3f, 0.3f), Is.EqualTo(0f));
                    Assert.That(SkillVfxRules.WindowAlpha(0.15f, 0f, 1f, 0.3f, 0.3f), Is.EqualTo(0.5f).Within(1e-5f));
                    Assert.That(SkillVfxRules.WindowAlpha(0.5f, 0f, 1f, 0.3f, 0.3f), Is.EqualTo(1f));
                    Assert.That(SkillVfxRules.WindowAlpha(0.85f, 0f, 1f, 0.3f, 0.3f), Is.EqualTo(0.5f).Within(1e-5f));
                    Assert.That(SkillVfxRules.WindowAlpha(1f, 0f, 1f, 0.3f, 0.3f), Is.EqualTo(0f));
                }),
                ("the body follows the collider at its own proportions", () =>
                {
                    Assert.That(SkillVfxRules.ContainScale(Reference, Reference), Is.EqualTo(1f).Within(1e-5f));
                    Assert.That(SkillVfxRules.ContainScale(Reference * 2f, Reference), Is.EqualTo(2f).Within(1e-5f));
                    // The anti volley's 60 × 50 × 20 collider: the palm doc's "small hand, about 0.6".
                    Assert.That(SkillVfxRules.ContainScale(new Vector3(60f, 50f, 20f), Reference), Is.EqualTo(50f / 90f).Within(1e-5f));
                    Assert.That(SkillVfxRules.UniformScale(new Vector3(60f, 50f, 20f), Reference), Is.EqualTo(50f / 90f).Within(1e-5f));
                }));
        }

        // README section 8 (2026-10-31): particle systems per slot prefab, empty roots included. The hand-tuned
        // Piloto-based slots keep their current count as the ceiling; every other slot keeps the original 10.
        static readonly Dictionary<string, int> ParticleSystemCeilings = new Dictionary<string, int>
        {
            { "cast_anti", 47 }, { "palm_release", 25 }, { "palm_release_anti", 25 },
            { "cast", 21 }, { "reverse_hit", 19 }, { "accel_release", 15 },
        };
        const int DefaultParticleSystemCeiling = 10;

        [Test]
        public void EverySlotStaysWithinItsParticleSystemCeilingAndUsesNoRealtimeLight()
        {
            EveryCase.Parts(
                ("every slot stays within its particle system ceiling and uses no realtime light", () =>
                {
                    var database = AssetDatabase.LoadAssetAtPath<SkillVfxDatabase>("Assets/Skills_Assets/SkillVfxDatabase.asset");
                    Assert.That(database.entries.Select(e => e.vfxId), Is.SupersetOf(ParticleSystemCeilings.Keys), "ceilings of retired ids");

                    var overruns = new List<string>();
                    foreach (SkillVfxDatabase.Entry entry in database.entries)
                    {
                        Assert.That(entry.prefab, Is.Not.Null, entry.vfxId);
                        int ceiling = ParticleSystemCeilings.TryGetValue(entry.vfxId, out int listed) ? listed : DefaultParticleSystemCeiling;
                        int systems = entry.prefab.GetComponentsInChildren<ParticleSystem>(true).Length;
                        if (systems > ceiling)
                            overruns.Add(entry.vfxId + ": " + systems + " particle systems > " + ceiling);
                        int lights = entry.prefab.GetComponentsInChildren<Light>(true).Length;
                        if (lights > 0)
                            overruns.Add(entry.vfxId + ": " + lights + " realtime lights");
                    }
                    Assert.That(overruns, Is.Empty);
                }),
                // README section 8: no shadows and light meshes. The other slots' particle totals and shadow flags wait for
                // the user's decision (README section 8, 2026-10-31 inventory).
                ("the buff prefab casts and receives no shadows", () =>
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BufPrefab);
                    foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (filter.sharedMesh != null)
                            Assert.That(filter.sharedMesh.triangles.Length / 3, Is.LessThanOrEqualTo(3000), filter.name);
                    }

                    foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    {
                        Assert.That(renderer.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off), renderer.name);
                        Assert.That(renderer.receiveShadows, Is.False, renderer.name);
                    }
                }));
        }

        [Test]
        public void VfxTableRowsPairAntiEffectsAndPointAtTheirPrefabs()
        {
            EveryCase.Parts(
                ("anti rows pair with their normal row", () =>
                {
                    var database = AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(MainAsset);
                    List<VfxRegistryRecord> rows = database.vfxRegistries;
                    var ids = new HashSet<string>(rows.Select(r => r.vfxId));
                    foreach (VfxRegistryRecord row in rows)
                    {
                        if (!string.IsNullOrEmpty(row.antiOf))
                        {
                            Assert.That(ids.Contains(row.antiOf), Is.True, row.vfxId + " antiOf " + row.antiOf);
                            Assert.That(row.vfxId, Is.EqualTo(row.antiOf + "_anti"), "anti naming");
                        }
                    }

                    foreach (string id in new[] { "palm_release", "palm_release_anti", "palm_buff" })
                    {
                        VfxRegistryRecord row = rows.Single(r => r.vfxId == id);
                        Assert.That(row.linSeconds, Is.LessThanOrEqualTo(1.5f), "README section 8: residue <= 1.5 s");
                        Assert.That(row.lightSeconds, Is.LessThanOrEqualTo(0.5f), "README section 8: launch light <= 0.5 s");
                        Assert.That(row.impFrames, Is.InRange(2, 4), "flash 2-4 frames");
                    }
                }),
                // README section 6 (2026-10-30): vfx_id = <skill>_<slot>[_anti] (the shared wind-up only cast / cast_anti),
                // the prefab is Assets/VFX/<技能中文名>/<Slot>/VFX_<Skill>_<Slot>[_Anti].prefab, and the slot controller
                // carries the same id. The VFX table and SkillVfxDatabase list the same ids and the same prefabs.
                ("every vfx id follows the naming rule and loads its prefab", () =>
                {
                    var database = AssetDatabase.LoadAssetAtPath<SkillVfxDatabase>("Assets/Skills_Assets/SkillVfxDatabase.asset");
                    List<VfxRegistryRecord> rows = AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(MainAsset).vfxRegistries;
                    Assert.That(rows.Select(r => r.vfxId), Is.EquivalentTo(database.entries.Select(e => e.vfxId)),
                        "VFX table and SkillVfxDatabase ids");

                    foreach (VfxRegistryRecord row in rows)
                    {
                        System.Text.RegularExpressions.Match match = VfxIdRule.Match(row.vfxId);
                        Assert.That(match.Success, Is.True, "vfx_id '" + row.vfxId + "' is not <skill>_<slot>[_anti]");
                        bool cast = match.Groups[1].Success;
                        (string folder, string name) skill = SkillFolders[cast ? "cast" : match.Groups[2].Value];
                        string slot = cast ? "Cast" : char.ToUpperInvariant(match.Groups[3].Value[0]) + match.Groups[3].Value.Substring(1);
                        string expectedPath = "Assets/VFX/" + skill.folder + "/" + slot + "/VFX_" + skill.name + "_" + slot
                            + (match.Groups[4].Success ? "_Anti" : "") + ".prefab";
                        Assert.That(row.prefabPath, Is.EqualTo(expectedPath), row.vfxId);

                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(row.prefabPath);
                        Assert.That(prefab, Is.Not.Null, row.prefabPath);
                        Assert.That(database.TryGetPrefab(row.vfxId, out GameObject listed) ? listed : null, Is.SameAs(prefab), row.vfxId);
                        var controller = prefab.GetComponent<SkillVfxPhaseController>();
                        // Bullets are projectiles that may carry a slot controller; every other slot is one.
                        if (match.Groups[3].Value != "bullet")
                            Assert.That(controller, Is.Not.Null, row.vfxId + " is not a slot prefab");
                        if (controller != null)
                            Assert.That(controller.VfxId, Is.EqualTo(row.vfxId), row.prefabPath);
                    }
                }));
        }

        static readonly Regex VfxIdRule = new Regex(@"^(?:(cast)|(palm|accel|slow|reverse|curtain|giant)_(release|bullet|hit|buff))(_anti)?$");

        static readonly Dictionary<string, (string folder, string name)> SkillFolders = new Dictionary<string, (string, string)>
        {
            ["cast"] = ("通用前摇", "Common"),
            ["palm"] = ("如来神掌", "Palm"),
            ["accel"] = ("神足通", "Accel"),
            ["slow"] = ("禅定缓流", "Slow"),
            ["reverse"] = ("转向反转", "Reverse"),
            ["curtain"] = ("一念光明", "Curtain"),
            ["giant"] = ("巨大化", "Giant"),
        };

        // README section 5: an empty (0) cell of the VFX table falls back to the default look instead of collapsing the
        // effect; residue, light and buff fade pass through as written (0 there means none).
        [Test]
        public void EmptyTableCellsFallBackToTheDefaults()
        {
            SkillVfxVisualValues defaults = SkillVfxVisualValues.Defaults;
            SkillVfxVisualValues empty = SkillVfxTable.FromRecord(new VfxRegistryRecord { vfxId = "empty" });
            Assert.That(empty.chgRatio, Is.EqualTo(defaults.chgRatio));
            Assert.That(empty.impFrames, Is.EqualTo(defaults.impFrames));
            Assert.That(empty.disSeconds, Is.EqualTo(defaults.disSeconds));
            Assert.That(empty.trailSeconds, Is.EqualTo(defaults.trailSeconds));
            Assert.That(empty.scaleMultiplier, Is.EqualTo(defaults.scaleMultiplier));
            Assert.That(empty.linSeconds, Is.EqualTo(0f));
            Assert.That(empty.lightSeconds, Is.EqualTo(0f));
            Assert.That(empty.bufFadeSeconds, Is.EqualTo(0f));

            SkillVfxVisualValues written = SkillVfxTable.FromRecord(new VfxRegistryRecord
            {
                vfxId = "written", chgRatio = 0.6f, impFrames = 3, disSeconds = 0.8f, trailSeconds = 0.5f,
                scaleMultiplier = 1.2f, linSeconds = 1f, lightSeconds = 0.2f, lightIntensity = 2f, bufFadeSeconds = 0.25f,
            });
            Assert.That(written.chgRatio, Is.EqualTo(0.6f));
            Assert.That(written.impFrames, Is.EqualTo(3));
            Assert.That(written.disSeconds, Is.EqualTo(0.8f));
            Assert.That(written.trailSeconds, Is.EqualTo(0.5f));
            Assert.That(written.scaleMultiplier, Is.EqualTo(1.2f));
            Assert.That(written.linSeconds, Is.EqualTo(1f));
            Assert.That(written.lightSeconds, Is.EqualTo(0.2f));
            Assert.That(written.lightIntensity, Is.EqualTo(2f));
            Assert.That(written.bufFadeSeconds, Is.EqualTo(0.25f));
        }

        // README 4: the effect of one cast (charge + flight + dissipation) fits in the cast lock. The buff state layer
        // runs for the buff's length and is outside this rule (paper confirmation Q1, 2026-10-28).
        [Test]
        public void OneCastOfTheEffectFitsInTheCastLock()
        {
            var cases = new[]
            {
                (skillId: "push_projectile_hands", pushId: "charged", vfxId: "palm_release"),
                (skillId: "push_projectile_hands_anti", pushId: "charged_anti", vfxId: "palm_release_anti"),
            };
            EveryCase.Check(cases, c =>
            {
                var database = AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(MainAsset);
                SkillRecord skill = database.skills.Single(s => s.skillId == c.skillId);
                PushRecord push = database.pushes.Single(p => p.pushId == c.pushId);
                VfxRegistryRecord vfx = database.vfxRegistries.Single(r => r.vfxId == c.vfxId);
                float total = push.windupSeconds + push.lifetimeSeconds + vfx.disSeconds;
                Assert.That(total, Is.LessThanOrEqualTo(skill.castLockSeconds), c.skillId);
            });
        }
    }
}
