using System;

namespace BuddahGo.Match
{
    // Humans use their FishNet ClientId; server AI use AIBase + index. Both share one int space
    // so leaderboard, finish and result dictionaries can key every racer the same way.
    public readonly struct RacerId : IEquatable<RacerId>
    {
        public const int AIBase = 10000;
        public const int MaxAI = 5;

        public int Value { get; }
        public bool IsAI => IsAIValue(Value);
        public bool IsHuman => !IsAI;
        public int AIIndex => IsAI ? Value - AIBase : -1;
        private RacerId(int value) { Value = value; }

        public static bool IsHumanValue(int value) => value >= 0 && value < AIBase;
        public static bool IsAIValue(int value) => value >= AIBase && value < AIBase + MaxAI;
        public static bool IsValidValue(int value) => IsHumanValue(value) || IsAIValue(value);

        public static RacerId FromClient(int clientId)
        {
            if (!IsHumanValue(clientId)) throw new ArgumentOutOfRangeException(nameof(clientId));
            return new RacerId(clientId);
        }
        public static RacerId FromValue(int value) => value >= AIBase ? ForAI(value - AIBase) : FromClient(value);
        public static RacerId ForAI(int index)
        {
            if (index < 0 || index >= MaxAI) throw new ArgumentOutOfRangeException(nameof(index));
            return new RacerId(AIBase + index);
        }
        public bool Equals(RacerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RacerId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
        public static bool operator ==(RacerId left, RacerId right) => left.Equals(right);
        public static bool operator !=(RacerId left, RacerId right) => !left.Equals(right);
    }
}
