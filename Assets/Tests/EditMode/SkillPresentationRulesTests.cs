using NUnit.Framework;

namespace BuddahGo.Tests
{
    /// <summary>
    /// Wave 3 presentation rules (动作层): the start/end edge every presentation event uses (freeze, burst, backfire),
    /// the backfire state read from the replicated end tick, and the Animator speed hold of a frozen racer.
    /// </summary>
    public class SkillPresentationRulesTests
    {
        private const double TickDelta = 1.0 / 60.0;

        // ObsessionFigure.IsBackfireActive on any peer: the replicated end tick against this peer's tick.
        private static bool BackfireActive(uint endTick, uint nowTick)
            => ObsessionBackfireRules.SecondsLeft(endTick, nowTick, TickDelta) > 0f;

        [Test]
        public void FlagReportsTheStartAtOnceAndOnlyOnce()
        {
            var flag = new PresentationFlag();
            Assert.IsFalse(flag.Reported);
            Assert.IsFalse(flag.Update(false, 0f, 0.1f), "inactive from the start: nothing to report");
            Assert.IsTrue(flag.Update(true, 0.5f, 0.1f), "start");
            Assert.IsTrue(flag.Reported);
            Assert.IsFalse(flag.Update(true, 0.6f, 0.1f), "still active: no second start");
        }

        [Test]
        public void FlagReportsTheEndOnlyAfterTheReleaseDelay()
        {
            var flag = new PresentationFlag();
            flag.Update(true, 0f, 0.1f);
            Assert.IsFalse(flag.Update(false, 1f, 0.1f), "end seen, still inside the release delay");
            Assert.IsTrue(flag.Reported);
            Assert.IsFalse(flag.Update(false, 1.05f, 0.1f));
            Assert.IsTrue(flag.Update(false, 1.1f, 0.1f), "end reported once the delay elapsed");
            Assert.IsFalse(flag.Reported);
            Assert.IsFalse(flag.Update(false, 2f, 0.1f), "no second end");
        }

        [Test]
        public void AFlickerInsideTheReleaseDelayRaisesNothing()
        {
            // A stale reconcile snapshot can drop a fresh owner freeze for a tick or two.
            var flag = new PresentationFlag();
            flag.Update(true, 0f, 0.1f);
            Assert.IsFalse(flag.Update(false, 0.016f, 0.1f));
            Assert.IsFalse(flag.Update(true, 0.033f, 0.1f), "back on before the delay: no end, no second start");
            Assert.IsTrue(flag.Reported);
            // The delay restarts with the next inactive sample.
            Assert.IsFalse(flag.Update(false, 1f, 0.1f));
            Assert.IsFalse(flag.Update(false, 1.09f, 0.1f));
            Assert.IsTrue(flag.Update(false, 1.1f, 0.1f));
        }

        [Test]
        public void ZeroReleaseDelayEndsOnTheFirstInactiveSample()
        {
            var flag = new PresentationFlag();
            Assert.IsTrue(flag.Update(true));
            Assert.IsTrue(flag.Update(false));
            Assert.IsFalse(flag.Reported);
        }

        [Test]
        public void ResetReportsWhetherAnEndIsOwed()
        {
            var flag = new PresentationFlag();
            Assert.IsFalse(flag.Reset(), "never active");
            flag.Update(true);
            Assert.IsTrue(flag.Reset(), "was active: the caller raises the end");
            Assert.IsFalse(flag.Reported);
            Assert.IsTrue(flag.Update(true), "a reset flag reports the next start again");
        }

        [Test]
        public void BackfireStartsWithTheEndTickAndEndsWhenThisPeerPassesIt()
        {
            var flag = new PresentationFlag();
            uint endTick = 100u + 600u;   // 10 s at 60 Hz from tick 100

            Assert.IsTrue(flag.Update(BackfireActive(endTick, 100u)), "SyncVar change: penalty starts");
            Assert.IsFalse(flag.Update(BackfireActive(endTick, 400u)));
            Assert.IsFalse(flag.Update(BackfireActive(endTick, 699u)), "last locked tick");
            Assert.IsTrue(flag.Update(BackfireActive(endTick, 700u)), "client-side end check: the end tick passed");
            Assert.IsFalse(flag.Update(BackfireActive(0u, 703u)), "the server's zero arrives later: already ended");
        }

