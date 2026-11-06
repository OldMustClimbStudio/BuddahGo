using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class ObsessionBackfireRulesTests
    {
        // Skill/Obsession table values.
        private const float Min = 0f, Max = 1000f, PMax = 95f, K = 0.008f, Mid = 500f, KeepAt = 1000f;

        private static float P(float x) => ObsessionBackfireRules.ProbabilityPercent(x, Min, Max, PMax, K, Mid, KeepAt);

        [Test]
        public void BackfireProbabilityFollowsTheCurveAndClampsTheInput()
        {
            EveryCase.Parts(
                ("the curve is unchanged", () => EveryCase.Check(new[]
                {
                    (obsession: 0f, percent: 1.71f), (obsession: 500f, percent: 47.5f), (obsession: 1000f, percent: 95f),
                }, c => Assert.That(P(c.obsession), Is.EqualTo(c.percent).Within(0.01f)), c => "obsession " + c.obsession)),
                ("matches the README table and clamps the input", () =>
                {
                    float[] expected = { 1.7f, 3.7f, 7.9f, 16f, 30f, 48f, 66f, 79f, 87f, 91f, 95f };
                    for (int i = 0; i < expected.Length; i++)
                        Assert.That(P(i * 100f), Is.EqualTo(expected[i]).Within(0.6f), "obsession " + (i * 100));
                    Assert.That(P(-50f), Is.EqualTo(P(0f)));
                    Assert.That(P(5000f), Is.EqualTo(PMax));
                }));
        }

        [Test]
        public void ObsessionDecaysUnlessPausedAndTheEndKeepsEightyPercent()
        {
            EveryCase.Parts(
                ("the backfire end keeps eighty percent", () => EveryCase.Check(new[]
                {
                    (atBackfire: 500f, atUnlock: 400f), (atBackfire: 700f, atUnlock: 560f), (atBackfire: 900f, atUnlock: 720f),
                }, c => Assert.That(ObsessionBackfireRules.Scale(c.atBackfire, 0.8f, Min, Max), Is.EqualTo(c.atUnlock).Within(1e-3f)),
                c => "atBackfire " + c.atBackfire)),
                ("scale clamps to the range and rejects negative ratios", () =>
                {
                    Assert.That(ObsessionBackfireRules.Scale(900f, 2f, Min, Max), Is.EqualTo(Max));
                    Assert.That(ObsessionBackfireRules.Scale(900f, -1f, Min, Max), Is.EqualTo(Min));
                }),
                ("natural decay pauses during the backfire", () =>
                {
                    Assert.That(ObsessionBackfireRules.Drain(700f, Min, Max, 10f, 0.5f, paused: true), Is.EqualTo(700f));
                    Assert.That(ObsessionBackfireRules.Drain(700f, Min, Max, 10f, 0.5f, paused: false), Is.EqualTo(695f).Within(1e-4f));
                    Assert.That(ObsessionBackfireRules.Drain(3f, Min, Max, 10f, 0.5f, paused: false), Is.EqualTo(Min));
                    Assert.That(ObsessionBackfireRules.Drain(Min, Min, Max, 10f, 0.5f, paused: false), Is.EqualTo(Min));
                }),
                ("the frozen value times the keep ratio is the unlock value", () =>
                {
                    // Ten seconds of paused decay followed by the end ratio: 900 → 720 (README 6.2).
                    float value = 900f;
                    for (int frame = 0; frame < 600; frame++)
                        value = ObsessionBackfireRules.Drain(value, Min, Max, 10f, 1f / 60f, paused: true);
                    Assert.That(ObsessionBackfireRules.Scale(value, 0.8f, Min, Max), Is.EqualTo(720f).Within(1e-3f));
                }));
        }

        [Test]
        public void BackfireEndTickRunsOnServerTicks()
        {
            EveryCase.Parts(
                ("end tick and seconds left use server ticks", () =>
                {
                    const double delta = 0.02;
                    uint end = ObsessionBackfireRules.EndTick(100u, 10f, delta);
                    Assert.That(end, Is.EqualTo(600u));
                    Assert.That(ObsessionBackfireRules.SecondsLeft(end, 100u, delta), Is.EqualTo(10f).Within(1e-4f));
                    Assert.That(ObsessionBackfireRules.SecondsLeft(end, 350u, delta), Is.EqualTo(5f).Within(1e-4f));
                    Assert.That(ObsessionBackfireRules.SecondsLeft(end, 600u, delta), Is.EqualTo(0f));
                    Assert.That(ObsessionBackfireRules.SecondsLeft(end, 900u, delta), Is.EqualTo(0f));
                    Assert.That(ObsessionBackfireRules.SecondsLeft(0u, 0u, delta), Is.EqualTo(0f), "zero is the inactive sentinel");
                }),
                ("a zero lock has no end tick and short locks still last one tick", () =>
                {
                    Assert.That(ObsessionBackfireRules.EndTick(100u, 0f, 0.02), Is.EqualTo(0u));
                    Assert.That(ObsessionBackfireRules.EndTick(0u, 0.001f, 0.02), Is.EqualTo(1u));
                }),
                ("the end is due from the end tick only while the end tick is set", () =>
                {
                    Assert.That(ObsessionBackfireRules.IsEndDue(600u, 599u), Is.False, "still running");
                    Assert.That(ObsessionBackfireRules.IsEndDue(600u, 600u), Is.True, "the end tick itself is due");
                    Assert.That(ObsessionBackfireRules.IsEndDue(600u, 900u), Is.True, "a late check is still due");
                    Assert.That(ObsessionBackfireRules.IsEndDue(0u, 900u), Is.False, "0 = no penalty to end");
                }),
                // The cast gate settles a due end before it opens (SkillExecutor.EvaluateCastGate → SettleBackfireServer): the
                // first request at the end tick sees the gate open exactly when the end is due, and rolls against the kept value.
                ("a request at the end tick rolls against the kept value", () =>
                {
                    const double delta = 0.02;
                    uint end = ObsessionBackfireRules.EndTick(100u, 10f, delta);
                    Assert.That(ObsessionBackfireRules.SecondsLeft(end, end, delta), Is.EqualTo(0f), "the gate opens at the end tick");
                    Assert.That(ObsessionBackfireRules.IsEndDue(end, end), Is.True, "so the end must be settled at the same tick");
                    float settled = ObsessionBackfireRules.Scale(700f, 0.8f, Min, Max);
                    Assert.That(P(settled), Is.LessThan(P(700f)) & Is.EqualTo(P(560f)).Within(1e-4f));
                }),
                ("re-entering keeps the later end", () =>
                {
                    Assert.That(ObsessionBackfireRules.ExtendEndTick(600u, 400u), Is.EqualTo(600u));
                    Assert.That(ObsessionBackfireRules.ExtendEndTick(600u, 900u), Is.EqualTo(900u));
                    Assert.That(ObsessionBackfireRules.ExtendEndTick(0u, 400u), Is.EqualTo(400u));
                }));
        }
    }
}
