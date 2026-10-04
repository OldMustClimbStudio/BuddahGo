using System.Collections.Generic;
using System.Reflection;
using BuddahGo.Match;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace BuddahGo.Tests
{
    public class LeaderboardHudTests
    {
        private GameObject _root;
        private LeaderboardTMPUI _view;
        private TextMeshProUGUI _output;
        private IMatchRules _previousRules;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void CreateInactiveView()
        {
            _previousRules = MatchRules.Current;
            MatchRules.Current = new OnlineMatchRules();
            _root = new GameObject("Leaderboard HUD test", typeof(RectTransform));
            _root.SetActive(false); // No scene discovery, network lifecycle, or font rendering required.
            _output = _root.AddComponent<TextMeshProUGUI>();
            _view = _root.AddComponent<LeaderboardTMPUI>();
            typeof(LeaderboardTMPUI).GetField("outputText", PrivateInstance).SetValue(_view, _output);
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(_root);
            MatchRules.Current = _previousRules;
        }

        [Test]
        public void OnlineSnapshotDisplaysRankLapCompletionAndFinishWithoutTelemetry()
        {
            Render("1. Ada - Lap 4 - 100.0% - Finished #1\n2. Bo - Lap 2 - 48.0%", new List<RankEntry>());
            Assert.That(_output.text, Does.Contain("1. Ada - Lap 4 - 100.0% - Finished #1"));
            Assert.That(_output.text, Does.Contain("2. Bo - Lap 2 - 48.0%"));
            AssertNoDiagnostics();
        }

        [Test]
        public void OnlineListFallbackDisplaysCurrentStandingsAndHonorsRowLimit()
        {
            typeof(LeaderboardTMPUI).GetField("maxRows", PrivateInstance).SetValue(_view, 1);
            Render(null, new List<RankEntry>
            {
                new RankEntry { DisplayName = "Ada", Lap = 4, FinalCompletionPercent = 100f,
                    IsFinished = true, FinishOrder = 1 },
                new RankEntry { DisplayName = "Bo", Lap = 2, FinalCompletionPercent = 48f }
            });
            Assert.That(_output.text, Does.Contain("1. Ada - Lap 4 - "));
            Assert.That(_output.text, Does.Contain("Finished #1"));
            Assert.That(_output.text, Does.Not.Contain("Bo"));
            AssertNoDiagnostics();
        }

        [Test]
        public void PracticeClearsPreviousRankingAndOnlineRestoresItOnSameView()
        {
            const string standings = "1. Ada - Lap 1 - 12.0%";
            var entries = new List<RankEntry>();
            Render(standings, entries);
            Assert.That(_output.text, Does.Contain(standings));
            MatchRules.Current = new SoloMatchRules(new SoloMatchSettings(0, SoloDifficulty.Normal));
            Render(standings, entries);
            Assert.That(_output.text, Is.Empty);
            MatchRules.Current = new OnlineMatchRules();
            Render(standings, entries);
            Assert.That(_output.text, Does.Contain(standings));
            AssertNoDiagnostics();
        }

        private void Render(string snapshot, IList<RankEntry> entries)
        {
            // Exercise the production TMP writer, with only its synchronized data supplied.
            typeof(LeaderboardTMPUI).GetMethod("RenderRankings", PrivateInstance)
                .Invoke(_view, new object[] { snapshot, entries });
        }

        private void AssertNoDiagnostics()
        {
            foreach (string diagnostic in new[] { "Race Status:", "Your Progress:", "LapSpline:",
                "WrongWayDot:", "Your Obsession:", "Backfire Chance:", "Gap To Leader:" })
                Assert.That(_output.text, Does.Not.Contain(diagnostic));
        }
    }
}
