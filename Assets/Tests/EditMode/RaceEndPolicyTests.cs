using System;
using BuddahGo.Match;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class RaceEndPolicyTests
    {
        private readonly RaceEndPolicy _policy = new RaceEndPolicy();

        [Test]
        public void OnlineEndsOnlyAfterTheCountdownFromTheFirstFinish()
        {
            EveryCase.Parts(
                ("online waits fifteen seconds even when everyone finished", () =>
                    EveryCase.Check(new[] { (now: 114.999d, expected: false), (now: 115d, expected: true), (now: 116d, expected: true) }, c =>
                        Assert.That(_policy.ShouldEnd(new OnlineMatchRules(), 2, 2, true, c.now, 100d, 15d),
                            Is.EqualTo(c.expected)))),
                ("online does not end without a first finisher", () =>
                    Assert.That(_policy.ShouldEnd(new OnlineMatchRules(), 2, 0, false, 999d, null, 15d), Is.False)),
                ("the online countdown survives roster changes", () =>
                    EveryCase.Check(new[] { (racers: 0, finished: 0), (racers: 1, finished: 2), (racers: 0, finished: 2) }, c =>
                    {
                        Assert.That(_policy.ShouldEnd(new OnlineMatchRules(), c.racers, c.finished, true, 114d, 100d, 15d), Is.False);
                        Assert.That(_policy.ShouldEnd(new OnlineMatchRules(), c.racers, c.finished, true, 115d, 100d, 15d), Is.True);
                    })),
                ("a zero countdown still requires reaching the first finish time", () =>
                {
                    var rules = new OnlineMatchRules();
                    Assert.That(_policy.ShouldEnd(rules, 2, 1, true, 99d, 100d, 0d), Is.False);
                    Assert.That(_policy.ShouldEnd(rules, 2, 1, true, 100d, 100d, 0d), Is.True);
                }));
        }

        [Test]
        public void SoloEndsOnHumanFinishOnlyInPractice()
        {
            EveryCase.Parts(
                ("practice ends at the human finish without waiting for the countdown or a roster update", () =>
                {
                    var rules = new SoloMatchRules(new SoloMatchSettings(0, SoloDifficulty.Normal));
                    Assert.That(_policy.ShouldEnd(rules, 1, 0, false, 100d, null, 15d), Is.False);
                    Assert.That(_policy.ShouldEnd(rules, 1, 0, true, 100d, null, 15d), Is.True);
                    Assert.That(_policy.ShouldEnd(rules, 1, 1, true, 100d, 100d, 15d), Is.True);
                }),
                ("solo with other racers waits for all or the countdown", () =>
                    EveryCase.Check(new[] { 3, 5 }, aiCount =>
                    {
                        var rules = new SoloMatchRules(new SoloMatchSettings(aiCount, SoloDifficulty.Normal));
                        int racers = aiCount + 1;
                        Assert.That(_policy.ShouldEnd(rules, racers, 1, true, 100d, 100d, 15d), Is.False);
                        Assert.That(_policy.ShouldEnd(rules, racers, 1, false, 114.9d, 100d, 15d), Is.False);
                        Assert.That(_policy.ShouldEnd(rules, racers, racers, true, 101d, 100d, 15d), Is.True);
                        Assert.That(_policy.ShouldEnd(rules, racers, 1, true, 115d, 100d, 15d), Is.True);
                        Assert.That(_policy.ShouldEnd(rules, racers, 1, false, 115d, 100d, 15d), Is.True);
                    }, aiCount => "aiCount=" + aiCount)));
        }

        [Test]
        public void InvalidInputNeverEndsTheRace()
        {
            EveryCase.Parts(
                ("an empty or inconsistent roster is not all finished", () =>
                    EveryCase.Check(new[] { (racers: 0, finished: 0), (racers: -1, finished: 0), (racers: 1, finished: -1), (racers: 1, finished: 2) }, c =>
                    {
                        var rules = new SoloMatchRules(new SoloMatchSettings(3, SoloDifficulty.Normal));
                        Assert.That(_policy.ShouldEnd(rules, c.racers, c.finished, false, 100d, null, 15d), Is.False);
                    })),
                ("an invalid clock or countdown cannot expire", () =>
                    EveryCase.Check(new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity }, invalid =>
                    {
                        var rules = new OnlineMatchRules();
                        Assert.That(_policy.ShouldEnd(rules, 2, 1, true, invalid, 0d, 15d), Is.False);
                        Assert.That(_policy.ShouldEnd(rules, 2, 1, true, 100d, invalid, 15d), Is.False);
                        Assert.That(_policy.ShouldEnd(rules, 2, 1, true, 100d, 0d, invalid), Is.False);
                    }, invalid => "invalid=" + invalid)),
                ("null rules are a configuration error", () =>
                    Assert.Throws<ArgumentNullException>(() => _policy.ShouldEnd(null, 1, 0, false, 0d, null, 15d))));
        }
    }
}
