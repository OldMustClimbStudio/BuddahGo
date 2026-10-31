using NUnit.Framework;
using NewBuddah.PredictionV2.Core;

namespace BuddahGo.Tests
{
    public class BuddahPredictedModifierStateOpsTests
    {
        private const uint Now = 100u;

        [Test]
        public void FreezeDeadlineTakesTheLaterOfTwoFreezes()
        {
            var state = new BuddahPredictedModifierState { FreezeUntilTick = 150 };
            BuddahPredictedModifierStateOps.ApplyFreeze(ref state, 120);
            Assert.That(state.FreezeUntilTick, Is.EqualTo(150u));
            BuddahPredictedModifierStateOps.ApplyFreeze(ref state, 200);
            Assert.That(state.FreezeUntilTick, Is.EqualTo(200u));
        }

        [Test]
        public void EndAccelerationEndsABuffAndClearsItsMagnitudes()
        {
            var state = new BuddahPredictedModifierState { AccelUntilTick = 200, AccelExtraForwardForce = 10f, AccelExtraMaxSpeed = 20f };
            Assert.That(BuddahPredictedModifierStateOps.EndModifier(ref state, BuddahPredictedModifierCommandType.Acceleration, Now), Is.True);
            Assert.That(state.AccelUntilTick, Is.EqualTo(Now));
            Assert.That(state.AccelExtraForwardForce, Is.Zero);
            Assert.That(state.AccelExtraMaxSpeed, Is.Zero);
        }

        [Test]
        public void EndAccelerationEndsABuffWithOnlyOnePositiveMagnitude()
        {
            var state = new BuddahPredictedModifierState { AccelUntilTick = 200, AccelExtraForwardForce = 0f, AccelExtraMaxSpeed = 20f };
            BuddahPredictedModifierStateOps.EndModifier(ref state, BuddahPredictedModifierCommandType.Acceleration, Now);
            Assert.That(state.AccelUntilTick, Is.EqualTo(Now));
        }

        [Test]
        public void EndAccelerationDoesNotCancelASlowTrapDebuffSharingTheChannel()
        {
            var slowed = new BuddahPredictedModifierState { AccelUntilTick = 200, AccelExtraForwardForce = -10f, AccelExtraMaxSpeed = -20f };
            var state = slowed;
            BuddahPredictedModifierStateOps.EndModifier(ref state, BuddahPredictedModifierCommandType.Acceleration, Now);
            Assert.That(state, Is.EqualTo(slowed));
        }

        [Test]
        public void EndRootThenAccelerationEndsBothPhases()
        {
            var state = new BuddahPredictedModifierState { RootUntilTick = 150, PostRootAccelUntilTick = 250, PostRootAccelExtraMaxSpeed = 30f };
            Assert.That(BuddahPredictedModifierStateOps.EndModifier(ref state, BuddahPredictedModifierCommandType.RootThenAcceleration, Now), Is.True);
            Assert.That(state.RootUntilTick, Is.EqualTo(Now));
            Assert.That(state.PostRootAccelUntilTick, Is.EqualTo(Now));
        }

        [Test]
        public void EndScaleEndsTheDeadlineAndRestoresMagnitudesToOne()
        {
            var state = new BuddahPredictedModifierState { ScaleUntilTick = 300, ScaleMultiplier = 2f, ScaleMassMultiplier = 3f, ScaleForwardForceMultiplier = 1.5f };
            Assert.That(BuddahPredictedModifierStateOps.EndModifier(ref state, BuddahPredictedModifierCommandType.Scale, Now), Is.True);
            Assert.That(state.ScaleUntilTick, Is.EqualTo(Now));
            Assert.That(new[] { state.ScaleMultiplier, state.ScaleMassMultiplier, state.ScaleForwardForceMultiplier },
                Is.EqualTo(new[] { 1f, 1f, 1f }));
        }

