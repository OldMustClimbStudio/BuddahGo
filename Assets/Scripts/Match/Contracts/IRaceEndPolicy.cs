namespace BuddahGo.Match
{
    public interface IRaceEndPolicy
    {
        bool ShouldEnd(IMatchRules rules, int racerCount, int finishedCount,
            bool humanFinished, double now, double? firstFinishTime, double countdownSeconds);
    }

    // Per-race services registered by RaceMap scene objects (MatchClockSync, RaceTimingSync,
    // RacerRegistry). Cleared by SessionLauncher.ResetMatchGlobals.
    public static class MatchServices
    {
        public static IMatchClock Clock { get; set; }
        public static IRaceTiming Timing { get; set; }
        public static IRaceEndPolicy EndPolicy { get; set; }
        public static void Reset()
        {
            RacerDirectory.Current = null;
            Clock = null;
            Timing = null;
            EndPolicy = null;
        }
    }
}
