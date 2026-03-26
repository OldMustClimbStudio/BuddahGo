using System;

namespace SteamMultiplayer.Network
{
    [Serializable]
    public struct RoomPlayerState : IEquatable<RoomPlayerState>
    {
        public int PlayerId;
        public string SteamId;
        public string PlayerName;
        public bool IsHost;
        public bool IsReady;

        public bool Equals(RoomPlayerState other)
        {
            return PlayerId == other.PlayerId
                && SteamId == other.SteamId
                && PlayerName == other.PlayerName
                && IsHost == other.IsHost
                && IsReady == other.IsReady;
        }

        public override bool Equals(object obj)
        {
            return obj is RoomPlayerState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PlayerId, SteamId, PlayerName, IsHost, IsReady);
        }
    }
}