        [Test]
        public void EndFreezeBurstAndPenaltyShortenTheirDeadlines()
        {
            var state = new BuddahPredictedModifierState { FreezeUntilTick = 190, BurstUntilTick = 148, BurstDurationTicks = 48, MaxSpeedPenaltyUntilTick = 700 };
            Assert.That(BuddahPredictedModifierStateOps.EndModifier(ref state, BuddahPredictedModifierCommandType.Freeze, Now), Is.True);
            Assert.That(BuddahPredictedModifierStateOps.EndModifier(ref state, BuddahPredictedModifierCommandType.Burst, Now), Is.True);
            Assert.That(BuddahPredictedModifierStateOps.EndModifier(ref state, BuddahPredictedModifierCommandType.MaxSpeedMultiplier, Now), Is.True);
            Assert.That(new[] { state.FreezeUntilTick, state.BurstUntilTick, state.MaxSpeedPenaltyUntilTick }, Is.EqualTo(new[] { Now, Now, Now }));
            Assert.That(BuddahPredictedModifierResolver.Resolve(state, null, Now).IsFrozen, Is.False);
        }

        [TestCase(BuddahPredictedModifierCommandType.Acceleration)]
        [TestCase(BuddahPredictedModifierCommandType.RootThenAcceleration)]
        [TestCase(BuddahPredictedModifierCommandType.Scale)]
        [TestCase(BuddahPredictedModifierCommandType.Freeze)]
        [TestCase(BuddahPredictedModifierCommandType.Burst)]
        [TestCase(BuddahPredictedModifierCommandType.MaxSpeedMultiplier)]
        public void EndModifierNeverExtendsAndLeavesUnsetDeadlinesUnset(BuddahPredictedModifierCommandType target)
        {
            // Unset (0) stays 0, an already expired deadline (50) stays 50.
            var unset = default(BuddahPredictedModifierState);
            BuddahPredictedModifierStateOps.EndModifier(ref unset, target, Now);
            Assert.That(new[] { unset.AccelUntilTick, unset.RootUntilTick, unset.PostRootAccelUntilTick, unset.ScaleUntilTick,
                unset.FreezeUntilTick, unset.BurstUntilTick, unset.MaxSpeedPenaltyUntilTick }, Is.All.Zero);

            var expired = new BuddahPredictedModifierState
            {
                AccelUntilTick = 50, AccelExtraMaxSpeed = 20f, RootUntilTick = 50, PostRootAccelUntilTick = 50, ScaleUntilTick = 50,
                FreezeUntilTick = 50, BurstUntilTick = 50, MaxSpeedPenaltyUntilTick = 50
            };
            BuddahPredictedModifierStateOps.EndModifier(ref expired, target, Now);
            Assert.That(new[] { expired.AccelUntilTick, expired.RootUntilTick, expired.PostRootAccelUntilTick, expired.ScaleUntilTick,
                expired.FreezeUntilTick, expired.BurstUntilTick, expired.MaxSpeedPenaltyUntilTick }, Is.All.EqualTo(50u));
        }

        [TestCase(BuddahPredictedModifierCommandType.None)]
        [TestCase(BuddahPredictedModifierCommandType.InvertTurn)]
        [TestCase(BuddahPredictedModifierCommandType.PushGrace)]
        [TestCase(BuddahPredictedModifierCommandType.SteeringSuppression)]
        [TestCase(BuddahPredictedModifierCommandType.RoomBypass)]
        [TestCase(BuddahPredictedModifierCommandType.EndModifier)]
        public void EndModifierRejectsUnsupportedTargetsWithoutChanges(BuddahPredictedModifierCommandType target)
        {
            var original = new BuddahPredictedModifierState
            {
                InvertTurnUntilTick = 200, PushGraceUntilTick = 200, SuppressSteeringUntilTick = 200, RoomBypassUntilTick = 200
            };
            var state = original;
            Assert.That(BuddahPredictedModifierStateOps.EndModifier(ref state, target, Now), Is.False);
            Assert.That(state, Is.EqualTo(original));
        }

