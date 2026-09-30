using UnityEngine;

namespace NewBuddah.PredictionV2.Core
{
    internal static class BuddahTickMath
    {
        // The impulse channel stores server ticks. During replay use the historical
        // server tick supplied by FishNet, not its live (and adjustable) clock.
        internal static uint ImpulseEventClock(bool isServer, bool isReplaying,
            uint localTick, uint serverTick, uint serverReplayTick)
        {
            return isServer ? localTick : isReplaying ? serverReplayTick : serverTick;
        }

        // Server-stamped events must enter the owner's local simulation clock before queuing.
        // Event tick zero is valid (unlike a modifier deadline's unset sentinel).
        internal static uint ServerEventToLocalTick(uint eventTick, uint serverTick, uint localTick)
        {
            long translated = (long)localTick + (long)eventTick - serverTick;
            if (translated <= 0L)
                return 0u;
            if (translated >= uint.MaxValue)
                return uint.MaxValue;
            return (uint)translated;
        }

        // Zero is an unset deadline; event timestamps do not share this sentinel.
        internal static uint ServerDeadlineToLocalTick(uint deadline, uint serverTick, uint localTick)
        {
            return deadline == 0u ? 0u : ServerEventToLocalTick(deadline, serverTick, localTick);
        }

        internal static BuddahPredictedModifierState ModifiersToLocal(BuddahPredictedModifierState state, uint serverTick, uint localTick)
        {
            state.RootUntilTick = ServerDeadlineToLocalTick(state.RootUntilTick, serverTick, localTick);
            state.AccelUntilTick = ServerDeadlineToLocalTick(state.AccelUntilTick, serverTick, localTick);
            state.PostRootAccelUntilTick = ServerDeadlineToLocalTick(state.PostRootAccelUntilTick, serverTick, localTick);
            state.ScaleUntilTick = ServerDeadlineToLocalTick(state.ScaleUntilTick, serverTick, localTick);
            state.InvertTurnUntilTick = ServerDeadlineToLocalTick(state.InvertTurnUntilTick, serverTick, localTick);
            state.PushGraceUntilTick = ServerDeadlineToLocalTick(state.PushGraceUntilTick, serverTick, localTick);
            state.SuppressSteeringUntilTick = ServerDeadlineToLocalTick(state.SuppressSteeringUntilTick, serverTick, localTick);
            state.RoomBypassUntilTick = ServerDeadlineToLocalTick(state.RoomBypassUntilTick, serverTick, localTick);
            return state;
        }

        internal static BuddahPredictedLaunchHandoffState HandoffToLocal(
            BuddahPredictedLaunchHandoffState state, uint serverTick, uint localTick)
        {
            // A reset must stay empty, but inactive states may still carry suppression/bypass tails.
            if (!state.IsActive && state.EventId == 0u && state.EventTick == 0u && state.StartTick == 0u
                && state.InheritEndTick == 0u && state.BlendEndTick == 0u
                && state.SuppressSteeringUntilTick == 0u && state.RoomBypassUntilTick == 0u)
                return state;

            state.EventTick = ServerEventToLocalTick(state.EventTick, serverTick, localTick);
            state.StartTick = ServerEventToLocalTick(state.StartTick, serverTick, localTick);
            // Phase endpoints can equal a valid zero start for a zero-duration phase.
            state.InheritEndTick = ServerEventToLocalTick(state.InheritEndTick, serverTick, localTick);
            state.BlendEndTick = ServerEventToLocalTick(state.BlendEndTick, serverTick, localTick);
            state.SuppressSteeringUntilTick = ServerDeadlineToLocalTick(state.SuppressSteeringUntilTick, serverTick, localTick);
            state.RoomBypassUntilTick = ServerDeadlineToLocalTick(state.RoomBypassUntilTick, serverTick, localTick);
            return state;
        }

        // serverTick/localTick must be the paired ServerStateTick/ClientStateTick of this
        // reconcile, not the receipt-time clocks. Return diagnostics in that same local clock.
        internal static uint ReconcileToLocal(ref BuddahPredictedModifierState modifiers,
            ref BuddahPredictedLaunchHandoffState handoff, uint stateTick,
            bool isServer, bool isOwner, uint serverTick, uint localTick)
        {
            if (isServer)
                return stateTick;

            modifiers = ModifiersToLocal(modifiers, serverTick, localTick);
            handoff = HandoffToLocal(handoff, serverTick, localTick);
            // FishNet's owner tick is already ClientStateTick; observers receive ServerStateTick.
            return isOwner ? stateTick : ServerEventToLocalTick(stateTick, serverTick, localTick);
        }

        internal static uint DurationToTicks(float durationSeconds, float tickDelta)
        {
            return (uint)Mathf.CeilToInt(durationSeconds / Mathf.Max(0.0001f, tickDelta));
        }
    }
}
