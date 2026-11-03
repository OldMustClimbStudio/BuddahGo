using System;
using BuddahGo.Match;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class MatchClockTests
    {
        [Test]
        public void ConvertsServerTicksAndRejectsBadInput()
        {
            EveryCase.Parts(
                ("reads the current server tick without local time or pause offset", () =>
                {
                    uint tick = 600;
                    var clock = new MatchClock(() => tick, 1d / 60d);
                    Assert.That(clock.Now, Is.EqualTo(10d).Within(1e-10));
                    tick = 690;
                    Assert.That(clock.Now, Is.EqualTo(11.5d).Within(1e-10));
                    Assert.That(clock.IsPaused, Is.False);
                    tick = 0;
                    Assert.That(clock.Now, Is.Zero);
                }),
                ("large ticks are converted without integer overflow", () =>
                {
                    var clock = new MatchClock(() => uint.MaxValue, 0.02d);
                    Assert.That(clock.Now, Is.EqualTo(85899345.9d).Within(1e-7));
                }),
                ("rejects a missing tick source", () =>
                    Assert.Throws<ArgumentNullException>(() => new MatchClock(null, 0.02d))),
                ("rejects an invalid tick delta", () =>
                    EveryCase.Check(new[] { 0d, -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity }, delta =>
                        Assert.Throws<ArgumentOutOfRangeException>(() => new MatchClock(() => 0, delta)),
                        delta => "delta=" + delta)));
        }
    }
}
