using NUnit.Framework;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Events;

namespace BuddahGo.Tests
{
    public class BuddahTickMathTests
    {
        [Test]
        public void ReplayDoesNotConsumeAnImpulseFromItsFuture()
        {
            EveryCase.Check(new[]
            {
                (replayTick: 5687u, eventTick: 5690u),
                (replayTick: 0u, eventTick: 1u),
                (replayTick: uint.MaxValue - 1, eventTick: uint.MaxValue),
            }, c =>
            {
                var channel = new BuddahPredictionEventChannel<int>();
                channel.TryEnqueue(7, c.eventTick, 1, out _);
                int applications = 0;
                bool Apply(in BuddahPredictionEventChannel<int>.Entry entry)
                { applications += entry.Payload; return true; }

                // A live timing update may already have advanced beyond the event.
                uint clock = BuddahTickMath.ImpulseEventClock(false, true, 4021, uint.MaxValue, c.replayTick);
                Assert.That(channel.ConsumeReady(clock, Apply), Is.Zero);
                Assert.That(channel.Count, Is.EqualTo(1));
                clock = BuddahTickMath.ImpulseEventClock(false, true, 4021, uint.MaxValue, c.eventTick);
                Assert.That(channel.ConsumeReady(clock, Apply), Is.EqualTo(1));
                Assert.That(channel.ConsumeReady(clock, Apply), Is.Zero);
                Assert.That(applications, Is.EqualTo(7));
            }, c => $"replayTick={c.replayTick} eventTick={c.eventTick}");
        }

        [Test]
        public void ForwardAndHostImpulseClocksRetainTheirCanonicalTicks()
        {
            Assert.That(BuddahTickMath.ImpulseEventClock(false, false, 100, 1900, 0), Is.EqualTo(1900));
            Assert.That(BuddahTickMath.ImpulseEventClock(true, true, 1900, 1901, 1800), Is.EqualTo(1900));
            Assert.That(BuddahTickMath.ImpulseEventClock(false, true, 100, 1900, 0), Is.Zero);
        }

        [Test]
        public void ModifiersResolveAtTheReplayedTickOnlyWhileAClientReplays()
        {
            Assert.That(BuddahTickMath.ModifierResolveTick(true, true, 1900, 1800), Is.EqualTo(1900), "the server never replays");
            Assert.That(BuddahTickMath.ModifierResolveTick(true, false, 1900, 1800), Is.EqualTo(1900));
            Assert.That(BuddahTickMath.ModifierResolveTick(false, true, 3019, 3012), Is.EqualTo(3012));
            Assert.That(BuddahTickMath.ModifierResolveTick(false, false, 3019, uint.MaxValue), Is.EqualTo(3019),
                "outside a replay FishNet leaves ClientReplayTick unset");
        }

        [Test]
        public void ServerTicksTranslateWithSaturation()
        {
            EveryCase.Parts(
                ("server events map to the local clock without unsigned underflow", () => EveryCase.Check(new[]
                {
                    (tick: 35000u, server: 37477u, local: 34560u, expected: 32083u),
                    (tick: 37477u, server: 37477u, local: 34560u, expected: 34560u),
                    (tick: 37480u, server: 37477u, local: 34560u, expected: 34563u),
                    (tick: 0u, server: 0u, local: 100u, expected: 100u),
                    (tick: 1u, server: 3000u, local: 10u, expected: 0u),
                    (tick: uint.MaxValue, server: 0u, local: 1u, expected: uint.MaxValue),
                    (tick: 100u, server: 100u, local: 100u, expected: 100u),
                    (tick: 100u, server: 90u, local: 200u, expected: 210u),
                }, c => Assert.That(BuddahTickMath.ServerEventToLocalTick(c.tick, c.server, c.local), Is.EqualTo(c.expected)),
                c => $"tick={c.tick} server={c.server} local={c.local}")),
                // Moved from HandoffClockTests: events and deadlines saturate rather than wrapping.
                ("events and deadlines saturate rather than wrapping", () => EveryCase.Check(new[]
                {
                    (value: 0u, server: 10u, local: 100u, expectedEvent: 90u, expectedDeadline: 0u),
                    (value: 1u, server: 3000u, local: 10u, expectedEvent: 0u, expectedDeadline: 0u),
                    (value: uint.MaxValue, server: 0u, local: 1u, expectedEvent: uint.MaxValue, expectedDeadline: uint.MaxValue),
                    (value: 1u, server: uint.MaxValue, local: 5u, expectedEvent: 0u, expectedDeadline: 0u),
                    (value: uint.MaxValue - 1u, server: 2u, local: 10u, expectedEvent: uint.MaxValue, expectedDeadline: uint.MaxValue),
                }, c =>
                {
                    Assert.That(BuddahTickMath.ServerEventToLocalTick(c.value, c.server, c.local), Is.EqualTo(c.expectedEvent));
                    Assert.That(BuddahTickMath.ServerDeadlineToLocalTick(c.value, c.server, c.local), Is.EqualTo(c.expectedDeadline));
                }, c => $"value={c.value} server={c.server} local={c.local}")));
        }

        [Test]
        public void DurationRoundsUpUsingExistingFloatAndMinimumDeltaSemantics()
        {
            EveryCase.Check(new[]
            {
                (seconds: 0f, delta: .02f, expected: 0u),
                (seconds: .001f, delta: .02f, expected: 1u),
                (seconds: .02f, delta: .02f, expected: 1u),
                (seconds: 1f, delta: .02f, expected: 50u),
                (seconds: 1.01f, delta: .02f, expected: 51u),
                (seconds: .0001f, delta: 0f, expected: 1u),
                (seconds: .0001f, delta: -1f, expected: 1u),
                (seconds: .001f, delta: .0001f, expected: 11u),
            }, c => Assert.That(BuddahTickMath.DurationToTicks(c.seconds, c.delta), Is.EqualTo(c.expected)),
            c => $"seconds={c.seconds} delta={c.delta}");
        }
    }
}
