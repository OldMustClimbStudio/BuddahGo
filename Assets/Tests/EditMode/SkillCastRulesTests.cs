using NUnit.Framework;

namespace BuddahGo.Tests
{
    public class SkillCastRulesTests
    {
        private const float Window = 5f;
        private const float Gain = 1.15f;

        [Test]
        public void CastGateReportsTheBlockingRule()
        {
            EveryCase.Parts(
                ("a pending cast blocks every slot", () =>
                {
                    Assert.That(SkillCastRules.EvaluateGate(true, false, false, 10f, 0f), Is.EqualTo(SkillCastBlock.Pending));
                    Assert.That(SkillCastRules.EvaluateGate(false, false, false, 10f, 0f), Is.EqualTo(SkillCastBlock.None));
                }),
                ("cooldown is per slot and other slots stay free without a global lock", () =>
                {
                    // Slot 0 was just cast (ready at 12 s); slots 1 and 2 were never cast. There is no global lock term.
                    float[] readyTimes = { 12f, 0f, 0f };
                    const float now = 7f;
                    Assert.That(SkillCastRules.EvaluateGate(false, false, false, now, readyTimes[0]), Is.EqualTo(SkillCastBlock.Cooldown));
                    Assert.That(SkillCastRules.EvaluateGate(false, false, false, now, readyTimes[1]), Is.EqualTo(SkillCastBlock.None));
                    Assert.That(SkillCastRules.EvaluateGate(false, false, false, now, readyTimes[2]), Is.EqualTo(SkillCastBlock.None));
                    Assert.That(SkillCastRules.EvaluateGate(false, false, false, 12f, readyTimes[0]), Is.EqualTo(SkillCastBlock.None),
                        "A slot is ready again exactly at its cooldown end");
                }),
                ("the backfire lock blocks all slots", () =>
                {
                    float[] readyTimes = { 0f, 0f, 0f };
                    foreach (float ready in readyTimes)
                        Assert.That(SkillCastRules.EvaluateGate(false, true, false, 50f, ready), Is.EqualTo(SkillCastBlock.BackfireLocked));
                }),
                ("frozen blocks casting", () =>
                    Assert.That(SkillCastRules.EvaluateGate(false, false, true, 50f, 0f), Is.EqualTo(SkillCastBlock.Frozen))),
                ("pending is reported before other blocks", () =>
                {
                    Assert.That(SkillCastRules.EvaluateGate(true, true, true, 0f, 99f), Is.EqualTo(SkillCastBlock.Pending));
                    Assert.That(SkillCastRules.EvaluateGate(false, true, true, 0f, 99f), Is.EqualTo(SkillCastBlock.BackfireLocked));
                    Assert.That(SkillCastRules.EvaluateGate(false, false, true, 0f, 99f), Is.EqualTo(SkillCastBlock.Frozen));
                }));
        }

        [Test]
        public void BackfireRollComparesThePercentAgainstTheDraw()
        {
            EveryCase.Check(new[]
            {
                (percent: 0f, roll01: 0.5f, expected: false),
                (percent: 0.00005f, roll01: 0f, expected: false),
                (percent: 50f, roll01: 0.49f, expected: true),
                (percent: 50f, roll01: 0.5f, expected: false),
                (percent: 100f, roll01: 0.999f, expected: true),
            }, c => Assert.That(SkillCastRules.RollsBackfire(c.percent, c.roll01), Is.EqualTo(c.expected)),
            c => $"percent={c.percent} roll01={c.roll01}");
        }

        [Test]
        public void ConfirmOutcomeAppliesTheAiChecksOnlyToAiCasts()
        {
            EveryCase.Parts(
                ("a human cast always resolves and ignores the AI-only checks", () =>
                {
                    Assert.That(SkillCastRules.ResolveConfirmOutcome(false, false, false, false), Is.EqualTo(SkillConfirmOutcome.Execute));
                    Assert.That(SkillCastRules.ResolveConfirmOutcome(true, false, false, false), Is.EqualTo(SkillConfirmOutcome.Backfire));
                }),
                ("an AI backfire lands even when the continuation recheck fails", () =>
                {
                    // Two AIs commit the same global skill; the first one's curtain is visible when the second's window ends.
                    // The recheck only avoids a duplicate effect, and a backfire runs no skill: the penalty still starts.
                    Assert.That(SkillCastRules.ResolveConfirmOutcome(true, true, true, false), Is.EqualTo(SkillConfirmOutcome.Backfire));
                    Assert.That(SkillCastRules.ResolveConfirmOutcome(true, true, true, true), Is.EqualTo(SkillConfirmOutcome.Backfire));
                }),
                ("an AI normal cast is cancelled when the continuation recheck fails", () =>
                {
                    Assert.That(SkillCastRules.ResolveConfirmOutcome(false, true, true, false), Is.EqualTo(SkillConfirmOutcome.Cancel));
                    Assert.That(SkillCastRules.ResolveConfirmOutcome(false, true, true, true), Is.EqualTo(SkillConfirmOutcome.Execute));
                }),
                ("an AI cast outside the race is dropped, backfire or not", () =>
                {
                    Assert.That(SkillCastRules.ResolveConfirmOutcome(false, true, false, true), Is.EqualTo(SkillConfirmOutcome.Cancel));
                    Assert.That(SkillCastRules.ResolveConfirmOutcome(true, true, false, true), Is.EqualTo(SkillConfirmOutcome.Cancel));
                }));
        }

