using System;
using BuddahGo.Match;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class MatchRulesTests
    {
        [TearDown]
        public void RestoreOnlineDefaults() => MatchRules.Reset();

        [Test]
        public void OnlineRetainsExistingRoomSelectionAndResultPolicies()
        {
            var rules = new OnlineMatchRules();
            Assert.That(rules.IsSolo, Is.False);
            Assert.That(rules.AutoStartRoom, Is.False);
            Assert.That(rules.RequiresReady, Is.True);
            Assert.That(rules.SelectionTimeoutEnabled, Is.True);
            Assert.That(rules.SkipMapVote, Is.False);
            Assert.That(rules.VoteOnResults, Is.True);
            Assert.That(rules.ResultDecisionTimeoutEnabled, Is.True);
            Assert.That(rules.ReturnTarget, Is.EqualTo(MatchReturnTarget.Room));
            Assert.That(rules.EndOnHumanFinish, Is.False);
            Assert.That(rules.EndWhenAllRacersFinished, Is.False);
            Assert.That(rules.AllowQuitDialog, Is.False);
            Assert.That(rules.DefaultPlayerName, Is.Null);
            AssertDeferredFeaturesDisabled(rules);
        }

        [TestCase(0, true), TestCase(1, false), TestCase(3, false), TestCase(5, false)]
        public void SoloRulesRemoveWaitsAndOnlyPracticeEndsOnHumanFinish(int aiCount, bool endOnHuman)
        {
            var rules = new SoloMatchRules(new SoloMatchSettings(aiCount, SoloDifficulty.Normal));
            Assert.That(rules.IsSolo, Is.True);
            Assert.That(rules.AutoStartRoom, Is.True);
            Assert.That(rules.RequiresReady, Is.False);
            Assert.That(rules.SelectionTimeoutEnabled, Is.False);
            Assert.That(rules.SkipMapVote, Is.True);
            Assert.That(rules.VoteOnResults, Is.False);
            Assert.That(rules.ResultDecisionTimeoutEnabled, Is.False);
            Assert.That(rules.ReturnTarget, Is.EqualTo(MatchReturnTarget.MainMenuHome));
            Assert.That(rules.EndOnHumanFinish, Is.EqualTo(endOnHuman));
            Assert.That(rules.EndWhenAllRacersFinished, Is.True);
            Assert.That(rules.AllowQuitDialog, Is.True);
            Assert.That(rules.DefaultPlayerName, Is.EqualTo("玩家"));
            AssertDeferredFeaturesDisabled(rules);
        }

        [Test]
        public void ResetAfterSoloRestoresOnlineRules()
        {
            MatchRules.Current = new SoloMatchRules(new SoloMatchSettings(0, SoloDifficulty.Easy));
            Assert.That(MatchRules.Current.AutoStartRoom, Is.True);
            MatchRules.Reset();
            Assert.That(MatchRules.Current, Is.TypeOf<OnlineMatchRules>());
            Assert.That(MatchRules.Current.RequiresReady, Is.True);
            Assert.That(MatchRules.Current.IsSolo, Is.False);
            Assert.That(MatchRules.Current.ReturnTarget, Is.EqualTo(MatchReturnTarget.Room));
        }

        [Test]
        public void NullRulesAndSettingsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => MatchRules.Current = null);
            Assert.Throws<ArgumentNullException>(() => new SoloMatchRules(null));
        }

        private static void AssertDeferredFeaturesDisabled(IMatchRules rules)
        {
            Assert.That(rules.AllowPause, Is.False);
            Assert.That(rules.AllowSkipSpectating, Is.False);
            Assert.That(rules.ShowRacerNameTags, Is.False);
            Assert.That(rules.ShowOpponentsOnMinimap, Is.False);
        }
    }
}
