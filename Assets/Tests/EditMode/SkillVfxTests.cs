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

        [TestCase(VfxQualityTier.High, 3, false, SkillVfxFunction.Sparks, true)]
        [TestCase(VfxQualityTier.Medium, 3, false, SkillVfxFunction.Sparks, false)]
        [TestCase(VfxQualityTier.Medium, 2, false, SkillVfxFunction.Smoke, true)]
        [TestCase(VfxQualityTier.Low, 2, false, SkillVfxFunction.Smoke, false)]
        [TestCase(VfxQualityTier.Low, 1, false, SkillVfxFunction.Flash, true)]
        [TestCase(VfxQualityTier.Low, 3, true, SkillVfxFunction.Smoke, true)]
        [TestCase(VfxQualityTier.Low, 1, false, SkillVfxFunction.Light, false)]
        [TestCase(VfxQualityTier.High, 3, false, SkillVfxFunction.Light, true)]
        public void TierKeepsPrioritiesAndNeverDropsGameplayInformation(VfxQualityTier tier, int priority, bool gameplayInfo,
            SkillVfxFunction function, bool expected)
        {
            Assert.That(SkillVfxRules.Keeps(tier, priority, gameplayInfo, function), Is.EqualTo(expected));
        }

        [Test]
        public void EmissionScalesFollowTheTiers()
        {
            Assert.That(SkillVfxRules.EmissionScale(VfxQualityTier.Low), Is.EqualTo(0.4f));
            Assert.That(SkillVfxRules.EmissionScale(VfxQualityTier.Medium), Is.EqualTo(0.7f));
            Assert.That(SkillVfxRules.EmissionScale(VfxQualityTier.High), Is.EqualTo(1f));
        }

        [Test]
        public void WindowUnitsConvertFromGameplayAndTableValues()
        {
            SkillVfxVisualValues visual = SkillVfxVisualValues.Defaults;
            visual.impFrames = 4;
            visual.lightSeconds = 0.3f;
            Assert.That(SkillVfxRules.ToSeconds(0.5f, SkillVfxWindowUnit.PhaseFraction, 2f, visual), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(SkillVfxRules.ToSeconds(1f, SkillVfxWindowUnit.ImpFrames, 0f, visual), Is.EqualTo(4f / 60f).Within(1e-5f));
            Assert.That(SkillVfxRules.ToSeconds(1f, SkillVfxWindowUnit.LightSeconds, 0f, visual), Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(SkillVfxRules.ToSeconds(0.35f, SkillVfxWindowUnit.Seconds, 9f, visual), Is.EqualTo(0.35f));
        }

        [Test]
        public void WindowAlphaFadesInsideTheWindowOnly()
        {
            Assert.That(SkillVfxRules.WindowAlpha(-0.1f, 0f, 1f, 0.3f, 0.3f), Is.EqualTo(0f));
            Assert.That(SkillVfxRules.WindowAlpha(0.15f, 0f, 1f, 0.3f, 0.3f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(SkillVfxRules.WindowAlpha(0.5f, 0f, 1f, 0.3f, 0.3f), Is.EqualTo(1f));
            Assert.That(SkillVfxRules.WindowAlpha(0.85f, 0f, 1f, 0.3f, 0.3f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(SkillVfxRules.WindowAlpha(1f, 0f, 1f, 0.3f, 0.3f), Is.EqualTo(0f));
        }

        [Test]
        public void BodyFollowsTheColliderAtItsOwnProportions()
        {
            Assert.That(SkillVfxRules.ContainScale(Reference, Reference), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(SkillVfxRules.ContainScale(Reference * 2f, Reference), Is.EqualTo(2f).Within(1e-5f));
            // The anti volley's 60 × 50 × 20 collider: the palm doc's "small hand, about 0.6".
            Assert.That(SkillVfxRules.ContainScale(new Vector3(60f, 50f, 20f), Reference), Is.EqualTo(50f / 90f).Within(1e-5f));
            Assert.That(SkillVfxRules.UniformScale(new Vector3(60f, 50f, 20f), Reference), Is.EqualTo(50f / 90f).Within(1e-5f));
        }

        // Only the buff state layer is still a first-pass prefab; VFX_Palm / VFX_Palm_Anti were retired
        // (Docs/vfx/retired.md) for the palm_release slots and the palm_bullet ink hands.
        [TestCase(BufPrefab, "palm_buff", new[] { SkillVfxPhase.Buf })]
        public void PrefabCoversItsLayerSheetPhasesWithNamedLayers(string path, string vfxId, SkillVfxPhase[] phases)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            var controller = prefab.GetComponent<SkillVfxPhaseController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.VfxId, Is.EqualTo(vfxId));

            SkillVfxLayer[] layers = prefab.GetComponentsInChildren<SkillVfxLayer>(true);
            foreach (SkillVfxPhase phase in phases)
                Assert.That(layers.Any(l => l.phase == phase && l.tier == SkillVfxTier.Core || l.phase == phase && phase == SkillVfxPhase.Buf), Is.True,
                    path + " has no layer for " + phase);

            var name = new Regex(@"^FX_(Palm|PalmAnti)_(Buf|Chg|Prj|Imp|Dis|Lin)_(C|S|A)_(Glow|Flash|Distortion|Smoke|Sparks|Decal|Mesh|Light|Post)_\d\d$");
            foreach (SkillVfxLayer layer in layers)
            {
                System.Text.RegularExpressions.Match match = name.Match(layer.name);
                Assert.That(match.Success, Is.True, "layer name " + layer.name);
                Assert.That(match.Groups[2].Value, Is.EqualTo(layer.phase.ToString()), layer.name);
                Assert.That(match.Groups[3].Value, Is.EqualTo(layer.tier.ToString().Substring(0, 1)), layer.name);
                Assert.That(match.Groups[4].Value, Is.EqualTo(layer.function.ToString()), layer.name);
                if (layer.tier == SkillVfxTier.Core)
                    Assert.That(layer.priority, Is.EqualTo(1), layer.name);
            }
        }

        // README section 8 budget, per cast.
        [TestCase(BufPrefab, 1)]
        public void PrefabStaysWithinTheCastBudget(string path, int copies)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            ParticleSystem[] systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(systems.Length, Is.LessThanOrEqualTo(4), "particle systems per cast");
            Assert.That(systems.Sum(s => s.main.maxParticles) * copies, Is.LessThanOrEqualTo(2000), "particles per cast");

            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                    Assert.That(filter.sharedMesh.triangles.Length / 3, Is.LessThanOrEqualTo(3000), filter.name);
            }

            Light[] lights = prefab.GetComponentsInChildren<Light>(true);
            Assert.That(lights.Length, Is.LessThanOrEqualTo(2));
            foreach (Light light in lights)
                Assert.That(light.shadows, Is.EqualTo(LightShadows.None), light.name);
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off), renderer.name);
                Assert.That(renderer.receiveShadows, Is.False, renderer.name);
            }
        }

        [Test]
        public void VfxTableRowsPairAntiEffectsAndPointAtTheirPrefabs()
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

            // Prefab and controller id of every row: EveryVfxIdFollowsTheNamingRuleAndLoadsItsPrefab.
            foreach (string id in new[] { "palm_release", "palm_release_anti", "palm_buff" })
            {
                VfxRegistryRecord row = rows.Single(r => r.vfxId == id);
                Assert.That(row.linSeconds, Is.LessThanOrEqualTo(1.5f), "README section 8: residue <= 1.5 s");
                Assert.That(row.lightSeconds, Is.LessThanOrEqualTo(0.5f), "README section 8: launch light <= 0.5 s");
                Assert.That(row.impFrames, Is.InRange(2, 4), "flash 2-4 frames");
            }
        }

        // README section 6 (2026-10-30): vfx_id = <skill>_<slot>[_anti] (the shared wind-up only cast / cast_anti), the
        // prefab is Assets/VFX/<技能中文名>/<Slot>/VFX_<Skill>_<Slot>[_Anti].prefab, and the slot controller carries the
        // same id. The VFX table and SkillVfxDatabase list the same ids and the same prefabs.
        [Test]
        public void EveryVfxIdFollowsTheNamingRuleAndLoadsItsPrefab()
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

        // The palm skills name ids SkillVfxDatabase has (the table / database pairing itself is
        // EveryVfxIdFollowsTheNamingRuleAndLoadsItsPrefab); the anti release pairs with the normal one.
        [Test]
        public void SkillsDatabaseAndTableAgreeOnTheVfxIds()
        {
            var database = AssetDatabase.LoadAssetAtPath<SkillVfxDatabase>("Assets/Skills_Assets/SkillVfxDatabase.asset");
            var rows = AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(MainAsset).vfxRegistries;

            var palm = new SerializedObject(AssetDatabase.LoadAssetAtPath<Skill_PushProjectileHands>("Assets/Skills_Assets/Skills/Skill_PushProjectileHands.asset"));
            var anti = AssetDatabase.LoadAssetAtPath<Skill_PushProjectileHands_Anti>("Assets/Skills_Assets/Skills/Skill_PushProjectileHands_Anti.asset");
            foreach (string id in new[] { palm.FindProperty("vfxId").stringValue, palm.FindProperty("handVfxId").stringValue, palm.FindProperty("buffVfxId").stringValue, anti.VfxId, anti.HandVfxId })
                Assert.That(database.TryGetPrefab(id, out GameObject prefab) && prefab != null, Is.True, "skill vfx id " + id);
            Assert.That(rows.Single(r => r.vfxId == anti.VfxId).antiOf, Is.EqualTo(palm.FindProperty("vfxId").stringValue));
        }

        // README 4: the effect of one cast (charge + flight + dissipation) fits in the cast lock. The buff state layer
        // runs for the buff's length and is outside this rule (paper confirmation Q1, 2026-10-28).
        [TestCase("push_projectile_hands", "charged", "palm_release")]
        [TestCase("push_projectile_hands_anti", "charged_anti", "palm_release_anti")]
        public void OneCastOfTheEffectFitsInTheCastLock(string skillId, string pushId, string vfxId)
        {
            var database = AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(MainAsset);
            SkillRecord skill = database.skills.Single(s => s.skillId == skillId);
            PushRecord push = database.pushes.Single(p => p.pushId == pushId);
            VfxRegistryRecord vfx = database.vfxRegistries.Single(r => r.vfxId == vfxId);
            float total = push.windupSeconds + push.lifetimeSeconds + vfx.disSeconds;
            Assert.That(total, Is.LessThanOrEqualTo(skill.castLockSeconds), skillId);
        }

        [Test]
        public void FxShadersArePreloaded()
        {
            var collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>("Assets/VFX/PreloadShader.shadervariants");
            var serialized = new SerializedObject(collection).FindProperty("m_Shaders");
            var shaders = new HashSet<string>();
            for (int i = 0; i < serialized.arraySize; i++)
            {
                var shader = serialized.GetArrayElementAtIndex(i).FindPropertyRelative("first").objectReferenceValue as Shader;
                if (shader != null)
                    shaders.Add(shader.name);
            }
            // The two hand-written shaders VFX_Palm_Buff still uses; FX/Decal and FX/Mesh_Energy were retired.
            foreach (string name in new[] { "FX/Additive_Glow", "FX/Alpha_Smoke" })
                Assert.That(shaders, Does.Contain(name));
        }

        [Test]
        public void LaunchPostPulseIsRoutedOnTheCharacter()
        {
            GameObject buddah = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Prefab/Buddah.prefab");
            var router = new SerializedObject(buddah.GetComponentInChildren<SkillFeelRouter>(true)).FindProperty("entries");
            var mapped = new Dictionary<string, Object>();
            for (int i = 0; i < router.arraySize; i++)
            {
                SerializedProperty entry = router.GetArrayElementAtIndex(i);
                mapped[entry.FindPropertyRelative("eventId").stringValue] = entry.FindPropertyRelative("feedbackPlayer").objectReferenceValue;
            }
            foreach (string id in new[] { "palm_launch_local", "palm_anti_launch_local" })
            {
                Assert.That(mapped.ContainsKey(id) && mapped[id] != null, Is.True, id);
                var player = new SerializedObject(mapped[id]).FindProperty("FeedbacksList");
                for (int i = 0; i < player.arraySize; i++)
                {
                    SerializedProperty feedback = player.GetArrayElementAtIndex(i);
                    Assert.That(feedback.FindPropertyRelative("Timing.InitialDelay").floatValue, Is.EqualTo(0f), id);
                    SerializedProperty shake = feedback.FindPropertyRelative("ShakeDuration");
                    if (shake != null && feedback.FindPropertyRelative("Active").boolValue)
                        Assert.That(shake.floatValue, Is.LessThanOrEqualTo(0.15f), id + " pulse <= 0.15 s (README 5)");
                }
            }
        }
    }
}
