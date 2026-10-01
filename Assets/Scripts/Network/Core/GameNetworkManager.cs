using BuddahGo.Match;
using System;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Debugging;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace SteamMultiplayer.Network
{
    /// <summary>
    /// Lightweight wrapper around Fish-Networking's NetworkManager.
    /// Responsibilities:
    ///   - Singleton access
    ///   - StartHost / StartClient / StopConnection convenience API
    ///   - Connection-state event forwarding with structured logging
    ///
    /// This script does NOT handle:
    ///   - Steam Lobby creation/joining (see future SteamLobbyManager)
    ///   - Scene flow logic (see future GameFlowManager)
    ///   - Player spawning (see future PlayerSpawnManager)
    /// </summary>
    public class GameNetworkManager : MonoBehaviour
    {
        // ───────── Singleton ─────────
        public static GameNetworkManager Instance { get; private set; }

        // ───────── Inspector References ─────────
        [Header("References (auto-resolved if left empty)")]
        [Tooltip("Fish-Net NetworkManager on this GameObject or in the scene.")]
        [SerializeField] private NetworkManager _networkManager;
        private SessionLauncher _sessionLauncher;

        [Header("Network Prefabs")]
        [Tooltip("RoomStateManager network prefab to spawn automatically when the host/server starts.")]
        [SerializeField] private NetworkObject _roomStateManagerPrefab;

        // ───────── Public Read-Only Accessors ─────────
        /// <summary>Current Fish-Net NetworkManager.</summary>
        public NetworkManager FishNetManager => _networkManager;

        /// <summary>True when this instance is acting as both server and client (Host).</summary>
        public bool IsHost => IsServer && IsClient;

        /// <summary>True when the local server is started.</summary>
        public bool IsServer => _networkManager != null
            && _networkManager.ServerManager != null
            && _networkManager.ServerManager.Started;

        /// <summary>True when the local client is connected.</summary>
        public bool IsClient => _networkManager != null
            && _networkManager.ClientManager != null
            && _networkManager.ClientManager.Started;

        // ───────── Events (subscribe from UI or other managers) ─────────
        /// <summary>Fired when the local server connection state changes.</summary>
        public event Action<LocalConnectionState> OnServerStateChanged;

        /// <summary>Fired when the local client connection state changes.</summary>
        public event Action<LocalConnectionState> OnClientStateChanged;

        /// <summary>Fired when a remote client connects or disconnects (server-side only).</summary>
        public event Action<NetworkConnection, RemoteConnectionState> OnRemoteClientStateChanged;

        // ───────── Unity Lifecycle ─────────
        private void Awake()
        {
            // Singleton enforcement
            if (Instance != null && Instance != this)
            {
                NetLog.Warn("Duplicate GameNetworkManager detected – destroying this instance.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            ResolveNetworkManager();
            SubscribeEvents();
            _sessionLauncher = new SessionLauncher(_networkManager);
            SessionControl.Current = _sessionLauncher;
        }

        private void Update() => _sessionLauncher?.Pump();

        private void OnDestroy()
        {
            UnsubscribeEvents();

            if (Instance == this)
            {
                Instance = null;
                SessionControl.Current = null;
                MatchRules.Reset();
                LocalInputBlock.Current = null;
                MatchServices.Reset();
            }
        }

        // ───────── Public API ─────────

        /// <summary>
        /// Start as Host (server + local client).
        /// Call this after creating / configuring a Steam lobby.
        /// </summary>
        public void StartHost() => SessionControl.Current?.StartOnlineHost();

        public void StartClient(string hostAddress) => SessionControl.Current?.StartOnlineClient(hostAddress);

        public void StopConnection() => SessionControl.Current?.RequestStopSession();

        // ───────── Internal Helpers ─────────

        /// <summary>
        /// Resolve the Fish-Net NetworkManager if not assigned in Inspector.
        /// </summary>
        private void ResolveNetworkManager()
        {
            if (_networkManager == null)
                _networkManager = GetComponent<NetworkManager>();

            if (_networkManager == null)
                _networkManager = InstanceFinder.NetworkManager;

            if (_networkManager == null)
            {
                NetLog.Error("No Fish-Net NetworkManager found! "
                    + "Attach one to this GameObject or ensure one exists in the scene.");
            }
            else
            {
                NetLog.Dev($"NetworkManager resolved: {_networkManager.gameObject.name}");
                ConfigureFishNetDiagnostics();
            }
        }

        private void ConfigureFishNetDiagnostics()
        {
            if (_networkManager == null)
                return;

            DebugManager debugManager = _networkManager.DebugManager;
            if (debugManager == null)
                return;

            if (!debugManager.WriteSceneObjectDetails)
            {
                debugManager.WriteSceneObjectDetails = true;
                GameLog.Verbose("[NetworkDiag] Enabled FishNet DebugManager.WriteSceneObjectDetails for scene object diagnostics.");
            }
        }

        private void SubscribeEvents()
        {
            if (_networkManager == null) return;

            _networkManager.ServerManager.OnServerConnectionState += HandleServerConnectionState;
            _networkManager.ClientManager.OnClientConnectionState += HandleClientConnectionState;
            _networkManager.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
        }

        private void UnsubscribeEvents()
        {
            if (_networkManager == null) return;

            if (_networkManager.ServerManager != null)
            {
                _networkManager.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
                _networkManager.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
            }
            if (_networkManager.ClientManager != null)
            {
                _networkManager.ClientManager.OnClientConnectionState -= HandleClientConnectionState;
            }
        }

        // ───────── Event Handlers ─────────

        private void HandleServerConnectionState(ServerConnectionStateArgs args)
        {
            LocalConnectionState state = args.ConnectionState;
            NetLog.Info($"[Server] Connection state → {state}");

            if (state == LocalConnectionState.Started)
            {
                EnsureRoomStateManagerSpawned();
            }

            OnServerStateChanged?.Invoke(state);
        }

        private void HandleClientConnectionState(ClientConnectionStateArgs args)
        {
            LocalConnectionState state = args.ConnectionState;
            NetLog.Info($"[Client] Connection state → {state}");
            OnClientStateChanged?.Invoke(state);
        }

        private void HandleRemoteConnectionState(
            FishNet.Connection.NetworkConnection conn,
            RemoteConnectionStateArgs args)
        {
            RemoteConnectionState state = args.ConnectionState;
            NetLog.Info($"[Server] Remote client {conn.ClientId} → {state}");
            OnRemoteClientStateChanged?.Invoke(conn, state);
        }

        private void EnsureRoomStateManagerSpawned()
        {
            if (_networkManager == null || _networkManager.ServerManager == null || !_networkManager.ServerManager.Started)
                return;

            if (RoomStateManager.Instance != null
                && RoomStateManager.Instance.NetworkObject != null
                && RoomStateManager.Instance.NetworkObject.IsSpawned)
                return;

            if (_roomStateManagerPrefab == null)
            {
                NetLog.Warn("RoomStateManager prefab is not assigned on GameNetworkManager.");
                return;
            }

            NetworkObject roomStateManagerInstance = Instantiate(_roomStateManagerPrefab);
            DontDestroyOnLoad(roomStateManagerInstance.gameObject);
            _networkManager.ServerManager.Spawn(roomStateManagerInstance);
            NetLog.Info($"Spawned RoomStateManager network object: {roomStateManagerInstance.gameObject.name}");
        }

    }
}
