using System;
using System.Linq;
using BuddahGo.Match;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class RaceTimingTests
    {
        private static readonly RacerId Racer = RacerId.FromClient(7);

        [Test]
        public void FinalCrossingCompletesLapTimesWhoseSumIsTotal()
        {
            var timing = new RaceTiming();
            timing.Begin(100d);
            timing.ObserveCompletedLaps(Racer, 1, 112.5d);
            timing.ObserveCompletedLaps(Racer, 2, 125d);
            timing.Finish(Racer, 3, 138.25d);
            Assert.That(timing.TryGetResult(Racer, out var result), Is.True);
            Assert.That(result.Racer, Is.EqualTo(Racer));
            Assert.That(result.Finished, Is.True);
            Assert.That(result.LapSeconds, Is.EqualTo(new[] { 12.5d, 12.5d, 13.25d }));
            Assert.That(result.TotalSeconds, Is.EqualTo(38.25d));
            Assert.That(result.LapSeconds.Sum(), Is.EqualTo(result.TotalSeconds).Within(1e-10));
        }

        [Test]
        public void ObservedFinalCrossingIsNotOverwrittenByLateOrDuplicateFinish()
        {
            var timing = new RaceTiming();
            timing.Begin(100d);
            timing.ObserveCompletedLaps(Racer, 1, 110d);
            timing.ObserveCompletedLaps(Racer, 2, 125d);
            timing.Finish(Racer, 2, 126d);
            timing.Finish(Racer, 2, 200d);
            timing.ObserveCompletedLaps(Racer, 3, 250d);
            timing.TryGetResult(Racer, out var result);
            Assert.That(result.Finished, Is.True);
            Assert.That(result.TotalSeconds, Is.EqualTo(25d));
            Assert.That(result.LapSeconds, Is.EqualTo(new[] { 10d, 15d }));
        }

        [Test]
        public void ZeroCompletedLapsDoNotMoveTheAuthoritativeGoTime()
        {
            var timing = new RaceTiming();
            timing.Begin(100d);
            timing.ObserveCompletedLaps(Racer, 0, 107d);
            timing.ObserveCompletedLaps(Racer, 1, 120d);
            timing.Finish(Racer, 2, 130d);
            timing.TryGetResult(Racer, out var result);
            Assert.That(result.LapSeconds, Is.EqualTo(new[] { 20d, 10d }));
            Assert.That(result.TotalSeconds, Is.EqualTo(30d));
        }

        [Test]
        public void FinishCannotRegressAnAlreadyRecordedLapCount()
        {
            var timing = new RaceTiming();
            timing.Begin(0d);
            timing.ObserveCompletedLaps(Racer, 1, 10d);
            timing.ObserveCompletedLaps(Racer, 2, 25d);
            timing.Finish(Racer, 1, 30d);
            timing.TryGetResult(Racer, out var result);
            Assert.That(result.Finished, Is.False);
            Assert.That(result.LapSeconds, Is.EqualTo(new[] { 10d, 15d }));
        }

        [Test]
        public void DuplicateAndRegressiveLapReportsDoNotMoveCrossingTime()
        {
            var timing = new RaceTiming();
            timing.Begin(10d);
            timing.ObserveCompletedLaps(Racer, 1, 20d);
            timing.ObserveCompletedLaps(Racer, 1, 30d);
            timing.ObserveCompletedLaps(Racer, 0, 35d);
            timing.ObserveCompletedLaps(Racer, -1, 36d);
            timing.Finish(Racer, 2, 40d);
            timing.TryGetResult(Racer, out var result);
            Assert.That(result.LapSeconds, Is.EqualTo(new[] { 10d, 20d }));
        }

        [Test]
        public void MissingCrossingsDoNotFabricateLapTimesOrFinishedResults()
        {
            var timing = new RaceTiming();
            timing.Begin(10d);
            timing.ObserveCompletedLaps(Racer, 2, 40d);
            Assert.That(timing.TryGetResult(Racer, out _), Is.False);
            timing.ObserveCompletedLaps(Racer, 1, 20d);
            timing.Finish(Racer, 3, 60d);
            timing.TryGetResult(Racer, out var result);
            Assert.That(result.Finished, Is.False);
            Assert.That(result.LapSeconds, Is.EqualTo(new[] { 10d }));
            // Real missing timestamps can still be supplied in order; they are never interpolated.
            timing.ObserveCompletedLaps(Racer, 2, 40d);
            timing.Finish(Racer, 3, 60d);
            timing.TryGetResult(Racer, out result);
            Assert.That(result.Finished, Is.True);
            Assert.That(result.LapSeconds, Is.EqualTo(new[] { 10d, 20d, 20d }));
        }

        [TestCase(-1d), TestCase(9d), TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity), TestCase(double.NegativeInfinity)]
        public void InvalidObservationAndFinishTimesCannotCreateOrChangeResults(double now)
        {
            var timing = new RaceTiming();
            timing.Begin(10d);
            timing.ObserveCompletedLaps(Racer, 1, now);
            timing.Finish(Racer, 1, now);
            Assert.That(timing.TryGetResult(Racer, out _), Is.False);
            timing.ObserveCompletedLaps(Racer, 1, 20d);
            timing.ObserveCompletedLaps(Racer, 2, now);
            timing.Finish(Racer, 2, now);
            timing.TryGetResult(Racer, out var result);
            Assert.That(result.Finished, Is.False);
            Assert.That(result.LapSeconds, Is.EqualTo(new[] { 10d }));
        }

        [Test]
        public void CrossingTimesCannotRegressEvenWhenAfterTheStart()
        {
            var timing = new RaceTiming();
            timing.Begin(10d);
            timing.ObserveCompletedLaps(Racer, 1, 30d);
            timing.ObserveCompletedLaps(Racer, 2, 25d);
            timing.Finish(Racer, 1, 25d);
            timing.TryGetResult(Racer, out var result);
            Assert.That(result.Finished, Is.False);
            Assert.That(result.LapSeconds, Is.EqualTo(new[] { 20d }));
        }

        [Test]
        public void SeparateRacersUseOneStartAndIndependentCrossings()
        {
            var other = RacerId.FromClient(8);
            var timing = new RaceTiming();
            timing.Begin(10d);
            timing.Finish(Racer, 1, 30d);
            timing.ObserveCompletedLaps(other, 1, 35d);
            timing.Finish(other, 2, 60d);
            timing.TryGetResult(Racer, out var first);
            timing.TryGetResult(other, out var second);
            Assert.That(first.TotalSeconds, Is.EqualTo(20d));
            Assert.That(second.TotalSeconds, Is.EqualTo(50d));
            Assert.That(second.LapSeconds, Is.EqualTo(new[] { 25d, 25d }));
        }

        [Test]
        public void ReturnedArraysCannotMutateTimingAndSnapshotsDoNotGrow()
        {
            var timing = new RaceTiming();
            timing.Begin(0d);
            timing.ObserveCompletedLaps(Racer, 1, 10d);
            timing.TryGetResult(Racer, out var snapshot);
            snapshot.LapSeconds[0] = 999d;
            timing.Finish(Racer, 2, 25d);
            timing.TryGetResult(Racer, out var finished);
            Assert.That(finished.LapSeconds, Is.EqualTo(new[] { 10d, 15d }));
            Assert.That(snapshot.LapSeconds.Length, Is.EqualTo(1));
            Assert.That(snapshot.TotalSeconds, Is.EqualTo(10d));
            Assert.That(snapshot.Finished, Is.False);
            timing.Reset();
            Assert.That(finished.LapSeconds, Is.EqualTo(new[] { 10d, 15d }));
        }

        [Test]
        public void ResetAndBeginClearPriorMatchAndRequireAStart()
        {
            var timing = new RaceTiming();
            timing.Finish(Racer, 1, 20d);
            Assert.That(timing.TryGetResult(Racer, out _), Is.False);
            timing.Begin(0d);
            timing.Finish(Racer, 1, 20d);
            timing.Reset();
            timing.ObserveCompletedLaps(Racer, 1, 30d);
            Assert.That(timing.TryGetResult(Racer, out _), Is.False);
            timing.Begin(100d);
            timing.Finish(Racer, 1, 125d);
            timing.Begin(200d);
            Assert.That(timing.TryGetResult(Racer, out _), Is.False);
            timing.Finish(Racer, 1, 210d);
            timing.TryGetResult(Racer, out var result);
            Assert.That(result.TotalSeconds, Is.EqualTo(10d));
        }

        [TestCase(-1d), TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity), TestCase(double.NegativeInfinity)]
        public void InvalidBeginDoesNotClearValidMatch(double now)
        {
            var timing = new RaceTiming();
            timing.Begin(0d);
            timing.Finish(Racer, 1, 20d);
            Assert.Throws<ArgumentOutOfRangeException>(() => timing.Begin(now));
            Assert.That(timing.TryGetResult(Racer, out var result), Is.True);
            Assert.That(result.TotalSeconds, Is.EqualTo(20d));
        }

        [TestCase(0), TestCase(-1)]
        public void InvalidFinishLapCountCannotFinish(int count)
        {
            var timing = new RaceTiming();
            timing.Begin(0d);
            timing.Finish(Racer, count, 10d);
            Assert.That(timing.TryGetResult(Racer, out _), Is.False);
        }

        [Test]
        public void ZeroLapObservationAndSameTickCrossingStayNonNegative()
        {
            var timing = new RaceTiming();
            timing.Begin(10d);
            timing.ObserveCompletedLaps(Racer, 0, 10d);
            timing.TryGetResult(Racer, out var pending);
            Assert.That(pending.LapSeconds, Is.Empty);
            Assert.That(pending.TotalSeconds, Is.Zero);
            Assert.That(pending.Finished, Is.False);
            timing.Finish(Racer, 1, 10d);
            timing.TryGetResult(Racer, out var result);
            Assert.That(result.LapSeconds, Is.EqualTo(new[] { 0d }));
            Assert.That(result.Finished, Is.True);
        }
    }
}
