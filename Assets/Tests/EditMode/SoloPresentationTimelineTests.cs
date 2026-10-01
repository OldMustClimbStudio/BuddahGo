using NewBuddah.PredictionV2.Visual;
using NUnit.Framework;
using UnityEngine;

namespace BuddahGo.Tests
{
    public class SoloPresentationTimelineTests
    {
        private const double Step = 1d / 60d;

        [Test]
        public void EmptyHistoryDoesNotInventPose()
        {
            Assert.False(new SoloPresentationTimeline(Step).TrySample(10, out _, out _));
        }

        [Test]
        public void DrivingKeepsIntroDelayAndSignedSpeedAcrossGo()
        {
            var history = new SoloPresentationTimeline(Step, 256);
            // A completed intro endpoint at t=1, then the first real physics step.
            for (int i = 0; i <= 120; i++)
                history.Record(i * Step, Vector3.right * i, Quaternion.identity);
            for (double render = 0.95; render < 1.1; render += 1d / 144d)
            {
                history.TrySample(render, out var a, out _);
                history.TrySample(render + 1d / 144d, out var b, out _);
                Assert.That((b.x - a.x) * 144d, Is.EqualTo(60d).Within(0.002));
                Assert.That(a.x, Is.EqualTo((render - Step) * 60).Within(0.0001));
            }
        }

        [Test]
        public void RealRepeatedEndpointIsPreservedAndDetectedAsStop()
        {
            var history = new SoloPresentationTimeline(Step);
            history.Record(1, Vector3.right, Quaternion.identity);
            history.Record(1 + Step, Vector3.right, Quaternion.identity);
            history.Record(1 + 2 * Step, Vector3.right * 2, Quaternion.identity);
            history.TrySample(1 + Step, out var a, out _);
            history.TrySample(1 + 2 * Step, out var b, out _);
            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(b.x - a.x, Is.Zero);
        }

        [Test]
        public void DuplicateTimeReplacesObservationButOlderSamplesCannotRewind()
        {
            var history = new SoloPresentationTimeline(Step);
            history.Record(2, Vector3.zero, Quaternion.identity);
            history.Record(2, Vector3.right, Quaternion.identity);
            Assert.False(history.Record(1, Vector3.left, Quaternion.identity));
            Assert.That(history.Count, Is.EqualTo(1));
            history.TrySample(3, out var pose, out _);
            Assert.That(pose, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void UnderflowClampsWithoutExtrapolatingLaunchVelocity()
        {
            var history = new SoloPresentationTimeline(Step);
            history.Record(1, Vector3.zero, Quaternion.identity);
            history.Record(2, Vector3.right * 60, Quaternion.identity);
            history.TrySample(4, out var after, out _);
            history.TrySample(0, out var before, out _);
            Assert.That(after.x, Is.EqualTo(60));
            Assert.That(before.x, Is.Zero);
        }

        [Test]
        public void ResetForTeleportControlOrRematchDiscardsAllOldMotion()
        {
            var history = new SoloPresentationTimeline(Step);
            history.Record(10, Vector3.right * 600, Quaternion.identity);
            history.Clear();
            history.Record(1, Vector3.left, Quaternion.identity);
            history.TrySample(1, out var position, out _);
            Assert.That(position, Is.EqualTo(Vector3.left));
            Assert.That(history.Count, Is.EqualTo(1));
        }

        [Test]
        public void BoundedBufferRetainsNewestSamples()
        {
            var history = new SoloPresentationTimeline(Step, 3);
            for (int i = 0; i < 100; i++) history.Record(i, Vector3.right * i, Quaternion.identity);
            history.TrySample(-1, out var first, out _);
            Assert.That(first.x, Is.EqualTo(97));
            Assert.That(history.Count, Is.EqualTo(3));
        }

        [TestCase(0.0)]
        [TestCase(0.001)]
        [TestCase(0.008)]
        [TestCase(0.0166)]
        public void GoEpochUsesOneFixedBoundaryAndNeverRoundsEarlier(double phase)
        {
            double localGo = 10 + phase;
            double aligned = SoloPresentationTimeline.AlignGoToPhysics(2, 102, 110 + phase, 2, Step);
            Assert.That(aligned, Is.GreaterThanOrEqualTo(localGo - 1e-12));
            Assert.That(aligned - localGo, Is.LessThan(Step + 1e-12));
            Assert.That((aligned - 2) / Step, Is.EqualTo(System.Math.Round((aligned - 2) / Step)).Within(1e-10));
        }

        [Test]
        public void RotationSamplesTheSameTimeAsPosition()
        {
            var history = new SoloPresentationTimeline(Step);
            history.Record(0, Vector3.zero, Quaternion.identity);
            history.Record(1, Vector3.right, Quaternion.Euler(0, 90, 0));
            history.TrySample(0.5 + Step, out var p, out var q);
            Assert.That(p.x, Is.EqualTo(0.5).Within(0.0001));
            Assert.That(Quaternion.Angle(Quaternion.identity, q), Is.EqualTo(45).Within(0.001));
        }
    }
}