        [Test]
        public void PendingCastDropsDependOnTheCancelSource()
        {
            EveryCase.Parts(
                ("an ordinary cancel keeps a confirming backfire", () =>
                {
                    // AI opportunity-expired / input-blocked / effects-reset and the respawn reset all use Request.
                    Assert.That(SkillCastRules.DropsPendingCast(true, SkillPendingCancelSource.Request), Is.False);
                    Assert.That(SkillCastRules.DropsPendingCast(false, SkillPendingCancelSource.Request), Is.True);
                }),
                ("freeze drops an ordinary cast but keeps a confirming backfire", () =>
                {
                    Assert.That(SkillCastRules.DropsPendingCast(false, SkillPendingCancelSource.Freeze), Is.True);
                    Assert.That(SkillCastRules.DropsPendingCast(true, SkillPendingCancelSource.Freeze), Is.False);
                }),
                ("teardown drops any confirming cast", () =>
                {
                    foreach (bool backfire in new[] { false, true })
                        Assert.That(SkillCastRules.DropsPendingCast(backfire, SkillPendingCancelSource.Teardown), Is.True, "backfire=" + backfire);
                }));
        }

        [Test]
        public void SurchargeStacksOnRecastsOfTheSameSkillWithinTheWindow()
        {
            EveryCase.Parts(
                ("compounds on each recast right after the cooldown", () =>
                {
                    // 神足通: confirm 1 s, cooldown 5 s, recast as soon as the slot is ready.
                    var surcharge = new SkillRepeatSurcharge();
                    float[] expected = { 1f, 1.15f, 1.3225f, 1.520875f };
                    float request = 0f;
                    for (int i = 0; i < expected.Length; i++)
                    {
                        int stacks = surcharge.ResolveStacks("acceleration", request, Window);
                        Assert.That(SkillRepeatSurcharge.ResolveGain(1f, Gain, stacks), Is.EqualTo(expected[i]).Within(1e-4f), "cast " + (i + 1));
                        float cooldownEnd = request + 1f + 5f;
                        surcharge.CommitExecution("acceleration", stacks, cooldownEnd);
                        request = cooldownEnd;
                    }
                    Assert.That(SkillRepeatSurcharge.ResolveGain(300f, Gain, 1), Is.EqualTo(345f).Within(1e-3f));
                }),
                ("resets when the recast comes after the window", () =>
                {
                    var surcharge = new SkillRepeatSurcharge();
                    surcharge.CommitExecution("giant", surcharge.ResolveStacks("giant", 0f, Window), 9f);
                    Assert.That(surcharge.ResolveStacks("giant", 9f + Window, Window), Is.EqualTo(1), "window end is inclusive");
                    Assert.That(surcharge.ResolveStacks("giant", 9f + Window + 0.01f, Window), Is.EqualTo(0));

                    surcharge.CommitExecution("giant", 0, 30f);
                    Assert.That(surcharge.ResolveStacks("giant", 31f, Window), Is.EqualTo(1));
                }),
                ("keyed by skill and ignores other skills cast in between", () =>
                {
                    var surcharge = new SkillRepeatSurcharge();
                    surcharge.CommitExecution("acceleration", 0, 6f);
                    surcharge.CommitExecution("slowtrap", 0, 8f);
                    Assert.That(surcharge.ResolveStacks("acceleration", 7f, Window), Is.EqualTo(1));
                    Assert.That(surcharge.ResolveStacks("reverseturn", 7f, Window), Is.EqualTo(0), "never executed");
                    Assert.That(surcharge.ResolveStacks("acceleration", 5f, Window), Is.EqualTo(0),
                        "a request before the anchor (same skill in another slot) is not a recast after the cooldown");
                }),
                ("resolving stacks does not commit them", () =>
                {
                    // A cancelled cast (or one still confirming) leaves the committed stacks unchanged.
                    var surcharge = new SkillRepeatSurcharge();
                    surcharge.CommitExecution("acceleration", 0, 6f);
                    Assert.That(surcharge.ResolveStacks("acceleration", 6f, Window), Is.EqualTo(1));
                    Assert.That(surcharge.ResolveStacks("acceleration", 7f, Window), Is.EqualTo(1));
                    Assert.That(surcharge.CommittedStacks("acceleration"), Is.EqualTo(0));
                }));
        }

        [Test]
        public void SurchargeClearsOnBackfireAndGuardsTheGain()
        {
            EveryCase.Parts(
                ("a backfire clears every stack and the backfired cast does not count", () =>
                {
                    var surcharge = new SkillRepeatSurcharge();
                    surcharge.CommitExecution("acceleration", 0, 6f);
                    surcharge.CommitExecution("acceleration", surcharge.ResolveStacks("acceleration", 6f, Window), 12f);
                    surcharge.CommitExecution("slowtrap", 0, 10f);
                    Assert.That(surcharge.CommittedStacks("acceleration"), Is.EqualTo(1));

                    // The next acceleration request rolls a backfire: its stacks (2) are resolved but never committed.
                    Assert.That(surcharge.ResolveStacks("acceleration", 12f, Window), Is.EqualTo(2));
                    surcharge.Clear();

                    Assert.That(surcharge.CommittedStacks("acceleration"), Is.EqualTo(0));
                    Assert.That(surcharge.ResolveStacks("acceleration", 13f, Window), Is.EqualTo(0));
                    Assert.That(surcharge.ResolveStacks("slowtrap", 11f, Window), Is.EqualTo(0));
                }),
                ("gain never goes negative and ignores invalid multipliers", () =>
                {
                    Assert.That(SkillRepeatSurcharge.ResolveGain(-10f, Gain, 2), Is.EqualTo(0f));
                    Assert.That(SkillRepeatSurcharge.ResolveGain(200f, 0.5f, 2), Is.EqualTo(200f), "a multiplier below 1 is treated as 1");
                    Assert.That(SkillRepeatSurcharge.ResolveGain(200f, Gain, -1), Is.EqualTo(200f));
                }));
        }
    }
}
