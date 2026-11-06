using NUnit.Framework;
using NewBuddah.PredictionV2.Core;
using UnityEngine;

namespace BuddahGo.Tests
{
    public class HandoffClockTests
    {
        [Test]
        public void ConsumeTickHonorsNewGoBypassWithoutRevivingOtherBlockedInput()
        {
            EveryCase.Check(new[]
            {
                (inputAllowed: false, consumedThisTick: false, bypass: false, expected: false),
                (inputAllowed: false, consumedThisTick: false, bypass: true, expected: false),
                (inputAllowed: false, consumedThisTick: true, bypass: false, expected: false),
                (inputAllowed: false, consumedThisTick: true, bypass: true, expected: true),
                (inputAllowed: true, consumedThisTick: false, bypass: false, expected: true),
                (inputAllowed: true, consumedThisTick: false, bypass: true, expected: true),
                (inputAllowed: true, consumedThisTick: true, bypass: false, expected: true),
                (inputAllowed: true, consumedThisTick: true, bypass: true, expected: true),
            }, c => Assert.That(BuddahPredictedLaunchHandoffResolver.IsMovementAllowedAfterConsume(
                c.inputAllowed, c.consumedThisTick, c.bypass), Is.EqualTo(c.expected)),
            c => $"inputAllowed={c.inputAllowed} consumedThisTick={c.consumedThisTick} bypass={c.bypass}");
        }

        private static BuddahPredictedLaunchHandoffState Snapshot(uint start, uint inherit = 3, uint blend = 4)
        {
            return BuddahPredictedLaunchHandoffState.FromData(new BuddahPredictedLaunchHandoffData
            {
                EventId = 7, StartTick = start, InheritDurationTicks = inherit, BlendDurationTicks = blend,
                SuppressSteeringDurationTicks = 9, RoomBypassDurationTicks = 11,
                SnapshotPosition = new Vector3(1, 2, 3), SnapshotRotation = Quaternion.Euler(0, 30, 0),
                SnapshotVelocity = new Vector3(4, 5, 6), SnapshotAngularVelocity = Vector3.up,
                SnapshotForward = Vector3.forward
            });
        }

        [Test]
        public void HistoricalSnapshotKeepsPhaseLengthsAndBlendProgress()
        {
            EveryCase.Check(new[] { 1828, 2917, -600 }, offset =>
            {
                uint serverStart = 10000, localStart = (uint)(10000 - offset);
                var wire = Snapshot(serverStart);
                // Translate with the current clock pair, then replay historical ticks, not receipt time.
                var local = BuddahTickMath.HandoffToLocal(wire, serverStart + 20, localStart + 20);
                Assert.That(local.EventTick, Is.EqualTo(localStart));
                Assert.That(local.StartTick, Is.EqualTo(localStart));
                Assert.That(local.InheritEndTick - local.StartTick, Is.EqualTo(3));
                Assert.That(local.BlendEndTick - local.InheritEndTick, Is.EqualTo(4));
                Assert.That(local.SuppressSteeringUntilTick, Is.EqualTo(localStart + 9));
                Assert.That(local.RoomBypassUntilTick, Is.EqualTo(localStart + 11));
                for (uint elapsed = 0; elapsed <= 8; elapsed++)
                {
                    var authoritative = BuddahPredictedLaunchHandoffResolver.Advance(wire, serverStart + elapsed);
                    var predicted = BuddahPredictedLaunchHandoffResolver.Advance(local, localStart + elapsed);
                    Assert.That(predicted.CurrentState, Is.EqualTo(authoritative.CurrentState), $"phase at {elapsed}");
                    Assert.That(predicted.BlendAlpha, Is.EqualTo(authoritative.BlendAlpha), $"blend at {elapsed}");
                    Assert.That(predicted.IsActive, Is.EqualTo(authoritative.IsActive));
                }
                Assert.That(wire.StartTick, Is.EqualTo(serverStart));
                Assert.That(BuddahTickMath.HandoffToLocal(wire, serverStart + 40, localStart + 40), Is.EqualTo(local),
                    "Repeated reconciles must translate a fresh snapshot.");
                Assert.That(local.EventId, Is.EqualTo(wire.EventId));
                Assert.That(local.SnapshotPosition, Is.EqualTo(wire.SnapshotPosition));
                Assert.That(local.SnapshotRotation, Is.EqualTo(wire.SnapshotRotation));
                Assert.That(local.SnapshotVelocity, Is.EqualTo(wire.SnapshotVelocity));
                Assert.That(local.SnapshotAngularVelocity, Is.EqualTo(wire.SnapshotAngularVelocity));
                Assert.That(local.SnapshotForward, Is.EqualTo(wire.SnapshotForward));
            }, offset => "clock offset " + offset);
        }

