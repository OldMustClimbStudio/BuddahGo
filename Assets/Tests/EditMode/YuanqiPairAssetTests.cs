using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.Tests
{
    /// <summary>
    /// The three confirmed 缘起 pair assets (phase A): identity, skill pair, consume flags and mode as yuanqi.md
    /// specifies them, serialized parameters equal to the class defaults, and every pair listed in Skill Database.
    /// </summary>
    public class YuanqiPairAssetTests
    {
        private const string FrostLockAsset = "Assets/Skills_Assets/Yuanqi/Yuanqi_FrostLock.asset";
        private const string GiantChargeAsset = "Assets/Skills_Assets/Yuanqi/Yuanqi_GiantCharge.asset";
        private const string GiantPalmAsset = "Assets/Skills_Assets/Yuanqi/Yuanqi_GiantPalm.asset";
        private const string SkillDatabaseAsset = "Assets/Skills_Assets/Skill Database.asset";

        private static T Load<T>(string path) where T : YuanqiAction
        {
            var asset = AssetDatabase.LoadAssetAtPath<YuanqiAction>(path);
            Assert.IsNotNull(asset, path + " missing or not a YuanqiAction");
            Assert.IsInstanceOf<T>(asset, path);
            return (T)asset;
        }

        private static void AssertPair(YuanqiAction action, string id, string skillA, string skillB, bool consumeA, bool consumeB, YuanqiMode mode)
        {
            Assert.AreEqual(id, action.yuanqiId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(action.displayName), id + " displayName");
            Assert.AreEqual(skillA, action.skillA, id + " skillA");
            Assert.AreEqual(skillB, action.skillB, id + " skillB");
            Assert.AreEqual(consumeA, action.consumeA, id + " consumeA");
            Assert.AreEqual(consumeB, action.consumeB, id + " consumeB");
            Assert.AreEqual(mode, action.mode, id + " mode");
            Assert.AreEqual(YuanqiRetrigger.OncePerOverlap, action.retriggerPolicy, id + " retriggerPolicy");
            Assert.IsTrue(action.isEnabled, id + " isEnabled");
        }

        [Test]
        public void FrostLockAssetMatchesSpecAndDefaults()
        {
            var asset = Load<Yuanqi_FrostLock>(FrostLockAsset);
            AssertPair(asset, "frost_lock", "acceleration", "slowtrap", true, true, YuanqiMode.Instant);
            var defaults = ScriptableObject.CreateInstance<Yuanqi_FrostLock>();
            try
            {
                Assert.AreEqual(defaults.freezeSeconds, asset.freezeSeconds, "freezeSeconds");
                Assert.AreEqual(defaults.radiusMeters, asset.radiusMeters, "radiusMeters");
                Assert.AreEqual(defaults.affectsTrainingDummies, asset.affectsTrainingDummies, "affectsTrainingDummies");
            }
            finally { Object.DestroyImmediate(defaults); }
        }

        [Test]
        public void GiantChargeAssetMatchesSpecAndDefaults()
        {
            var asset = Load<Yuanqi_GiantCharge>(GiantChargeAsset);
            AssertPair(asset, "giant_charge", "acceleration", "giant", true, false, YuanqiMode.Instant);
            var defaults = ScriptableObject.CreateInstance<Yuanqi_GiantCharge>();
            try
            {
                Assert.AreEqual(defaults.burstExtraMaxSpeed, asset.burstExtraMaxSpeed, "burstExtraMaxSpeed");
                Assert.AreEqual(defaults.burstSeconds, asset.burstSeconds, "burstSeconds");
                Assert.AreEqual(defaults.impactImpulseMultiplier, asset.impactImpulseMultiplier, "impactImpulseMultiplier");
            }
            finally { Object.DestroyImmediate(defaults); }
        }

        [Test]
        public void GiantPalmAssetMatchesSpec()
        {
            var asset = Load<Yuanqi_GiantPalm>(GiantPalmAsset);
            AssertPair(asset, "giant_palm", "giant", "push_projectile_hands", false, false, YuanqiMode.Sustained);
        }

        /// <summary>Phase A placeholder numbers (yuanqi.md, 2026-10-25); phase B moves them into the tables.</summary>
        [Test]
        public void ClassDefaultsAreThePhaseAPlaceholders()
        {
            var frost = ScriptableObject.CreateInstance<Yuanqi_FrostLock>();
            var charge = ScriptableObject.CreateInstance<Yuanqi_GiantCharge>();
            try
            {
                Assert.AreEqual(1.5f, frost.freezeSeconds, "N");
                Assert.AreEqual(12f, frost.radiusMeters, "R");
                Assert.IsTrue(frost.affectsTrainingDummies);
                Assert.AreEqual(60f, charge.burstExtraMaxSpeed, "cap + 60");
                Assert.AreEqual(0.8f, charge.burstSeconds, "decays over 0.8 s");
                Assert.AreEqual(3f, charge.impactImpulseMultiplier, "M");
            }
            finally
            {
                Object.DestroyImmediate(frost);
                Object.DestroyImmediate(charge);
            }
        }

        [Test]
        public void SkillDatabaseListsTheThreePairs()
        {
            var database = AssetDatabase.LoadAssetAtPath<SkillDatabase>(SkillDatabaseAsset);
            Assert.IsNotNull(database, SkillDatabaseAsset);
            Assert.IsNotNull(database.yuanqis, "yuanqis list");
            foreach (string path in new[] { FrostLockAsset, GiantChargeAsset, GiantPalmAsset })
            {
                var asset = AssetDatabase.LoadAssetAtPath<YuanqiAction>(path);
                Assert.IsNotNull(asset, path);
                Assert.Contains(asset, database.yuanqis, path + " not listed in Skill Database.yuanqis");
            }
            Assert.IsFalse(database.yuanqis.Contains(null), "no empty entries in Skill Database.yuanqis");
        }
    }
}
