using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SteamMultiplayer.Network;

namespace SteamMultiplayer.UI
{
    public class RoomUI : MonoBehaviour
    {
        /* 此脚本用于处理房间界面，显示当前房间的名称、ID、状态以及玩家列表，并提供准备、开始游戏和离开房间等功能。
            脚本会监听SteamLobbyManager和RoomStateManager的相关事件，以便在房间状态变化、玩家列表更新等情况下刷新界面显示。
            同时提供返回按钮功能，允许用户离开房间并返回主菜单界面。
            */
            
        [Header("Roots")]
        [SerializeField] private GameObject roomRoot;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI roomNameText;
        [SerializeField] private TextMeshProUGUI roomIdText;
        [SerializeField] private TextMeshProUGUI roomStatusText;

        [Header("Players")]
        [SerializeField] private Transform playerListContentRoot;
        [SerializeField] private RoomPlayerEntryUI playerEntryPrefab;
        [SerializeField] private TextMeshProUGUI emptyPlayersText;

        [Header("Actions")]
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveRoomButton;
        [SerializeField] private TextMeshProUGUI readyButtonText;
        [SerializeField] private string mainMenuSceneName = "";

        private SteamLobbyManager _steamLobbyManager;
        private RoomStateManager _roomStateManager;
        private readonly List<RoomPlayerEntryUI> _spawnedEntries = new List<RoomPlayerEntryUI>();
        private bool _subscribedSteam;
        private bool _subscribedRoomPlayers;

        private void Awake()
        {
            if (readyButton != null)
                readyButton.onClick.AddListener(HandleReadyClicked);

            if (startGameButton != null)
                startGameButton.onClick.AddListener(HandleStartGameClicked);

            if (leaveRoomButton != null)
                leaveRoomButton.onClick.AddListener(HandleLeaveRoomClicked);
        }

        private void Start()
        {
            ResolveManagers();
            SubscribeSteam();
            RefreshRoomUi();
        }

        private void Update()
        {
            ResolveManagers();
            SubscribeSteam();
            SubscribeRoomPlayers();
            RefreshRoomUi();
        }

        private void OnDisable()
        {
            UnsubscribeSteam();
            UnsubscribeRoomPlayers();
        }

        private void OnDestroy()
        {
            if (readyButton != null)
                readyButton.onClick.RemoveListener(HandleReadyClicked);

            if (startGameButton != null)
                startGameButton.onClick.RemoveListener(HandleStartGameClicked);

            if (leaveRoomButton != null)
                leaveRoomButton.onClick.RemoveListener(HandleLeaveRoomClicked);

            UnsubscribeSteam();
            UnsubscribeRoomPlayers();
        }

        private void ResolveManagers()
        {
            if (_steamLobbyManager == null)
                _steamLobbyManager = SteamLobbyManager.Instance;

            if (_roomStateManager == null)
                _roomStateManager = RoomStateManager.Instance;
        }

        private void SubscribeSteam()
        {
            if (_subscribedSteam || _steamLobbyManager == null)
                return;

            _steamLobbyManager.OnLobbyJoined += HandleLobbyStateChanged;
            _steamLobbyManager.OnLobbyCreated += HandleLobbyStateChanged;
            _steamLobbyManager.OnLobbyLeft += HandleLobbyLeft;
            _steamLobbyManager.OnHostLeft += HandleHostLeft;
            _subscribedSteam = true;
        }

        private void UnsubscribeSteam()
        {
            if (!_subscribedSteam || _steamLobbyManager == null)
                return;

            _steamLobbyManager.OnLobbyJoined -= HandleLobbyStateChanged;
            _steamLobbyManager.OnLobbyCreated -= HandleLobbyStateChanged;
            _steamLobbyManager.OnLobbyLeft -= HandleLobbyLeft;
            _steamLobbyManager.OnHostLeft -= HandleHostLeft;
            _subscribedSteam = false;
        }

        private void SubscribeRoomPlayers()
        {
            if (_subscribedRoomPlayers || _roomStateManager == null)
                return;

            if (_roomStateManager.NetworkObject == null || !_roomStateManager.NetworkObject.IsSpawned)
                return;

            _roomStateManager.Players.OnChange += HandlePlayersChanged;
            _roomStateManager.OnPropertiesSelectorTransitionRequested += HandlePropertiesSelectorTransitionRequested;
            _subscribedRoomPlayers = true;
        }

        private void UnsubscribeRoomPlayers()
        {
            if (!_subscribedRoomPlayers || _roomStateManager == null)
                return;

            _roomStateManager.Players.OnChange -= HandlePlayersChanged;
            _roomStateManager.OnPropertiesSelectorTransitionRequested -= HandlePropertiesSelectorTransitionRequested;
            _subscribedRoomPlayers = false;
        }

        private void HandleLobbyStateChanged(Steamworks.Data.Lobby lobby)
        {
            RefreshRoomUi();
        }

