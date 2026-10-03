using System;
using BuddahGo.Match;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class RaceEndPolicyTests
    {
        private readonly RaceEndPolicy _policy = new RaceEndPolicy();

        [TestCase(114.999d, false), TestCase(115d, true), TestCase(116d, true)]
        public void OnlineWaitsFifteenSecondsEvenWhenEveryoneFinished(double now, bool expected)
        {
            Assert.That(_policy.ShouldEnd(new OnlineMatchRules(), 2, 2, true, now, 100d, 15d),
                Is.EqualTo(expected));
        }

        [Test]
        public void OnlineDoesNotEndWithoutFirstFinisher()
        {
            Assert.That(_policy.ShouldEnd(new OnlineMatchRules(), 2, 0, false, 999d, null, 15d), Is.False);
        }

        [TestCase(0, 0), TestCase(1, 2), TestCase(0, 2)]
        public void OnlineCountdownSurvivesRosterChanges(int racers, int finished)
        {
            Assert.That(_policy.ShouldEnd(new OnlineMatchRules(), racers, finished, true, 114d, 100d, 15d), Is.False);
            Assert.That(_policy.ShouldEnd(new OnlineMatchRules(), racers, finished, true, 115d, 100d, 15d), Is.True);
        }

        [Test]
        public void PracticeEndsAtHumanFinishWithoutWaitingForCountdownOrRosterUpdate()
        {
            var rules = new SoloMatchRules(new SoloMatchSettings(0, SoloDifficulty.Normal));
            Assert.That(_policy.ShouldEnd(rules, 1, 0, false, 100d, null, 15d), Is.False);
            Assert.That(_policy.ShouldEnd(rules, 1, 0, true, 100d, null, 15d), Is.True);
            Assert.That(_policy.ShouldEnd(rules, 1, 1, true, 100d, 100d, 15d), Is.True);
        }

        [Test]
        public void SoloWithOtherRacersWaitsForAllOrCountdown()
        {
            var rules = new SoloMatchRules(new SoloMatchSettings(3, SoloDifficulty.Normal));
            Assert.That(_policy.ShouldEnd(rules, 4, 1, true, 100d, 100d, 15d), Is.False);
            Assert.That(_policy.ShouldEnd(rules, 4, 4, true, 105d, 100d, 15d), Is.True);
            Assert.That(_policy.ShouldEnd(rules, 4, 1, true, 115d, 100d, 15d), Is.True);
        }

        [TestCase(0, 0), TestCase(-1, 0), TestCase(1, -1), TestCase(1, 2)]
        public void EmptyOrInconsistentRosterIsNotAllFinished(int racers, int finished)
        {
            var rules = new SoloMatchRules(new SoloMatchSettings(3, SoloDifficulty.Normal));
            Assert.That(_policy.ShouldEnd(rules, racers, finished, false, 100d, null, 15d), Is.False);
        }

        [TestCase(-1d), TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity), TestCase(double.NegativeInfinity)]
        public void InvalidClockOrCountdownCannotExpire(double invalid)
        {
            var rules = new OnlineMatchRules();
            Assert.That(_policy.ShouldEnd(rules, 2, 1, true, invalid, 0d, 15d), Is.False);
            Assert.That(_policy.ShouldEnd(rules, 2, 1, true, 100d, invalid, 15d), Is.False);
            Assert.That(_policy.ShouldEnd(rules, 2, 1, true, 100d, 0d, invalid), Is.False);
        }

        [Test]
        public void ZeroCountdownStillRequiresReachingFirstFinishTime()
        {
            var rules = new OnlineMatchRules();
            Assert.That(_policy.ShouldEnd(rules, 2, 1, true, 99d, 100d, 0d), Is.False);
            Assert.That(_policy.ShouldEnd(rules, 2, 1, true, 100d, 100d, 0d), Is.True);
        }

        [Test]
        public void NullRulesAreAConfigurationError()
        {
            Assert.Throws<ArgumentNullException>(() => _policy.ShouldEnd(null, 1, 0, false, 0d, null, 15d));
        }
    }
}
