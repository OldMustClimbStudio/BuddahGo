using System;
using FishNet;
using FishNet.Connection;
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

        [Header("Network Prefabs")]
        [Tooltip("RoomStateManager network prefab to spawn automatically when the host/server starts.")]
        [SerializeField] private NetworkObject _roomStateManagerPrefab;

        [Tooltip("Legacy LobbyManager network prefab. Optional fallback while migrating.")]
        [SerializeField] private NetworkObject _legacyLobbyManagerPrefab;

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
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();

            if (Instance == this)
                Instance = null;
        }

        // ───────── Public API ─────────

        /// <summary>
        /// Start as Host (server + local client).
        /// Call this after creating / configuring a Steam lobby.
        /// </summary>
        public void StartHost()
        {
            if (_networkManager == null)
            {
                NetLog.Error("Cannot StartHost – NetworkManager reference is missing.");
                return;
            }

            NetLog.Info("Starting Host (Server + Client)...");

            // Start server first, then client
            bool serverOk = _networkManager.ServerManager.StartConnection();
            if (!serverOk)
            {
                NetLog.Error("ServerManager.StartConnection() returned false.");
                return;
            }

            bool clientOk = _networkManager.ClientManager.StartConnection();
            if (!clientOk)
            {
                NetLog.Error("[Host] ClientManager.StartConnection() failed after server started. Rolling back host startup.");

                // Roll back to a clean stopped state to avoid half-started host instances.
                if (_networkManager.ClientManager != null
                    && _networkManager.ClientManager.Started)
                {
                    _networkManager.ClientManager.StopConnection();
                }

                if (_networkManager.ServerManager != null
                    && _networkManager.ServerManager.Started)
                {
                    _networkManager.ServerManager.StopConnection(true);
                }

                NetLog.Warn("[Host] Host startup rolled back due to client start failure.");
                return;
            }

            NetLog.Info("[Host] Host started successfully.");
        }

        /// <summary>
        /// Start as Client and connect to a remote host.
        /// <paramref name="hostAddress"/> is typically the host's SteamId as a string
        /// when using FishyFacepunch transport.
        /// </summary>
        public void StartClient(string hostAddress)
        {
            if (_networkManager == null)
            {
                NetLog.Error("Cannot StartClient – NetworkManager reference is missing.");
                return;
            }

            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                NetLog.Error("Cannot StartClient – hostAddress is null or empty.");
                return;
            }

            NetLog.Info($"Starting Client → connecting to {hostAddress}...");

            // FishyFacepunch uses SetClientAddress to set the target SteamId
            _networkManager.TransportManager.Transport.SetClientAddress(hostAddress);
            _networkManager.ClientManager.StartConnection();
        }

        /// <summary>
        /// Gracefully stop all connections (both server and client).
        /// Safe to call regardless of current role.
        /// </summary>
        public void StopConnection()
        {
            if (_networkManager == null) return;

            NetLog.Info("Stopping all connections...");

            // Stop client first so host-client gets cleaned up before server shuts down
            if (_networkManager.ClientManager != null
                && _networkManager.ClientManager.Started)
            {
                _networkManager.ClientManager.StopConnection();
            }

            if (_networkManager.ServerManager != null
                && _networkManager.ServerManager.Started)
            {
                _networkManager.ServerManager.StopConnection(true);
            }
        }

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
                EnsureLegacyLobbyManagerSpawned();
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
            _networkManager.ServerManager.Spawn(roomStateManagerInstance);
            NetLog.Info($"Spawned RoomStateManager network object: {roomStateManagerInstance.gameObject.name}");
        }

        private void EnsureLegacyLobbyManagerSpawned()
        {
            if (_networkManager == null || _networkManager.ServerManager == null || !_networkManager.ServerManager.Started)
                return;

            if (_legacyLobbyManagerPrefab == null)
                return;

            if (LobbyManager.Instance != null && LobbyManager.Instance.NetworkObject != null && LobbyManager.Instance.NetworkObject.IsSpawned)
                return;

            NetworkObject lobbyManagerInstance = Instantiate(_legacyLobbyManagerPrefab);
            _networkManager.ServerManager.Spawn(lobbyManagerInstance);
            NetLog.Info($"Spawned legacy LobbyManager network object: {lobbyManagerInstance.gameObject.name}");
        }
    }
}
