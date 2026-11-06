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

        [Test]
        public void DummiesAreSpacedAlongTheLapAndWrap()
        {
            // The RaceMap launch slot projects about 37 m before the start line of a 5750 m lap; the default
            // offset puts the three dummies on the first straight after the line.
            EveryCase.Check(new[]
            {
                (start: 5713f, offset: 90f, spacing: 15f, index: 0, lap: 5750f, expected: 53f, tol: 1e-2f),
                (start: 5713f, offset: 90f, spacing: 15f, index: 1, lap: 5750f, expected: 68f, tol: 1e-2f),
                (start: 5713f, offset: 90f, spacing: 15f, index: 2, lap: 5750f, expected: 83f, tol: 1e-2f),
                // Negative distances wrap into the lap; a degenerate track has no length.
                (start: 10f, offset: -30f, spacing: 15f, index: 0, lap: 5750f, expected: 5730f, tol: 1e-2f),
                (start: 10f, offset: 5f, spacing: 15f, index: 1, lap: 0f, expected: 0f, tol: 0f),
            }, c => Assert.That(TrainingDummyLayout.DistanceAlongTrack(c.start, c.offset, c.spacing, c.index, c.lap), Is.EqualTo(c.expected).Within(c.tol)),
            c => $"start={c.start} offset={c.offset} spacing={c.spacing} index={c.index} lap={c.lap}");
        }

        [Test]
        public void DummiesSpreadAcrossTheTrackClearOfTheWalls()
        {
            EveryCase.Parts(
                ("lateral factor spreads left, centre, right", () => EveryCase.Check(new[]
                {
                    (index: 0, count: 3, expected: -1f), (index: 1, count: 3, expected: 0f), (index: 2, count: 3, expected: 1f),
                    (index: 0, count: 1, expected: 0f), (index: 0, count: 2, expected: -1f), (index: 1, count: 2, expected: 1f),
                    (index: 2, count: 5, expected: 0f), (index: 1, count: 5, expected: -0.5f),
                }, c => Assert.That(TrainingDummyLayout.LateralFactor(c.index, c.count), Is.EqualTo(c.expected).Within(Tolerance)),
                c => $"index={c.index} count={c.count}")),
                ("lateral offset keeps clearance from the wall on its own side", () => EveryCase.Check(new[]
                {
                    // Left dummy measured against the left wall, right dummy against the right wall, centre never moves.
                    (index: 0, leftWall: 12f, rightWall: 30f, expected: -7f),
                    (index: 2, leftWall: 12f, rightWall: 30f, expected: 10f),
                    (index: 2, leftWall: 30f, rightWall: 8f, expected: 3f),
                    (index: 1, leftWall: 1f, rightWall: 1f, expected: 0f),
                    // No wall hit (open section) keeps the configured offset; a wall closer than the clearance leaves the body centred.
                    (index: 0, leftWall: float.PositiveInfinity, rightWall: 30f, expected: -10f),
                    (index: 0, leftWall: 3f, rightWall: 30f, expected: 0f),
                }, c => Assert.That(TrainingDummyLayout.ResolveLateralOffset(c.index, 3, 10f, c.leftWall, c.rightWall, 5f), Is.EqualTo(c.expected).Within(Tolerance)),
                c => $"index={c.index} of 3 offset=10 leftWall={c.leftWall} rightWall={c.rightWall} clearance=5")),
                // When the ground under a side dummy is off the track, placement retries closer to the racing line, ending on it.
                ("lateral candidates shrink toward the racing line", () => EveryCase.Check(new[]
                {
                    (offset: -8f, attempt: 0, attempts: 3, expected: -8f), (offset: -8f, attempt: 1, attempts: 3, expected: -4f),
                    (offset: -8f, attempt: 2, attempts: 3, expected: 0f), (offset: 6f, attempt: 0, attempts: 2, expected: 6f),
                    (offset: 6f, attempt: 1, attempts: 2, expected: 0f), (offset: 6f, attempt: 0, attempts: 1, expected: 6f),
                    (offset: 6f, attempt: 5, attempts: 3, expected: 0f),
                }, c => Assert.That(TrainingDummyLayout.LateralCandidate(c.offset, c.attempt, c.attempts), Is.EqualTo(c.expected).Within(Tolerance)),
                c => $"offset={c.offset} attempt={c.attempt}/{c.attempts}")),
                ("right and facing use the flat track tangent", () =>
                {
                    AssertVector(TrainingDummyLayout.RightOf(Vector3.forward), Vector3.right);
                    AssertVector(TrainingDummyLayout.RightOf(new Vector3(1f, 0.5f, 0f)), Vector3.back);
                    AssertVector(TrainingDummyLayout.RightOf(Vector3.up), Vector3.right, "A vertical tangent falls back to world forward.");
                    AssertVector(TrainingDummyLayout.FacingAlong(new Vector3(0f, 0.3f, 2f)) * Vector3.forward, Vector3.forward);
                    AssertVector(TrainingDummyLayout.FacingAlong(Vector3.right) * Vector3.forward, Vector3.right);
                    AssertVector(TrainingDummyLayout.Place(new Vector3(1f, 2f, 3f), Vector3.forward, -4f), new Vector3(-3f, 2f, 3f));
                }));
        }

        [Test]
        public void ResetWaitsForTheDelayAfterTheLastRealDisturbance()
        {
            EveryCase.Parts(
                ("waits after the last disturbance only while displaced", () =>
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
                }),
                ("only a contact with real relative motion is a disturbance", () => EveryCase.Check(new[]
                {
                    (speed: 0f, expected: false), (speed: 0.02f, expected: false), (speed: 0.5f, expected: false),
                    (speed: 0.51f, expected: true), (speed: 12f, expected: true),
                }, c => Assert.That(TrainingDummyResetClock.IsDisturbingContact(c.speed, 0.5f), Is.EqualTo(c.expected)),
                c => $"relativeSpeed={c.speed} threshold=0.5")),
                ("a dummy resting against another body still resets", () =>
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
                }),
                ("seconds until reset is negative when no reset is pending", () =>
                {
                    var clock = new TrainingDummyResetClock();
                    clock.Restart(0f);
                    clock.MarkDisturbed(1f);
                    Assert.That(clock.SecondsUntilReset(2f, true, 3f), Is.EqualTo(2f).Within(Tolerance));
                    Assert.That(clock.SecondsUntilReset(9f, true, 3f), Is.EqualTo(0f).Within(Tolerance));
                    Assert.That(clock.SecondsUntilReset(2f, false, 3f), Is.LessThan(0f));
                    clock.Restart(9f);
                    Assert.That(clock.ShouldReset(9f, true, 3f), Is.False, "A reset restarts the wait so the queued teleport is not requested twice.");
                }));
        }

        [Test]
        public void EffectsReadFromModifierDeadlines()
        {
            EveryCase.Parts(
                ("launch bypass and steering suppression are ignored", () =>
                {
                    var state = new BuddahPredictedModifierState { SuppressSteeringUntilTick = 500u, RoomBypassUntilTick = 500u };
                    TrainingDummyEffects fx = TrainingDummyEffects.FromModifiers(state, 100u, 0.1d, 0f);
                    Assert.That(fx.AnyActive, Is.False);
                }),
                ("active deadlines read as remaining seconds", () =>
                {
                    var state = new BuddahPredictedModifierState
                    {
                        AccelUntilTick = 125u, AccelExtraMaxSpeed = -20f, InvertTurnUntilTick = 130u, RootUntilTick = 90u,
                        ScaleUntilTick = 140u, ScaleMultiplier = 1.5f, PushGraceUntilTick = 102u
                    };
                    TrainingDummyEffects fx = TrainingDummyEffects.FromModifiers(state, 100u, 0.1d, 1.5f);
                    Assert.That(fx.AnyActive, Is.True);
                    Assert.That(fx.IsSlow, Is.True);
                    Assert.That(fx.AccelSeconds, Is.EqualTo(2.5f).Within(Tolerance));
                    Assert.That(fx.InvertSeconds, Is.EqualTo(3f).Within(Tolerance));
                    Assert.That(fx.RootSeconds, Is.Zero, "Expired deadlines read as inactive.");
                    Assert.That(fx.ScaleSeconds, Is.EqualTo(4f).Within(Tolerance));
                    Assert.That(fx.ScaleMultiplier, Is.EqualTo(1.5f).Within(Tolerance));
                    Assert.That(fx.PushGraceSeconds, Is.EqualTo(0.2f).Within(Tolerance));
                    Assert.That(fx.FrozenSeconds, Is.EqualTo(1.5f).Within(Tolerance));
                }),
                ("post-root acceleration reads as acceleration when no direct accel is active", () =>
                {
                    var state = new BuddahPredictedModifierState { PostRootAccelUntilTick = 110u, PostRootAccelExtraMaxSpeed = 12f };
                    TrainingDummyEffects fx = TrainingDummyEffects.FromModifiers(state, 100u, 0.1d, 0f);
                    Assert.That(fx.AccelSeconds, Is.EqualTo(1f).Within(Tolerance));
                    Assert.That(fx.IsSlow, Is.False);
                }));
        }

        [Test]
        public void ReadoutListsActiveEffectsWithRemainingSeconds()
        {
            EveryCase.Parts(
                ("remaining seconds never underflow", () =>
                {
                    Assert.That(TrainingDummyReadoutText.RemainingSeconds(70u, 10u, 1d / 60d), Is.EqualTo(1f).Within(Tolerance));
                    Assert.That(TrainingDummyReadoutText.RemainingSeconds(10u, 70u, 1d / 60d), Is.Zero);
                    Assert.That(TrainingDummyReadoutText.RemainingSeconds(0u, 0u, 1d / 60d), Is.Zero);
                }),
                ("every active effect", () =>
                {
                    var fx = new TrainingDummyEffects
                    {
                        FrozenSeconds = 1.2f, RootSeconds = 0.5f, AccelSeconds = 2.5f, AccelExtraMaxSpeed = -20f,
                        InvertSeconds = 3f, ScaleSeconds = 4f, ScaleMultiplier = 1.5f, PushGraceSeconds = 0.2f
                    };
                    string text = TrainingDummyReadoutText.Build("Training Dummy 1", fx, 300.4f, 2f);
                    Assert.That(text, Is.EqualTo(
                        "Training Dummy 1\nFROZEN 1.2s\nROOT 0.5s\nSLOW 2.5s\nINVERT 3.0s\nSCALE x1.50 4.0s\nPUSH GRACE 0.2s\nIMPULSE 300\nRESET 2.0s"));
                }),
                ("acceleration and the idle line", () =>
                {
                    var fx = new TrainingDummyEffects { AccelSeconds = 1f, AccelExtraMaxSpeed = 15f };
                    Assert.That(TrainingDummyReadoutText.Build("D", fx, -1f, -1f), Is.EqualTo("D\nACCEL 1.0s"));
                    Assert.That(TrainingDummyReadoutText.Build("D", default, -1f, -1f), Is.EqualTo("D\n-"));
                }));
        }

        private static void AssertVector(Vector3 actual, Vector3 expected, string message = null)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"{message} expected {expected} got {actual}");
        }
    }
}
