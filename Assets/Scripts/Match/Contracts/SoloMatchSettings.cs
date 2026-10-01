using System;
using System.Collections.Generic;

namespace BuddahGo.Match
{
    public enum SoloDifficulty { Easy, Normal, Hard }

    // Immutable request data belongs to Contracts because ISessionControl accepts it.
    public sealed class SoloMatchSettings
    {
        public int AICount { get; }
        public SoloDifficulty Difficulty { get; }
        public IReadOnlyList<string> AINames { get; }

        public SoloMatchSettings(int aiCount, SoloDifficulty difficulty, IEnumerable<string> aiNames = null)
        {
            if (aiCount < 0 || aiCount > 5) throw new ArgumentOutOfRangeException(nameof(aiCount));
            if (!Enum.IsDefined(typeof(SoloDifficulty), difficulty)) throw new ArgumentOutOfRangeException(nameof(difficulty));
            AICount = aiCount;
            Difficulty = difficulty;
            AINames = new List<string>(aiNames ?? Array.Empty<string>()).AsReadOnly();
        }
    }
}
