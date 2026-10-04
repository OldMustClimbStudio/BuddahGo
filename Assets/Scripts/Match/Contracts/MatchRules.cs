using System;

namespace BuddahGo.Match
{
    public static class MatchRules
    {
        private static IMatchRules _current = new OnlineMatchRules();
        public static IMatchRules Current
        {
            get => _current;
            set => _current = value ?? throw new ArgumentNullException(nameof(value));
        }
        public static void Reset() => _current = new OnlineMatchRules();
    }
}
