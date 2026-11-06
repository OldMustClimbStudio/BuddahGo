using NUnit.Framework;
using UnityEditor;

namespace BuddahGo.Tests
{
    /// <summary>
    /// The three confirmed 缘起 pair assets: each is its Yuanqi_* class under its yuanqiId, has a fallback name, and is
    /// listed in Skill Database. Skill pair, consume flags, mode, retrigger, enabled and the effect numbers come from
    /// Skill/Yuanqi and YuanqiEffect* (DesignTableRuntimeTests checks that every mapped cell reaches the asset).
    /// </summary>
    public class YuanqiPairAssetTests
    {
        private const string SkillDatabaseAsset = "Assets/Skills_Assets/Skill Database.asset";

        [Test]
        public void SkillDatabaseListsTheThreePairAssets()
        {
            var database = AssetDatabase.LoadAssetAtPath<SkillDatabase>(SkillDatabaseAsset);
            Assert.IsNotNull(database, SkillDatabaseAsset);
            Assert.IsNotNull(database.yuanqis, "yuanqis list");
            EveryCase.Check(new[]
            {
                (path: "Assets/Skills_Assets/Yuanqi/Yuanqi_FrostLock.asset", id: "frost_lock", type: typeof(Yuanqi_FrostLock)),
                (path: "Assets/Skills_Assets/Yuanqi/Yuanqi_GiantCharge.asset", id: "giant_charge", type: typeof(Yuanqi_GiantCharge)),
                (path: "Assets/Skills_Assets/Yuanqi/Yuanqi_GiantPalm.asset", id: "giant_palm", type: typeof(Yuanqi_GiantPalm)),
            }, c =>
            {
                var asset = AssetDatabase.LoadAssetAtPath<YuanqiAction>(c.path);
                Assert.IsNotNull(asset, c.path + " missing or not a YuanqiAction");
                Assert.IsInstanceOf(c.type, asset, c.path);
                Assert.AreEqual(c.id, asset.yuanqiId);
                Assert.IsFalse(string.IsNullOrWhiteSpace(asset.displayName), c.id + " displayName");
                Assert.Contains(asset, database.yuanqis, c.path + " not listed in Skill Database.yuanqis");
            }, c => c.id);
            Assert.IsFalse(database.yuanqis.Contains(null), "no empty entries in Skill Database.yuanqis");
        }
    }
}