        [Test]
        public void BurstRestartsItsDecayWindow()
        {
            var state = new BuddahPredictedModifierState { BurstUntilTick = 120, BurstDurationTicks = 48, BurstExtraMaxSpeed = 30f };
            BuddahPredictedModifierStateOps.ApplyBurst(ref state, 60f, Now + 48, 48);
            Assert.That(state.BurstUntilTick, Is.EqualTo(Now + 48));
            Assert.That(state.BurstDurationTicks, Is.EqualTo(48u));
            Assert.That(state.BurstExtraMaxSpeed, Is.EqualTo(60f));
        }

        [Test]
        public void BurstExtraIsNeverNegative()
        {
            var state = default(BuddahPredictedModifierState);
            BuddahPredictedModifierStateOps.ApplyBurst(ref state, -5f, Now + 48, 48);
            Assert.That(state.BurstExtraMaxSpeed, Is.Zero);
        }

        [TestCase(0.85f, 0.85f)]
        [TestCase(0f, 0.1f)]
        [TestCase(3f, 1f)]
        public void MaxSpeedMultiplierIsClampedAndItsDeadlineTakesTheLater(float multiplier, float expected)
        {
            var state = new BuddahPredictedModifierState { MaxSpeedPenaltyUntilTick = 800 };
            BuddahPredictedModifierStateOps.ApplyMaxSpeedMultiplier(ref state, multiplier, 700);
            Assert.That(state.MaxSpeedPenaltyUntilTick, Is.EqualTo(800u));
            Assert.That(state.MaxSpeedPenaltyMultiplier, Is.EqualTo(expected));
            BuddahPredictedModifierStateOps.ApplyMaxSpeedMultiplier(ref state, multiplier, 900);
            Assert.That(state.MaxSpeedPenaltyUntilTick, Is.EqualTo(900u));
        }

        private static BuddahPredictedModifierState EveryModifierActive()
        {
            return new BuddahPredictedModifierState
            {
                RootUntilTick = 200, AccelUntilTick = 200, AccelExtraForwardForce = 1f, AccelExtraMaxSpeed = 2f,
                PostRootAccelUntilTick = 200, PostRootAccelExtraForwardForce = 3f, PostRootAccelExtraMaxSpeed = 4f,
                ScaleUntilTick = 200, ScaleMultiplier = 2f, ScaleMassMultiplier = 2f, ScaleForwardForceMultiplier = 2f,
                InvertTurnUntilTick = 200, PushGraceUntilTick = 200, SuppressSteeringUntilTick = 200, RoomBypassUntilTick = 200,
                FreezeUntilTick = 200, BurstUntilTick = 200, BurstDurationTicks = 48, BurstExtraMaxSpeed = 60f,
                MaxSpeedPenaltyUntilTick = 700, MaxSpeedPenaltyMultiplier = 0.85f
            };
        }

        [Test]
        public void SkillResetClearsSkillEffectsButKeepsTheBackfirePenalty()
        {
            var state = EveryModifierActive();
            BuddahPredictedModifierStateOps.ClearActiveSkillModifiers(ref state);
            Assert.That(new[] { state.RootUntilTick, state.AccelUntilTick, state.PostRootAccelUntilTick, state.ScaleUntilTick,
                state.InvertTurnUntilTick, state.FreezeUntilTick, state.BurstUntilTick, state.BurstDurationTicks }, Is.All.Zero);
            Assert.That(new[] { state.AccelExtraForwardForce, state.AccelExtraMaxSpeed, state.PostRootAccelExtraForwardForce,
                state.PostRootAccelExtraMaxSpeed, state.BurstExtraMaxSpeed }, Is.All.Zero);
            Assert.That(new[] { state.ScaleMultiplier, state.ScaleMassMultiplier, state.ScaleForwardForceMultiplier }, Is.All.EqualTo(1f));
            // Not skill effects: push grace / steering suppression / room bypass (unchanged behavior).
            Assert.That(new[] { state.PushGraceUntilTick, state.SuppressSteeringUntilTick, state.RoomBypassUntilTick }, Is.All.EqualTo(200u));
            // The backfire penalty persists until its own 10 s end.
            Assert.That(state.MaxSpeedPenaltyUntilTick, Is.EqualTo(700u));
            Assert.That(state.MaxSpeedPenaltyMultiplier, Is.EqualTo(0.85f));
        }