        [Test]
        public void BackfireReEntryKeepsOneStartAndOneEnd()
        {
            var flag = new PresentationFlag();
            Assert.IsTrue(flag.Update(BackfireActive(700u, 100u)));
            Assert.IsFalse(flag.Update(BackfireActive(900u, 300u)), "the later end replaces the first: no event");
            Assert.IsFalse(flag.Update(BackfireActive(900u, 700u)), "the first end tick no longer ends it");
            Assert.IsTrue(flag.Update(BackfireActive(900u, 900u)));
        }

        [Test]
        public void BackfireForALateJoinerFollowsTheReplicatedEndTick()
        {
            var joinedDuring = new PresentationFlag();
            Assert.IsTrue(joinedDuring.Update(BackfireActive(700u, 650u)), "joined mid-penalty: one start");
            Assert.IsFalse(joinedDuring.Update(BackfireActive(700u, 651u)));

            var joinedAfter = new PresentationFlag();
            Assert.IsFalse(joinedAfter.Update(BackfireActive(700u, 750u)), "end tick already passed: nothing");
            Assert.IsFalse(joinedAfter.Update(BackfireActive(0u, 750u)), "0 is the inactive sentinel");
        }

        [Test]
        public void BackfireEndThenNewPenaltyInOneSyncRaisesEndThenStart()
        {
            // The server ends a penalty and begins another before the SyncVar is sent: the client sees old → new end tick.
            var flag = new PresentationFlag();
            flag.Update(BackfireActive(700u, 100u));
            Assert.IsTrue(flag.Update(BackfireActive(700u, 701u)), "client clock passed the old end");
            Assert.IsTrue(flag.Update(BackfireActive(1400u, 702u)), "new end tick arrives: a new start");
            Assert.IsTrue(flag.Reported);
        }

        [Test]
        public void AnimatorHoldStopsThePlaybackAndRestoresThePreviousSpeed()
        {
            var hold = new AnimatorSpeedHold();
            Assert.IsFalse(hold.IsHolding);
            Assert.AreEqual(0f, hold.Begin(1.25f), "frozen: playback stops");
            Assert.IsTrue(hold.IsHolding);
            Assert.AreEqual(1.25f, hold.End(0f), "unfrozen: the speed before the freeze comes back");
            Assert.IsFalse(hold.IsHolding);
        }

        [Test]
        public void AnimatorHoldKeepsTheFirstSpeedWhileItHolds()
        {
            var hold = new AnimatorSpeedHold();
            hold.Begin(1f);
            Assert.AreEqual(0f, hold.Begin(0f), "re-entering while held keeps holding");
            Assert.AreEqual(1f, hold.End(0f), "the held 0 never overwrites the original speed");
        }

        [Test]
        public void FrostLockDrawsNothingItself()
        {
            // Interfaces only (2026-10-26): the placeholder ice visuals are gone; observers use SkillExecutor.YuanqiObserved.
            var method = typeof(Yuanqi_FrostLock).GetMethod(nameof(YuanqiAction.PlayObservers));
            Assert.IsNotNull(method);
            Assert.AreEqual(typeof(YuanqiAction), method.DeclaringType);
        }

        [Test]
        public void GiantChargeCasterFovKickIsModestAndSpansTheBurst()
        {
            var charge = UnityEngine.ScriptableObject.CreateInstance<Yuanqi_GiantCharge>();
            try
            {
                Assert.That(charge.casterFovKickDegrees, Is.GreaterThan(0f).And.LessThanOrEqualTo(10f));
                Assert.That(charge.casterFovKickHoldSeconds + charge.casterFovKickSettleSeconds,
                    Is.EqualTo(charge.burstSeconds).Within(1e-4f), "the kick settles when the burst has decayed");
            }
            finally { UnityEngine.Object.DestroyImmediate(charge); }
        }

