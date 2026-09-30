using System;
using System.Collections;
using System.Collections.Generic;
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
using Scene = UnityEngine.SceneManagement.Scene;
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
        [SerializeField] private string _propertiesSelectorSceneName = SceneNames.PropertySelection;
        [SerializeField] private bool _requireAllClientsReady = true;
        [SerializeField] private bool _enableDebugLogs = false;

        [Header("Race Flow")]
        [SerializeField] private string _raceSceneName = SceneNames.RaceMap;
        [SerializeField] private string _resultSceneName = "RaceMapEndField";
        [SerializeField] private string _mainMenuSceneName = SceneNames.MainMenu;
        [SerializeField] private int _pregameCountdownSeconds = 15;

        public readonly SyncList<RoomPlayerState> Players = new SyncList<RoomPlayerState>();

        private readonly SyncVar<bool> _transitioningToPropertiesSelector = new SyncVar<bool>();
        private readonly SyncVar<string> _playersSummaryText = new SyncVar<string>();
        private readonly SyncVar<bool> _waitingForRacePlayers = new SyncVar<bool>();
        private readonly SyncVar<bool> _raceCountdownActive = new SyncVar<bool>();
        private readonly SyncVar<int> _raceCountdownSecondsRemaining = new SyncVar<int>();
        private readonly SyncVar<bool> _pregameCountdownCompleted = new SyncVar<bool>();
        private readonly SyncVar<bool> _raceStarted = new SyncVar<bool>();
        private readonly SyncVar<bool> _gameplayMovementUnlocked = new SyncVar<bool>();
        private readonly SyncVar<bool> _authoritativeGoIssued = new SyncVar<bool>();
        private readonly SyncVar<MatchSessionPhase> _matchSessionPhase = new SyncVar<MatchSessionPhase>();
        private float _nextRaceReadinessPollTime;
        private bool _returningToRoomMenu;
        private Coroutine _clientRaceSceneDiagnosticsRoutine;
        private Coroutine _clientRaceSceneReadyRoutine;
        private readonly RaceStartHandshake _raceStartHandshake = new RaceStartHandshake();
        private RoomRoster _roomRoster;
        private RoomRoster Roster => _roomRoster ??= new RoomRoster(Players);

        private bool _reportedRaceSceneManagersReadyLocal;
        private int _reportedIntroAssignmentSequenceId = -1;
        private int _reportedIntroVisualSequenceId = -1;
        private int _reportedGameplayLiveSequenceId = -1;
        private float _localRaceSceneReadyStableSince = -1f;
        private bool _lastLoggedAuthoritativeGoIssued;
        private bool _lastLoggedGameplayMovementUnlocked;

        internal string DiagnosticRaceSceneName => _raceSceneName;
        internal bool DiagnosticManagersReported => _reportedRaceSceneManagersReadyLocal;
        internal int DiagnosticAssignmentSequence => _reportedIntroAssignmentSequenceId;
        internal int DiagnosticVisualSequence => _reportedIntroVisualSequenceId;
        internal int DiagnosticGameplaySequence => _reportedGameplayLiveSequenceId;

        public bool IsTransitioningToPropertiesSelector => _transitioningToPropertiesSelector.Value;
        public string PlayersSummaryText => _playersSummaryText.Value;
        public bool RequireAllClientsReady => _requireAllClientsReady;
        public bool CanHostStartGame => Players.Count > 0 && (!_requireAllClientsReady || AreAllRequiredPlayersReady());
        public bool IsWaitingForRacePlayers => _waitingForRacePlayers.Value;
        public bool IsRaceCountdownActive => _raceCountdownActive.Value;
        public int RaceCountdownSecondsRemaining => _raceCountdownSecondsRemaining.Value;
        public bool IsPregameCountdownCompleted => _pregameCountdownCompleted.Value;
        public bool IsRaceStarted => _raceStarted.Value;
        public bool IsGameplayMovementUnlocked => _gameplayMovementUnlocked.Value;
        public bool IsAuthoritativeGoIssued => _authoritativeGoIssued.Value;
        public MatchSessionPhase CurrentMatchSessionPhase => _matchSessionPhase.Value;
        public bool IsRaceSceneLoadedLocally => !string.IsNullOrWhiteSpace(_raceSceneName) && UnitySceneManager.GetSceneByName(_raceSceneName).isLoaded;
        public bool IsMatchPhaseActive => _matchSessionPhase.Value == MatchSessionPhase.InMatch;
        public bool IsResultPhaseActive => _matchSessionPhase.Value == MatchSessionPhase.InResult;

        public bool ShouldEnableOwnerMovementInputNow()
        {
            if (IsResultPhaseActive)
                return true;

            if (IsMatchPhaseActive)
                return _gameplayMovementUnlocked.Value;

            // When still in race scene but not in match/result phase, keep gameplay input closed.
            if (IsRaceSceneLoadedLocally)
                return false;

            return true;
        }

        public string GetOwnerMovementInputGateReason()
        {
            if (IsResultPhaseActive)
                return "result-phase";

            if (IsMatchPhaseActive)
                return _gameplayMovementUnlocked.Value
                    ? "match-gameplay-unlocked"
                    : "match-waiting-gameplay-live";

            if (IsRaceSceneLoadedLocally)
                return $"race-scene-transition-{_matchSessionPhase.Value}";

            return $"non-race-phase-{_matchSessionPhase.Value}";
        }

        public event Action OnPropertiesSelectorTransitionRequested;

        private void Update()
        {
            if (_lastLoggedAuthoritativeGoIssued != _authoritativeGoIssued.Value)
            {
                _lastLoggedAuthoritativeGoIssued = _authoritativeGoIssued.Value;
                GameLog.Verbose($"[IntroGo][{(IsServerInitialized ? "Server" : "Client")}] AuthoritativeGoIssued changed -> {_authoritativeGoIssued.Value} phase={_matchSessionPhase.Value}");
            }

            if (_lastLoggedGameplayMovementUnlocked != _gameplayMovementUnlocked.Value)
            {
                _lastLoggedGameplayMovementUnlocked = _gameplayMovementUnlocked.Value;
                GameLog.Verbose($"[GameplayUnlock][{(IsServerInitialized ? "Server" : "Client")}] MovementUnlocked changed -> {_gameplayMovementUnlocked.Value} phase={_matchSessionPhase.Value}");
            }

            if (!IsServerInitialized || _matchSessionPhase.Value != MatchSessionPhase.InMatch || !ShouldMonitorRaceFlowServer())
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

            // Authentication can register the host before its local ClientId is assigned.
            // Refresh once the local client is initialized so host authority is recorded.
            if (IsServerInitialized && ClientManager.Connection.IsValid)
                AddOrUpdatePlayer(ClientManager.Connection);

            SubmitLocalDisplayName();

            if (InstanceFinder.SceneManager != null)
                InstanceFinder.SceneManager.OnLoadEnd += HandleClientSceneLoadEnd;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (InstanceFinder.SceneManager != null)
                InstanceFinder.SceneManager.OnLoadEnd -= HandleClientSceneLoadEnd;

            if (_clientRaceSceneDiagnosticsRoutine != null)
            {
                StopCoroutine(_clientRaceSceneDiagnosticsRoutine);
                _clientRaceSceneDiagnosticsRoutine = null;
            }

            if (_clientRaceSceneReadyRoutine != null)
            {
                StopCoroutine(_clientRaceSceneReadyRoutine);
                _clientRaceSceneReadyRoutine = null;
            }

            _reportedRaceSceneManagersReadyLocal = false;
            _reportedIntroAssignmentSequenceId = -1;
            _reportedIntroVisualSequenceId = -1;
            _reportedGameplayLiveSequenceId = -1;
            _localRaceSceneReadyStableSince = -1f;

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
            _pregameCountdownCompleted.Value = false;
            _raceStarted.Value = false;
            _gameplayMovementUnlocked.Value = false;
            _authoritativeGoIssued.Value = false;
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

        public void ReportLocalIntroAssignmentApplied(int sequenceId)
        {
            if (TryReportLocalSequence(sequenceId, ref _reportedIntroAssignmentSequenceId))
                ReportIntroAssignmentAppliedServerRpc(sequenceId);
        }

        public void ReportLocalIntroVisualPrepared(int sequenceId)
        {
            if (TryReportLocalSequence(sequenceId, ref _reportedIntroVisualSequenceId))
                ReportIntroVisualPreparedServerRpc(sequenceId);
        }

        public void ReportLocalGameplayLive(int sequenceId)
        {
            if (TryReportLocalSequence(sequenceId, ref _reportedGameplayLiveSequenceId))
                ReportGameplayLiveServerRpc(sequenceId);
        }

        public bool AreAllClientsIntroVisualsReadyForSequenceServer(int sequenceId)
        {
            return AreAllClientsReadyForSequenceServer(sequenceId, _raceStartHandshake._introVisualReadySequenceByClientId);
        }

        public bool AreAllClientsGameplayLiveForSequenceServer(int sequenceId)
        {
            return AreAllClientsReadyForSequenceServer(sequenceId, _raceStartHandshake._gameplayLiveSequenceByClientId);
        }

        public bool TryGetPlayer(int playerId, out RoomPlayerState player)
        {
            return Roster.TryGetPlayer(playerId, out player);
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

            GameLog.Verbose($"[SceneDiag][Server] OnLoadEnd scenes={FormatSceneNames(args.LoadedScenes)} time={Time.unscaledTime:F3}");

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

        private void HandleClientSceneLoadEnd(SceneLoadEndEventArgs args)
        {
            GameLog.Verbose($"[SceneDiag][Client] OnLoadEnd scenes={FormatSceneNames(args.LoadedScenes)} time={Time.unscaledTime:F3} localClientId={GetLocalClientId()}");

        if (!ContainsRaceScene(args.LoadedScenes))
            return;

        _reportedRaceSceneManagersReadyLocal = false;
        _reportedIntroAssignmentSequenceId = -1;
        _reportedIntroVisualSequenceId = -1;
        _reportedGameplayLiveSequenceId = -1;
        _localRaceSceneReadyStableSince = -1f;

        if (_clientRaceSceneDiagnosticsRoutine != null)
            StopCoroutine(_clientRaceSceneDiagnosticsRoutine);

            _clientRaceSceneDiagnosticsRoutine = StartCoroutine(RunClientRaceSceneDiagnostics());

            if (_clientRaceSceneReadyRoutine != null)
                StopCoroutine(_clientRaceSceneReadyRoutine);

            _clientRaceSceneReadyRoutine = StartCoroutine(WaitForLocalRaceSceneManagersAndReportReady());
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

            if (_matchSessionPhase.Value != MatchSessionPhase.InMatch)
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
            Roster.Upsert(playerState, conn.ClientId);

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
            _raceStartHandshake.RemovePlayer(playerId);
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
            return PlayerIdentity.GetSteamIdForConnection(GameNetworkManager.Instance?.FishNetManager?.TransportManager?.Transport, conn);
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
            Lobby? lobby = !string.IsNullOrWhiteSpace(steamId) && SteamLobbyManager.Instance != null
                ? SteamLobbyManager.Instance.CurrentLobby
                : null;
            return PlayerIdentity.ResolvePlayerName(steamId, clientId, lobby);
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
            return RoomRoster.SanitizePlayerName(name, MaxPlayerNameLength);
        }

        private bool AreAllRequiredPlayersReady()
        {
            return Roster.AreAllRequiredPlayersReady();
        }

        private int FindPlayerIndex(int playerId)
        {
            return Roster.FindPlayerIndex(playerId);
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
            _pregameCountdownCompleted.Value = false;
            _raceStarted.Value = false;
            _gameplayMovementUnlocked.Value = false;
            _authoritativeGoIssued.Value = false;
            _matchSessionPhase.Value = MatchSessionPhase.InMatch;
            _raceStartHandshake.ResetAllReadiness();
            GameLog.Verbose($"[SceneDiag][Server] BeginRacePreparationServer scene='{_raceSceneName}' time={Time.unscaledTime:F3}");
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
        public void EnterResultPhaseServer()
        {
            if (!IsServerInitialized)
                return;

            BeginResultPhaseServer();
        }

        [Server]
        private void BeginResultPhaseServer()
        {
            ResetRaceFlowStateServer();
            _matchSessionPhase.Value = MatchSessionPhase.InResult;
            _returningToRoomMenu = false;
            LogDebug("Entered in-scene result phase. Waiting for host-authoritative return-to-room trigger.");
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
            ResolvedPropertySelectionCache.Clear();
            ResetRaceFlowStateServer();
            ResetPlayersReadyStateForRoomReturn();
            _raceStartHandshake.ResetAllReadiness();
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
            _pregameCountdownCompleted.Value = false;
            _raceStarted.Value = false;
            _gameplayMovementUnlocked.Value = false;
            _authoritativeGoIssued.Value = false;
            _raceStartHandshake.ResetSequenceReadiness();
        }

        [Server]
        private void ResetPlayersReadyStateForRoomReturn()
        {
            Roster.ResetReadyForRoomReturn();
            UpdatePlayersSummaryText();
        }

        [Server]
        private void EvaluateRaceStartReadinessServer()
        {
            if (_matchSessionPhase.Value != MatchSessionPhase.InMatch || !ShouldMonitorRaceFlowServer())
                return;

            bool allPlayersReady = AreAllPlayersReadyForRaceServer();
            if (_enableDebugLogs && NetDebug.EnableVerboseLog)
                LogSceneDiag($"[SceneDiag][Server] EvaluateRaceStartReadiness allPlayersReady={allPlayersReady} waiting={_waitingForRacePlayers.Value} countdown={_raceCountdownActive.Value} started={_raceStarted.Value} time={Time.unscaledTime:F3} details={BuildServerRaceReadinessSummary()}");
            if (!allPlayersReady)
            {
                StopRaceCountdownServer();
                _pregameCountdownCompleted.Value = false;
                _waitingForRacePlayers.Value = true;
                return;
            }

            if (_pregameCountdownCompleted.Value || _authoritativeGoIssued.Value || _raceStarted.Value || _raceCountdownActive.Value)
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
            _pregameCountdownCompleted.Value = false;

            while (_raceCountdownSecondsRemaining.Value > 0)
            {
                if (!AreAllPlayersReadyForRaceServer())
                {
                    StopRaceCountdownServer();
                    _pregameCountdownCompleted.Value = false;
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
            _waitingForRacePlayers.Value = false;
            _pregameCountdownCompleted.Value = true;
            _raceStarted.Value = false;
            _gameplayMovementUnlocked.Value = false;
            GameLog.Verbose($"[IntroGo][Server] Pregame countdown completed. Waiting for synchronized intro go. details={BuildServerRaceReadinessSummary()}");
        }

        [Server]
        private void StopRaceCountdownServer()
        {
            _raceCountdownActive.Value = false;
            _raceCountdownSecondsRemaining.Value = 0;
        }

        [Server]
        public void MarkAuthoritativeGoIssuedServer()
        {
            if (!IsServerInitialized || _authoritativeGoIssued.Value)
                return;

            _authoritativeGoIssued.Value = true;
            _pregameCountdownCompleted.Value = false;
            _gameplayMovementUnlocked.Value = false;
            GameLog.Verbose($"[IntroGo][Server] Authoritative go state set by intro sequence. details={BuildServerRaceReadinessSummary()}");
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
            {
                if (_enableDebugLogs && NetDebug.EnableVerboseLog)
                    LogSceneDiag($"[SceneDiag][Server] Race readiness false: monitor disabled or race scene not loaded. details={BuildServerRaceReadinessSummary()}");
                return false;
            }

            if (Players.Count == 0 || InstanceFinder.ServerManager == null || InstanceFinder.SceneManager == null)
            {
                if (_enableDebugLogs && NetDebug.EnableVerboseLog)
                    LogSceneDiag($"[SceneDiag][Server] Race readiness false: Players={Players.Count}, ServerManager={(InstanceFinder.ServerManager != null)}, SceneManager={(InstanceFinder.SceneManager != null)} details={BuildServerRaceReadinessSummary()}");
                return false;
            }

            UnityEngine.SceneManagement.Scene raceScene = UnitySceneManager.GetSceneByName(_raceSceneName);
            if (!raceScene.isLoaded)
            {
                if (_enableDebugLogs && NetDebug.EnableVerboseLog)
                    LogSceneDiag($"[SceneDiag][Server] Race readiness false: scene '{_raceSceneName}' not loaded on server. details={BuildServerRaceReadinessSummary()}");
                return false;
            }

            if (!InstanceFinder.SceneManager.SceneConnections.TryGetValue(raceScene, out HashSet<NetworkConnection> sceneConnections))
            {
                if (_enableDebugLogs && NetDebug.EnableVerboseLog)
                    LogSceneDiag($"[SceneDiag][Server] Race readiness false: no SceneConnections entry for '{_raceSceneName}'. details={BuildServerRaceReadinessSummary()}");
                return false;
            }

            for (int i = 0; i < Players.Count; i++)
            {
                RoomPlayerState playerState = Players[i];
                if (!InstanceFinder.ServerManager.Clients.TryGetValue(playerState.PlayerId, out NetworkConnection conn) || conn == null || !conn.IsAuthenticated)
                {
                    if (_enableDebugLogs && NetDebug.EnableVerboseLog)
                        LogSceneDiag($"[SceneDiag][Server] Race readiness false: player {playerState.PlayerId} missing authenticated connection. details={BuildServerRaceReadinessSummary()}");
                    return false;
                }

                if (!sceneConnections.Contains(conn))
                {
                    if (_enableDebugLogs && NetDebug.EnableVerboseLog)
                        LogSceneDiag($"[SceneDiag][Server] Race readiness false: player {playerState.PlayerId} not present in race scene connections. details={BuildServerRaceReadinessSummary()}");
                    return false;
                }

                if (!HasOwnedRacePlayer(conn))
                {
                    if (_enableDebugLogs && NetDebug.EnableVerboseLog)
                        LogSceneDiag($"[SceneDiag][Server] Race readiness false: player {playerState.PlayerId} has no owned race player in '{_raceSceneName}'. details={BuildServerRaceReadinessSummary()}");
                    return false;
                }

                if (!_raceStartHandshake._raceSceneManagersReadyClientIds.Contains(playerState.PlayerId))
                {
                    if (_enableDebugLogs && NetDebug.EnableVerboseLog)
                        LogSceneDiag($"[SceneDiag][Server] Race readiness false: player {playerState.PlayerId} has not reported race scene managers ready. details={BuildServerRaceReadinessSummary()}");
                    return false;
                }
            }

            if (_enableDebugLogs && NetDebug.EnableVerboseLog)
                LogSceneDiag($"[SceneDiag][Server] Race readiness true for {Players.Count} players. details={BuildServerRaceReadinessSummary()}");
            return true;
        }

        [Server]
        internal bool HasOwnedRacePlayer(NetworkConnection conn)
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

        [ServerRpc(RequireOwnership = false)]
        private void ReportRaceSceneManagersReadyServerRpc(NetworkConnection caller = null)
        {
            if (caller == null || !caller.IsAuthenticated)
                return;

            _raceStartHandshake._raceSceneManagersReadyClientIds.Add(caller.ClientId);
            GameLog.Verbose($"[SceneDiag][Server] Client {caller.ClientId} reported race scene managers ready. details={BuildServerRaceReadinessSummary()}");
            EvaluateRaceStartReadinessServer();
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportIntroAssignmentAppliedServerRpc(int sequenceId, NetworkConnection caller = null)
        {
            if (!TryRecordReadySequenceServer(sequenceId, caller,
                    _raceStartHandshake._introAssignmentReadyClientIds, _raceStartHandshake._introAssignmentReadySequenceByClientId))
                return;
            GameLog.Verbose($"[SceneDiag][Server] Client {caller.ClientId} reported intro assignment ready for seq={sequenceId}. details={BuildServerRaceReadinessSummary()}");
            EvaluateRaceStartReadinessServer();
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportIntroVisualPreparedServerRpc(int sequenceId, NetworkConnection caller = null)
        {
            if (!TryRecordReadySequenceServer(sequenceId, caller,
                    _raceStartHandshake._introVisualReadyClientIds, _raceStartHandshake._introVisualReadySequenceByClientId))
                return;
            GameLog.Verbose($"[IntroVisual][Server] Client {caller.ClientId} reported visual prepared for seq={sequenceId}. details={BuildServerRaceReadinessSummary()}");
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportGameplayLiveServerRpc(int sequenceId, NetworkConnection caller = null)
        {
            if (!TryRecordReadySequenceServer(sequenceId, caller,
                    _raceStartHandshake._gameplayLiveClientIds, _raceStartHandshake._gameplayLiveSequenceByClientId))
                return;
            GameLog.Verbose($"[GameplayUnlock][Server] Client {caller.ClientId} reported gameplay live for seq={sequenceId}. details={BuildServerRaceReadinessSummary()}");

            if (AreAllClientsGameplayLiveForSequenceServer(sequenceId))
            {
                _gameplayMovementUnlocked.Value = true;
                _raceStarted.Value = true;
                GameLog.Verbose($"[GameplayUnlock][Server] Gameplay movement unlocked for all players. details={BuildServerRaceReadinessSummary()}");
            }
        }

        private bool TryReportLocalSequence(int sequenceId, ref int reportedSequenceId)
        {
            return RaceStartHandshake.TryMarkLocalSequence(IsClientInitialized, sequenceId, ref reportedSequenceId);
        }

        private bool AreAllClientsReadyForSequenceServer(int sequenceId, Dictionary<int, int> readiness)
        {
            return RaceStartHandshake.AreAllReadyForSequence(IsServerInitialized, Players, sequenceId, readiness);
        }

        private bool TryRecordReadySequenceServer(int sequenceId, NetworkConnection caller,
            HashSet<int> readyClientIds, Dictionary<int, int> readySequences)
        {
            if (caller == null || !caller.IsAuthenticated)
                return false;

            RaceStartHandshake.RecordReadySequence(caller.ClientId, sequenceId, readyClientIds, readySequences);
            return true;
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
            _playersSummaryText.Value = Roster.BuildSummary();
        }

        internal int GetLocalClientId()
        {
            return PlayerIdentity.GetLocalClientId(GameNetworkManager.Instance?.FishNetManager?.ClientManager?.Connection);
        }

        private void LogDebug(string message)
        {
            // Require both local inspector intent and global verbose switch.
            if (_enableDebugLogs && NetDebug.EnableVerboseLog)
                GameLog.Verbose($"[RoomStateManager] {message}");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogSceneDiag(string message)
        {
            if (_enableDebugLogs && NetDebug.EnableVerboseLog)
                GameLog.Verbose(message);
        }

        private IEnumerator RunClientRaceSceneDiagnostics()
        {
            yield return null;
            LogClientRaceSceneDiagnostics("next-frame");
            yield return new WaitForSecondsRealtime(0.25f);
            LogClientRaceSceneDiagnostics("t+0.25");
            yield return new WaitForSecondsRealtime(1f);
            LogClientRaceSceneDiagnostics("t+1.25");
            _clientRaceSceneDiagnosticsRoutine = null;
        }

        private IEnumerator WaitForLocalRaceSceneManagersAndReportReady()
        {
            const float timeoutSeconds = 10f;
            const float stableWindowSeconds = 0.35f;
            float startedAt = Time.unscaledTime;

            while (Time.unscaledTime - startedAt < timeoutSeconds)
            {
                if (IsLocalRaceSceneNetworkReady())
                {
                    if (_localRaceSceneReadyStableSince < 0f)
                    {
                        _localRaceSceneReadyStableSince = Time.unscaledTime;
                        GameLog.Verbose($"[SceneDiag][Client] Race scene ready conditions met. Holding stable window {stableWindowSeconds:0.00}s status={BuildLocalRaceSceneStatusReport()}");
                    }

                    if (Time.unscaledTime - _localRaceSceneReadyStableSince < stableWindowSeconds)
                    {
                        yield return new WaitForSecondsRealtime(0.1f);
                        continue;
                    }

                    if (!_reportedRaceSceneManagersReadyLocal)
                    {
                        _reportedRaceSceneManagersReadyLocal = true;
                        GameLog.Verbose($"[SceneDiag][Client] Required race scene managers are present. Reporting ready to server. status={BuildLocalRaceSceneStatusReport()}");
                        ReportRaceSceneManagersReadyServerRpc();
                    }

                    _clientRaceSceneReadyRoutine = null;
                    yield break;
                }

                _localRaceSceneReadyStableSince = -1f;
                GameLog.Verbose($"[SceneDiag][Client] Waiting for race scene managers... elapsed={(Time.unscaledTime - startedAt):0.00}s status={BuildLocalRaceSceneStatusReport()}");
                yield return new WaitForSecondsRealtime(0.1f);
            }

            Debug.LogWarning($"[SceneDiag][Client] Timed out waiting for required race scene managers. status={BuildLocalRaceSceneStatusReport()}");
            _clientRaceSceneReadyRoutine = null;
        }

        private void LogClientRaceSceneDiagnostics(string stageLabel)
        {
            RoomDiagnostics.LogClientRaceSceneDiagnostics(this, stageLabel);
        }

        private static bool AreRequiredRaceSceneManagersPresentLocally()
        {
            return IsSceneObjectReady(FindFirstObjectByType<IntroSequenceManager>(FindObjectsInactive.Include))
                   && IsSceneObjectReady(FindFirstObjectByType<LeaderboardManager>(FindObjectsInactive.Include))
                   && IsSceneObjectReady(FindFirstObjectByType<RaceFinishManager>(FindObjectsInactive.Include))
                   && IsSceneObjectReady(FindFirstObjectByType<ResultDecisionManager>(FindObjectsInactive.Include))
                   && IsSceneObjectReady(FindFirstObjectByType<MatchResultPresentationCoordinator>(FindObjectsInactive.Include))
                   && IsSceneObjectReady(FindFirstObjectByType<RaceResultAreaManager>(FindObjectsInactive.Include));
        }

        private bool IsLocalRaceSceneNetworkReady()
        {
            if (!AreRequiredRaceSceneManagersPresentLocally())
                return false;

            int localClientId = GetLocalClientId();
            if (localClientId < 0)
                return false;

            RaceBodyIntroStateController[] introBodies = FindObjectsByType<RaceBodyIntroStateController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < introBodies.Length; i++)
            {
                RaceBodyIntroStateController body = introBodies[i];
                if (body == null || body.OwnerId != localClientId)
                    continue;

                NetworkObject nob = body.NetworkObject;
                return nob != null && nob.IsSpawned;
            }

            return false;
        }

        private string BuildLocalRaceSceneStatusReport()
        {
            return RoomDiagnostics.BuildLocalRaceSceneStatusReport(this);
        }

        internal static bool IsSceneObjectReady(NetworkBehaviour behaviour)
        {
            if (behaviour == null)
                return false;

            NetworkObject nob = behaviour.NetworkObject;
            return nob != null && nob.IsSceneObject;
        }

        private string BuildServerRaceReadinessSummary()
        {
            return RoomDiagnostics.BuildServerRaceReadinessSummary(this, _raceStartHandshake);
        }

        private static string FormatSceneNames(Scene[] scenes)
        {
            return RoomDiagnostics.FormatSceneNames(scenes);
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