        [Test]
        public void TeleportResetWipesEverythingExceptTheBackfirePenalty()
        {
            var reset = BuddahPredictedModifierStateOps.PreservePersistentOnReset(EveryModifierActive());
            var expected = new BuddahPredictedModifierState { MaxSpeedPenaltyUntilTick = 700, MaxSpeedPenaltyMultiplier = 0.85f };
            Assert.That(reset, Is.EqualTo(expected));
            Assert.That(BuddahPredictedModifierStateOps.PreservePersistentOnReset(default), Is.EqualTo(default(BuddahPredictedModifierState)));

            var stats = BuddahPredictedModifierResolver.Resolve(reset, null, Now);
            Assert.That(stats.IsMaxSpeedPenaltyActive, Is.True);
            Assert.That(stats.IsFrozen || stats.IsRooted || stats.IsBurstActive, Is.False);
        }

        [Test]
        public void HasAnyActiveCoversTheNewDeadlines()
        {
            Assert.That(new BuddahPredictedModifierState { FreezeUntilTick = Now + 1 }.HasAnyActive(Now), Is.True);
            Assert.That(new BuddahPredictedModifierState { BurstUntilTick = Now + 1 }.HasAnyActive(Now), Is.True);
            Assert.That(new BuddahPredictedModifierState { MaxSpeedPenaltyUntilTick = Now + 1 }.HasAnyActive(Now), Is.True);
            Assert.That(new BuddahPredictedModifierState { BurstDurationTicks = 48 }.HasAnyActive(Now), Is.False, "a duration is not a deadline");
        }

        [Test]
        public void FactoriesCarryTheirArguments()
        {
            var freeze = BuddahPredictedModifierCommandData.CreateFreeze(1.5f, "t");
            Assert.That((freeze.Type, freeze.ValueA, freeze.Source), Is.EqualTo((BuddahPredictedModifierCommandType.Freeze, 1.5f, "t")));
            var end = BuddahPredictedModifierCommandData.CreateEndModifier(BuddahPredictedModifierCommandType.Scale, "t");
            Assert.That(end.Type, Is.EqualTo(BuddahPredictedModifierCommandType.EndModifier));
            Assert.That(BuddahPredictedModifierCommandData.ReadEndModifierTarget(end), Is.EqualTo(BuddahPredictedModifierCommandType.Scale));
            var burst = BuddahPredictedModifierCommandData.CreateBurst(60f, 0.8f, "t");
            Assert.That((burst.Type, burst.ValueA, burst.ValueB), Is.EqualTo((BuddahPredictedModifierCommandType.Burst, 60f, 0.8f)));
            var penalty = BuddahPredictedModifierCommandData.CreateMaxSpeedMultiplier(0.85f, 10f, "t");
            Assert.That((penalty.Type, penalty.ValueA, penalty.ValueB), Is.EqualTo((BuddahPredictedModifierCommandType.MaxSpeedMultiplier, 0.85f, 10f)));
        }

        [Test]
        public void NewCommandTypesKeepTheirWireNumbers()
        {
            Assert.That((int)BuddahPredictedModifierCommandType.Freeze, Is.EqualTo(8));
            Assert.That((int)BuddahPredictedModifierCommandType.EndModifier, Is.EqualTo(9));
            Assert.That((int)BuddahPredictedModifierCommandType.Burst, Is.EqualTo(10));
            Assert.That((int)BuddahPredictedModifierCommandType.MaxSpeedMultiplier, Is.EqualTo(11));
        }
    }
}
