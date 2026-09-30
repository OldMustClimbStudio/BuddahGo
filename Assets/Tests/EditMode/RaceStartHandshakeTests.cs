using System.Collections.Generic;
using NUnit.Framework;
using SteamMultiplayer.Network;

namespace BuddahGo.Tests
{
    public class RaceStartHandshakeTests
    {
        private static IEnumerable<HashSet<int>> Sets(RaceStartHandshake h)
        {
            yield return h._raceSceneManagersReadyClientIds;
            yield return h._introAssignmentReadyClientIds;
            yield return h._introVisualReadyClientIds;
            yield return h._gameplayLiveClientIds;
        }
        private static IEnumerable<Dictionary<int, int>> Sequences(RaceStartHandshake h)
        {
            yield return h._introAssignmentReadySequenceByClientId;
            yield return h._introVisualReadySequenceByClientId;
            yield return h._gameplayLiveSequenceByClientId;
        }
        private static RaceStartHandshake Seed()
        {
            var h = new RaceStartHandshake();
            foreach (var set in Sets(h)) set.UnionWith(new[] { 1, 2 });
            foreach (var map in Sequences(h)) { map[1] = 4; map[2] = 4; }
            return h;
        }

        [Test]
        public void SequenceResetRetainsSceneReadinessButClearsSequenceState()
        {
            var h = Seed(); h.ResetSequenceReadiness();
            Assert.That(h._raceSceneManagersReadyClientIds, Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(h._introAssignmentReadyClientIds, Is.Empty);
            Assert.That(h._introVisualReadyClientIds, Is.Empty);
            Assert.That(h._gameplayLiveClientIds, Is.Empty);
            foreach (var map in Sequences(h)) Assert.That(map, Is.Empty);
        }

        [Test]
        public void DisconnectRemovesOnlyDepartingPlayerFromAllReadinessCollections()
        {
            var h = Seed(); h.RemovePlayer(1);
            foreach (var set in Sets(h)) Assert.That(set, Is.EquivalentTo(new[] { 2 }));
            foreach (var map in Sequences(h)) { Assert.That(map.Count, Is.EqualTo(1)); Assert.That(map[2], Is.EqualTo(4)); }
        }

        [Test]
        public void FullResetClearsAllReadiness()
        {
            var h = Seed(); h.ResetAllReadiness();
            foreach (var set in Sets(h)) Assert.That(set, Is.Empty);
            foreach (var map in Sequences(h)) Assert.That(map, Is.Empty);
        }

        [Test]
        public void LocalReportRejectsDuplicateInvalidAndUninitializedButAcceptsDifferentSequence()
        {
            int reported = -1;
            Assert.That(RaceStartHandshake.TryMarkLocalSequence(true, 4, ref reported), Is.True);
            Assert.That(reported, Is.EqualTo(4));
            Assert.That(RaceStartHandshake.TryMarkLocalSequence(true, 4, ref reported), Is.False);
            Assert.That(RaceStartHandshake.TryMarkLocalSequence(false, 5, ref reported), Is.False);
            Assert.That(RaceStartHandshake.TryMarkLocalSequence(true, -1, ref reported), Is.False);
            Assert.That(reported, Is.EqualTo(4));
            Assert.That(RaceStartHandshake.TryMarkLocalSequence(true, 3, ref reported), Is.True);
            Assert.That(reported, Is.EqualTo(3));
        }

        [Test]
        public void ReadyRequiresExactSequenceForCurrentRosterAndIgnoresDepartedEntries()
        {
            var players = new List<RoomPlayerState>();
            var ready = new Dictionary<int, int>();
            Assert.That(RaceStartHandshake.AreAllReadyForSequence(true, players, 4, ready), Is.True);
            players.Add(new RoomPlayerState { PlayerId = 1 }); players.Add(new RoomPlayerState { PlayerId = 2 });
            Assert.That(RaceStartHandshake.AreAllReadyForSequence(true, players, 4, ready), Is.False);
            var ids = new HashSet<int>();
            RaceStartHandshake.RecordReadySequence(1, 4, ids, ready);
            RaceStartHandshake.RecordReadySequence(2, 3, ids, ready);
            Assert.That(RaceStartHandshake.AreAllReadyForSequence(true, players, 4, ready), Is.False);
            RaceStartHandshake.RecordReadySequence(2, 4, ids, ready); ready[99] = 1;
            Assert.That(ids, Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(RaceStartHandshake.AreAllReadyForSequence(true, players, 4, ready), Is.True);
            Assert.That(RaceStartHandshake.AreAllReadyForSequence(false, players, 4, ready), Is.False);
            Assert.That(RaceStartHandshake.AreAllReadyForSequence(true, players, -1, ready), Is.False);
        }
    }
}
