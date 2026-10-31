using System;
using NUnit.Framework;
using UnityEditor;

namespace BuddahGo.Tests
{
    // User rule 2026-10-31: a backfire applies the unified penalty and always runs the requested skill's own _anti variant,
    // restored as it was on dev. These checks pin each skill to the variant class it had on dev and keep the variants out of
    // every selection and loadout path.
    public class BackfireVariantTests
    {
        private const string MainAsset = "Assets/Data/Config/ProjectConfigDatabase_Main.asset";
        private const string SkillDatabaseAsset = "Assets/Skills_Assets/Skill Database.asset";

        private static SkillDatabase LoadDatabase()
        {
            ProjectConfigDatabase main = AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(MainAsset);
            Assert.IsNotNull(main, MainAsset);
            Assert.IsTrue(ProjectConfigRuntime.EnsureInitialized(main));
            SkillDatabase database = AssetDatabase.LoadAssetAtPath<SkillDatabase>(SkillDatabaseAsset);
            Assert.IsNotNull(database, SkillDatabaseAsset);
            return database;
        }

        [TestCase("acceleration", "acceleration_anti", typeof(Skill_Acceleration_Anti))]
        [TestCase("slowtrap", "slowtrap_anti", typeof(Skill_SlowTrap_Anti))]
        [TestCase("blackcurtain", "blackcurtain_anti", typeof(Skill_BlackCurtain_Anti))]
        [TestCase("giant", "giant_anti", typeof(Skill_Giant_Anti))]
        [TestCase("push_projectile_hands", "push_projectile_hands_anti", typeof(Skill_PushProjectileHands_Anti))]
        [TestCase("reverseturn", "reverseturn_anti", typeof(Skill_ReverseTurn_Anti))]
        public void EverySkillResolvesItsDevVariant(string skillId, string antiId, Type variantType)
        {
            SkillDatabase database = LoadDatabase();
            Assert.IsTrue(database.TryGet(skillId, out SkillAction skill) && skill != null, skillId);
            Assert.AreEqual(antiId, database.GetAntiSkillId(skillId, skill), "Skill.antiSkillId of " + skillId);
            Assert.AreEqual(antiId, skill.antiSkillId, "the SO names the same variant as the table");
            Assert.IsTrue(database.TryGet(antiId, out SkillAction variant) && variant != null, antiId + " resolves");
            Assert.IsInstanceOf(variantType, variant, antiId);
            Assert.IsTrue(database.antiSkills.Contains(variant), antiId + " lives in SkillDatabase.antiSkills");
        }

        [Test]
        public void VariantsAreNeverOfferedForSelection()
        {
            SkillDatabase database = LoadDatabase();
            foreach (SkillAction offered in database.GetSelectableNormalSkills())
                Assert.IsFalse(offered.skillId.EndsWith("_anti", StringComparison.Ordinal), offered.skillId + " is a variant");
        }

        [Test]
        public void VariantsRunNoChainedVariantAndAddNoObsession()
        {
            SkillDatabase database = LoadDatabase();
            foreach (SkillAction variant in database.antiSkills)
            {
                Assert.IsNotNull(variant);
                Assert.IsTrue(string.IsNullOrEmpty(database.GetAntiSkillId(variant.skillId, variant)), variant.skillId + " names no variant of its own");
                Assert.AreEqual(0f, database.GetObsessionGain(variant.skillId, variant), variant.skillId + " adds no obsession");
            }
        }
    }
}
