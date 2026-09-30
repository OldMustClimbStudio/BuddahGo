using NUnit.Framework;
using NewBuddah.PredictionV2.Core;
using UnityEngine;

namespace BuddahGo.Tests
{
    public class HandoffClockTests
    {
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

        [TestCase(1828)]
        [TestCase(2917)]
        [TestCase(-600)]
        public void HistoricalSnapshotKeepsPhaseLengthsAndBlendProgress(int offset)
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
        }

        [TestCase(0u, 4u, BuddahPredictedLaunchState.Blend)]
        [TestCase(3u, 0u, BuddahPredictedLaunchState.Inherit)]
        [TestCase(0u, 0u, BuddahPredictedLaunchState.Normal)]
        public void ZeroStartAndZeroDurationEndpointsAreTimestamps(uint inherit, uint blend, BuddahPredictedLaunchState expected)
        {
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

        [TestCase(true, true, 400u)]
        [TestCase(true, false, 400u)]
        [TestCase(false, true, 400u)]
        [TestCase(false, false, 500u)]
        public void ReconcileUsesRoleCorrectClock(bool server, bool owner, uint expectedTick)
        {
            var handoff = Snapshot(400);
            var modifiers = new BuddahPredictedModifierState { RootUntilTick = 410 };
            uint tick = BuddahTickMath.ReconcileToLocal(ref modifiers, ref handoff, 400, server, owner, 420, 520);
            Assert.That(tick, Is.EqualTo(expectedTick));
            Assert.That(handoff.StartTick, Is.EqualTo(server ? 400u : 500u));
            Assert.That(modifiers.RootUntilTick, Is.EqualTo(server ? 410u : 510u));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SnapshotPairKeepsExpiredHandoffAndModifierExpiredAfterReceiptClockMovesBack(bool owner)
        {
            // Recorded pure-client stress: live (server, local) changed from (2745, 2835)
            // to (2741, 2835), while this historical packet remained (2732, 2814).
            var wire = Snapshot(2712, 12, 20);
            wire.RoomBypassUntilTick = 2742;
            var modifiers = new BuddahPredictedModifierState { RoomBypassUntilTick = 2742 };
            var receiptMapped = BuddahTickMath.HandoffToLocal(wire, 2741, 2835);
            Assert.That(BuddahPredictedLaunchHandoffResolver.Advance(receiptMapped, 2835).IsActive, Is.True,
                "The old receipt-clock mapping revives this expired phase.");

            uint diagnosticTick = BuddahTickMath.ReconcileToLocal(ref modifiers, ref wire,
                owner ? 2814u : 2732u, false, owner, 2732, 2814);
            Assert.That(diagnosticTick, Is.EqualTo(2814));
            Assert.That(wire.BlendEndTick, Is.EqualTo(2826));
            Assert.That(modifiers.RoomBypassUntilTick, Is.EqualTo(2824));
            Assert.That(BuddahPredictedLaunchHandoffResolver.Advance(wire, 2835).IsActive, Is.False);
            Assert.That(modifiers.RoomBypassUntilTick, Is.LessThanOrEqualTo(2835));
            // Historical replay is still entitled to reproduce the original active phase.
            var replay = BuddahPredictedLaunchHandoffResolver.Advance(wire, 2820);
            Assert.That(replay.CurrentState, Is.EqualTo(BuddahPredictedLaunchState.Blend));
            Assert.That(replay.BlendAlpha, Is.EqualTo(0.7f).Within(0.0001f));
        }

        [Test]
        public void LaterAuthoritativeSnapshotCanStillExtendADeadline()
        {
            var handoff = Snapshot(2712, 12, 20);
            var modifiers = new BuddahPredictedModifierState { RoomBypassUntilTick = 2742 };
            BuddahTickMath.ReconcileToLocal(ref modifiers, ref handoff, 2814, false, true, 2732, 2814);
            uint originalDeadline = modifiers.RoomBypassUntilTick;
            handoff = Snapshot(2712, 12, 20);
            modifiers = new BuddahPredictedModifierState { RoomBypassUntilTick = 2762 };
            BuddahTickMath.ReconcileToLocal(ref modifiers, ref handoff, 2815, false, true, 2733, 2815);
            Assert.That(modifiers.RoomBypassUntilTick, Is.EqualTo(originalDeadline + 20));
            // No monotonic cache/tombstone suppresses a legitimate authoritative update.
        }

        [TestCase(0u, 10u, 100u, 90u, 0u)]
        [TestCase(1u, 3000u, 10u, 0u, 0u)]
        [TestCase(uint.MaxValue, 0u, 1u, uint.MaxValue, uint.MaxValue)]
        [TestCase(1u, uint.MaxValue, 5u, 0u, 0u)]
        [TestCase(uint.MaxValue - 1u, 2u, 10u, uint.MaxValue, uint.MaxValue)]
        public void TranslationSaturatesRatherThanUnsignedWrapping(uint value, uint server, uint local, uint expectedEvent, uint expectedDeadline)
        {
            Assert.That(BuddahTickMath.ServerEventToLocalTick(value, server, local), Is.EqualTo(expectedEvent));
            Assert.That(BuddahTickMath.ServerDeadlineToLocalTick(value, server, local), Is.EqualTo(expectedDeadline));
        }

        [Test]
        public void ModifierDeadlinesTranslateAndNonTimeFieldsStayUnchanged()
        {
            var state = new BuddahPredictedModifierState
            {
                RootUntilTick = 100, AccelUntilTick = 101, PostRootAccelUntilTick = 102,
                ScaleUntilTick = 103, InvertTurnUntilTick = 104, PushGraceUntilTick = 105,
                SuppressSteeringUntilTick = 106, RoomBypassUntilTick = 107,
                AccelExtraForwardForce = 2, AccelExtraMaxSpeed = 3, PostRootAccelExtraForwardForce = 4,
                PostRootAccelExtraMaxSpeed = 5, ScaleMultiplier = 6, ScaleMassMultiplier = 7, ScaleForwardForceMultiplier = 8
            };
            var local = BuddahTickMath.ModifiersToLocal(state, 100, 200);
            Assert.That(new[] { local.RootUntilTick, local.AccelUntilTick, local.PostRootAccelUntilTick,
                local.ScaleUntilTick, local.InvertTurnUntilTick, local.PushGraceUntilTick,
                local.SuppressSteeringUntilTick, local.RoomBypassUntilTick },
                Is.EqualTo(new uint[] { 200, 201, 202, 203, 204, 205, 206, 207 }));
            Assert.That(new[] { local.AccelExtraForwardForce, local.AccelExtraMaxSpeed, local.PostRootAccelExtraForwardForce,
                local.PostRootAccelExtraMaxSpeed, local.ScaleMultiplier, local.ScaleMassMultiplier, local.ScaleForwardForceMultiplier },
                Is.EqualTo(new float[] { 2, 3, 4, 5, 6, 7, 8 }));
            Assert.That(state.RootUntilTick, Is.EqualTo(100));
            Assert.That(BuddahTickMath.ModifiersToLocal(default, 100, 200), Is.EqualTo(default(BuddahPredictedModifierState)));
        }
    }
}
