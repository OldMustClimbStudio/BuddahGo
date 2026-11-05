using System.Collections.Generic;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class SkillStateBoardCoreTests
    {
        private SkillStateBoardCore _board;
        private List<string> _events;

        [SetUp]
        public void SetUp()
        {
            _board = new SkillStateBoardCore();
            _events = new List<string>();
            _board.Activated += e => _events.Add("activated:" + e.SkillId + "#" + e.EntryId);
            _board.Ended += e => _events.Add("ended:" + e.SkillId + "#" + e.EntryId + ":" + e.EndReason);
        }

        [Test]
        public void BeginWithoutOffsetActivatesImmediately()
        {
            SkillStateEntry entry = _board.Begin("acceleration", 0, 100u, 0u, 150u, null);

            Assert.IsNotNull(entry);
            Assert.AreEqual(100u, entry.StartTick);
            Assert.AreEqual(250u, entry.UntilTick);
            Assert.AreEqual(0, entry.Slot);
            Assert.IsTrue(entry.IsActivated);
            Assert.IsFalse(entry.IsEnded);
            CollectionAssert.AreEqual(new[] { "activated:acceleration#1" }, _events);
            Assert.IsTrue(_board.TryGetActive("acceleration", 100u, out SkillStateEntry active));
            Assert.AreSame(entry, active);
        }

        [Test]
        public void BeginWithOffsetActivatesWhenTheStartTickIsReached()
        {
            SkillStateEntry entry = _board.Begin("blackcurtain", 2, 100u, 50u, 500u, null);

            Assert.AreEqual(150u, entry.StartTick);
            Assert.AreEqual(650u, entry.UntilTick);
            Assert.IsFalse(entry.IsActivated);
            Assert.IsEmpty(_events, "nothing fires before the window starts");
            Assert.IsFalse(_board.TryGetActive("blackcurtain", 100u, out _), "a pending entry is not active");

            _board.Tick(149u);
            Assert.IsEmpty(_events);
            Assert.IsFalse(_board.TryGetActive("blackcurtain", 149u, out _));

            _board.Tick(150u);
            CollectionAssert.AreEqual(new[] { "activated:blackcurtain#1" }, _events);
            Assert.IsTrue(_board.TryGetActive("blackcurtain", 150u, out _));

            _board.Tick(151u);
            Assert.AreEqual(1, _events.Count, "Activated fires once");
        }

        [Test]
        public void EntryExpiresExactlyAtItsUntilTick()
        {
            SkillStateEntry entry = _board.Begin("slowtrap", 1, 10u, 0u, 300u, null);
            _events.Clear();

            _board.Tick(309u);
            Assert.IsEmpty(_events);
            Assert.IsTrue(_board.TryGetActive("slowtrap", 309u, out _), "still active one tick before the end");

            _board.Tick(310u);
            CollectionAssert.AreEqual(new[] { "ended:slowtrap#1:" + SkillStateBoardCore.ExpiredReason }, _events);
            Assert.IsTrue(entry.IsEnded);
            Assert.IsFalse(_board.TryGetActive("slowtrap", 310u, out _));

            _board.Tick(400u);
            Assert.AreEqual(1, _events.Count, "Ended fires once");
        }

        [Test]
        public void TryGetActiveIgnoresAnEntryWhoseWindowPassedBeforeTheNextTick()
        {
            _board.Begin("giant", 0, 0u, 0u, 10u, null);
            Assert.IsFalse(_board.TryGetActive("giant", 10u, out _), "the window is [start, until)");
        }

        [Test]
        public void EndFiresEndedOnceAndNothingAfterwards()
        {
            SkillStateEntry entry = _board.Begin("acceleration", 0, 0u, 0u, 100u, null);
            _events.Clear();

            Assert.IsTrue(_board.End(entry.EntryId, "consumed"));
            CollectionAssert.AreEqual(new[] { "ended:acceleration#1:consumed" }, _events);
            Assert.IsTrue(entry.IsEnded);

            Assert.IsFalse(_board.End(entry.EntryId, "again"), "ending twice is a no-op");
            _board.Tick(200u);
            _board.Clear("reset");
            Assert.AreEqual(1, _events.Count);
            Assert.IsFalse(_board.End(999, "unknown"));
        }

        [Test]
        public void ClearEndsEveryEntryOnceIncludingPendingOnes()
        {
            _board.Begin("acceleration", 0, 0u, 0u, 100u, null);
            SkillStateEntry pending = _board.Begin("blackcurtain", 1, 0u, 50u, 500u, null);
            _events.Clear();

            _board.Clear("respawn");
            CollectionAssert.AreEquivalent(new[] { "ended:acceleration#1:respawn", "ended:blackcurtain#2:respawn" }, _events);
            Assert.IsFalse(pending.IsActivated, "a cleared pending entry never activated");

            _board.Clear("respawn");
            _board.Tick(60u);
            Assert.AreEqual(2, _events.Count, "nothing fires twice and the pending entry never activates");
            Assert.AreEqual(0, _board.LiveEntries.Count);
        }

        [Test]
        public void PendingEntryEndedBeforeItsStartNeverActivates()
        {
            SkillStateEntry pending = _board.Begin("blackcurtain", 1, 0u, 50u, 500u, null);
            Assert.IsTrue(_board.End(pending.EntryId, "consumed"));
            _board.Tick(60u);
            CollectionAssert.AreEqual(new[] { "ended:blackcurtain#1:consumed" }, _events);
        }

        [Test]
        public void EntryIdsIncreasePerBoard()
        {
            SkillStateEntry a = _board.Begin("acceleration", 0, 0u, 0u, 10u, null);
            SkillStateEntry b = _board.Begin("slowtrap", 1, 0u, 0u, 10u, null);
            _board.Clear("reset");
            SkillStateEntry c = _board.Begin("acceleration", 0, 20u, 0u, 10u, null);
            Assert.AreEqual(1, a.EntryId);
            Assert.AreEqual(2, b.EntryId);
            Assert.AreEqual(3, c.EntryId, "ids keep increasing after a clear, so an old overlap never looks new");

            var other = new SkillStateBoardCore();
            Assert.AreEqual(1, other.Begin("acceleration", 0, 0u, 0u, 10u, null).EntryId, "each board counts on its own");
        }

        [Test]
        public void ZeroDurationRegistersNothing()
        {
            Assert.IsNull(_board.Begin("acceleration", 0, 0u, 0u, 0u, null));
            Assert.IsNull(_board.Begin("", 0, 0u, 0u, 10u, null));
            Assert.IsEmpty(_events);
            Assert.AreEqual(0, _board.LiveEntries.Count);
        }

        [Test]
        public void RecastSupersedesTheLiveEntryOfTheSameSkill()
        {
            SkillStateEntry first = _board.Begin("push_projectile_hands", 1, 0u, 0u, 750u, null);
            _events.Clear();

            SkillStateEntry second = _board.Begin("push_projectile_hands", 1, 650u, 0u, 750u, null);
            CollectionAssert.AreEqual(new[]
            {
                "ended:push_projectile_hands#1:" + SkillStateBoardCore.SupersededReason,
                "activated:push_projectile_hands#2"
            }, _events, "the old entry ends before the new one activates");
            Assert.IsTrue(first.IsEnded);
            Assert.IsTrue(_board.TryGetActive("push_projectile_hands", 700u, out SkillStateEntry active));
            Assert.AreSame(second, active);

            _board.Tick(750u);
            Assert.AreEqual(2, _events.Count, "the superseded entry does not expire a second time");
        }

        [Test]
        public void EndingAnEntryInsideItsActivatedHandlerIsSafe()
        {
            _board.Activated += e => _board.End(e.EntryId, "consumed");
            SkillStateEntry entry = _board.Begin("acceleration", 0, 0u, 0u, 100u, null);

            Assert.IsTrue(entry.IsEnded);
            CollectionAssert.AreEqual(new[] { "activated:acceleration#1", "ended:acceleration#1:consumed" }, _events);
            _board.Tick(200u);
            Assert.AreEqual(2, _events.Count);
        }

        [Test]
        public void EntryKeepsItsHandle()
        {
            var handle = new FakeHandle("giant");
            SkillStateEntry entry = _board.Begin("giant", 2, 0u, 0u, 10u, handle);
            Assert.AreSame(handle, entry.Handle);
        }

        private sealed class FakeHandle : ISkillRuntimeHandle
        {
            public FakeHandle(string skillId) { SkillId = skillId; }
            public string SkillId { get; }
            public bool IsActive => true;
            public void Consume(string reason) { }
        }
    }
}
