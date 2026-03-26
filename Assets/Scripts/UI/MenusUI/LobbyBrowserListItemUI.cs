using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SteamMultiplayer.Network;

namespace SteamMultiplayer.UI
{
    public class LobbyBrowserListItemUI : MonoBehaviour
    {
        /* 此脚本用于显示大厅浏览界面中的每个房间项，包含房间名称、玩家数量、房主信息等，并提供加入功能。
            当用户点击加入按钮时，会通知LobbyBrowserUI进行相应的处理，如尝试加入该房间等。
            */
        [SerializeField] private TextMeshProUGUI roomNameText;
        [SerializeField] private TextMeshProUGUI memberCountText;
        [SerializeField] private TextMeshProUGUI hostText;
        [SerializeField] private TextMeshProUGUI lobbyIdText;
        [SerializeField] private Button joinButton;

        private ulong _lobbyId;
        private LobbyBrowserUI _owner;

        private void Awake()
        {
            if (joinButton != null)
                joinButton.onClick.AddListener(HandleJoinClicked);
        }

        private void OnDestroy()
        {
            if (joinButton != null)
                joinButton.onClick.RemoveListener(HandleJoinClicked);
        }

        public void Bind(LobbyListItemData data, LobbyBrowserUI owner)
        {
            _owner = owner;
            _lobbyId = data.LobbyId;

            if (roomNameText != null)
                roomNameText.text = string.IsNullOrWhiteSpace(data.LobbyName) ? "Unnamed Room" : data.LobbyName;

            if (memberCountText != null)
                memberCountText.text = $"{data.CurrentPlayers} / {data.MaxPlayers}";

            if (hostText != null)
                hostText.text = string.IsNullOrWhiteSpace(data.HostSteamId) ? "Host" : $"Host: {data.HostSteamId}";

            if (lobbyIdText != null)
                lobbyIdText.text = $"Lobby ID: {data.LobbyId}";

            if (joinButton != null)
                joinButton.interactable = data.IsJoinable;
        }

        private void HandleJoinClicked()
        {
            _owner?.JoinLobby(_lobbyId);
        }
    }
}