        private void HandleLobbyLeft()
        {
            UnsubscribeRoomPlayers();
            ClearPlayerEntries();
            RefreshRoomUi();
        }

        private void HandleHostLeft()
        {
            if (roomStatusText != null)
                roomStatusText.text = "Host has left the room.";

            RefreshRoomUi();
        }

        private void HandlePlayersChanged(SyncListOperation op, int index, RoomPlayerState oldItem, RoomPlayerState newItem, bool asServer)
        {
            RefreshRoomUi();
        }

        private void HandlePropertiesSelectorTransitionRequested()
        {
            if (roomStatusText != null)
                roomStatusText.text = "Opening Properties Selector...";
        }

        private void HandleReadyClicked()
        {
            if (_roomStateManager == null)
                return;

            _roomStateManager.RequestToggleReady();
        }

        private void HandleStartGameClicked()
        {
            if (_roomStateManager == null)
                return;

            _roomStateManager.RequestStartGame();
        }

        private void HandleLeaveRoomClicked()
        {
            if (_steamLobbyManager != null)
                _steamLobbyManager.LeaveLobby();

            if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
                SceneManager.LoadScene(mainMenuSceneName);
        }

        private void RefreshRoomUi()
        {
            bool isInLobby = _steamLobbyManager != null && _steamLobbyManager.IsInLobby;
            if (roomRoot != null)
                roomRoot.SetActive(isInLobby);

            if (!isInLobby)
                return;

            RefreshHeader();
            RefreshPlayerList();
            RefreshActions();
        }

        private void RefreshHeader()
        {
            if (_steamLobbyManager != null && _steamLobbyManager.CurrentLobby.HasValue)
            {
                var lobby = _steamLobbyManager.CurrentLobby.Value;
                if (roomNameText != null)
                    roomNameText.text = lobby.GetData(SteamLobbyManager.KEY_LOBBY_NAME);

                if (roomIdText != null)
                    roomIdText.text = $"Room #{lobby.Id.Value}";
            }
            else
            {
                if (roomNameText != null)
                    roomNameText.text = string.Empty;

                if (roomIdText != null)
                    roomIdText.text = string.Empty;
            }

            if (roomStatusText != null)
            {
                if (_roomStateManager == null)
                    roomStatusText.text = "Waiting for room state manager...";
                else if (_roomStateManager.IsTransitioningToPropertiesSelector)
                    roomStatusText.text = "Opening Properties Selector...";
                else if (_steamLobbyManager != null && _steamLobbyManager.IsLobbyOwner)
                    roomStatusText.text = _roomStateManager.CanHostStartGame
                        ? "Host can start the game."
                        : "Waiting for players to get ready.";
                else
                    roomStatusText.text = "Waiting for host to start the game.";
            }
        }

        private void RefreshPlayerList()
        {
            ClearPlayerEntries();

            bool hasPlayers = _roomStateManager != null && _roomStateManager.Players.Count > 0;
            if (emptyPlayersText != null)
                emptyPlayersText.gameObject.SetActive(!hasPlayers);

            Transform contentRoot = ResolvePlayerListContentRoot();
            if (!hasPlayers || contentRoot == null || playerEntryPrefab == null)
                return;

            for (int i = 0; i < _roomStateManager.Players.Count; i++)
            {
                RoomPlayerEntryUI entry = Instantiate(playerEntryPrefab, contentRoot);
                entry.Bind(_roomStateManager.Players[i]);
                _spawnedEntries.Add(entry);
            }
        }

        private Transform ResolvePlayerListContentRoot()
        {
            if (playerListContentRoot == null)
                return null;

            ScrollRect scrollRect = playerListContentRoot.GetComponent<ScrollRect>();
            if (scrollRect != null && scrollRect.content != null)
                return scrollRect.content;

            return playerListContentRoot;
        }

        private void RefreshActions()
        {
            bool hasRoomState = _roomStateManager != null;
            bool isHost = _steamLobbyManager != null && _steamLobbyManager.IsLobbyOwner;

            if (readyButton != null)
                readyButton.gameObject.SetActive(hasRoomState && !isHost && !_roomStateManager.IsTransitioningToPropertiesSelector);

            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(hasRoomState && isHost && !_roomStateManager.IsTransitioningToPropertiesSelector);
                startGameButton.interactable = hasRoomState && isHost && _roomStateManager.CanHostStartGame;
            }

            if (readyButtonText != null)
            {
                if (hasRoomState && _roomStateManager.TryGetLocalPlayer(out RoomPlayerState localPlayer))
                    readyButtonText.text = localPlayer.IsReady ? "Cancel Ready" : "Ready";
                else
                    readyButtonText.text = "Ready";
            }
        }

        private void ClearPlayerEntries()
        {
            for (int i = 0; i < _spawnedEntries.Count; i++)
            {
                if (_spawnedEntries[i] != null)
                    Destroy(_spawnedEntries[i].gameObject);
            }

            _spawnedEntries.Clear();
        }
    }
}
