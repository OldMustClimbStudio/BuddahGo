using System;

namespace BuddahGo.Match
{
    // S1 reads the authoritative server tick directly; pause offsets belong to S7.
    public sealed class MatchClock : IMatchClock
    {
        private readonly Func<uint> _tick;
        private readonly double _tickDelta;

        public MatchClock(Func<uint> tick, double tickDelta)
        {
            _tick = tick ?? throw new ArgumentNullException(nameof(tick));
            if (double.IsNaN(tickDelta) || double.IsInfinity(tickDelta) || tickDelta <= 0d)
                throw new ArgumentOutOfRangeException(nameof(tickDelta));
            _tickDelta = tickDelta;
        }

        public double Now => _tick() * _tickDelta;
        public bool IsPaused => false;
    }
}
