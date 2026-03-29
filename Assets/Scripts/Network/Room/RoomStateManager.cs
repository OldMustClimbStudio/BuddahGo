using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using SteamMultiplayer.Network.Results;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace SteamMultiplayer.Network
{
    public class RoomStateManager : NetworkBehaviour
    {
        private const int MaxPlayerNameLength = 32;

        /* 简介：此脚本用于管理房间内的玩家状态、准备状态、游戏开始流程等核心逻辑。
            作为一个网络对象，RoomStateManager在服务器上维护房间的核心数据，并通过同步变量和同步列表将状态更新传递给所有客户端。
            客户端可以通过调用RequestToggleReady和RequestStartGame等方法来与服务器交互，改变自己的准备状态或请求开始游戏。
            服务器会根据当前的玩家状态和设置来决定是否允许游戏开始，并在满足条件时加载属性选择场景和比赛场景。
            */
        public static RoomStateManager Instance { get; private set; }

        [Header("Properties Selector")]
        [SerializeField] private string _propertiesSelectorSceneName = "PropertySelection";
        [SerializeField] private bool _requireAllClientsReady = true;
        [SerializeField] private bool _enableDebugLogs = false;

        [Header("Race Flow")]
        [SerializeField] private string _raceSceneName = "RaceMap";
        [SerializeField] private string _resultSceneName = "RaceMapEndField";
        [SerializeField] private string _mainMenuSceneName = "MainMenu";
        [SerializeField] private int _pregameCountdownSeconds = 3;

        public readonly SyncList<RoomPlayerState> Players = new SyncList<RoomPlayerState>();

        private readonly SyncVar<bool> _transitioningToPropertiesSelector = new SyncVar<bool>();
        private readonly SyncVar<string> _playersSummaryText = new SyncVar<string>();
        private readonly SyncVar<bool> _waitingForRacePlayers = new SyncVar<bool>();
        private readonly SyncVar<bool> _raceCountdownActive = new SyncVar<bool>();
        private readonly SyncVar<int> _raceCountdownSecondsRemaining = new SyncVar<int>();
        private readonly SyncVar<bool> _raceStarted = new SyncVar<bool>();
        private readonly SyncVar<MatchSessionPhase> _matchSessionPhase = new SyncVar<MatchSessionPhase>();
        private float _nextRaceReadinessPollTime;
        private bool _returningToRoomMenu;

        public bool IsTransitioningToPropertiesSelector => _transitioningToPropertiesSelector.Value;
        public string PlayersSummaryText => _playersSummaryText.Value;
        public bool RequireAllClientsReady => _requireAllClientsReady;
        public bool CanHostStartGame => Players.Count > 0 && (!_requireAllClientsReady || AreAllRequiredPlayersReady());
        public bool IsWaitingForRacePlayers => _waitingForRacePlayers.Value;
        public bool IsRaceCountdownActive => _raceCountdownActive.Value;
        public int RaceCountdownSecondsRemaining => _raceCountdownSecondsRemaining.Value;
        public bool IsRaceStarted => _raceStarted.Value;
        public MatchSessionPhase CurrentMatchSessionPhase => _matchSessionPhase.Value;
        public bool IsRaceSceneLoadedLocally => !string.IsNullOrWhiteSpace(_raceSceneName) && UnitySceneManager.GetSceneByName(_raceSceneName).isLoaded;
        public bool ShouldBlockRaceGameplayInput => IsRaceSceneLoadedLocally && !_raceStarted.Value;

        public event Action OnPropertiesSelectorTransitionRequested;

        private void Update()
        {
            if (!IsServerInitialized || !ShouldMonitorRaceFlowServer())
                return;

            if (!_waitingForRacePlayers.Value && !_raceCountdownActive.Value)
                return;

            if (Time.unscaledTime < _nextRaceReadinessPollTime)
                return;

            _nextRaceReadinessPollTime = Time.unscaledTime + 0.25f;
            EvaluateRaceStartReadinessServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Instance = this;
            SubmitLocalDisplayName();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Instance = this;

            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.OnAuthenticationResult += HandleAuthenticationResult;
                InstanceFinder.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;

                foreach (KeyValuePair<int, NetworkConnection> kvp in InstanceFinder.ServerManager.Clients)
                {
                    NetworkConnection conn = kvp.Value;
                    if (conn != null && conn.IsAuthenticated)
                        AddOrUpdatePlayer(conn);
                }
            }

            if (InstanceFinder.SceneManager != null)
            {
                InstanceFinder.SceneManager.OnLoadEnd += HandleSceneLoadEnd;
                InstanceFinder.SceneManager.OnClientPresenceChangeEnd += HandleClientPresenceChangeEnd;
            }

            _transitioningToPropertiesSelector.Value = false;
            UpdatePlayersSummaryText();
            ResetRaceFlowStateServer();
            _matchSessionPhase.Value = MatchSessionPhase.InRoom;
            _returningToRoomMenu = false;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.OnAuthenticationResult -= HandleAuthenticationResult;
                InstanceFinder.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
            }

            if (InstanceFinder.SceneManager != null)
            {
                InstanceFinder.SceneManager.OnLoadEnd -= HandleSceneLoadEnd;
                InstanceFinder.SceneManager.OnClientPresenceChangeEnd -= HandleClientPresenceChangeEnd;
            }

            StopAllCoroutines();
            Players.Clear();
            _transitioningToPropertiesSelector.Value = false;
            _playersSummaryText.Value = string.Empty;
            _waitingForRacePlayers.Value = false;
            _raceCountdownActive.Value = false;
            _raceCountdownSecondsRemaining.Value = 0;
            _raceStarted.Value = false;
            _matchSessionPhase.Value = MatchSessionPhase.InRoom;
            _returningToRoomMenu = false;
        }

        public void RequestToggleReady()
        {
            if (!IsClientInitialized)
                return;

            ToggleReadyServerRpc();
        }

        public void RequestStartGame()
        {
            if (!IsClientInitialized)
                return;

            StartGameServerRpc();
        }

        public void RequestReturnToRoomMenuKeepingSession()
        {
            if (!IsClientInitialized)
                return;

            ReturnToRoomMenuKeepingSessionServerRpc();
        }

        public bool TryGetPlayer(int playerId, out RoomPlayerState player)
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

        public bool TryGetLocalPlayer(out RoomPlayerState player)
        {
            int localClientId = GetLocalClientId();
            if (localClientId >= 0)
                return TryGetPlayer(localClientId, out player);

            player = default;
            return false;
        }

        [ServerRpc(RequireOwnership = false)]
        private void ToggleReadyServerRpc(NetworkConnection caller = null)
        {
            if (caller == null || !caller.IsAuthenticated)
                return;

            int index = FindPlayerIndex(caller.ClientId);
            if (index < 0)
            {
                AddOrUpdatePlayer(caller);
                index = FindPlayerIndex(caller.ClientId);
            }

            if (index < 0)
                return;

            RoomPlayerState player = Players[index];
            if (player.IsHost)
                return;

            player.IsReady = !player.IsReady;
            Players[index] = player;
            UpdatePlayersSummaryText();
            LogDebug($"Ready toggled for {player.PlayerName}: {player.IsReady}");
        }

        [ServerRpc(RequireOwnership = false)]
        private void StartGameServerRpc(NetworkConnection caller = null)
        {
            if (caller == null || !caller.IsAuthenticated)
                return;

            int index = FindPlayerIndex(caller.ClientId);
            if (index < 0)
                return;

            RoomPlayerState callerState = Players[index];
            if (!callerState.IsHost)
            {
                LogDebug("Non-host attempted to start the game.");
                return;
            }

            if (!CanHostStartGame)
            {
                LogDebug("Host cannot start game yet.");
                return;
            }

            TransitionToPropertiesSelector();
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReturnToRoomMenuKeepingSessionServerRpc(NetworkConnection caller = null)
        {
            if (caller == null || !caller.IsAuthenticated)
                return;

            if (_matchSessionPhase.Value != MatchSessionPhase.InResult)
            {
                LogDebug($"Ignored room-return request from player {caller.ClientId} because phase={_matchSessionPhase.Value}");
                return;
            }

            ReturnToRoomMenuKeepingSessionServer();
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitDisplayNameServerRpc(string displayName, NetworkConnection caller = null)
        {
            if (caller == null || !caller.IsAuthenticated)
                return;

            string sanitizedName = SanitizePlayerName(displayName);
            if (string.IsNullOrWhiteSpace(sanitizedName))
                return;

            int index = FindPlayerIndex(caller.ClientId);
            if (index < 0)
            {
                AddOrUpdatePlayer(caller);
                index = FindPlayerIndex(caller.ClientId);
            }

            if (index < 0)
                return;

            RoomPlayerState player = Players[index];
            if (string.Equals(player.PlayerName, sanitizedName, StringComparison.Ordinal))
                return;

            player.PlayerName = sanitizedName;
            Players[index] = player;
            UpdatePlayersSummaryText();
        }

        private void HandleAuthenticationResult(NetworkConnection conn, bool authenticated)
        {
            if (!authenticated)
                return;

            AddOrUpdatePlayer(conn);
        }

        private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                AddOrUpdatePlayer(conn);
                if (ShouldMonitorRaceFlowServer())
                    EvaluateRaceStartReadinessServer();
            }
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                RemovePlayer(conn.ClientId);
                if (ShouldMonitorRaceFlowServer())
                    EvaluateRaceStartReadinessServer();
            }
        }

        private void HandleSceneLoadEnd(SceneLoadEndEventArgs args)
        {
            if (!IsServerInitialized)
                return;

            if (ContainsScene(args.LoadedScenes, _mainMenuSceneName))
            {
                CompleteReturnToRoomMenuServer();
                return;
            }

            if (ContainsScene(args.LoadedScenes, _propertiesSelectorSceneName))
                ValidatePropertiesSelectionManagerServer();

            if (ContainsScene(args.LoadedScenes, _resultSceneName))
            {
                BeginResultPhaseServer();
                return;
            }

            if (!ContainsRaceScene(args.LoadedScenes))
                return;

            BeginRacePreparationServer();
        }

        [Server]
        private void ValidatePropertiesSelectionManagerServer()
        {
            PropertiesSelectionManager manager = PropertiesSelectionManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[RoomStateManager] PropertySelection scene loaded but PropertiesSelectionManager.Instance is null on server.");
                return;
            }

            bool isSpawned = manager.NetworkObject != null && manager.NetworkObject.IsSpawned;
            if (!isSpawned)
            {
                Debug.LogWarning("[RoomStateManager] PropertiesSelectionManager exists but NetworkObject is not spawned on server.");
                return;
            }

            LogDebug($"PropertiesSelectionManager validated on server. spawned={isSpawned}, scene={manager.gameObject.scene.name}");
        }

        private void HandleClientPresenceChangeEnd(ClientPresenceChangeEventArgs args)
        {
            if (!IsServerInitialized)
                return;

            if (!string.Equals(args.Scene.name, _raceSceneName, StringComparison.Ordinal))
                return;

            EvaluateRaceStartReadinessServer();
        }

        [Server]
        private void AddOrUpdatePlayer(NetworkConnection conn)
        {
            if (conn == null)
                return;

            RoomPlayerState playerState = BuildRoomPlayerState(conn);
            int index = FindPlayerIndex(conn.ClientId);
            if (index < 0)
                Players.Add(playerState);
            else
                Players[index] = playerState;

            UpdatePlayersSummaryText();
            LogDebug($"Registered room player: {playerState.PlayerName} ({playerState.SteamId})");
        }

        [Server]
        private void RemovePlayer(int playerId)
        {
            int index = FindPlayerIndex(playerId);
            if (index < 0)
                return;

            Players.RemoveAt(index);
            UpdatePlayersSummaryText();
            LogDebug($"Removed room player {playerId}");
        }

        private RoomPlayerState BuildRoomPlayerState(NetworkConnection conn)
        {
            string steamId = GetSteamIdForConnection(conn);
            bool isHost = IsHostConnection(conn, steamId);
            bool wasReady = TryGetPlayer(conn.ClientId, out RoomPlayerState existing) && existing.IsReady;

            return new RoomPlayerState
            {
                PlayerId = conn.ClientId,
                SteamId = steamId,
                PlayerName = ResolvePlayerName(steamId, conn.ClientId),
                IsHost = isHost,
                IsReady = isHost ? true : wasReady
            };
        }

        private string GetSteamIdForConnection(NetworkConnection conn)
        {
            if (GameNetworkManager.Instance?.FishNetManager?.TransportManager?.Transport == null || conn == null)
                return string.Empty;

            string address = GameNetworkManager.Instance.FishNetManager.TransportManager.Transport.GetConnectionAddress(conn.ClientId);
            return string.IsNullOrWhiteSpace(address) ? string.Empty : address;
        }

        private bool IsHostConnection(NetworkConnection conn, string steamId)
        {
            if (!string.IsNullOrWhiteSpace(steamId)
                && SteamLobbyManager.Instance != null
                && SteamLobbyManager.Instance.CurrentLobby.HasValue)
            {
                string hostSteamId = SteamLobbyManager.Instance.CurrentLobby.Value.GetData(SteamLobbyManager.KEY_HOST_STEAM_ID);
                if (!string.IsNullOrWhiteSpace(hostSteamId))
                    return string.Equals(hostSteamId, steamId, StringComparison.Ordinal);
            }

            NetworkConnection localConnection = GameNetworkManager.Instance?.FishNetManager?.ClientManager?.Connection;
            return localConnection != null && localConnection.ClientId == conn.ClientId;
        }

        private string ResolvePlayerName(string steamId, int clientId)
        {
            if (!string.IsNullOrWhiteSpace(steamId)
                && SteamLobbyManager.Instance != null
                && SteamLobbyManager.Instance.CurrentLobby.HasValue)
            {
                Lobby lobby = SteamLobbyManager.Instance.CurrentLobby.Value;
                foreach (Friend member in lobby.Members)
                {
                    if (member.Id.Value.ToString() == steamId)
                        return member.Name;
                }
            }

            if (SteamClient.IsValid && SteamClient.SteamId.Value.ToString() == steamId)
                return SteamClient.Name;

            return $"Player {clientId}";
        }

        private void SubmitLocalDisplayName()
        {
            if (!IsClientInitialized || !SteamClient.IsValid)
                return;

            string localName = SanitizePlayerName(SteamClient.Name);
            if (string.IsNullOrWhiteSpace(localName))
                return;

            SubmitDisplayNameServerRpc(localName);
        }

        private static string SanitizePlayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            string trimmed = name.Trim();
            if (trimmed.Length > MaxPlayerNameLength)
                trimmed = trimmed.Substring(0, MaxPlayerNameLength);

            return trimmed;
        }

        private bool AreAllRequiredPlayersReady()
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

        private int FindPlayerIndex(int playerId)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].PlayerId == playerId)
                    return i;
            }

            return -1;
        }

        [Server]
        private void TransitionToPropertiesSelector()
        {
            if (_transitioningToPropertiesSelector.Value)
                return;

            ResetRaceFlowStateServer();
            _matchSessionPhase.Value = MatchSessionPhase.TransitioningToProperties;
            _transitioningToPropertiesSelector.Value = true;
            OnPropertiesSelectorTransitionRequested?.Invoke();
            NotifyPropertiesSelectorRequestedObserversRpc();

            if (!string.IsNullOrWhiteSpace(_propertiesSelectorSceneName) && InstanceFinder.SceneManager != null)
            {
                SceneLoadData sceneLoadData = new SceneLoadData(_propertiesSelectorSceneName)
                {
                    ReplaceScenes = ReplaceOption.All
                };

                InstanceFinder.SceneManager.LoadGlobalScenes(sceneLoadData);
                LogDebug($"Loading Properties Selector scene: {_propertiesSelectorSceneName}");
            }
            else
            {
                LogDebug("Properties Selector scene name is empty or SceneManager missing.");
            }
        }

        [Server]
        private void BeginRacePreparationServer()
        {
            StopAllCoroutines();
            _transitioningToPropertiesSelector.Value = false;
            _waitingForRacePlayers.Value = true;
            _raceCountdownActive.Value = false;
            _raceCountdownSecondsRemaining.Value = 0;
            _raceStarted.Value = false;
            _matchSessionPhase.Value = MatchSessionPhase.InMatch;
            EvaluateRaceStartReadinessServer();
            LogDebug("Race scene loaded. Waiting for all players and spawned characters before countdown.");
        }

        [Server]
        public void MarkTransitionToResultServer()
        {
            if (!IsServerInitialized)
                return;

            _matchSessionPhase.Value = MatchSessionPhase.TransitioningToResult;
        }

        [Server]
        private void BeginResultPhaseServer()
        {
            ResetRaceFlowStateServer();
            _matchSessionPhase.Value = MatchSessionPhase.InResult;
            _returningToRoomMenu = false;
            LogDebug("Result scene loaded. Waiting for host-authoritative return-to-room trigger.");
        }

        [Server]
        public void ReturnToRoomMenuKeepingSessionServer()
        {
            ReturnToRoomMenuKeepingSessionServer(_mainMenuSceneName);
        }

        [Server]
        public void ReturnToRoomMenuKeepingSessionServer(string targetMainMenuSceneName)
        {
            if (!IsServerInitialized)
                return;

            if (_returningToRoomMenu)
            {
                Debug.LogWarning("[RoomStateManager] ReturnToRoomMenuKeepingSessionServer ignored because room return is already in progress.");
                return;
            }

            _returningToRoomMenu = true;
            _matchSessionPhase.Value = MatchSessionPhase.ReturningToRoom;
            CleanupMatchSessionButKeepRoomServer();

            string sceneName = string.IsNullOrWhiteSpace(targetMainMenuSceneName) ? _mainMenuSceneName : targetMainMenuSceneName;
            if (string.IsNullOrWhiteSpace(sceneName) || InstanceFinder.SceneManager == null)
            {
                Debug.LogWarning("[RoomStateManager] Main menu scene name is empty or SceneManager is missing. Cannot return room to menu.");
                _returningToRoomMenu = false;
                _matchSessionPhase.Value = MatchSessionPhase.InRoom;
                return;
            }

            SceneLoadData sceneLoadData = new SceneLoadData(sceneName)
            {
                ReplaceScenes = ReplaceOption.All
            };

            InstanceFinder.SceneManager.LoadGlobalScenes(sceneLoadData);
            LogDebug($"Returning all players to main menu scene '{sceneName}' while keeping room session alive.");
        }

        [Server]
        public void StartNextGameFromResultServer(string targetPropertySelectionSceneName)
        {
            if (!IsServerInitialized)
                return;

            if (_returningToRoomMenu)
            {
                Debug.LogWarning("[RoomStateManager] StartNextGameFromResultServer ignored because a return-to-room flow is already in progress.");
                return;
            }

            string sceneName = string.IsNullOrWhiteSpace(targetPropertySelectionSceneName)
                ? _propertiesSelectorSceneName
                : targetPropertySelectionSceneName;

            if (string.IsNullOrWhiteSpace(sceneName) || InstanceFinder.SceneManager == null)
            {
                Debug.LogWarning("[RoomStateManager] Property selection scene name is empty or SceneManager is missing. Cannot start next game.");
                return;
            }

            CleanupMatchSessionButKeepRoomServer();
            _transitioningToPropertiesSelector.Value = true;
            _matchSessionPhase.Value = MatchSessionPhase.TransitioningToProperties;
            OnPropertiesSelectorTransitionRequested?.Invoke();
            NotifyPropertiesSelectorRequestedObserversRpc();

            SceneLoadData sceneLoadData = new SceneLoadData(sceneName)
            {
                ReplaceScenes = ReplaceOption.All
            };

            InstanceFinder.SceneManager.LoadGlobalScenes(sceneLoadData);
            LogDebug($"Starting next game from result scene by loading property selection scene '{sceneName}'.");
        }

        [Server]
        private void CleanupMatchSessionButKeepRoomServer()
        {
            MatchResultCache.Clear();
            ResolvedPropertySelectionCache.Clear();
            ResetRaceFlowStateServer();
            ResetPlayersReadyStateForRoomReturn();
        }

        [Server]
        private void CompleteReturnToRoomMenuServer()
        {
            ResetRaceFlowStateServer();
            _transitioningToPropertiesSelector.Value = false;
            _matchSessionPhase.Value = MatchSessionPhase.InRoom;
            _returningToRoomMenu = false;
            UpdatePlayersSummaryText();
            LogDebug("Main menu loaded while keeping the current room session alive.");
        }

        [Server]
        private void ResetRaceFlowStateServer()
        {
            StopRaceCountdownServer();
            _waitingForRacePlayers.Value = false;
            _raceStarted.Value = false;
        }

        [Server]
        private void ResetPlayersReadyStateForRoomReturn()
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

            UpdatePlayersSummaryText();
        }

        [Server]
        private void EvaluateRaceStartReadinessServer()
        {
            if (!ShouldMonitorRaceFlowServer())
                return;

            bool allPlayersReady = AreAllPlayersReadyForRaceServer();
            if (!allPlayersReady)
            {
                StopRaceCountdownServer();
                _waitingForRacePlayers.Value = true;
                return;
            }

            if (_raceStarted.Value || _raceCountdownActive.Value)
                return;

            // Only begin countdown after every authenticated connection owns a race-scene player avatar.
            LogDebug("[Race] Countdown start");
            StartCoroutine(RaceCountdownCoroutine());
        }

        [Server]
        private IEnumerator RaceCountdownCoroutine()
        {
            _waitingForRacePlayers.Value = false;
            _raceCountdownActive.Value = true;
            _raceCountdownSecondsRemaining.Value = Mathf.Max(1, _pregameCountdownSeconds);

            while (_raceCountdownSecondsRemaining.Value > 0)
            {
                if (!AreAllPlayersReadyForRaceServer())
                {
                    StopRaceCountdownServer();
                    _waitingForRacePlayers.Value = true;
                    yield break;
                }

                yield return new WaitForSeconds(1f);

                if (!_raceCountdownActive.Value)
                    yield break;

                _raceCountdownSecondsRemaining.Value--;
            }

            _raceCountdownActive.Value = false;
            _raceCountdownSecondsRemaining.Value = 0;
            _raceStarted.Value = true;
            _waitingForRacePlayers.Value = false;
            LogDebug("Race countdown completed. Gameplay unlocked.");
        }

        [Server]
        private void StopRaceCountdownServer()
        {
            _raceCountdownActive.Value = false;
            _raceCountdownSecondsRemaining.Value = 0;
        }

        [Server]
        private bool ShouldMonitorRaceFlowServer()
        {
            if (!IsServerInitialized || string.IsNullOrWhiteSpace(_raceSceneName))
                return false;

            return UnitySceneManager.GetSceneByName(_raceSceneName).isLoaded;
        }

        [Server]
        private bool AreAllPlayersReadyForRaceServer()
        {
            if (!ShouldMonitorRaceFlowServer())
                return false;

            if (Players.Count == 0 || InstanceFinder.ServerManager == null || InstanceFinder.SceneManager == null)
                return false;

            UnityEngine.SceneManagement.Scene raceScene = UnitySceneManager.GetSceneByName(_raceSceneName);
            if (!raceScene.isLoaded)
                return false;

            if (!InstanceFinder.SceneManager.SceneConnections.TryGetValue(raceScene, out HashSet<NetworkConnection> sceneConnections))
                return false;

            for (int i = 0; i < Players.Count; i++)
            {
                RoomPlayerState playerState = Players[i];
                if (!InstanceFinder.ServerManager.Clients.TryGetValue(playerState.PlayerId, out NetworkConnection conn) || conn == null || !conn.IsAuthenticated)
                    return false;

                if (!sceneConnections.Contains(conn))
                    return false;

                if (!HasOwnedRacePlayer(conn))
                    return false;
            }

            return true;
        }

        [Server]
        private bool HasOwnedRacePlayer(NetworkConnection conn)
        {
            if (conn == null || conn.Objects == null)
                return false;

            bool isReady = false;

            foreach (NetworkObject networkObject in conn.Objects)
            {
                if (networkObject == null || !networkObject.IsSpawned)
                    continue;

                if (!string.Equals(networkObject.gameObject.scene.name, _raceSceneName, StringComparison.Ordinal))
                    continue;

                // Tight readiness gate: only treat real player-avatar objects as race ready.
                bool isPlayerAvatar =
                    networkObject.GetComponent<BuddahMovement>() != null
                    || networkObject.GetComponent<PlayerProgressReporter>() != null
                    || networkObject.GetComponent<SkillExecutor>() != null;

                if (!isPlayerAvatar)
                    continue;

                isReady = true;
                break;
            }

            LogDebug($"[RaceReady] Conn {conn.ClientId} ready={isReady}");
            return isReady;
        }

        private bool ContainsRaceScene(UnityEngine.SceneManagement.Scene[] loadedScenes)
        {
            return ContainsScene(loadedScenes, _raceSceneName);
        }

        private static bool ContainsScene(UnityEngine.SceneManagement.Scene[] loadedScenes, string sceneName)
        {
            if (loadedScenes == null || string.IsNullOrWhiteSpace(sceneName))
                return false;

            for (int i = 0; i < loadedScenes.Length; i++)
            {
                if (string.Equals(loadedScenes[i].name, sceneName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        [ObserversRpc]
        private void NotifyPropertiesSelectorRequestedObserversRpc()
        {
            OnPropertiesSelectorTransitionRequested?.Invoke();
        }

        [Server]
        private void UpdatePlayersSummaryText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Players");

            if (Players.Count == 0)
            {
                sb.AppendLine("(none)");
                _playersSummaryText.Value = sb.ToString();
                return;
            }

            for (int i = 0; i < Players.Count; i++)
            {
                RoomPlayerState player = Players[i];
                string roleLabel = player.IsHost ? "Host" : "Player";
                string readyLabel = player.IsHost ? "Leader" : (player.IsReady ? "Ready" : "Waiting");
                sb.AppendLine($"{player.PlayerName} [{roleLabel}] [{readyLabel}]");
            }

            _playersSummaryText.Value = sb.ToString();
        }

        private int GetLocalClientId()
        {
            if (GameNetworkManager.Instance?.FishNetManager?.ClientManager?.Connection == null)
                return -1;

            return GameNetworkManager.Instance.FishNetManager.ClientManager.Connection.ClientId;
        }

        private void LogDebug(string message)
        {
            // Require both local inspector intent and global verbose switch.
            if (_enableDebugLogs && NetDebug.EnableVerboseLog)
                Debug.Log($"[RoomStateManager] {message}");
        }
    }

    public enum MatchSessionPhase
    {
        InRoom,
        TransitioningToProperties,
        InMatch,
        TransitioningToResult,
        InResult,
        ReturningToRoom
    }
}
