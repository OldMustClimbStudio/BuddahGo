using TMPro;
using UnityEngine;

namespace SteamMultiplayer.UI
{
    public class RoomPlayerEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI hostBadgeText;
        [SerializeField] private TextMeshProUGUI readyStateText;

        public void Bind(SteamMultiplayer.Network.RoomPlayerState playerState)
        {
            if (playerNameText != null)
                playerNameText.text = string.IsNullOrWhiteSpace(playerState.PlayerName) ? "Unknown Player" : playerState.PlayerName;

            if (hostBadgeText != null)
                hostBadgeText.text = playerState.IsHost ? "HOST" : string.Empty;

            if (readyStateText != null)
                readyStateText.text = playerState.IsHost ? "Leader" : (playerState.IsReady ? "Ready" : "Waiting");
        }
    }
}
