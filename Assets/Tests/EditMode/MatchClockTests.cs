using System;
using BuddahGo.Match;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class MatchClockTests
    {
        [Test]
        public void ReadsCurrentServerTickWithoutLocalTimeOrPauseOffset()
        {
            uint tick = 600;
            var clock = new MatchClock(() => tick, 1d / 60d);
            Assert.That(clock.Now, Is.EqualTo(10d).Within(1e-10));
            tick = 690;
            Assert.That(clock.Now, Is.EqualTo(11.5d).Within(1e-10));
            Assert.That(clock.IsPaused, Is.False);
            tick = 0;
            Assert.That(clock.Now, Is.Zero);
        }

        [Test]
        public void LargeTicksAreConvertedWithoutIntegerOverflow()
        {
            var clock = new MatchClock(() => uint.MaxValue, 0.02d);
            Assert.That(clock.Now, Is.EqualTo(85899345.9d).Within(1e-7));
        }

        [Test]
        public void RejectsMissingTickSource()
        {
            Assert.Throws<ArgumentNullException>(() => new MatchClock(null, 0.02d));
        }

        [TestCase(0d), TestCase(-1d), TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity), TestCase(double.NegativeInfinity)]
        public void RejectsInvalidTickDelta(double delta)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchClock(() => 0, delta));
        }
    }
}
