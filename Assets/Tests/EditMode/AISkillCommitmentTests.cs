using System;
using BuddahGo.AI;
using NUnit.Framework;

public class AISkillCommitmentTests
{
    [Test] public void CatchUpTicksCannotEvaluateTwoRacersInOneFrame()
    {
        var schedule = new AISkillDecisionSchedule();
        Assert.That(schedule.TryClaim(10, 0, 100), Is.True);
        Assert.That(schedule.TryClaim(11, 1, 100), Is.False);
        Assert.That(schedule.TryClaim(11, 1, 101), Is.True);
    }
    [Test] public void SnapshotFrameCannotAlsoEvaluateCandidates()
    {
        var schedule = new AISkillDecisionSchedule();
        schedule.SnapshotCaptured(100);
        Assert.That(schedule.TryClaim(11, 1, 100), Is.False);
        Assert.That(schedule.TryClaim(11, 1, 101), Is.True);
        Assert.That(schedule.TryClaim(12, 3, 102), Is.False, "Each racer retains its own tick phase.");
    }
    private static readonly ComboSkillInput.Token[] Sequence = { ComboSkillInput.Token.W, ComboSkillInput.Token.Up, ComboSkillInput.Token.W };
    [Test] public void CommitmentEmitsSeparatedKeysAndWaitsForRealExecution()
    {
        var action = new AISkillCommitment(); var random = new Random(23);
        action.Begin(0, Sequence, 100, 500);
        Assert.That(action.NextKey(100, .02f, .25f, .25f, .35f, 0, random, out _), Is.False);
        Assert.That(action.NextKey(101, .02f, .25f, .25f, .35f, 0, random, out var key), Is.True);
        Assert.That(key, Is.EqualTo(Sequence[0]));
        Assert.That(action.NextKey(102, .02f, .25f, .25f, .35f, 0, random, out _), Is.False);
        Assert.That(action.NextKey(115, .02f, .25f, .25f, .35f, 0, random, out key), Is.True);
        Assert.That(key, Is.EqualTo(Sequence[1]));
        Assert.Throws<InvalidOperationException>(() => action.Begin(1, Sequence, 116, 500));
        Assert.That(action.NextKey(130, .02f, .25f, .25f, .35f, 0, random, out key), Is.True);
        Assert.That(action.Phase, Is.EqualTo(AISkillCommitmentPhase.WaitingConfirm));
        Assert.That(action.NextKey(200, .02f, .25f, .25f, .35f, 0, random, out _), Is.False);
        action.Resolve(); Assert.That(action.Phase, Is.EqualTo(AISkillCommitmentPhase.Resolved));
        action.ReleaseResolved(); Assert.That(action.Active, Is.False);
    }
    [Test] public void MistakeCreatesTimeoutThenOnlyOneRetry()
    {
        var action = new AISkillCommitment(); var random = new Random(2);
        action.Begin(0, Sequence, 0, 500);
        action.NextKey(1, .02f, .25f, .25f, .35f, 1, random, out _);
        Assert.That(action.NextKey(20, .02f, .25f, .25f, .35f, 1, random, out _), Is.False);
        Assert.That(action.NextKey(21, .02f, .25f, .25f, .35f, 1, random, out _), Is.False);
        Assert.That(action.NextKey(40, .02f, .25f, .25f, .35f, 1, random, out var key), Is.True);
        Assert.That(key, Is.EqualTo(Sequence[0]));
        action.NextKey(60, .02f, .25f, .25f, .35f, 1, random, out _);
        Assert.That(action.Active, Is.False); Assert.That(action.FailedCombos, Is.EqualTo(2));
    }
    [Test] public void ExpiryAndCancellationNeverEmitALateKey()
    {
        var action = new AISkillCommitment(); action.Begin(0, Sequence, 1, 10);
        Assert.That(action.NextKey(10, .02f, .2f, .3f, .35f, 0, new Random(1), out _), Is.False);
        action.Cancel(); Assert.That(action.Slot, Is.EqualTo(-1));
        Assert.That(action.NextKey(11, .02f, .2f, .3f, .35f, 0, new Random(1), out _), Is.False);
    }
    [Test] public void IndependentRandomAndCommitmentStateReplaysWithoutCrossTalk()
    {
        var a = new AISkillCommitment(); var b = new AISkillCommitment();
        var ar = new Random(77); var br = new Random(77); var unrelated = new Random(88);
        a.Begin(0, Sequence, 1, 300); b.Begin(0, Sequence, 1, 300);
        for (uint tick = 2; tick < 300; tick++)
        {
            for (int i = 0; i < 3; i++) unrelated.NextDouble();
            bool ak = a.NextKey(tick, 1f / 60f, .2f, .3f, .35f, .15f, ar, out var at);
            bool bk = b.NextKey(tick, 1f / 60f, .2f, .3f, .35f, .15f, br, out var bt);
            Assert.That(ak, Is.EqualTo(bk)); Assert.That(at, Is.EqualTo(bt)); Assert.That(a.Phase, Is.EqualTo(b.Phase));
        }
        a.Cancel(); Assert.That(b.Active, Is.True);
    }
    [Test] public void InversionStartAndEndBothDelayCognitiveSign()
    {
        var cognition = new AISteeringPerception();
        Assert.That(cognition.Observe(-1, 100, .02f, 1f, .6f), Is.EqualTo(1f));
        Assert.That(cognition.Observe(-1, 149, .02f, 1f, .6f), Is.EqualTo(1f));
        Assert.That(cognition.Observe(-1, 151, .02f, 1f, .6f), Is.EqualTo(-1f));
        Assert.That(cognition.Observe(1, 200, .02f, 1f, .6f), Is.EqualTo(-1f));
        Assert.That(cognition.Observe(1, 229, .02f, 1f, .6f), Is.EqualTo(-1f));
        Assert.That(cognition.Observe(1, 232, .02f, 1f, .6f), Is.EqualTo(1f));
        cognition.Reset(); Assert.That(cognition.Sign, Is.EqualTo(1f));
    }
    [Test] public void CancelledInversionDoesNotApplyAStalePendingSign()
    {
        var cognition = new AISteeringPerception();
        cognition.Observe(-1, 100, .02f, 2f, 1f);
        cognition.Observe(1, 120, .02f, 2f, 1f);
        Assert.That(cognition.Observe(1, 210, .02f, 2f, 1f), Is.EqualTo(1f));
    }
}
