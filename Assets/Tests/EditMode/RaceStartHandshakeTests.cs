using System.Collections.Generic;
using NUnit.Framework;
using SteamMultiplayer.Network;

namespace BuddahGo.Tests
{
    public class RaceStartHandshakeTests
    {
        private static RaceStartHandshake Seed()
        {
            var h = new RaceStartHandshake();
            foreach (int player in new[] { 1, 2 })
            {
                h.MarkSceneManagersReady(player);
                foreach (RaceStartHandshake.Stage stage in System.Enum.GetValues(typeof(RaceStartHandshake.Stage)))
                    h.RecordReadySequence(player, 4, stage);
            }
            return h;
        }

        [Test]
        public void SequenceResetRetainsSceneReadinessButClearsSequenceState()
        {
            var h = Seed(); h.ResetSequenceReadiness();
            foreach (int player in new[] { 1, 2 })
            {
                Assert.That(h.AreSceneManagersReady(player), Is.True);
                foreach (RaceStartHandshake.Stage stage in System.Enum.GetValues(typeof(RaceStartHandshake.Stage)))
                    Assert.That(h.IsReadyForSequence(player, 4, stage), Is.False);
            }
        }

        [Test]
        public void DisconnectRemovesOnlyDepartingPlayerFromAllReadiness()
        {
            var h = Seed(); h.RemovePlayer(1);
            Assert.That(h.AreSceneManagersReady(1), Is.False);
            Assert.That(h.AreSceneManagersReady(2), Is.True);
            foreach (RaceStartHandshake.Stage stage in System.Enum.GetValues(typeof(RaceStartHandshake.Stage)))
            {
                Assert.That(h.IsReadyForSequence(1, 4, stage), Is.False);
                Assert.That(h.IsReadyForSequence(2, 4, stage), Is.True);
            }
        }

        [Test]
        public void FullResetClearsAllReadiness()
        {
            var h = Seed(); h.ResetAllReadiness();
            foreach (int player in new[] { 1, 2 })
            {
                Assert.That(h.AreSceneManagersReady(player), Is.False);
                foreach (RaceStartHandshake.Stage stage in System.Enum.GetValues(typeof(RaceStartHandshake.Stage)))
                    Assert.That(h.IsReadyForSequence(player, 4, stage), Is.False);
            }
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
            var h = new RaceStartHandshake();
            var stage = RaceStartHandshake.Stage.Visual;
            Assert.That(h.AreAllReadyForSequence(true, players, 4, stage), Is.True);
            players.Add(new RoomPlayerState { PlayerId = 1 }); players.Add(new RoomPlayerState { PlayerId = 2 });
            Assert.That(h.AreAllReadyForSequence(true, players, 4, stage), Is.False);
            h.RecordReadySequence(1, 4, stage); h.RecordReadySequence(2, 3, stage);
            Assert.That(h.AreAllReadyForSequence(true, players, 4, stage), Is.False);
            h.RecordReadySequence(2, 4, stage); h.RecordReadySequence(99, 1, stage);
            Assert.That(h.AreAllReadyForSequence(true, players, 4, stage), Is.True);
            Assert.That(h.AreAllReadyForSequence(true, players, 4, RaceStartHandshake.Stage.Assignment), Is.False);
            Assert.That(h.AreAllReadyForSequence(true, players, 4, RaceStartHandshake.Stage.Gameplay), Is.False);
            Assert.That(h.AreAllReadyForSequence(false, players, 4, stage), Is.False);
            Assert.That(h.AreAllReadyForSequence(true, players, -1, stage), Is.False);
        }
    }
}
