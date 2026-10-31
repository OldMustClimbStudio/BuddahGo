using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class ObsessionBackfireRulesTests
    {
        // Skill/Obsession table values.
        private const float Min = 0f, Max = 1000f, PMax = 95f, K = 0.008f, Mid = 500f, KeepAt = 1000f;

        private static float P(float x) => ObsessionBackfireRules.ProbabilityPercent(x, Min, Max, PMax, K, Mid, KeepAt);

        [TestCase(0f, 1.71f)]
        [TestCase(500f, 47.5f)]
        [TestCase(1000f, 95f)]
        public void BackfireProbabilityCurveIsUnchanged(float obsession, float expectedPercent)
        {
            Assert.That(P(obsession), Is.EqualTo(expectedPercent).Within(0.01f));
        }

        [Test]
        public void BackfireProbabilityMatchesTheReadmeTableAndClampsTheInput()
        {
            float[] expected = { 1.7f, 3.7f, 7.9f, 16f, 30f, 48f, 66f, 79f, 87f, 91f, 95f };
            for (int i = 0; i < expected.Length; i++)
                Assert.That(P(i * 100f), Is.EqualTo(expected[i]).Within(0.6f), "obsession " + (i * 100));
            Assert.That(P(-50f), Is.EqualTo(P(0f)));
            Assert.That(P(5000f), Is.EqualTo(PMax));
        }

        [TestCase(500f, 400f)]
        [TestCase(700f, 560f)]
        [TestCase(900f, 720f)]
        public void BackfireEndKeepsEightyPercent(float atBackfire, float atUnlock)
        {
            Assert.That(ObsessionBackfireRules.Scale(atBackfire, 0.8f, Min, Max), Is.EqualTo(atUnlock).Within(1e-3f));
        }

        [Test]
        public void ScaleClampsToTheRangeAndRejectsNegativeRatios()
        {
            Assert.That(ObsessionBackfireRules.Scale(900f, 2f, Min, Max), Is.EqualTo(Max));
            Assert.That(ObsessionBackfireRules.Scale(900f, -1f, Min, Max), Is.EqualTo(Min));
        }

        [Test]
        public void NaturalDecayPausesDuringTheBackfire()
        {
            Assert.That(ObsessionBackfireRules.Drain(700f, Min, Max, 10f, 0.5f, paused: true), Is.EqualTo(700f));
            Assert.That(ObsessionBackfireRules.Drain(700f, Min, Max, 10f, 0.5f, paused: false), Is.EqualTo(695f).Within(1e-4f));
            Assert.That(ObsessionBackfireRules.Drain(3f, Min, Max, 10f, 0.5f, paused: false), Is.EqualTo(Min));
            Assert.That(ObsessionBackfireRules.Drain(Min, Min, Max, 10f, 0.5f, paused: false), Is.EqualTo(Min));
        }

        [Test]
        public void FrozenValueTimesKeepRatioIsTheUnlockValue()
        {
            // Ten seconds of paused decay followed by the end ratio: 900 → 720 (README 6.2).
            float value = 900f;
            for (int frame = 0; frame < 600; frame++)
                value = ObsessionBackfireRules.Drain(value, Min, Max, 10f, 1f / 60f, paused: true);
            Assert.That(ObsessionBackfireRules.Scale(value, 0.8f, Min, Max), Is.EqualTo(720f).Within(1e-3f));
        }

        [Test]
        public void BackfireEndTickAndSecondsLeftUseServerTicks()
        {
            const double delta = 0.02;
            uint end = ObsessionBackfireRules.EndTick(100u, 10f, delta);
            Assert.That(end, Is.EqualTo(600u));
            Assert.That(ObsessionBackfireRules.SecondsLeft(end, 100u, delta), Is.EqualTo(10f).Within(1e-4f));
            Assert.That(ObsessionBackfireRules.SecondsLeft(end, 350u, delta), Is.EqualTo(5f).Within(1e-4f));
            Assert.That(ObsessionBackfireRules.SecondsLeft(end, 600u, delta), Is.EqualTo(0f));
            Assert.That(ObsessionBackfireRules.SecondsLeft(end, 900u, delta), Is.EqualTo(0f));
            Assert.That(ObsessionBackfireRules.SecondsLeft(0u, 0u, delta), Is.EqualTo(0f), "zero is the inactive sentinel");
        }

        [Test]
        public void ZeroLockHasNoEndTickAndShortLocksStillLastOneTick()
        {
            Assert.That(ObsessionBackfireRules.EndTick(100u, 0f, 0.02), Is.EqualTo(0u));
            Assert.That(ObsessionBackfireRules.EndTick(0u, 0.001f, 0.02), Is.EqualTo(1u));
        }

        [Test]
        public void EndIsDueFromTheEndTickOnlyWhileTheEndTickIsSet()
        {
            Assert.That(ObsessionBackfireRules.IsEndDue(600u, 599u), Is.False, "still running");
            Assert.That(ObsessionBackfireRules.IsEndDue(600u, 600u), Is.True, "the end tick itself is due");
            Assert.That(ObsessionBackfireRules.IsEndDue(600u, 900u), Is.True, "a late check is still due");
            Assert.That(ObsessionBackfireRules.IsEndDue(0u, 900u), Is.False, "0 = no penalty to end");
        }

        // The cast gate settles a due end before it opens (SkillExecutor.EvaluateCastGate → SettleBackfireServer): the
        // first request at the end tick sees the gate open exactly when the end is due, and rolls against the kept value.
        [Test]
        public void ARequestAtTheEndTickRollsAgainstTheKeptValue()
        {
            const double delta = 0.02;
            uint end = ObsessionBackfireRules.EndTick(100u, 10f, delta);
            Assert.That(ObsessionBackfireRules.SecondsLeft(end, end, delta), Is.EqualTo(0f), "the gate opens at the end tick");
            Assert.That(ObsessionBackfireRules.IsEndDue(end, end), Is.True, "so the end must be settled at the same tick");
            float settled = ObsessionBackfireRules.Scale(700f, 0.8f, Min, Max);
            Assert.That(P(settled), Is.LessThan(P(700f)) & Is.EqualTo(P(560f)).Within(1e-4f));
        }

        [Test]
        public void ReenteringKeepsTheLaterEnd()
        {
            Assert.That(ObsessionBackfireRules.ExtendEndTick(600u, 400u), Is.EqualTo(600u));
            Assert.That(ObsessionBackfireRules.ExtendEndTick(600u, 900u), Is.EqualTo(900u));
            Assert.That(ObsessionBackfireRules.ExtendEndTick(0u, 400u), Is.EqualTo(400u));
        }
    }
}
