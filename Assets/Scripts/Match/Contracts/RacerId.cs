using System;

namespace BuddahGo.Match
{
    public readonly struct RacerId : IEquatable<RacerId>
    {
        public int Value { get; }
        public bool IsAI => Value >= 10000;
        private RacerId(int value) { Value = value; }
        public static RacerId FromClient(int clientId)
        {
            if (clientId < 0 || clientId >= 10000) throw new ArgumentOutOfRangeException(nameof(clientId));
            return new RacerId(clientId);
        }
        public bool Equals(RacerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RacerId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
        public static bool operator ==(RacerId left, RacerId right) => left.Equals(right);
        public static bool operator !=(RacerId left, RacerId right) => !left.Equals(right);
    }
}
