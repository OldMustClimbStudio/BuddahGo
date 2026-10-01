using System.Collections.Generic;
using System.Linq;
using BuddahGo.Match;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class AcceptedLapTimingTests
    {
        private static readonly RacerId Racer = RacerId.FromClient(7);

        private sealed class Clock : IMatchClock
        {
            public double Now { get; set; }
            public bool IsPaused => false;
        }

        private sealed class RecordingTiming : IRaceTiming
        {
            public readonly List<(int Lap, double Time)> Observations = new List<(int, double)>();
            public void ObserveCompletedLaps(RacerId racer, int completedLaps, double now) => Observations.Add((completedLaps, now));
            public void Begin(double startTime) { }
            public void Finish(RacerId racer, int lapsToFinish, double now) { }
            public bool TryGetResult(RacerId racer, out RaceTimingResult result) { result = default; return false; }
            public void Reset() => Observations.Clear();
        }

        [TestCase(0.183333333333d)]
        [TestCase(1.25d)]
        public void DelayedReportsAndFinishKeepNaturalCrossingsAndGoOrigin(double reportDelay)
        {
            var clock = new Clock { Now = 40.88333333333333d };
            var timing = new RaceTiming();
            var pending = new AcceptedLapTiming();
            timing.Begin(clock.Now);
            clock.Now = 42.13333333333333d;
            pending.Capture(0, clock, timing); // Entering lap 1 does not complete a lap or restart GO.
            pending.ObservePending(clock, timing, Racer, 3);
            Assert.That(timing.TryGetResult(Racer, out _), Is.False);

            double[] crossings = { 293.96666666666666d, 541.65d, 774.4d };
            for (int i = 0; i < crossings.Length; i++)
            {
                clock.Now = crossings[i];
                pending.Capture(i + 1, clock, timing);
                clock.Now += reportDelay;
                pending.ObservePending(clock, timing, Racer, 3);
                // The existing periodic report still observes now, after draining accepted crossings.
                timing.ObserveCompletedLaps(Racer, i + 1, clock.Now);
            }
            timing.Finish(Racer, 3, clock.Now);
            timing.Finish(Racer, 3, clock.Now + 10d);
            timing.TryGetResult(Racer, out var result);
            Assert.That(result.Finished, Is.True);
            Assert.That(result.LapSeconds, Is.EqualTo(new[] { 253.08333333333333d, 247.68333333333334d, 232.75d }).Within(1e-10));
            Assert.That(result.TotalSeconds, Is.EqualTo(733.5166666666667d).Within(1e-10));
            Assert.That(result.LapSeconds.Sum(), Is.EqualTo(result.TotalSeconds).Within(1e-10));
        }

        [Test]
        public void MultiplePendingCrossingsKeepOrderAndConsumeOnceWithoutReadingReportClock()
        {
            var clock = new Clock { Now = 10d };
            var timing = new RecordingTiming();
            var pending = new AcceptedLapTiming();
            pending.Capture(1, clock, timing);
            clock.Now = 20d;
            pending.Capture(2, clock, timing);
            clock.Now = 30d;
            pending.Capture(3, clock, timing);
            clock.Now = 100d;
            pending.ObservePending(clock, timing, Racer, 3);
            pending.ObservePending(clock, timing, Racer, 3);
            Assert.That(timing.Observations, Is.EqualTo(new[] { (1, 10d), (2, 20d), (3, 30d) }));
        }

        [Test]
        public void DuplicateAndRegressiveCrossingsCannotOverwriteOrReplayAcceptedTimes()
        {
            var clock = new Clock { Now = 10d };
            var timing = new RecordingTiming();
            var pending = new AcceptedLapTiming();
            pending.Capture(1, clock, timing);
            clock.Now = 11d;
            pending.Capture(1, clock, timing);
            pending.ObservePending(clock, timing, Racer, 3);
            pending.Capture(1, clock, timing);
            pending.Capture(0, clock, timing);
            clock.Now = 20d;
            pending.Capture(2, clock, timing);
            pending.Capture(1, clock, timing);
            pending.ObservePending(clock, timing, Racer, 3);
            Assert.That(timing.Observations, Is.EqualTo(new[] { (1, 10d), (2, 20d) }));
        }

        [Test]
        public void ResetDiscardsPendingMatchAndAllowsFirstLapAgainWithSameServices()
        {
            var clock = new Clock { Now = 10d };
            var timing = new RaceTiming();
            var pending = new AcceptedLapTiming();
            timing.Begin(0d);
            pending.Capture(1, clock, timing);
            pending.Reset(); // Network stop/start or new lap-1 initialization on Rematch.
            timing.Reset();
            timing.Begin(100d);
            pending.ObservePending(clock, timing, Racer, 3);
            Assert.That(timing.TryGetResult(Racer, out _), Is.False);
            clock.Now = 115d;
            pending.Capture(1, clock, timing);
            pending.ObservePending(clock, timing, Racer, 3);
            timing.TryGetResult(Racer, out var result);
            Assert.That(result.LapSeconds, Is.EqualTo(new[] { 15d }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ReplacingEitherSessionServiceDiscardsOldPendingCrossings(bool replaceClock)
        {
            var clock = new Clock { Now = 10d };
            var timing = new RecordingTiming();
            var pending = new AcceptedLapTiming();
            pending.Capture(1, clock, timing);
            if (replaceClock) clock = new Clock { Now = 20d };
            else timing = new RecordingTiming();
            pending.ObservePending(clock, timing, Racer, 3);
            Assert.That(timing.Observations, Is.Empty);
            clock.Now = 30d;
            pending.Capture(1, clock, timing);
            pending.ObservePending(clock, timing, Racer, 3);
            Assert.That(timing.Observations, Is.EqualTo(new[] { (1, 30d) }));
        }

        [Test]
        public void CaptureIntoNewSessionDoesNotCarryPreviousQueueOrDuplicateGuard()
        {
            var clock = new Clock { Now = 10d };
            var oldTiming = new RecordingTiming();
            var timing = new RecordingTiming();
            var pending = new AcceptedLapTiming();
            pending.Capture(1, clock, oldTiming);
            clock.Now = 20d;
            pending.Capture(1, clock, timing);
            pending.ObservePending(clock, timing, Racer, 3);
            Assert.That(oldTiming.Observations, Is.Empty);
            Assert.That(timing.Observations, Is.EqualTo(new[] { (1, 20d) }));
        }

        [TestCase(-1d), TestCase(double.NaN), TestCase(double.PositiveInfinity)]
        public void InvalidCaptureDoesNotBlockLaterValidCrossing(double invalidTime)
        {
            var clock = new Clock { Now = invalidTime };
            var timing = new RecordingTiming();
            var pending = new AcceptedLapTiming();
            pending.Capture(1, null, timing);
            pending.Capture(1, clock, null);
            pending.Capture(1, clock, timing);
            pending.ObservePending(clock, timing, Racer, 3);
            Assert.That(timing.Observations, Is.Empty);
            clock.Now = 10d;
            pending.Capture(1, clock, timing);
            pending.ObservePending(clock, timing, Racer, 3);
            Assert.That(timing.Observations, Is.EqualTo(new[] { (1, 10d) }));
        }

        [Test]
        public void CrossingsBeyondRaceLengthDoNotAddOrClampToAnExtraFinalLap()
        {
            var clock = new Clock { Now = 10d };
            var timing = new RecordingTiming();
            var pending = new AcceptedLapTiming();
            pending.Capture(1, clock, timing);
            clock.Now = 20d;
            pending.Capture(2, clock, timing);
            pending.ObservePending(clock, timing, Racer, 1);
            Assert.That(timing.Observations, Is.EqualTo(new[] { (1, 10d) }));
        }
    }
}