        [Test]
        public void ZeroStartAndZeroDurationEndpointsAreTimestamps()
        {
            EveryCase.Check(new[]
            {
                (inherit: 0u, blend: 4u, expected: BuddahPredictedLaunchState.Blend),
                (inherit: 3u, blend: 0u, expected: BuddahPredictedLaunchState.Inherit),
                (inherit: 0u, blend: 0u, expected: BuddahPredictedLaunchState.Normal),
            }, c =>
            {
                uint inherit = c.inherit, blend = c.blend;
                BuddahPredictedLaunchState expected = c.expected;
                var state = Snapshot(0, inherit, blend);
                state.SuppressSteeringUntilTick = 0; state.RoomBypassUntilTick = 0;
                var local = BuddahTickMath.HandoffToLocal(state, 10, 110);
                Assert.That(local.EventTick, Is.EqualTo(100));
                Assert.That(local.StartTick, Is.EqualTo(100));
                Assert.That(local.InheritEndTick, Is.EqualTo(100 + inherit));
                Assert.That(local.BlendEndTick, Is.EqualTo(100 + inherit + blend));
                Assert.That(local.SuppressSteeringUntilTick, Is.Zero);
                Assert.That(local.RoomBypassUntilTick, Is.Zero);
                Assert.That(BuddahPredictedLaunchHandoffResolver.Advance(local, 100).CurrentState, Is.EqualTo(expected));
                Assert.That(BuddahPredictedLaunchHandoffResolver.Advance(local, 100 + inherit + blend).IsActive, Is.False);
            }, c => $"inherit={c.inherit} blend={c.blend}");
        }

        [Test]
        public void ResetStaysEmptyButInactiveSuppressionAndBypassTailsTranslate()
        {
            Assert.That(BuddahTickMath.HandoffToLocal(default, 100, 200), Is.EqualTo(default(BuddahPredictedLaunchHandoffState)));
            var wire = BuddahPredictedLaunchHandoffResolver.Advance(Snapshot(100), 107);
            Assert.That(wire.IsActive, Is.False);
            var local = BuddahTickMath.HandoffToLocal(wire, 107, 207);
            Assert.That(local.IsActive, Is.False);
            Assert.That(local.SuppressSteeringUntilTick, Is.EqualTo(209));
            Assert.That(local.RoomBypassUntilTick, Is.EqualTo(211));
        }

        [Test]
        public void ReconcileUsesRoleCorrectClock()
        {
            EveryCase.Check(new[]
            {
                (server: true, owner: true, expectedTick: 400u),
                (server: true, owner: false, expectedTick: 400u),
                (server: false, owner: true, expectedTick: 400u),
                (server: false, owner: false, expectedTick: 500u),
            }, c =>
            {
                var handoff = Snapshot(400);
                var modifiers = new BuddahPredictedModifierState { RootUntilTick = 410 };
                uint tick = BuddahTickMath.ReconcileToLocal(ref modifiers, ref handoff, 400, c.server, c.owner, 420, 520);
                Assert.That(tick, Is.EqualTo(c.expectedTick));
                Assert.That(handoff.StartTick, Is.EqualTo(c.server ? 400u : 500u));
                Assert.That(modifiers.RootUntilTick, Is.EqualTo(c.server ? 410u : 510u));
            }, c => $"server={c.server} owner={c.owner}");
        }

        [Test]
        public void SnapshotPairKeepsExpiredHandoffAndModifierExpiredAfterReceiptClockMovesBack()
        {
            EveryCase.Check(new[] { true, false }, owner =>
            {
                // Synthetic regression fixture: live (server, local) changed from (1033, 2033)
                // to (1029, 2033), while the hypothetical historical packet remains (1020, 2012).
                var wire = Snapshot(1000, 12, 20);
                wire.RoomBypassUntilTick = 1030;
                var modifiers = new BuddahPredictedModifierState { RoomBypassUntilTick = 1030 };
                var receiptMapped = BuddahTickMath.HandoffToLocal(wire, 1029, 2033);
                Assert.That(BuddahPredictedLaunchHandoffResolver.Advance(receiptMapped, 2033).IsActive, Is.True,
                    "The old receipt-clock mapping revives this expired phase.");

                uint diagnosticTick = BuddahTickMath.ReconcileToLocal(ref modifiers, ref wire,
                    owner ? 2012u : 1020u, false, owner, 1020, 2012);
                Assert.That(diagnosticTick, Is.EqualTo(2012));
                Assert.That(wire.BlendEndTick, Is.EqualTo(2024));
                Assert.That(modifiers.RoomBypassUntilTick, Is.EqualTo(2022));
                Assert.That(BuddahPredictedLaunchHandoffResolver.Advance(wire, 2033).IsActive, Is.False);
                Assert.That(modifiers.RoomBypassUntilTick, Is.LessThanOrEqualTo(2033));
                // Historical replay is still entitled to reproduce the original active phase.
                var replay = BuddahPredictedLaunchHandoffResolver.Advance(wire, 2018);
                Assert.That(replay.CurrentState, Is.EqualTo(BuddahPredictedLaunchState.Blend));
                Assert.That(replay.BlendAlpha, Is.EqualTo(0.7f).Within(0.0001f));
            }, owner => "owner=" + owner);
        }

