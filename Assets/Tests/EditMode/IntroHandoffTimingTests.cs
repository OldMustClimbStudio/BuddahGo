using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class IntroHandoffTimingTests
    {
        private static readonly IntroSequenceTiming Timing = new IntroSequenceTiming(1, 100d, 110d);

        [Test]
        public void DriveTimeClampsOnlyTheStart()
        {
            Assert.That(IntroTimeUtility.GetDriveIntroNetworkTime(Timing, 95d), Is.EqualTo(100d), "before intro start clamps to start");
            Assert.That(IntroTimeUtility.GetDriveIntroNetworkTime(Timing, 104.5d), Is.EqualTo(104.5d), "inside the intro is unchanged");
            Assert.That(IntroTimeUtility.GetDriveIntroNetworkTime(Timing, 110.04d), Is.EqualTo(110.04d), "past the scheduled GO is not clamped");
            Assert.That(IntroTimeUtility.GetClampedIntroNetworkTime(Timing, 110.04d), Is.EqualTo(110d), "the clamped variant still parks at GO");
        }

        [Test]
        public void DriveTimePassesThroughInvalidTiming()
        {
            IntroSequenceTiming invalid = new IntroSequenceTiming(-1, 100d, 110d);
            Assert.That(IntroTimeUtility.GetDriveIntroNetworkTime(invalid, 42d), Is.EqualTo(42d));
        }

        [Test]
        public void OvershootIsZeroBeforeScheduledGo()
        {
            Assert.That(IntroTimeUtility.GetGoOvershootSeconds(110d, 104d, 0.15d), Is.EqualTo(0d));
            Assert.That(IntroTimeUtility.GetGoOvershootSeconds(110d, 110d, 0.15d), Is.EqualTo(0d));
        }

        [Test]
        public void OvershootIsLinearAfterScheduledGoAndCapped()
        {
            Assert.That(IntroTimeUtility.GetGoOvershootSeconds(110d, 110.04d, 0.15d), Is.EqualTo(0.04d).Within(1e-9), "2 frames late extrapolates 2 frames");
            Assert.That(IntroTimeUtility.GetGoOvershootSeconds(110d, 110.15d, 0.15d), Is.EqualTo(0.15d).Within(1e-9), "cap boundary");
            Assert.That(IntroTimeUtility.GetGoOvershootSeconds(110d, 111d, 0.15d), Is.EqualTo(0.15d).Within(1e-9), "a second late is capped, body parks");
            Assert.That(IntroTimeUtility.GetGoOvershootSeconds(110d, 111d, 0d), Is.EqualTo(0d), "zero cap disables extrapolation");
        }

        [Test]
        public void OvershootIsZeroWithoutResolvedGoTime()
        {
            Assert.That(IntroTimeUtility.GetGoOvershootSeconds(-1d, 500d, 0.15d), Is.EqualTo(0d));
        }

        [TestCase(true, false, false, true, TestName = "SplineOwnsBody_DuringIntro")]
        [TestCase(true, true, true, true, TestName = "SplineOwnsBody_GoAppliedHandoffPending")]
        [TestCase(false, true, true, true, TestName = "SplineOwnsBody_GoAppliedPendingAfterIntroPhaseEnded")]
        [TestCase(false, true, false, false, TestName = "SplineOwnsBody_ReleasedOnceHandoffConsumed")]
        [TestCase(false, false, false, false, TestName = "SplineOwnsBody_Idle")]
        [TestCase(false, false, true, false, TestName = "SplineOwnsBody_PendingWithoutGoIsNotSplineOwned")]
        public void SplineOwnsBodyFollowsMotorHandoffState(bool introActive, bool goApplied, bool handoffPendingConsume, bool expected)
        {
            Assert.That(RaceBodyIntroStateController.ResolveSplineOwnsBody(introActive, goApplied, handoffPendingConsume), Is.EqualTo(expected));
        }
    }
}
