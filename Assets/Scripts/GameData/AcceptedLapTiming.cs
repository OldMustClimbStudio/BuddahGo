using System.Collections.Generic;
using BuddahGo.Match;

/// <summary>Holds validated local server crossings until the normal progress report consumes them.</summary>
internal sealed class AcceptedLapTiming
{
    private readonly Queue<(int Lap, double Time)> _pending = new Queue<(int, double)>();
    private IMatchClock _clock;
    private IRaceTiming _timing;
    private int _lastAcceptedLap;

    public void Capture(int completedLaps, IMatchClock clock, IRaceTiming timing)
    {
        if (clock == null || timing == null || completedLaps <= 0)
            return;

        if (!ReferenceEquals(clock, _clock) || !ReferenceEquals(timing, _timing))
        {
            Reset();
            _clock = clock;
            _timing = timing;
        }

        if (completedLaps <= _lastAcceptedLap)
            return;

        double now = clock.Now;
        if (double.IsNaN(now) || double.IsInfinity(now) || now < 0d)
            return;

        _lastAcceptedLap = completedLaps;
        _pending.Enqueue((completedLaps, now));
    }

    public void ObservePending(IMatchClock clock, IRaceTiming timing, RacerId racer, int lapsToFinish)
    {
        // Never replay an old session's pending timestamps into replacement services.
        if (!ReferenceEquals(clock, _clock) || !ReferenceEquals(timing, _timing))
        {
            Reset();
            return;
        }

        while (_pending.Count > 0)
        {
            var crossing = _pending.Dequeue();
            if (crossing.Lap <= lapsToFinish)
                timing.ObserveCompletedLaps(racer, crossing.Lap, crossing.Time);
        }
    }

    public void Reset()
    {
        _pending.Clear();
        _clock = null;
        _timing = null;
        _lastAcceptedLap = 0;
    }
}