        [Test]
        public void LaterAuthoritativeSnapshotCanStillExtendADeadline()
        {
            var handoff = Snapshot(1000, 12, 20);
            var modifiers = new BuddahPredictedModifierState { RoomBypassUntilTick = 1030 };
            BuddahTickMath.ReconcileToLocal(ref modifiers, ref handoff, 2012, false, true, 1020, 2012);
            uint originalDeadline = modifiers.RoomBypassUntilTick;
            handoff = Snapshot(1000, 12, 20);
            modifiers = new BuddahPredictedModifierState { RoomBypassUntilTick = 1050 };
            BuddahTickMath.ReconcileToLocal(ref modifiers, ref handoff, 2013, false, true, 1021, 2013);
            Assert.That(modifiers.RoomBypassUntilTick, Is.EqualTo(originalDeadline + 20));
            // No monotonic cache/tombstone suppresses a legitimate authoritative update.
        }

        // TranslationSaturatesRatherThanUnsignedWrapping moved to BuddahTickMathTests.ServerTicksTranslateWithSaturation.

        [Test]
        public void ModifierDeadlinesTranslateAndNonTimeFieldsStayUnchanged()
        {
            var state = new BuddahPredictedModifierState
            {
                RootUntilTick = 100, AccelUntilTick = 101, PostRootAccelUntilTick = 102,
                ScaleUntilTick = 103, InvertTurnUntilTick = 104, PushGraceUntilTick = 105,
                SuppressSteeringUntilTick = 106, RoomBypassUntilTick = 107,
                FreezeUntilTick = 108, BurstUntilTick = 109, MaxSpeedPenaltyUntilTick = 110,
                AccelExtraForwardForce = 2, AccelExtraMaxSpeed = 3, PostRootAccelExtraForwardForce = 4,
                PostRootAccelExtraMaxSpeed = 5, ScaleMultiplier = 6, ScaleMassMultiplier = 7, ScaleForwardForceMultiplier = 8,
                BurstExtraMaxSpeed = 9, MaxSpeedPenaltyMultiplier = 0.85f,
                // A duration, not a timestamp: translating it would change the burst decay length.
                BurstDurationTicks = 48
            };
            var local = BuddahTickMath.ModifiersToLocal(state, 100, 200);
            Assert.That(new[] { local.RootUntilTick, local.AccelUntilTick, local.PostRootAccelUntilTick,
                local.ScaleUntilTick, local.InvertTurnUntilTick, local.PushGraceUntilTick,
                local.SuppressSteeringUntilTick, local.RoomBypassUntilTick,
                local.FreezeUntilTick, local.BurstUntilTick, local.MaxSpeedPenaltyUntilTick },
                Is.EqualTo(new uint[] { 200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210 }));
            Assert.That(new[] { local.AccelExtraForwardForce, local.AccelExtraMaxSpeed, local.PostRootAccelExtraForwardForce,
                local.PostRootAccelExtraMaxSpeed, local.ScaleMultiplier, local.ScaleMassMultiplier, local.ScaleForwardForceMultiplier,
                local.BurstExtraMaxSpeed, local.MaxSpeedPenaltyMultiplier },
                Is.EqualTo(new float[] { 2, 3, 4, 5, 6, 7, 8, 9, 0.85f }));
            Assert.That(local.BurstDurationTicks, Is.EqualTo(48u));
            Assert.That(state.RootUntilTick, Is.EqualTo(100));
            Assert.That(BuddahTickMath.ModifiersToLocal(default, 100, 200), Is.EqualTo(default(BuddahPredictedModifierState)));
        }
    }
}
