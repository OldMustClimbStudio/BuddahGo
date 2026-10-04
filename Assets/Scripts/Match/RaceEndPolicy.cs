using System;

namespace BuddahGo.Match
{
    public sealed class RaceEndPolicy : IRaceEndPolicy
    {
        public bool ShouldEnd(IMatchRules rules, int racerCount, int finishedCount,
            bool humanFinished, double now, double? firstFinishTime, double countdownSeconds)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (rules.EndOnHumanFinish && humanFinished) return true;
            if (rules.EndWhenAllRacersFinished && racerCount > 0 && finishedCount == racerCount)
                return true;
            // Online keeps its countdown even if everyone finishes or roster counts change.
            // An empty roster alone never triggers the Solo all-finished condition above.
            if (!firstFinishTime.HasValue || !IsValidTime(firstFinishTime.Value)
                || !IsValidTime(now) || !IsValidTime(countdownSeconds)) return false;
            return now >= firstFinishTime.Value && now - firstFinishTime.Value >= countdownSeconds;
        }

        private static bool IsValidTime(double time) =>
            !double.IsNaN(time) && !double.IsInfinity(time) && time >= 0d;
    }
}
