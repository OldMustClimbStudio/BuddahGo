using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

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

        // Each part destroys the pairs of the previous one, as a separate test would.
        private (string name, Action check) Fresh(string name, Action body) => (name, () => { TearDown(); body(); });

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
        public void ForLoadoutKeepsTheEnabledCompletePairsOfTheLoadout()
        {
            EveryCase.Parts(
                Fresh("keeps only pairs whose two skills are in the loadout", () =>
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
                }),
                Fresh("pairs are unordered", () =>
                {
                    var all = new[] { Pair("frost_lock", "slowtrap", "acceleration") };
                    CollectionAssert.AreEqual(new[] { "frost_lock" }, Ids(YuanqiCatalog.ForLoadout(all, new[] { "acceleration", "giant", "slowtrap" })));
                    CollectionAssert.AreEqual(new[] { "frost_lock" }, Ids(YuanqiCatalog.ForLoadout(all, new[] { "slowtrap", "acceleration", "giant" })));
                }),
                Fresh("disabled pairs are skipped", () =>
                {
                    var all = new[] { Pair("frost_lock", "acceleration", "slowtrap", enabled: false), Pair("giant_charge", "acceleration", "giant") };
                    CollectionAssert.AreEqual(new[] { "giant_charge" }, Ids(YuanqiCatalog.ForLoadout(all, new[] { "acceleration", "slowtrap", "giant" })));
                }),
                Fresh("null and incomplete pairs are skipped", () =>
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
                }),
                Fresh("duplicate ids keep the first entry", () =>
                {
                    YuanqiAction first = Pair("frost_lock", "acceleration", "slowtrap");
                    var all = new[] { first, Pair("frost_lock", "acceleration", "slowtrap") };
                    List<YuanqiAction> result = YuanqiCatalog.ForLoadout(all, new[] { "acceleration", "slowtrap", "giant" });
                    Assert.AreEqual(1, result.Count);
                    Assert.AreSame(first, result[0]);
                }),
                Fresh("ids are compared after trimming and empty slots are ignored", () =>
                {
                    var all = new[] { Pair("frost_lock", " acceleration", "slowtrap ") };
                    CollectionAssert.AreEqual(new[] { "frost_lock" }, Ids(YuanqiCatalog.ForLoadout(all, new[] { "acceleration ", null, " slowtrap" })));
                    Assert.IsEmpty(YuanqiCatalog.ForLoadout(all, new[] { "", null, "" }));
                }),
                Fresh("null inputs give an empty list", () =>
                {
                    Assert.IsEmpty(YuanqiCatalog.ForLoadout(null, new[] { "acceleration" }));
                    Assert.IsEmpty(YuanqiCatalog.ForLoadout(new[] { Pair("frost_lock", "acceleration", "slowtrap") }, null));
                }));
        }
    }
}
