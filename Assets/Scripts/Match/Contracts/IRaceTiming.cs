namespace BuddahGo.Match
{
    public readonly struct RaceTimingResult
    {
        public RacerId Racer { get; }
        public double TotalSeconds { get; }
        public double[] LapSeconds { get; }
        public bool Finished { get; }
        public RaceTimingResult(RacerId racer, double totalSeconds, double[] lapSeconds, bool finished)
        {
            Racer = racer;
            TotalSeconds = totalSeconds;
            LapSeconds = lapSeconds;
            Finished = finished;
        }
    }

    public interface IRaceTiming
    {
        void Begin(double startTime);
        void ObserveCompletedLaps(RacerId racer, int completedLaps, double now);
        void Finish(RacerId racer, int lapsToFinish, double now);
        bool TryGetResult(RacerId racer, out RaceTimingResult result);
        void Reset();
    }
}
