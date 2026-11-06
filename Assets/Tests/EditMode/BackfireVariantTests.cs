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

        [Test]
        public void EverySkillResolvesItsDevVariant()
        {
            SkillDatabase database = LoadDatabase();
            EveryCase.Check(new[]
            {
                (skillId: "acceleration", antiId: "acceleration_anti", variantType: typeof(Skill_Acceleration_Anti)),
                (skillId: "slowtrap", antiId: "slowtrap_anti", variantType: typeof(Skill_SlowTrap_Anti)),
                (skillId: "blackcurtain", antiId: "blackcurtain_anti", variantType: typeof(Skill_BlackCurtain_Anti)),
                (skillId: "giant", antiId: "giant_anti", variantType: typeof(Skill_Giant_Anti)),
                (skillId: "push_projectile_hands", antiId: "push_projectile_hands_anti", variantType: typeof(Skill_PushProjectileHands_Anti)),
                (skillId: "reverseturn", antiId: "reverseturn_anti", variantType: typeof(Skill_ReverseTurn_Anti)),
            }, c =>
            {
                Assert.IsTrue(database.TryGet(c.skillId, out SkillAction skill) && skill != null, c.skillId);
                Assert.AreEqual(c.antiId, database.GetAntiSkillId(c.skillId, skill), "Skill.antiSkillId of " + c.skillId);
                Assert.AreEqual(c.antiId, skill.antiSkillId, "the SO names the same variant as the table");
                Assert.IsTrue(database.TryGet(c.antiId, out SkillAction variant) && variant != null, c.antiId + " resolves");
                Assert.IsInstanceOf(c.variantType, variant, c.antiId);
                Assert.IsTrue(database.antiSkills.Contains(variant), c.antiId + " lives in SkillDatabase.antiSkills");
            }, c => "skill " + c.skillId);
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
