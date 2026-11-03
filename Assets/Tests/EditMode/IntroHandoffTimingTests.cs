using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class IntroHandoffTimingTests
    {
        private static readonly IntroSequenceTiming Timing = new IntroSequenceTiming(1, 100d, 110d);

        [Test]
        public void DriveTimeClampsOnlyTheStartAndPassesThroughInvalidTiming()
        {
            EveryCase.Parts(
                ("drive time clamps only the start", () =>
                {
                    Assert.That(IntroTimeUtility.GetDriveIntroNetworkTime(Timing, 95d), Is.EqualTo(100d), "before intro start clamps to start");
                    Assert.That(IntroTimeUtility.GetDriveIntroNetworkTime(Timing, 104.5d), Is.EqualTo(104.5d), "inside the intro is unchanged");
                    Assert.That(IntroTimeUtility.GetDriveIntroNetworkTime(Timing, 110.04d), Is.EqualTo(110.04d), "past the scheduled GO is not clamped");
                    Assert.That(IntroTimeUtility.GetClampedIntroNetworkTime(Timing, 110.04d), Is.EqualTo(110d), "the clamped variant still parks at GO");
                }),
                ("drive time passes through invalid timing", () =>
                {
                    IntroSequenceTiming invalid = new IntroSequenceTiming(-1, 100d, 110d);
                    Assert.That(IntroTimeUtility.GetDriveIntroNetworkTime(invalid, 42d), Is.EqualTo(42d));
                }));
        }

        [Test]
        public void GoOvershootIsZeroUntilGoThenLinearAndCapped()
        {
            EveryCase.Parts(
                ("zero before the scheduled GO", () =>
                {
                    Assert.That(IntroTimeUtility.GetGoOvershootSeconds(110d, 104d, 0.15d), Is.EqualTo(0d));
                    Assert.That(IntroTimeUtility.GetGoOvershootSeconds(110d, 110d, 0.15d), Is.EqualTo(0d));
                }),
                ("linear after the scheduled GO and capped", () =>
                {
                    Assert.That(IntroTimeUtility.GetGoOvershootSeconds(110d, 110.04d, 0.15d), Is.EqualTo(0.04d).Within(1e-9), "2 frames late extrapolates 2 frames");
                    Assert.That(IntroTimeUtility.GetGoOvershootSeconds(110d, 110.15d, 0.15d), Is.EqualTo(0.15d).Within(1e-9), "cap boundary");
                    Assert.That(IntroTimeUtility.GetGoOvershootSeconds(110d, 111d, 0.15d), Is.EqualTo(0.15d).Within(1e-9), "a second late is capped, body parks");
                    Assert.That(IntroTimeUtility.GetGoOvershootSeconds(110d, 111d, 0d), Is.EqualTo(0d), "zero cap disables extrapolation");
                }),
                ("zero without a resolved GO time", () =>
                    Assert.That(IntroTimeUtility.GetGoOvershootSeconds(-1d, 500d, 0.15d), Is.EqualTo(0d))));
        }

        [Test]
        public void SplineOwnsBodyFollowsMotorHandoffState()
        {
            var cases = new[]
            {
                (name: "DuringIntro", introActive: true, goApplied: false, handoffPendingConsume: false, expected: true),
                (name: "GoAppliedHandoffPending", introActive: true, goApplied: true, handoffPendingConsume: true, expected: true),
                (name: "GoAppliedPendingAfterIntroPhaseEnded", introActive: false, goApplied: true, handoffPendingConsume: true, expected: true),
                (name: "ReleasedOnceHandoffConsumed", introActive: false, goApplied: true, handoffPendingConsume: false, expected: false),
                (name: "Idle", introActive: false, goApplied: false, handoffPendingConsume: false, expected: false),
                (name: "PendingWithoutGoIsNotSplineOwned", introActive: false, goApplied: false, handoffPendingConsume: true, expected: false),
            };
            EveryCase.Check(cases, c =>
                Assert.That(RaceBodyIntroStateController.ResolveSplineOwnsBody(c.introActive, c.goApplied, c.handoffPendingConsume), Is.EqualTo(c.expected)),
                c => c.name);
        }

        [Test]
        public void OvershootCapCoversThePendingRoundTripOnlyAfterLocalGo()
        {
            Assert.That(RaceBodyIntroStateController.ResolveOvershootCapSeconds(false, 0.15f, 0.35f), Is.EqualTo(0.15f).Within(1e-6f), "before GO only RPC latency");
            Assert.That(RaceBodyIntroStateController.ResolveOvershootCapSeconds(true, 0.15f, 0.35f), Is.EqualTo(0.5f).Within(1e-6f), "after GO the handoff round trip too");
            Assert.That(RaceBodyIntroStateController.ResolveOvershootCapSeconds(true, 0.15f, -1f), Is.EqualTo(0.15f).Within(1e-6f), "negative pending cap is ignored");
            Assert.That(RaceBodyIntroStateController.ResolveOvershootCapSeconds(true, 0f, 0f), Is.EqualTo(0f), "zero caps disable extrapolation");
        }
    }
}
