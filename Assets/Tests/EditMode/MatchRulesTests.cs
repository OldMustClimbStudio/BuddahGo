using System;
using BuddahGo.Match;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    // The table-driven rule flags (Match/ModeRule) are not pinned here (04-tests.md 3, #11-15); only the rules the code
    // decides are. The race end itself is covered by RaceEndPolicyTests.SoloEndsOnHumanFinishOnlyInPractice.
    public class MatchRulesTests
    {
        [TearDown]
        public void RestoreOnlineDefaults() => MatchRules.Reset();

        // Training dummies (README A1) belong to Practice only: Solo with zero AI racers.
        [Test]
        public void OnlyPracticeSpawnsTrainingDummiesAndEndsOnHumanFinish()
        {
            Assert.That(new OnlineMatchRules().SpawnTrainingDummies, Is.False, "Online never spawns training dummies.");
            EveryCase.Check(new[]
            {
                (aiCount: 0, spawnsDummies: true), (aiCount: 1, spawnsDummies: false),
                (aiCount: 3, spawnsDummies: false), (aiCount: 5, spawnsDummies: false),
            }, c =>
            {
                var rules = new SoloMatchRules(new SoloMatchSettings(c.aiCount, SoloDifficulty.Normal));
                Assert.That(rules.IsSolo, Is.True);
                Assert.That(rules.SpawnTrainingDummies, Is.EqualTo(c.spawnsDummies));
                Assert.That(rules.EndOnHumanFinish, Is.EqualTo(c.spawnsDummies), "only Practice ends on the human finish");
            }, c => "aiCount=" + c.aiCount);
        }

        [Test]
        public void CurrentRulesResetToOnlineAndRejectNull()
        {
            EveryCase.Parts(
                ("reset after solo restores the online rules", () =>
                {
                    MatchRules.Current = new SoloMatchRules(new SoloMatchSettings(0, SoloDifficulty.Easy));
                    Assert.That(MatchRules.Current, Is.TypeOf<SoloMatchRules>());
                    Assert.That(MatchRules.Current.IsSolo, Is.True);
                    MatchRules.Reset();
                    Assert.That(MatchRules.Current, Is.TypeOf<OnlineMatchRules>());
                    Assert.That(MatchRules.Current.IsSolo, Is.False);
                }),
                ("null rules and settings are rejected", () =>
                {
                    Assert.Throws<ArgumentNullException>(() => MatchRules.Current = null);
                    Assert.Throws<ArgumentNullException>(() => new SoloMatchRules(null));
                }));
        }
    }
}
