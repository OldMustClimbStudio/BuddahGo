namespace NewBuddah.PredictionV2.Core
{
    // Wire data remains in the server clock. Translate only the client's working copy.
    internal static class BuddahModifierTickClock
    {
        internal static BuddahPredictedModifierState ToLocal(BuddahPredictedModifierState state, uint serverTick, uint localTick)
        {
            state.RootUntilTick = ToLocalDeadline(state.RootUntilTick, serverTick, localTick);
            state.AccelUntilTick = ToLocalDeadline(state.AccelUntilTick, serverTick, localTick);
            state.PostRootAccelUntilTick = ToLocalDeadline(state.PostRootAccelUntilTick, serverTick, localTick);
            state.ScaleUntilTick = ToLocalDeadline(state.ScaleUntilTick, serverTick, localTick);
            state.InvertTurnUntilTick = ToLocalDeadline(state.InvertTurnUntilTick, serverTick, localTick);
            state.PushGraceUntilTick = ToLocalDeadline(state.PushGraceUntilTick, serverTick, localTick);
            state.SuppressSteeringUntilTick = ToLocalDeadline(state.SuppressSteeringUntilTick, serverTick, localTick);
            state.RoomBypassUntilTick = ToLocalDeadline(state.RoomBypassUntilTick, serverTick, localTick);
            return state;
        }

        internal static uint ToLocalDeadline(uint deadline, uint serverTick, uint localTick)
        {
            // Zero means unset. Widen before subtraction so a future deadline cannot wrap uint.
            if (deadline == 0u)
                return 0u;

            long translated = (long)localTick + (long)deadline - serverTick;
            if (translated <= 0L)
                return 0u;
            if (translated >= uint.MaxValue)
                return uint.MaxValue;
            return (uint)translated;
        }
    }
}
