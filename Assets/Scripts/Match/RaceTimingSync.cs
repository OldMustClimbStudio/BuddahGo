using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace BuddahGo.Match
{
    public struct RaceTimingRecord
    {
        public int RacerId;
        public int LapIndex;
        public double LapSeconds;
        public double TotalSeconds;
        public bool Finished;
    }

    public sealed class RaceTimingSync : NetworkBehaviour, IRaceTiming
    {
        public readonly SyncList<RaceTimingRecord> Records = new SyncList<RaceTimingRecord>();
        private readonly RaceTiming _timing = new RaceTiming();
        public override void OnStartServer()
        {
            base.OnStartServer();
            _timing.Reset();
            Records.Clear();
            MatchServices.Timing = this;
            MatchServices.EndPolicy = new RaceEndPolicy();
        }
        public override void OnStartClient() { base.OnStartClient(); MatchServices.Timing = this; }
        public override void OnStopNetwork()
        {
            if (ReferenceEquals(MatchServices.Timing, this))
            {
                MatchServices.Timing = null;
                MatchServices.EndPolicy = null;
            }
            _timing.Reset();
            base.OnStopNetwork();
        }
        public void Begin(double startTime)
        {
            if (!IsServerInitialized) return;
            // Validate/reset first: an invalid start must preserve the previous synchronized result.
            _timing.Begin(startTime);
            Records.Clear();
        }
        public void ObserveCompletedLaps(RacerId racer, int completedLaps, double now)
        {
            if (!IsServerInitialized) return;
            _timing.ObserveCompletedLaps(racer, completedLaps, now);
            Publish(racer);
        }
        public void Finish(RacerId racer, int lapsToFinish, double now)
        {
            if (!IsServerInitialized) return;
            _timing.Finish(racer, lapsToFinish, now);
            Publish(racer);
        }
        public bool TryGetResult(RacerId racer, out RaceTimingResult result)
        {
            if (IsServerInitialized) return _timing.TryGetResult(racer, out result);
            var laps = new List<double>();
            double total = 0;
            bool finished = false;
            for (int i = 0; i < Records.Count; i++)
            {
                RaceTimingRecord row = Records[i];
                if (row.RacerId != racer.Value) continue;
                laps.Add(row.LapSeconds);
                total = row.TotalSeconds;
                finished = row.Finished;
            }
            result = new RaceTimingResult(racer, total, laps.ToArray(), finished);
            return laps.Count > 0;
        }
        private void Publish(RacerId racer)
        {
            if (!_timing.TryGetResult(racer, out RaceTimingResult result)) return;
            for (int lap = 0; lap < result.LapSeconds.Length; lap++)
            {
                int index = -1;
                for (int i = 0; i < Records.Count; i++)
                    if (Records[i].RacerId == racer.Value && Records[i].LapIndex == lap) { index = i; break; }
                var row = new RaceTimingRecord { RacerId = racer.Value, LapIndex = lap,
                    LapSeconds = result.LapSeconds[lap], TotalSeconds = result.TotalSeconds, Finished = result.Finished };
                if (index < 0) Records.Add(row);
                else if (Records[index].Finished != row.Finished || Records[index].TotalSeconds != row.TotalSeconds)
                    Records[index] = row;
            }
        }
    }
}
