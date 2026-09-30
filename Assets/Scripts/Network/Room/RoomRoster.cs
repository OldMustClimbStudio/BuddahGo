using System.Collections.Generic;
using System.Text;

namespace SteamMultiplayer.Network
{
    internal sealed class RoomRoster
    {
        private readonly IList<RoomPlayerState> Players;

        internal RoomRoster(IList<RoomPlayerState> players)
        {
            Players = players;
        }

        internal bool TryGetPlayer(int playerId, out RoomPlayerState player)
        {
            int index = FindPlayerIndex(playerId);
            if (index >= 0)
            {
                player = Players[index];
                return true;
            }

            player = default;
            return false;
        }

        internal bool AreAllRequiredPlayersReady()
        {
            if (Players.Count == 0)
                return false;

            for (int i = 0; i < Players.Count; i++)
            {
                if (!Players[i].IsHost && !Players[i].IsReady)
                    return false;
            }

            return true;
        }

        internal int FindPlayerIndex(int playerId)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].PlayerId == playerId)
                    return i;
            }

            return -1;
        }

        internal static string SanitizePlayerName(string name, int maxPlayerNameLength)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            string trimmed = name.Trim();
            if (trimmed.Length > maxPlayerNameLength)
                trimmed = trimmed.Substring(0, maxPlayerNameLength);

            return trimmed;
        }

        internal void ResetReadyForRoomReturn()
        {
            for (int i = 0; i < Players.Count; i++)
            {
                RoomPlayerState player = Players[i];
                bool nextReady = player.IsHost;
                if (player.IsReady == nextReady)
                    continue;

                player.IsReady = nextReady;
                Players[i] = player;
            }
        }

        internal string BuildSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Players");

            if (Players.Count == 0)
            {
                sb.AppendLine("(none)");
                return sb.ToString();
            }

            for (int i = 0; i < Players.Count; i++)
            {
                RoomPlayerState player = Players[i];
                string roleLabel = player.IsHost ? "Host" : "Player";
                string readyLabel = player.IsHost ? "Leader" : (player.IsReady ? "Ready" : "Waiting");
                sb.AppendLine($"{player.PlayerName} [{roleLabel}] [{readyLabel}]");
            }

            return sb.ToString();
        }

        internal void Upsert(RoomPlayerState playerState, int playerId)
        {
            int index = FindPlayerIndex(playerId);
            if (index < 0)
                Players.Add(playerState);
            else
                Players[index] = playerState;
        }
    }
}
