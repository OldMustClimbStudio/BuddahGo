namespace BuddahGo.Match
{
    public interface IRaceEndPolicy
    {
        bool ShouldEnd(IMatchRules rules, int racerCount, int finishedCount,
            bool humanFinished, double now, double? firstFinishTime, double countdownSeconds);
    }

    public static class MatchServices
    {
        public static IMatchClock Clock { get; set; }
        public static IRaceTiming Timing { get; set; }
        public static IRaceEndPolicy EndPolicy { get; set; }
        public static void Reset()
        {
            Clock = null;
            Timing = null;
            EndPolicy = null;
        }
    }
}
