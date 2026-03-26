using System.Collections.Generic;
using SteamMultiplayer.Network;
using UnityEngine;

namespace SteamMultiplayer.Testing
{
    /// <summary>
    /// Minimal top-left debug overlay for Steam lobby flow and FishNet connection state.
    /// Attach to any GameObject in a test scene.
    /// </summary>
    public class LobbyFlowDebugOverlay : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private bool _visible = true;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F9;
        [SerializeField] private Vector2 _position = new Vector2(10f, 10f);
        [SerializeField] private Vector2 _size = new Vector2(360f, 180f);
        [SerializeField] private int _fontSize = 14;

        [Header("Status")]
        [SerializeField] private int _maxLogLines = 6;

        private SteamLobbyManager _lobbyManager;
        private GameNetworkManager _networkManager;
        private readonly Queue<string> _recentMessages = new Queue<string>();
        private bool _subscribedLobby;
        private bool _subscribedNetwork;

        private void Start()
        {
            ResolveManagers();
            Subscribe();
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
                _visible = !_visible;

            if (_lobbyManager == null || _networkManager == null)
            {
                ResolveManagers();
                Subscribe();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void OnGUI()
        {
            if (!_visible)
                return;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = _fontSize,
                richText = true
            };
            boxStyle.normal.textColor = Color.white;

            GUI.Box(new Rect(_position.x, _position.y, _size.x, _size.y), BuildOverlayText(), boxStyle);
        }

        private void ResolveManagers()
        {
            if (_lobbyManager == null)
                _lobbyManager = SteamLobbyManager.Instance;

            if (_networkManager == null)
                _networkManager = GameNetworkManager.Instance;
        }

        private void Subscribe()
        {
            if (!_subscribedLobby && _lobbyManager != null)
            {
                _lobbyManager.OnFlowStateChanged += HandleFlowStateChanged;
                _lobbyManager.OnHostLeft += HandleHostLeft;
                _lobbyManager.OnLobbyCreateFailed += HandleCreateFailed;
                _lobbyManager.OnLobbyJoinFailed += HandleJoinFailed;
                _subscribedLobby = true;
            }

            if (!_subscribedNetwork && _networkManager != null)
            {
                _networkManager.OnClientStateChanged += HandleClientStateChanged;
                _networkManager.OnServerStateChanged += HandleServerStateChanged;
                _subscribedNetwork = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribedLobby && _lobbyManager != null)
            {
                _lobbyManager.OnFlowStateChanged -= HandleFlowStateChanged;
                _lobbyManager.OnHostLeft -= HandleHostLeft;
                _lobbyManager.OnLobbyCreateFailed -= HandleCreateFailed;
                _lobbyManager.OnLobbyJoinFailed -= HandleJoinFailed;
            }

            if (_subscribedNetwork && _networkManager != null)
            {
                _networkManager.OnClientStateChanged -= HandleClientStateChanged;
                _networkManager.OnServerStateChanged -= HandleServerStateChanged;
            }

            _subscribedLobby = false;
            _subscribedNetwork = false;
        }

        private void HandleFlowStateChanged(LobbyFlowState state, string message)
        {
            AddLog($"Flow: {state} | {message}");
        }

        private void HandleHostLeft()
        {
            AddLog("Host left the room.");
        }

        private void HandleCreateFailed(string reason)
        {
            AddLog($"Create failed: {reason}");
        }

        private void HandleJoinFailed(string reason)
        {
            AddLog($"Join failed: {reason}");
        }

        private void HandleClientStateChanged(FishNet.Transporting.LocalConnectionState state)
        {
            AddLog($"Client: {state}");
        }

        private void HandleServerStateChanged(FishNet.Transporting.LocalConnectionState state)
        {
            AddLog($"Server: {state}");
        }

        private void AddLog(string message)
        {
            _recentMessages.Enqueue(message);
            while (_recentMessages.Count > Mathf.Max(1, _maxLogLines))
                _recentMessages.Dequeue();
        }

        private string BuildOverlayText()
        {
            if (_lobbyManager == null)
                return "Lobby Flow Debug\nSteamLobbyManager not found.";

            string roomName = _lobbyManager.IsInLobby ? _lobbyManager.CurrentLobbyName : "-";
            string roomId = _lobbyManager.IsInLobby && _lobbyManager.CurrentLobby.HasValue
                ? _lobbyManager.CurrentLobby.Value.Id.Value.ToString()
                : "-";
            string memberInfo = _lobbyManager.IsInLobby && _lobbyManager.CurrentLobby.HasValue
                ? $"{_lobbyManager.CurrentLobby.Value.MemberCount}/{GetMaxPlayers(_lobbyManager.CurrentLobby.Value)}"
                : $"-/{_lobbyManager.PendingMaxPlayers}";
            string networkRole = _networkManager == null
                ? "No GameNetworkManager"
                : _networkManager.IsHost ? "Host"
                : _networkManager.IsServer ? "Server"
                : _networkManager.IsClient ? "Client"
                : "Offline";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>Lobby Flow Debug</b>");
            sb.AppendLine($"Flow: {_lobbyManager.CurrentFlowState}");
            sb.AppendLine($"Status: {_lobbyManager.CurrentFlowMessage}");
            sb.AppendLine($"Role: {networkRole}");
            sb.AppendLine($"In Lobby: {_lobbyManager.IsInLobby}");
            sb.AppendLine($"Room: {roomName}");
            sb.AppendLine($"Lobby ID: {roomId}");
            sb.AppendLine($"Members: {memberInfo}");
            sb.AppendLine("Recent:");

            foreach (string message in _recentMessages)
                sb.AppendLine($"- {message}");

            return sb.ToString();
        }

        private int GetMaxPlayers(Steamworks.Data.Lobby lobby)
        {
            string metadata = lobby.GetData(SteamLobbyManager.KEY_MAX_PLAYERS);
            if (int.TryParse(metadata, out int parsed))
                return parsed;

            return lobby.MaxMembers;
        }
    }
}
