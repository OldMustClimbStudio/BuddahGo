using NUnit.Framework;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Events;

namespace BuddahGo.Tests
{
    public class BuddahTickMathTests
    {
        [TestCase(5687u, 5690u)]
        [TestCase(0u, 1u)]
        [TestCase(uint.MaxValue - 1, uint.MaxValue)]
        public void ReplayDoesNotConsumeAnImpulseFromItsFuture(uint replayTick, uint eventTick)
        {
            var channel = new BuddahPredictionEventChannel<int>();
            channel.TryEnqueue(7, eventTick, 1, out _);
            int applications = 0;
            bool Apply(in BuddahPredictionEventChannel<int>.Entry entry)
            { applications += entry.Payload; return true; }

            // A live timing update may already have advanced beyond the event.
            uint clock = BuddahTickMath.ImpulseEventClock(false, true, 4021, uint.MaxValue, replayTick);
            Assert.That(channel.ConsumeReady(clock, Apply), Is.Zero);
            Assert.That(channel.Count, Is.EqualTo(1));
            clock = BuddahTickMath.ImpulseEventClock(false, true, 4021, uint.MaxValue, eventTick);
            Assert.That(channel.ConsumeReady(clock, Apply), Is.EqualTo(1));
            Assert.That(channel.ConsumeReady(clock, Apply), Is.Zero);
            Assert.That(applications, Is.EqualTo(7));
        }

        [Test]
        public void ForwardAndHostImpulseClocksRetainTheirCanonicalTicks()
        {
            Assert.That(BuddahTickMath.ImpulseEventClock(false, false, 100, 1900, 0), Is.EqualTo(1900));
            Assert.That(BuddahTickMath.ImpulseEventClock(true, true, 1900, 1901, 1800), Is.EqualTo(1900));
            Assert.That(BuddahTickMath.ImpulseEventClock(false, true, 100, 1900, 0), Is.Zero);
        }

        [TestCase(35000u, 37477u, 34560u, 32083u)]
        [TestCase(37477u, 37477u, 34560u, 34560u)]
        [TestCase(37480u, 37477u, 34560u, 34563u)]
        [TestCase(0u, 0u, 100u, 100u)]
        [TestCase(1u, 3000u, 10u, 0u)]
        [TestCase(uint.MaxValue, 0u, 1u, uint.MaxValue)]
        [TestCase(100u, 100u, 100u, 100u)]
        [TestCase(100u, 90u, 200u, 210u)]
        public void ServerEventsMapToLocalClockWithoutUnsignedUnderflow(uint tick, uint server, uint local, uint expected)
        {
            Assert.That(BuddahTickMath.ServerEventToLocalTick(tick, server, local), Is.EqualTo(expected));
        }

        [TestCase(0f, .02f, 0u)]
        [TestCase(.001f, .02f, 1u)]
        [TestCase(.02f, .02f, 1u)]
        [TestCase(1f, .02f, 50u)]
        [TestCase(1.01f, .02f, 51u)]
        [TestCase(.0001f, 0f, 1u)]
        [TestCase(.0001f, -1f, 1u)]
        [TestCase(.001f, .0001f, 11u)]
        public void DurationRoundsUpUsingExistingFloatAndMinimumDeltaSemantics(float seconds, float delta, uint expected)
        {
            Assert.That(BuddahTickMath.DurationToTicks(seconds, delta), Is.EqualTo(expected));
        }
    }
}
