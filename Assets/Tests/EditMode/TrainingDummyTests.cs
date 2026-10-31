using BuddahGo.Match;
using NewBuddah.PredictionV2.Core;
using NUnit.Framework;
using UnityEngine;

namespace BuddahGo.Tests
{
    // README A1 training dummies: placement along the track, the reset clock and the overhead readout text.
    public class TrainingDummyTests
    {
        private const float Tolerance = 1e-4f;

        // The RaceMap launch slot projects about 37 m before the start line of a 5750 m lap; the default
        // offset puts the three dummies on the first straight after the line.
        [TestCase(0, 53f), TestCase(1, 68f), TestCase(2, 83f)]
        public void DistanceAlongTrackSpacesDummiesAndWrapsPastTheLapEnd(int index, float expected)
        {
            Assert.That(TrainingDummyLayout.DistanceAlongTrack(5713f, 90f, 15f, index, 5750f), Is.EqualTo(expected).Within(1e-2f));
        }

        [Test]
        public void DistanceAlongTrackWrapsNegativeDistancesIntoTheLap()
        {
            Assert.That(TrainingDummyLayout.DistanceAlongTrack(10f, -30f, 15f, 0, 5750f), Is.EqualTo(5730f).Within(1e-2f));
            Assert.That(TrainingDummyLayout.DistanceAlongTrack(10f, 5f, 15f, 1, 0f), Is.EqualTo(0f), "A degenerate track has no length.");
        }