        [Test]
        public void SkillUiEventDefaultsMeanNoSurcharge()
        {
            var plain = new SkillExecutor.LocalSkillUiEvent(1, 5f);
            Assert.AreEqual(0, plain.SurchargeStacks);
            Assert.AreEqual(0f, plain.RepeatWindowSeconds);
            Assert.AreEqual(1f, plain.RepeatGainMultiplier);

            var backfire = new SkillExecutor.LocalSkillUiEvent(2, 8f, SkillExecutor.LocalSkillUiEvent.StacksClearedByBackfire, 5f, 1.15f);
            Assert.AreEqual(-1, backfire.SurchargeStacks);
            Assert.AreEqual(5f, backfire.RepeatWindowSeconds);
            Assert.AreEqual(1.15f, backfire.RepeatGainMultiplier);
            Assert.AreEqual(0f, plain.SecondsSinceServerEvent, "no age = anchored at arrival");
            Assert.AreEqual(0f, new SkillExecutor.LocalSkillUiEvent(0, 5f, 0, 5f, 1.15f, -1f).SecondsSinceServerEvent, "never negative");
        }

        [Test]
        public void SkillUiEventAgeComesFromTheServerTick()
        {
            const double tickDelta = 0.02;
            Assert.AreEqual(0.12f, SkillExecutor.LocalSkillUiEvent.SecondsSinceServerTick(1000u, 1006u, tickDelta), 1e-5f,
                "6 ticks since the execution on the server's clock");
            Assert.AreEqual(0f, SkillExecutor.LocalSkillUiEvent.SecondsSinceServerTick(1000u, 1000u, tickDelta), "host: same tick");
            Assert.AreEqual(0f, SkillExecutor.LocalSkillUiEvent.SecondsSinceServerTick(1000u, 998u, tickDelta), "estimate behind: arrival anchor");
            Assert.AreEqual(0f, SkillExecutor.LocalSkillUiEvent.SecondsSinceServerTick(0u, 1006u, tickDelta), "unknown tick");
            Assert.AreEqual(0f, SkillExecutor.LocalSkillUiEvent.SecondsSinceServerTick(1000u, 1006u, 0d), "no tick delta");
        }

        // A remote client hears the execution 0.06 s late: anchored on the server's clock, the cooldown end and the
        // surcharge window are where the server puts them, not shifted by the downlink delay.
        [Test]
        public void SlotTimelineAnchoredAtTheServerEventMatchesTheServerWindow()
        {
            const float executedAt = 10f, cooldown = 4f, window = 5f, downlink = 0.06f;
            var ev = new SkillExecutor.LocalSkillUiEvent(0, cooldown, 0, window, 1.15f, downlink);
            float arrival = executedAt + downlink;

            var timeline = new SkillSlotHudTimeline();
            timeline.OnSkillUiEvent(arrival - ev.SecondsSinceServerEvent, true, true, ev.CooldownSeconds, ev.SurchargeStacks,
                ev.RepeatWindowSeconds, ev.RepeatGainMultiplier);

            Assert.AreEqual(SkillSlotHudKind.Cooldown, timeline.Sample(executedAt + cooldown - 0.01f, 0f, 0f).Kind);
            Assert.AreEqual(SkillSlotHudKind.SurchargeWindow, timeline.Sample(executedAt + cooldown + 0.01f, 0f, 0f).Kind,
                "the window opens at the server's cooldown end");
            Assert.AreEqual(SkillSlotHudKind.SurchargeWindow, timeline.Sample(executedAt + cooldown + window - 0.01f, 0f, 0f).Kind);
            Assert.AreEqual(SkillSlotHudKind.Ready, timeline.Sample(executedAt + cooldown + window + 0.01f, 0f, 0f).Kind,
                "and closes at the server's window end");
        }

        [Test]
        public void AnimatorHoldEndWithoutBeginLeavesTheSpeedAlone()
        {
            var hold = new AnimatorSpeedHold();
            Assert.AreEqual(0.7f, hold.End(0.7f));
            hold.Begin(0.7f);
            hold.End(0f);
            Assert.AreEqual(2f, hold.End(2f), "a second end is a no-op");
            Assert.AreEqual(0f, hold.Begin(2f));
            Assert.AreEqual(2f, hold.End(0f), "a new hold stores the new speed");
        }
    }
}
