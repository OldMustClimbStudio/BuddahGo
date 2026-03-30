using System;

namespace SteamMultiplayer.Network.Results
{
    public enum ResultPlayerChoice
    {
        None,
        StartNewGame,
        ReturnToRoom
    }

    public enum ResultFinalDecision
    {
        None,
        StartNewGame,
        ReturnToRoom
    }

    [Serializable]
    public struct ResultPlayerDecision : IEquatable<ResultPlayerDecision>
    {
        public int ClientId;
        public string PlayerName;
        public ResultPlayerChoice Choice;
        public bool HasResponded;

        public bool Equals(ResultPlayerDecision other)
        {
            return ClientId == other.ClientId
                && PlayerName == other.PlayerName
                && Choice == other.Choice
                && HasResponded == other.HasResponded;
        }

        public override bool Equals(object obj)
        {
            return obj is ResultPlayerDecision other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ClientId, PlayerName, Choice, HasResponded);
        }
    }
}
