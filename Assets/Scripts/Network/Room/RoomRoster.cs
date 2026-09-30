using System.Collections.Generic;
using System.Text;

namespace SteamMultiplayer.Network
{
    internal sealed class RoomRoster
    {
        private readonly IList<RoomPlayerState> _players;

        internal RoomRoster(IList<RoomPlayerState> players)
        {
            _players = players;
        }

        internal bool TryGetPlayer(int playerId, out RoomPlayerState player)
        {
            int index = FindPlayerIndex(playerId);
            if (index >= 0)
            {
                player = _players[index];
                return true;
            }

            player = default;
            return false;
        }

        internal bool AreAllRequiredPlayersReady()
        {
            if (_players.Count == 0)
                return false;

            for (int i = 0; i < _players.Count; i++)
            {
                if (!_players[i].IsHost && !_players[i].IsReady)
                    return false;
            }

            return true;
        }

        internal int FindPlayerIndex(int playerId)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].PlayerId == playerId)
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
            for (int i = 0; i < _players.Count; i++)
            {
                RoomPlayerState player = _players[i];
                bool nextReady = player.IsHost;
                if (player.IsReady == nextReady)
                    continue;

                player.IsReady = nextReady;
                _players[i] = player;
            }
        }

        internal string BuildSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Players");

            if (_players.Count == 0)
            {
                sb.AppendLine("(none)");
                return sb.ToString();
            }

            for (int i = 0; i < _players.Count; i++)
            {
                RoomPlayerState player = _players[i];
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
                _players.Add(playerState);
            else
                _players[index] = playerState;
        }
    }
}
