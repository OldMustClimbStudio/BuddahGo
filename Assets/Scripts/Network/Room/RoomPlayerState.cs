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

        // The player's pick from the menu (LocalMatchSelection), checked by the server. Empty = not picked yet.
        public string Skill0;
        public string Skill1;
        public string Skill2;
        public string SkinId;

        public string[] SkillIds => new[] { Skill0 ?? string.Empty, Skill1 ?? string.Empty, Skill2 ?? string.Empty };

        public void SetSkills(string[] skillIds)
        {
            Skill0 = skillIds != null && skillIds.Length > 0 ? skillIds[0] : string.Empty;
            Skill1 = skillIds != null && skillIds.Length > 1 ? skillIds[1] : string.Empty;
            Skill2 = skillIds != null && skillIds.Length > 2 ? skillIds[2] : string.Empty;
        }

        /// <summary>Carries the pick over when the roster rebuilds a player's entry.</summary>
        public void CopySelectionFrom(RoomPlayerState other)
        {
            Skill0 = other.Skill0;
            Skill1 = other.Skill1;
            Skill2 = other.Skill2;
            SkinId = other.SkinId;
        }

        public bool Equals(RoomPlayerState other)
        {
            return PlayerId == other.PlayerId
                && SteamId == other.SteamId
                && PlayerName == other.PlayerName
                && IsHost == other.IsHost
                && IsReady == other.IsReady
                && Skill0 == other.Skill0
                && Skill1 == other.Skill1
                && Skill2 == other.Skill2
                && SkinId == other.SkinId;
        }

        public override bool Equals(object obj)
        {
            return obj is RoomPlayerState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(HashCode.Combine(PlayerId, SteamId, PlayerName, IsHost, IsReady), Skill0, Skill1, Skill2, SkinId);
        }
    }
}