        [TestCase(0, 3, -1f), TestCase(1, 3, 0f), TestCase(2, 3, 1f)]
        [TestCase(0, 1, 0f), TestCase(0, 2, -1f), TestCase(1, 2, 1f), TestCase(2, 5, 0f), TestCase(1, 5, -0.5f)]
        public void LateralFactorSpreadsLeftCentreRight(int index, int count, float expected)
        {
            Assert.That(TrainingDummyLayout.LateralFactor(index, count), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void LateralOffsetKeepsClearanceFromTheWallOnItsOwnSide()
        {
            // Left dummy measured against the left wall, right dummy against the right wall, centre never moves.
            Assert.That(TrainingDummyLayout.ResolveLateralOffset(0, 3, 10f, 12f, 30f, 5f), Is.EqualTo(-7f).Within(Tolerance));
            Assert.That(TrainingDummyLayout.ResolveLateralOffset(2, 3, 10f, 12f, 30f, 5f), Is.EqualTo(10f).Within(Tolerance));
            Assert.That(TrainingDummyLayout.ResolveLateralOffset(2, 3, 10f, 30f, 8f, 5f), Is.EqualTo(3f).Within(Tolerance));
            Assert.That(TrainingDummyLayout.ResolveLateralOffset(1, 3, 10f, 1f, 1f, 5f), Is.EqualTo(0f).Within(Tolerance));
            // No wall hit (open section) keeps the configured offset; a wall closer than the clearance leaves the body centred.
            Assert.That(TrainingDummyLayout.ResolveLateralOffset(0, 3, 10f, float.PositiveInfinity, 30f, 5f), Is.EqualTo(-10f).Within(Tolerance));
            Assert.That(TrainingDummyLayout.ResolveLateralOffset(0, 3, 10f, 3f, 30f, 5f), Is.EqualTo(0f).Within(Tolerance));
        }

        // When the ground under a side dummy is off the track, placement retries closer to the racing line, ending on it.
        [TestCase(-8f, 0, 3, -8f), TestCase(-8f, 1, 3, -4f), TestCase(-8f, 2, 3, 0f)]
        [TestCase(6f, 0, 2, 6f), TestCase(6f, 1, 2, 0f), TestCase(6f, 0, 1, 6f), TestCase(6f, 5, 3, 0f)]
        public void LateralCandidatesShrinkTowardTheRacingLine(float offset, int attempt, int attempts, float expected)
        {
            Assert.That(TrainingDummyLayout.LateralCandidate(offset, attempt, attempts), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void RightAndFacingUseTheFlatTrackTangent()
        {
            AssertVector(TrainingDummyLayout.RightOf(Vector3.forward), Vector3.right);
            AssertVector(TrainingDummyLayout.RightOf(new Vector3(1f, 0.5f, 0f)), Vector3.back);
            AssertVector(TrainingDummyLayout.RightOf(Vector3.up), Vector3.right, "A vertical tangent falls back to world forward.");
            AssertVector(TrainingDummyLayout.FacingAlong(new Vector3(0f, 0.3f, 2f)) * Vector3.forward, Vector3.forward);
            AssertVector(TrainingDummyLayout.FacingAlong(Vector3.right) * Vector3.forward, Vector3.right);
            AssertVector(TrainingDummyLayout.Place(new Vector3(1f, 2f, 3f), Vector3.forward, -4f), new Vector3(-3f, 2f, 3f));
        }

        [Test]
        public void ResetWaitsForTheDelayAfterTheLastDisturbanceOnlyWhileDisplaced()
        {
            var clock = new TrainingDummyResetClock();
            clock.Restart(0f);
            Assert.That(clock.ShouldReset(100f, false, 3f), Is.False, "An undisplaced dummy never resets.");
            clock.MarkDisturbed(1f);
            Assert.That(clock.ShouldReset(3.9f, true, 3f), Is.False);
            Assert.That(clock.ShouldReset(4f, true, 3f), Is.True);
            clock.MarkDisturbed(3.5f);
            Assert.That(clock.ShouldReset(4f, true, 3f), Is.False, "A new disturbance restarts the wait.");
            clock.MarkDisturbed(2f);
            Assert.That(clock.ShouldReset(6.5f, true, 3f), Is.True, "An older timestamp never moves the wait back.");
        }

        [TestCase(0f, false), TestCase(0.02f, false), TestCase(0.5f, false), TestCase(0.51f, true), TestCase(12f, true)]
        public void OnlyAContactWithRealRelativeMotionIsADisturbance(float relativeSpeed, bool expected)
        {
            Assert.That(TrainingDummyResetClock.IsDisturbingContact(relativeSpeed, 0.5f), Is.EqualTo(expected));
        }

        [Test]
        public void ADummyRestingAgainstAnotherBodyStillResets()
        {
            // Pushed into the centre dummy at 1 s, both come to rest touching. OnCollisionStay then fires every physics
            // step (the motor keeps the bodies awake) with almost no relative motion; that must not hold off the reset.
            const float delay = 3f, threshold = 0.5f, step = 0.02f, hit = 1f;
            var clock = new TrainingDummyResetClock();
            clock.Restart(0f);
            if (TrainingDummyResetClock.IsDisturbingContact(8f, threshold)) clock.MarkDisturbed(hit);
            float now = hit;
            while (now < hit + delay + step)
            {
                now += step;
                if (TrainingDummyResetClock.IsDisturbingContact(0.01f, threshold)) clock.MarkDisturbed(now);
            }
            Assert.That(clock.ShouldReset(hit + delay, true, delay), Is.True);
        }

        [Test]
        public void SecondsUntilResetIsNegativeWhenNoResetIsPending()
        {
            var clock = new TrainingDummyResetClock();
            clock.Restart(0f);
            clock.MarkDisturbed(1f);
            Assert.That(clock.SecondsUntilReset(2f, true, 3f), Is.EqualTo(2f).Within(Tolerance));
            Assert.That(clock.SecondsUntilReset(9f, true, 3f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(clock.SecondsUntilReset(2f, false, 3f), Is.LessThan(0f));
            clock.Restart(9f);
            Assert.That(clock.ShouldReset(9f, true, 3f), Is.False, "A reset restarts the wait so the queued teleport is not requested twice.");
        }

        [Test]
        public void RemainingSecondsNeverUnderflows()
        {
            Assert.That(TrainingDummyReadoutText.RemainingSeconds(70u, 10u, 1d / 60d), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(TrainingDummyReadoutText.RemainingSeconds(10u, 70u, 1d / 60d), Is.Zero);
            Assert.That(TrainingDummyReadoutText.RemainingSeconds(0u, 0u, 1d / 60d), Is.Zero);
        }

        [Test]
        public void EffectsFromModifiersIgnoreLaunchBypassAndSteeringSuppression()
        {
            var state = new BuddahPredictedModifierState { SuppressSteeringUntilTick = 500u, RoomBypassUntilTick = 500u };
            TrainingDummyEffects fx = TrainingDummyEffects.FromModifiers(state, 100u, 0.1d, 0f);
            Assert.That(fx.AnyActive, Is.False);

            state = new BuddahPredictedModifierState
            {
                AccelUntilTick = 125u, AccelExtraMaxSpeed = -20f, InvertTurnUntilTick = 130u, RootUntilTick = 90u,
                ScaleUntilTick = 140u, ScaleMultiplier = 1.5f, PushGraceUntilTick = 102u
            };
            fx = TrainingDummyEffects.FromModifiers(state, 100u, 0.1d, 1.5f);
            Assert.That(fx.AnyActive, Is.True);
            Assert.That(fx.IsSlow, Is.True);
            Assert.That(fx.AccelSeconds, Is.EqualTo(2.5f).Within(Tolerance));
            Assert.That(fx.InvertSeconds, Is.EqualTo(3f).Within(Tolerance));
            Assert.That(fx.RootSeconds, Is.Zero, "Expired deadlines read as inactive.");
            Assert.That(fx.ScaleSeconds, Is.EqualTo(4f).Within(Tolerance));
            Assert.That(fx.ScaleMultiplier, Is.EqualTo(1.5f).Within(Tolerance));
            Assert.That(fx.PushGraceSeconds, Is.EqualTo(0.2f).Within(Tolerance));
            Assert.That(fx.FrozenSeconds, Is.EqualTo(1.5f).Within(Tolerance));
        }

        [Test]
        public void PostRootAccelerationReadsAsAccelerationWhenNoDirectAccelIsActive()
        {
            var state = new BuddahPredictedModifierState { PostRootAccelUntilTick = 110u, PostRootAccelExtraMaxSpeed = 12f };
            TrainingDummyEffects fx = TrainingDummyEffects.FromModifiers(state, 100u, 0.1d, 0f);
            Assert.That(fx.AccelSeconds, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(fx.IsSlow, Is.False);
        }

        [Test]
        public void ReadoutListsActiveEffectsWithRemainingSeconds()
        {
            var fx = new TrainingDummyEffects
            {
                FrozenSeconds = 1.2f, RootSeconds = 0.5f, AccelSeconds = 2.5f, AccelExtraMaxSpeed = -20f,
                InvertSeconds = 3f, ScaleSeconds = 4f, ScaleMultiplier = 1.5f, PushGraceSeconds = 0.2f
            };
            string text = TrainingDummyReadoutText.Build("Training Dummy 1", fx, 300.4f, 2f);
            Assert.That(text, Is.EqualTo(
                "Training Dummy 1\nFROZEN 1.2s\nROOT 0.5s\nSLOW 2.5s\nINVERT 3.0s\nSCALE x1.50 4.0s\nPUSH GRACE 0.2s\nIMPULSE 300\nRESET 2.0s"));
        }

        [Test]
        public void ReadoutShowsAccelerationAndAnIdleLine()
        {
            var fx = new TrainingDummyEffects { AccelSeconds = 1f, AccelExtraMaxSpeed = 15f };
            Assert.That(TrainingDummyReadoutText.Build("D", fx, -1f, -1f), Is.EqualTo("D\nACCEL 1.0s"));
            Assert.That(TrainingDummyReadoutText.Build("D", default, -1f, -1f), Is.EqualTo("D\n-"));
        }

        private static void AssertVector(Vector3 actual, Vector3 expected, string message = null)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"{message} expected {expected} got {actual}");
        }
    }
}
