using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class YuanqiPairTableTests
    {
        private YuanqiPairTable<string, string> _table;
        private Dictionary<string, int> _active;
        private Func<string, int> _activeEntryIdOf;

        [SetUp]
        public void SetUp()
        {
            _table = new YuanqiPairTable<string, string>();
            _active = new Dictionary<string, int>();
            _activeEntryIdOf = skillId => _active.TryGetValue(skillId, out int id) ? id : 0;
            _table.Register("frost_lock", "acceleration", "slowtrap", sustained: false);
            _table.Register("giant_palm", "giant", "push_projectile_hands", sustained: true);
        }

        private List<int> Candidates(string skillId)
        {
            var list = new List<int>();
            _table.CollectCandidates(skillId, list);
            return list;
        }

        [Test]
        public void CandidatesAreOnlyThePairsContainingTheSkill()
        {
            CollectionAssert.AreEqual(new[] { 0 }, Candidates("acceleration"));
            CollectionAssert.AreEqual(new[] { 0 }, Candidates("slowtrap"));
            CollectionAssert.AreEqual(new[] { 1 }, Candidates("push_projectile_hands"));
            Assert.IsEmpty(Candidates("blackcurtain"), "a skill in no registered pair is never matched");
            Assert.AreEqual("frost_lock", _table.PairAt(0));
            Assert.AreEqual(2, _table.Count);
        }

        [Test]
        public void MatchesWhenTheSecondSkillActivatesWhicheverWasFirst()
        {
            // Slow trap first, acceleration second: EntryA is always skillA's entry.
            _active["slowtrap"] = 1;
            _active["acceleration"] = 2;
            Assert.IsTrue(_table.TryMatch(0, "acceleration", 2, _activeEntryIdOf, out int a, out int b));
            Assert.AreEqual(2, a);
            Assert.AreEqual(1, b);

            // Acceleration first, slow trap second.
            _table.Reset(new List<string>());
            _active["acceleration"] = 3;
            _active["slowtrap"] = 4;
            Assert.IsTrue(_table.TryMatch(0, "slowtrap", 4, _activeEntryIdOf, out a, out b));
            Assert.AreEqual(3, a);
            Assert.AreEqual(4, b);
        }

        [Test]
        public void DoesNotMatchWhenTheOtherSkillIsNotActive()
        {
            _active["acceleration"] = 1;
            Assert.IsFalse(_table.TryMatch(0, "acceleration", 1, _activeEntryIdOf, out _, out _));
        }

        [Test]
        public void DoesNotMatchASkillOutsideThePair()
        {
            _active["acceleration"] = 1;
            _active["giant"] = 2;
            Assert.IsFalse(_table.TryMatch(0, "giant", 2, _activeEntryIdOf, out _, out _), "giant is not part of frost_lock");
            Assert.IsFalse(_table.TryMatch(7, "acceleration", 1, _activeEntryIdOf, out _, out _), "an unknown pair index never matches");
        }

        [Test]
        public void UnregisteredPairsNeverMatch()
        {
            var empty = new YuanqiPairTable<string, string>();
            _active["acceleration"] = 1;
            _active["slowtrap"] = 2;
            var list = new List<int>();
            empty.CollectCandidates("slowtrap", list);
            Assert.IsEmpty(list);
            Assert.IsFalse(empty.TryMatch(0, "slowtrap", 2, _activeEntryIdOf, out _, out _));
        }

        [Test]
        public void TriggersOncePerOverlap()
        {
            _active["slowtrap"] = 1;
            _active["acceleration"] = 2;
            Assert.IsTrue(_table.TryMatch(0, "acceleration", 2, _activeEntryIdOf, out _, out _));
            Assert.IsFalse(_table.TryMatch(0, "acceleration", 2, _activeEntryIdOf, out _, out _), "the same two entries never trigger twice");
            Assert.IsFalse(_table.TryMatch(0, "slowtrap", 1, _activeEntryIdOf, out _, out _), "nor from the other side");
        }

        [Test]
        public void RetriggersOnANewEntry()
        {
            _active["slowtrap"] = 1;
            _active["acceleration"] = 2;
            Assert.IsTrue(_table.TryMatch(0, "acceleration", 2, _activeEntryIdOf, out _, out _));

            // Acceleration ends and is cast again while the same slow trap is still running: a new overlap.
            _active["acceleration"] = 3;
            Assert.IsTrue(_table.TryMatch(0, "acceleration", 3, _activeEntryIdOf, out int a, out int b));
            Assert.AreEqual(3, a);
            Assert.AreEqual(1, b);
        }

        [Test]
        public void TheActivatedEntryIsNeverItsOwnPartner()
        {
            var same = new YuanqiPairTable<string, string>();
            same.Register("self", "acceleration", "acceleration", sustained: false);
            _active["acceleration"] = 5;
            Assert.IsFalse(same.TryMatch(0, "acceleration", 5, _activeEntryIdOf, out _, out _));
        }

        [Test]
        public void SustainedInstanceEndsWhenEitherEntryEnds()
        {
            var ended = new List<string>();
            _table.BeginSustained(1, 10, 11, "palm#10+11");
            _table.BeginSustained(1, 20, 21, "palm#20+21");
            Assert.AreEqual(2, _table.SustainedCount);

            _table.TakeSustainedEndedBy(11, ended);
            CollectionAssert.AreEqual(new[] { "palm#10+11" }, ended, "entry B ending ends the instance");

            ended.Clear();
            _table.TakeSustainedEndedBy(10, ended);
            Assert.IsEmpty(ended, "an instance ends only once");

            _table.TakeSustainedEndedBy(20, ended);
            CollectionAssert.AreEqual(new[] { "palm#20+21" }, ended, "entry A ending ends the instance");
            Assert.AreEqual(0, _table.SustainedCount);

            ended.Clear();
            _table.TakeSustainedEndedBy(99, ended);
            Assert.IsEmpty(ended);
        }

        [Test]
        public void ResetEndsSustainedInstancesAndForgetsTriggeredOverlaps()
        {
            _active["giant"] = 1;
            _active["push_projectile_hands"] = 2;
            Assert.IsTrue(_table.TryMatch(1, "push_projectile_hands", 2, _activeEntryIdOf, out int a, out int b));
            _table.BeginSustained(1, a, b, "palm");

            var ended = new List<string>();
            _table.Reset(ended);
            CollectionAssert.AreEqual(new[] { "palm" }, ended);
            Assert.AreEqual(0, _table.SustainedCount);
            Assert.IsTrue(_table.TryMatch(1, "push_projectile_hands", 2, _activeEntryIdOf, out _, out _), "a reset forgets what already triggered");
            Assert.AreEqual(2, _table.Count, "a reset keeps the registrations");
        }

        [Test]
        public void ClearRegistrationsDropsEverything()
        {
            _table.BeginSustained(1, 1, 2, "palm");
            var ended = new List<string>();
            _table.ClearRegistrations(ended);
            CollectionAssert.AreEqual(new[] { "palm" }, ended, "sustained instances are handed back to be ended");
            Assert.AreEqual(0, _table.Count);
            Assert.IsEmpty(Candidates("acceleration"));
        }

        [Test]
        public void SkillIdsAreTrimmed()
        {
            var table = new YuanqiPairTable<string, string>();
            table.Register("frost_lock", " acceleration ", "slowtrap ", sustained: false);
            var list = new List<int>();
            table.CollectCandidates("acceleration", list);
            CollectionAssert.AreEqual(new[] { 0 }, list);
            _active["acceleration"] = 1;
            _active["slowtrap"] = 2;
            Assert.IsTrue(table.TryMatch(0, "slowtrap", 2, _activeEntryIdOf, out _, out _));
            Assert.IsTrue(table.IsSustained(0) == false);
        }
    }
}
