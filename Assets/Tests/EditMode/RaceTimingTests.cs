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
        public void CrossingsAndFinishNeverRegress()
        {
            EveryCase.Parts(
                ("an observed final crossing is not overwritten by a late or duplicate finish", () =>
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
                }),
                ("a finish cannot regress an already recorded lap count", () =>
                {
                    var timing = new RaceTiming();
                    timing.Begin(0d);
                    timing.ObserveCompletedLaps(Racer, 1, 10d);
                    timing.ObserveCompletedLaps(Racer, 2, 25d);
                    timing.Finish(Racer, 1, 30d);
                    timing.TryGetResult(Racer, out var result);
                    Assert.That(result.Finished, Is.False);
                    Assert.That(result.LapSeconds, Is.EqualTo(new[] { 10d, 15d }));
                }),
                ("duplicate and regressive lap reports do not move the crossing time", () =>
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
                }),
                ("crossing times cannot regress even when after the start", () =>
                {
                    var timing = new RaceTiming();
                    timing.Begin(10d);
                    timing.ObserveCompletedLaps(Racer, 1, 30d);
                    timing.ObserveCompletedLaps(Racer, 2, 25d);
                    timing.Finish(Racer, 1, 25d);
                    timing.TryGetResult(Racer, out var result);
                    Assert.That(result.Finished, Is.False);
                    Assert.That(result.LapSeconds, Is.EqualTo(new[] { 20d }));
                }));
        }

        [Test]
        public void ZeroLapObservationsDoNotMoveTheGoTimeAndStayNonNegative()
        {
            EveryCase.Parts(
                ("zero completed laps do not move the authoritative GO time", () =>
                {
                    var timing = new RaceTiming();
                    timing.Begin(100d);
                    timing.ObserveCompletedLaps(Racer, 0, 107d);
                    timing.ObserveCompletedLaps(Racer, 1, 120d);
                    timing.Finish(Racer, 2, 130d);
                    timing.TryGetResult(Racer, out var result);
                    Assert.That(result.LapSeconds, Is.EqualTo(new[] { 20d, 10d }));
                    Assert.That(result.TotalSeconds, Is.EqualTo(30d));
                }),
                ("a zero-lap observation and a same-tick crossing stay non-negative", () =>
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
                }));
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

        [Test]
        public void InvalidTimesAndLapCountsCannotCreateOrChangeResults()
        {
            EveryCase.Parts(
                ("invalid observation and finish times cannot create or change results", () =>
                    EveryCase.Check(new[] { -1d, 9d, double.NaN, double.PositiveInfinity, double.NegativeInfinity }, now =>
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
                    }, now => "now=" + now)),
                ("an invalid finish lap count cannot finish", () =>
                    EveryCase.Check(new[] { 0, -1 }, count =>
                    {
                        var timing = new RaceTiming();
                        timing.Begin(0d);
                        timing.Finish(Racer, count, 10d);
                        Assert.That(timing.TryGetResult(Racer, out _), Is.False);
                    }, count => "count=" + count)));
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
        public void ResetAndBeginClearPriorMatchButInvalidBeginKeepsIt()
        {
            EveryCase.Parts(
                ("reset and begin clear the prior match and require a start", () =>
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
                }),
                ("an invalid begin does not clear a valid match", () =>
                    EveryCase.Check(new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity }, now =>
                    {
                        var timing = new RaceTiming();
                        timing.Begin(0d);
                        timing.Finish(Racer, 1, 20d);
                        Assert.Throws<ArgumentOutOfRangeException>(() => timing.Begin(now));
                        Assert.That(timing.TryGetResult(Racer, out var result), Is.True);
                        Assert.That(result.TotalSeconds, Is.EqualTo(20d));
                    }, now => "now=" + now)));
        }
    }
}
