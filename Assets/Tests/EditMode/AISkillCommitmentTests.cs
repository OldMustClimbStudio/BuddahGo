using System;
using BuddahGo.AI;
using BuddahGo.Tests;
using NUnit.Framework;

public class AISkillCommitmentTests
{
    [Test] public void DecisionScheduleGivesEachTickOneEvaluation()
    {
        EveryCase.Parts(
            ("catch-up ticks cannot evaluate two racers in one frame", () =>
            {
                var schedule = new AISkillDecisionSchedule();
                Assert.That(schedule.TryClaim(10, 0, 100), Is.True);
                Assert.That(schedule.TryClaim(11, 1, 100), Is.False);
                Assert.That(schedule.TryClaim(11, 1, 101), Is.True);
            }),
            ("the snapshot frame cannot also evaluate candidates", () =>
            {
                var schedule = new AISkillDecisionSchedule();
                schedule.SnapshotCaptured(100);
                Assert.That(schedule.TryClaim(11, 1, 100), Is.False);
                Assert.That(schedule.TryClaim(11, 1, 101), Is.True);
                Assert.That(schedule.TryClaim(12, 3, 102), Is.False, "Each racer retains its own tick phase.");
            }));
    }
    [Test] public void CommitmentRequestsOnceThenWaitsForTheExecutor()
    {
        EveryCase.Parts(
            ("requests on the tick after begin, then waits for real execution", () =>
            {
                // A7: no combo keys. The commitment requests the slot on the tick after the decision (like a Human Player
                // pressing the key) and then waits for the executor's real Accepted / Executed / Cancelled events.
                var action = new AISkillCommitment();
                action.Begin(0, 100, 500);
                Assert.That(action.Phase, Is.EqualTo(AISkillCommitmentPhase.Decided));
                Assert.That(action.Slot, Is.EqualTo(0)); Assert.That(action.Deadline, Is.EqualTo(500u));
                Assert.That(action.ShouldRequest(100), Is.False, "Never on the decision tick itself.");
                Assert.That(action.ShouldRequest(101), Is.True);
                Assert.That(action.Phase, Is.EqualTo(AISkillCommitmentPhase.WaitingConfirm));
                Assert.That(action.Waiting, Is.True);
                Assert.That(action.ShouldRequest(102), Is.False, "One request per commitment.");
                Assert.Throws<InvalidOperationException>(() => action.Begin(1, 103, 500));
                action.Accepted(); Assert.That(action.Phase, Is.EqualTo(AISkillCommitmentPhase.WaitingConfirm));
                action.Resolve(); Assert.That(action.Phase, Is.EqualTo(AISkillCommitmentPhase.Resolved));
                Assert.That(action.Active, Is.False);
                action.ReleaseResolved(); Assert.That(action.Phase, Is.EqualTo(AISkillCommitmentPhase.Idle));
                Assert.That(action.Slot, Is.EqualTo(-1));
            }),
            ("an expired commitment cancels instead of requesting", () =>
            {
                var action = new AISkillCommitment(); action.Begin(0, 1, 10);
                Assert.That(action.ShouldRequest(10), Is.False);
                Assert.That(action.Active, Is.False, "The opportunity expired: the commitment cancels itself.");
                Assert.That(action.Slot, Is.EqualTo(-1));
                Assert.That(action.ShouldRequest(11), Is.False);
            }),
            ("a cancelled commitment never requests", () =>
            {
                var action = new AISkillCommitment(); action.Begin(2, 1, 300);
                action.Cancel();
                Assert.That(action.Slot, Is.EqualTo(-1)); Assert.That(action.Active, Is.False);
                Assert.That(action.ShouldRequest(2), Is.False);
                Assert.That(action.ShouldRequest(3), Is.False);
            }),
            ("a waiting commitment is only released by the executor", () =>
            {
                var action = new AISkillCommitment(); action.Begin(1, 1, 300);
                Assert.That(action.ShouldRequest(2), Is.True);
                for (uint tick = 3; tick < 400; tick++) Assert.That(action.ShouldRequest(tick), Is.False);
                Assert.That(action.Waiting, Is.True, "Past the deadline the accepted request is not withdrawn by ticks.");
                action.Cancel(); Assert.That(action.Phase, Is.EqualTo(AISkillCommitmentPhase.Idle));
            }));
    }
    [Test] public void TwoCommitmentsWithTheSameInputsStayInStep()
    {
        var a = new AISkillCommitment(); var b = new AISkillCommitment();
        a.Begin(0, 1, 300); b.Begin(0, 1, 300);
        for (uint tick = 1; tick < 310; tick++)
        {
            Assert.That(a.ShouldRequest(tick), Is.EqualTo(b.ShouldRequest(tick)));
            Assert.That(a.Phase, Is.EqualTo(b.Phase));
        }
        a.Cancel(); Assert.That(b.Active, Is.True);
    }
    [Test] public void InversionPerceptionDelaysTheSignAndDropsCancelledChanges()
    {
        EveryCase.Parts(
            ("inversion start and end both delay the cognitive sign", () =>
            {
                var cognition = new AISteeringPerception();
                Assert.That(cognition.Observe(-1, 100, .02f, 1f, .6f), Is.EqualTo(1f));
                Assert.That(cognition.Observe(-1, 149, .02f, 1f, .6f), Is.EqualTo(1f));
                Assert.That(cognition.Observe(-1, 151, .02f, 1f, .6f), Is.EqualTo(-1f));
                Assert.That(cognition.Observe(1, 200, .02f, 1f, .6f), Is.EqualTo(-1f));
                Assert.That(cognition.Observe(1, 229, .02f, 1f, .6f), Is.EqualTo(-1f));
                Assert.That(cognition.Observe(1, 232, .02f, 1f, .6f), Is.EqualTo(1f));
                cognition.Reset(); Assert.That(cognition.Sign, Is.EqualTo(1f));
            }),
            ("a cancelled inversion does not apply a stale pending sign", () =>
            {
                var cognition = new AISteeringPerception();
                cognition.Observe(-1, 100, .02f, 2f, 1f);
                cognition.Observe(1, 120, .02f, 2f, 1f);
                Assert.That(cognition.Observe(1, 210, .02f, 2f, 1f), Is.EqualTo(1f));
            }));
    }
}
