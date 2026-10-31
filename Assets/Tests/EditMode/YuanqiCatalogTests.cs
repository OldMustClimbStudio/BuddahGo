using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BuddahGo.Tests
{
    public class YuanqiCatalogTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private YuanqiAction Pair(string id, string a, string b, bool enabled = true)
        {
            var action = ScriptableObject.CreateInstance<YuanqiTestAction>();
            _created.Add(action);
            action.yuanqiId = id;
            action.skillA = a;
            action.skillB = b;
            action.isEnabled = enabled;
            return action;
        }

        private static List<string> Ids(List<YuanqiAction> actions) => actions.ConvertAll(a => a.yuanqiId);

        [Test]
        public void KeepsOnlyPairsWhoseTwoSkillsAreInTheLoadout()
        {
            var all = new[]
            {
                Pair("frost_lock", "acceleration", "slowtrap"),
                Pair("giant_charge", "acceleration", "giant"),
                Pair("giant_palm", "giant", "push_projectile_hands"),
            };

            CollectionAssert.AreEqual(new[] { "frost_lock" },
                Ids(YuanqiCatalog.ForLoadout(all, new[] { "acceleration", "slowtrap", "blackcurtain" })));
            CollectionAssert.AreEqual(new[] { "giant_charge", "giant_palm" },
                Ids(YuanqiCatalog.ForLoadout(all, new[] { "giant", "push_projectile_hands", "acceleration" })), "database order is kept");
            Assert.IsEmpty(YuanqiCatalog.ForLoadout(all, new[] { "acceleration", "blackcurtain", "reverseturn" }), "one skill of a pair is not enough");
        }

        [Test]
        public void PairsAreUnordered()
        {
            var all = new[] { Pair("frost_lock", "slowtrap", "acceleration") };
            CollectionAssert.AreEqual(new[] { "frost_lock" }, Ids(YuanqiCatalog.ForLoadout(all, new[] { "acceleration", "giant", "slowtrap" })));
            CollectionAssert.AreEqual(new[] { "frost_lock" }, Ids(YuanqiCatalog.ForLoadout(all, new[] { "slowtrap", "acceleration", "giant" })));
        }

        [Test]
        public void DisabledPairsAreSkipped()
        {
            var all = new[] { Pair("frost_lock", "acceleration", "slowtrap", enabled: false), Pair("giant_charge", "acceleration", "giant") };
            CollectionAssert.AreEqual(new[] { "giant_charge" }, Ids(YuanqiCatalog.ForLoadout(all, new[] { "acceleration", "slowtrap", "giant" })));
        }

        [Test]
        public void NullAndIncompletePairsAreSkipped()
        {
            var all = new List<YuanqiAction>
            {
                null,
                Pair("", "acceleration", "slowtrap"),
                Pair("no_b", "acceleration", " "),
                Pair("self", "acceleration", "acceleration"),
                Pair("frost_lock", "acceleration", "slowtrap"),
            };
            CollectionAssert.AreEqual(new[] { "frost_lock" }, Ids(YuanqiCatalog.ForLoadout(all, new[] { "acceleration", "slowtrap", "giant" })));
        }

        [Test]
        public void DuplicateIdsKeepTheFirstEntry()
        {
            YuanqiAction first = Pair("frost_lock", "acceleration", "slowtrap");
            var all = new[] { first, Pair("frost_lock", "acceleration", "slowtrap") };
            List<YuanqiAction> result = YuanqiCatalog.ForLoadout(all, new[] { "acceleration", "slowtrap", "giant" });
            Assert.AreEqual(1, result.Count);
            Assert.AreSame(first, result[0]);
        }

        [Test]
        public void IdsAreComparedAfterTrimmingAndEmptySlotsAreIgnored()
        {
            var all = new[] { Pair("frost_lock", " acceleration", "slowtrap ") };
            CollectionAssert.AreEqual(new[] { "frost_lock" }, Ids(YuanqiCatalog.ForLoadout(all, new[] { "acceleration ", null, " slowtrap" })));
            Assert.IsEmpty(YuanqiCatalog.ForLoadout(all, new[] { "", null, "" }));
        }

        [Test]
        public void NullInputsGiveAnEmptyList()
        {
            Assert.IsEmpty(YuanqiCatalog.ForLoadout(null, new[] { "acceleration" }));
            Assert.IsEmpty(YuanqiCatalog.ForLoadout(new[] { Pair("frost_lock", "acceleration", "slowtrap") }, null));
        }
    }
}
