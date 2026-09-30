using System.Globalization;
using NewBuddah.PredictionV2.Debugging;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class PredictionDiagnosticSamplingTests
    {
        [Test]
        public void NoConsumerDoesNotAdvanceClock()
        {
            float last = float.NegativeInfinity;
            Assert.That(BuddahDiagnosticLogSampling.TryBeginSample(false, 10f, .5f, ref last), Is.False);
            Assert.That(last, Is.EqualTo(float.NegativeInfinity));
        }

        [Test]
        public void BuildGateMatchesVerboseAvailability()
        {
            float last = float.NegativeInfinity;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.That(BuddahDiagnosticLogSampling.TryBeginSample(true, 10f, .5f, ref last), Is.True);
            Assert.That(last, Is.EqualTo(10f));
#else
            Assert.That(BuddahDiagnosticLogSampling.TryBeginSample(true, 10f, .5f, ref last), Is.False);
            Assert.That(last, Is.EqualTo(float.NegativeInfinity));
#endif
        }

        [Test]
        public void UnchangedTextStillConsumesInterval()
        {
            float last = float.NegativeInfinity;
            int formats = 0;
            // No text changes for two seconds at 100 fps. Sampling cannot depend on dedup.
            for (int frame = 0; frame < 200; frame++)
                if (BuddahDiagnosticLogSampling.TryBeginSample(true, frame / 100f, .5f, ref last))
                    formats++;
            Assert.That(formats, Is.EqualTo(BuddahDiagnosticLogSampling.VerboseCompiledIn ? 4 : 0));
        }

        [Test]
        public void InvalidSerializedIntervalHasMinimum()
        {
            float last = 0f;
            Assert.That(BuddahDiagnosticLogSampling.TryBeginSample(true, .099f, -1f, ref last), Is.False);
            Assert.That(BuddahDiagnosticLogSampling.TryBeginSample(true, .1f, -1f, ref last),
                Is.EqualTo(BuddahDiagnosticLogSampling.VerboseCompiledIn));
        }

        [Test]
        public void ReplicateUsesLatestEventAndClearsOldWriterReason()
        {
            var snapshot = new BuddahPredictionLogSnapshot();
            Assert.That(snapshot.BuildReplicateSummary(), Is.EqualTo("n/a"));
            snapshot.CaptureReplicate(12, 1, 1, "writer-relinquished", "intro-control");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            StringAssert.Contains("writer-relinquished:intro-control", snapshot.BuildReplicateSummary());
#endif
            snapshot.CaptureReplicate(28, 0, 1, "active");
            string text = snapshot.BuildReplicateSummary();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            StringAssert.Contains("tick=28", text);
            StringAssert.EndsWith("active", text);
            StringAssert.DoesNotContain("intro-control", text);
#else
            Assert.That(text, Is.EqualTo("n/a"));
#endif
        }

        [Test]
        public void ReconcileRetainsItsOwnTimeAndValuesAcrossLaterReplicates()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                var snapshot = new BuddahPredictionLogSnapshot();
                Assert.That(snapshot.BuildReconcileSummary(), Is.EqualTo("n/a"));
                snapshot.CaptureReconcile(10, 7, 1, 2, 3, 4);
                snapshot.CaptureReplicate(25, -1, 0, "blocked");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Assert.That(snapshot.BuildReconcileSummary(), Is.EqualTo(
                    "tick=10 speed=7.00 posDelta=1.000 velDelta=2.000 planarVelDelta=3.000 verticalVelDelta=4.000"));
                snapshot.CaptureReconcile(22, 9, 5, 6, 7, 8);
                StringAssert.StartsWith("tick=22 speed=9.00", snapshot.BuildReconcileSummary());
#else
                Assert.That(snapshot.BuildReconcileSummary(), Is.EqualTo("n/a"));
#endif
            }
            finally { CultureInfo.CurrentCulture = originalCulture; }
        }

        [Test]
        public void ReleaseStripsCaptureArgumentEvaluation()
        {
            int evaluations = 0;
            var snapshot = new BuddahPredictionLogSnapshot();
            snapshot.CaptureReplicate((uint)++evaluations, 0, 0, "active");
            snapshot.CaptureReconcile((uint)++evaluations, 0, 0, 0, 0, 0);
            Assert.That(evaluations, Is.EqualTo(BuddahDiagnosticLogSampling.VerboseCompiledIn ? 2 : 0));
        }
    }
}
