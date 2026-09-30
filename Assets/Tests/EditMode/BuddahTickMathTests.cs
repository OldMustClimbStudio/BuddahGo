using NUnit.Framework;
using NewBuddah.PredictionV2.Core;

namespace BuddahGo.Tests
{
    public class BuddahTickMathTests
    {
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
