using System;
using System.Collections.Generic;

namespace BuddahGo.Match
{
    /// <summary>Server-side crossing times, all in the same Match Clock domain.</summary>
    public sealed class RaceTiming : IRaceTiming
    {
        private sealed class Entry
        {
            public readonly List<double> Laps = new List<double>();
            public double LastCrossing;
            public bool Finished;
        }

        private readonly Dictionary<RacerId, Entry> _entries = new Dictionary<RacerId, Entry>();
        private bool _begun;
        private double _startTime;

        public void Begin(double startTime)
        {
            if (!IsValidTime(startTime)) throw new ArgumentOutOfRangeException(nameof(startTime));
            Reset();
            _startTime = startTime;
            _begun = true;
        }

        public void ObserveCompletedLaps(RacerId racer, int completedLaps, double now)
        {
            if (!_begun || completedLaps < 0 || !IsValidTime(now) || now < _startTime) return;
            _entries.TryGetValue(racer, out Entry entry);
            int recordedLaps = entry == null ? 0 : entry.Laps.Count;
            if (entry != null && (entry.Finished || now < entry.LastCrossing)) return;
            // A jump cannot supply the missing crossing timestamps. Do not invent Lap Times.
            if (completedLaps < recordedLaps || completedLaps - recordedLaps > 1) return;
            if (entry == null)
            {
                entry = new Entry { LastCrossing = _startTime };
                _entries.Add(racer, entry);
            }
            if (completedLaps == recordedLaps) return;
            entry.Laps.Add(now - entry.LastCrossing);
            entry.LastCrossing = now;
        }

        public void Finish(RacerId racer, int lapsToFinish, double now)
        {
            if (!_begun || lapsToFinish <= 0 || !IsValidTime(now) || now < _startTime) return;
            if (_entries.TryGetValue(racer, out Entry entry)
                && (entry.Finished || now < entry.LastCrossing || entry.Laps.Count > lapsToFinish)) return;
            // The final crossing may be submitted before or by Finish; both paths are idempotent.
            ObserveCompletedLaps(racer, lapsToFinish, now);
            if (_entries.TryGetValue(racer, out entry) && entry.Laps.Count == lapsToFinish)
                entry.Finished = true;
        }

        public bool TryGetResult(RacerId racer, out RaceTimingResult result)
        {
            if (!_entries.TryGetValue(racer, out Entry entry))
            {
                result = default;
                return false;
            }
            // Until finished, TotalSeconds covers recorded complete laps only.
            // Copy the array so callers and subsequent reports cannot mutate each other's data.
            result = new RaceTimingResult(racer, entry.LastCrossing - _startTime,
                entry.Laps.ToArray(), entry.Finished);
            return true;
        }

        public void Reset()
        {
            _entries.Clear();
            _startTime = 0d;
            _begun = false;
        }

        private static bool IsValidTime(double time) =>
            !double.IsNaN(time) && !double.IsInfinity(time) && time >= 0d;
    }
}
