using NewBuddah.PredictionV2.Debugging;
using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class PredictionDiagnosticSamplingTests
    {
        [Test]
        public void SamplingGateAndReleaseStripping()
        {
            EveryCase.Parts(
                ("no consumer does not advance the clock", () =>
                {
                    float last = float.NegativeInfinity;
                    Assert.That(BuddahDiagnosticLogSampling.TryBeginSample(false, 10f, .5f, ref last), Is.False);
                    Assert.That(last, Is.EqualTo(float.NegativeInfinity));
                }),
                ("the build gate matches verbose availability", () =>
                {
                    float last = float.NegativeInfinity;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Assert.That(BuddahDiagnosticLogSampling.TryBeginSample(true, 10f, .5f, ref last), Is.True);
                    Assert.That(last, Is.EqualTo(10f));
#else
                    Assert.That(BuddahDiagnosticLogSampling.TryBeginSample(true, 10f, .5f, ref last), Is.False);
                    Assert.That(last, Is.EqualTo(float.NegativeInfinity));
#endif
                }),
                ("unchanged text still consumes the interval", () =>
                {
                    float last = float.NegativeInfinity;
                    int formats = 0;
                    // No text changes for two seconds at 100 fps. Sampling cannot depend on dedup.
                    for (int frame = 0; frame < 200; frame++)
                        if (BuddahDiagnosticLogSampling.TryBeginSample(true, frame / 100f, .5f, ref last))
                            formats++;
                    Assert.That(formats, Is.EqualTo(BuddahDiagnosticLogSampling.VerboseCompiledIn ? 4 : 0));
                }),
                ("an invalid serialized interval has a minimum", () =>
                {
                    float last = 0f;
                    Assert.That(BuddahDiagnosticLogSampling.TryBeginSample(true, .099f, -1f, ref last), Is.False);
                    Assert.That(BuddahDiagnosticLogSampling.TryBeginSample(true, .1f, -1f, ref last),
                        Is.EqualTo(BuddahDiagnosticLogSampling.VerboseCompiledIn));
                }),
                ("release strips capture argument evaluation", () =>
                {
                    int evaluations = 0;
                    var snapshot = new BuddahPredictionLogSnapshot();
                    snapshot.CaptureReplicate((uint)++evaluations, 0, 0, "active");
                    snapshot.CaptureReconcile((uint)++evaluations, 0, 0, 0, 0, 0);
                    Assert.That(evaluations, Is.EqualTo(BuddahDiagnosticLogSampling.VerboseCompiledIn ? 2 : 0));
                }));
        }
    }
}
