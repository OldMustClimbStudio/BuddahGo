using BuddahGo.Match;
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
using SteamMultiplayer.UI;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using Scene = UnityEngine.SceneManagement.Scene;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace SteamMultiplayer.Network
{
    public class RoomStateManager : NetworkBehaviour
    {
        private const int DefaultMaxPlayerNameLength = 32;

        // Match/Lobby.maxPlayerNameLength; the constant is the fallback without a table row.
        private static int MaxPlayerNameLength
        {
            get
            {
                LobbyRecord lobby = DesignTableOverlay.Lobby();
                return lobby != null && lobby.maxPlayerNameLength > 0 ? lobby.maxPlayerNameLength : DefaultMaxPlayerNameLength;
            }
        }

        /* 简介：此脚本用于管理房间内的玩家状态、准备状态、游戏开始流程等核心逻辑。
            作为一个网络对象，RoomStateManager在服务器上维护房间的核心数据，并通过同步变量和同步列表将状态更新传递给所有客户端。
            客户端可以通过调用RequestToggleReady和RequestStartGame等方法来与服务器交互，改变自己的准备状态或请求开始游戏。
            服务器会根据当前的玩家状态和设置来决定是否允许游戏开始；开局时按房间里每人在菜单选的技能、皮肤和房主选的地图直接加载比赛场景。
            */
        public static RoomStateManager Instance { get; private set; }

        [Header("Room")]
        [SerializeField] private bool _requireAllClientsReady = true;
        [SerializeField] private bool _enableDebugLogs = false;

        [Header("Match Selection")]
        [Tooltip("Validates the skill loadouts players pick in the menu and fills empty slots.")]
        [SerializeField] private SkillDatabase _skillDatabase;
        [Tooltip("Seconds of fade to black before the race scene loads.")]
        [SerializeField, Min(0f)] private float _matchTransitionFadeSeconds = 1f;

        [Header("Race Flow")]
        [Tooltip("Race scene when the selection catalog has no map for the room's map id.")]
        [SerializeField] private string _raceSceneName = SceneNames.RaceMap;
        [SerializeField] private string _resultSceneName = "RaceMapEndField";
        private static string MainMenuSceneName => SceneNames.MainMenu;
        [SerializeField] private int _pregameCountdownSeconds = 15;

        public readonly SyncList<RoomPlayerState> Players = new SyncList<RoomPlayerState>();

        private readonly SyncVar<bool> _transitioningToMatch = new SyncVar<bool>();
        private readonly SyncVar<bool> _waitingForRacePlayers = new SyncVar<bool>();
        private readonly SyncVar<bool> _raceCountdownActive = new SyncVar<bool>();
        private readonly SyncVar<int> _raceCountdownSecondsRemaining = new SyncVar<int>();
        private readonly SyncVar<bool> _pregameCountdownCompleted = new SyncVar<bool>();
        private readonly SyncVar<bool> _raceStarted = new SyncVar<bool>();
        private readonly SyncVar<bool> _gameplayMovementUnlocked = new SyncVar<bool>();
        private readonly SyncVar<bool> _authoritativeGoIssued = new SyncVar<bool>();
        private readonly SyncVar<MatchSessionPhase> _matchSessionPhase = new SyncVar<MatchSessionPhase>();
        // Room-level map (SelectionCatalog id), set by the host from the menu.
        private readonly SyncVar<string> _mapId = new SyncVar<string>();
        // Scene of the match in progress; empty outside a match (falls back to _raceSceneName).
        private readonly SyncVar<string> _matchSceneName = new SyncVar<string>();
        private MatchLoadoutResolver _loadoutResolver;
        private MatchLoadoutResolver LoadoutResolver => _loadoutResolver ??= new MatchLoadoutResolver(_skillDatabase);
        private float _nextRaceReadinessPollTime;
        private bool _returningToRoomMenu;
        private bool _rematchPending;
        private Coroutine _clientRaceSceneDiagnosticsRoutine;
        private Coroutine _clientRaceSceneReadyRoutine;
        private Coroutine _raceCountdownRoutine;
        // Newest gameplay-live sequence any client reported this race; lets a disconnect re-check the unlock.
        private int _latestGameplayLiveSequenceIdServer = -1;
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

        internal string DiagnosticRaceSceneName => RaceSceneName;
        internal bool DiagnosticManagersReported => _reportedRaceSceneManagersReadyLocal;
        internal int DiagnosticAssignmentSequence => _reportedIntroAssignmentSequenceId;
        internal int DiagnosticVisualSequence => _reportedIntroVisualSequenceId;
        internal int DiagnosticGameplaySequence => _reportedGameplayLiveSequenceId;

        private string RaceSceneName => string.IsNullOrWhiteSpace(_matchSceneName.Value) ? _raceSceneName : _matchSceneName.Value;
        /// <summary>The room's map (SelectionCatalog id); only the host can change it.</summary>
        public string MapId => _mapId.Value;
        public bool IsTransitioningToMatch => _transitioningToMatch.Value;
        public bool CanHostStartGame => Players.Count > 0 && (!_requireAllClientsReady || AreAllRequiredPlayersReady());
        public bool IsWaitingForRacePlayers => _waitingForRacePlayers.Value;
        public bool IsRaceCountdownActive => _raceCountdownActive.Value;
        public int RaceCountdownSecondsRemaining => _raceCountdownSecondsRemaining.Value;
        public bool IsPregameCountdownCompleted => _pregameCountdownCompleted.Value;
        public bool IsRaceStarted => _raceStarted.Value;
        public bool IsGameplayMovementUnlocked => _gameplayMovementUnlocked.Value;
        public bool IsAuthoritativeGoIssued => _authoritativeGoIssued.Value;
        public MatchSessionPhase CurrentMatchSessionPhase => _matchSessionPhase.Value;
        public bool IsRaceSceneLoadedLocally => !string.IsNullOrWhiteSpace(RaceSceneName) && UnitySceneManager.GetSceneByName(RaceSceneName).isLoaded;
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

        public event Action OnMatchTransitionRequested;

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
            SubmitLocalSelection();
            LocalMatchSelection.Changed += SubmitLocalSelection;
            if (IsServerInitialized && MatchRules.Current.AutoStartRoom)
                StartCoroutine(StartSoloAfterInitialScenes());

            if (InstanceFinder.SceneManager != null)
                InstanceFinder.SceneManager.OnLoadEnd += HandleClientSceneLoadEnd;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            LocalMatchSelection.Changed -= SubmitLocalSelection;

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
            // Match/ModeRule owns the pregame countdown and whether every client must be ready before the host can start.
            ModeRuleRecord mode = DesignTableOverlay.CurrentModeRule();
            if (mode != null && mode.pregameCountdownSeconds > 0)
                _pregameCountdownSeconds = mode.pregameCountdownSeconds;
            if (mode != null)
                _requireAllClientsReady = mode.requiresReady;

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

            _transitioningToMatch.Value = false;
            ResetRaceFlowStateServer();
            _matchSessionPhase.Value = MatchSessionPhase.InRoom;
            _returningToRoomMenu = false;
            // The server runs in the host's process: the host's menu pick is the room's map.
            _mapId.Value = NormalizeMapId(LocalMatchSelection.MapId);
            _matchSceneName.Value = string.Empty;
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
            _raceCountdownRoutine = null;
            Players.Clear();
            _transitioningToMatch.Value = false;
            _waitingForRacePlayers.Value = false;
            _raceCountdownActive.Value = false;
            _raceCountdownSecondsRemaining.Value = 0;
            _pregameCountdownCompleted.Value = false;
            _raceStarted.Value = false;
            _gameplayMovementUnlocked.Value = false;
            _authoritativeGoIssued.Value = false;
            _latestGameplayLiveSequenceIdServer = -1;
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
            return AreAllClientsReadyForSequenceServer(sequenceId, RaceStartHandshake.Stage.Visual);
        }

        public bool AreAllClientsGameplayLiveForSequenceServer(int sequenceId)
        {
            return AreAllClientsReadyForSequenceServer(sequenceId, RaceStartHandshake.Stage.Gameplay);
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

            TransitionToMatchServer();
        }

        // Each client sends its menu pick when it joins and whenever it changes; the host also sends the room's map.
        private void SubmitLocalSelection()
        {
            if (!IsClientInitialized)
                return;

            string[] skills = LocalMatchSelection.Skills;
            SubmitSelectionServerRpc(skills[0], skills[1], skills[2], LocalMatchSelection.SkinId);
            if (IsServerInitialized)
                SetMapServerRpc(LocalMatchSelection.MapId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitSelectionServerRpc(string skill0, string skill1, string skill2, string skinId, NetworkConnection caller = null)
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
            if (LoadoutResolver.TryNormalize(new[] { skill0, skill1, skill2 }, out string[] skills, out string error))
                player.SetSkills(skills);
            else
                Debug.LogWarning($"[RoomStateManager] Rejected skill loadout from player {caller.ClientId}: {error}");

            player.SkinId = NormalizeSkinId(skinId);
            if (!player.Equals(Players[index]))
                Players[index] = player;
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetMapServerRpc(string mapId, NetworkConnection caller = null)
        {
            if (caller == null || !caller.IsAuthenticated)
                return;

            int index = FindPlayerIndex(caller.ClientId);
            if (index < 0 || !Players[index].IsHost)
                return;

            if (_matchSessionPhase.Value != MatchSessionPhase.InRoom && _matchSessionPhase.Value != MatchSessionPhase.InResult)
                return;

            string normalized = NormalizeMapId(mapId);
            if (_mapId.Value != normalized)
                _mapId.Value = normalized;
        }

        private static string NormalizeSkinId(string skinId)
        {
            SelectionCatalog catalog = SelectionCatalog.Current;
            return catalog != null ? catalog.NormalizeSkinId(skinId) : string.Empty;
        }

        private static string NormalizeMapId(string mapId)
        {
            SelectionCatalog catalog = SelectionCatalog.Current;
            return catalog != null ? catalog.NormalizeMapId(mapId) : string.Empty;
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

            if (ContainsScene(args.LoadedScenes, MainMenuSceneName))
            {
                CompleteReturnToRoomMenuServer();
                if (_rematchPending)
                {
                    _rematchPending = false;
                    TransitionToMatchServer(screenAlreadyBlack: true);
                }
                return;
            }

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

        private void HandleClientPresenceChangeEnd(ClientPresenceChangeEventArgs args)
        {
            if (!IsServerInitialized)
                return;

            if (_matchSessionPhase.Value != MatchSessionPhase.InMatch)
                return;

            if (!string.Equals(args.Scene.name, RaceSceneName, StringComparison.Ordinal))
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
            LogDebug($"Removed room player {playerId}");

            // The leaver may have been the last client the gameplay unlock was waiting on.
            if (_matchSessionPhase.Value == MatchSessionPhase.InMatch
                && _authoritativeGoIssued.Value
                && !_gameplayMovementUnlocked.Value
                && Players.Count > 0)
            {
                TryUnlockGameplayServer(_latestGameplayLiveSequenceIdServer);
            }
        }

        private RoomPlayerState BuildRoomPlayerState(NetworkConnection conn)
        {
            string steamId = GetSteamIdForConnection(conn);
            bool isHost = IsHostConnection(conn, steamId);
            bool wasReady = TryGetPlayer(conn.ClientId, out RoomPlayerState existing) && existing.IsReady;

            RoomPlayerState playerState = new RoomPlayerState
            {
                PlayerId = conn.ClientId,
                SteamId = steamId,
                PlayerName = ResolvePlayerName(steamId, conn.ClientId),
                IsHost = isHost,
                IsReady = isHost ? true : wasReady
            };
            playerState.CopySelectionFrom(existing);
            return playerState;
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

        private IEnumerator StartSoloAfterInitialScenes()
        {
            yield return null;
            while (IsServerInitialized && IsClientInitialized && MatchRules.Current.AutoStartRoom)
            {
                NetworkConnection local = ClientManager.Connection;
                if (local.IsValid && local.IsAuthenticated && local.LoadedStartScenes(true))
                {
                    TryStartSoloMatchServer();
                    yield break;
                }
                yield return null;
            }
        }

        [Server]
        public void TryStartSoloMatchServer()
        {
            if (!MatchRules.Current.AutoStartRoom || _transitioningToMatch.Value
                || _matchSessionPhase.Value != MatchSessionPhase.InRoom) return;
            NetworkConnection local = ClientManager.Connection;
            if (!local.IsValid || !local.LoadedStartScenes(true)) return;
            if (!TryGetPlayer(local.ClientId, out RoomPlayerState player) || !player.IsHost) return;
            TransitionToMatchServer();
        }

        private void SubmitLocalDisplayName()
        {
            if (!IsClientInitialized) return;
            string localName = SanitizePlayerName(SteamClient.IsValid
                ? SteamClient.Name : MatchRules.Current.DefaultPlayerName);
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

        // Straight from the room into the race: every player's menu pick goes into the selection cache
        // (MatchSpawnManager applies it on spawn) and the room's map decides the scene.
        [Server]
        private void TransitionToMatchServer(bool screenAlreadyBlack = false)
        {
            if (_transitioningToMatch.Value)
                return;

            if (InstanceFinder.SceneManager == null)
            {
                Debug.LogWarning("[RoomStateManager] SceneManager missing. Cannot start the match.");
                return;
            }

            ResetRaceFlowStateServer();
            ResolveMatchSelectionServer();
            string sceneName = ResolveMatchSceneName();
            _matchSceneName.Value = sceneName;
            _matchSessionPhase.Value = MatchSessionPhase.TransitioningToMatch;
            _transitioningToMatch.Value = true;
            OnMatchTransitionRequested?.Invoke();

            float fadeSeconds = screenAlreadyBlack ? 0f : _matchTransitionFadeSeconds;
            NotifyMatchTransitionObserversRpc(fadeSeconds);
            StartCoroutine(LoadSceneAfterFade(sceneName, fadeSeconds));
        }

        [Server]
        private IEnumerator LoadSceneAfterFade(string sceneName, float fadeSeconds)
        {
            if (fadeSeconds > 0f)
                yield return new WaitForSecondsRealtime(fadeSeconds);

            InstanceFinder.SceneManager.LoadGlobalScenes(new SceneLoadData(sceneName) { ReplaceScenes = ReplaceOption.All });
            LogDebug($"Loading scene '{sceneName}' (map '{_mapId.Value}').");
        }

        [Server]
        private string ResolveMatchSceneName()
        {
            SelectionCatalog catalog = SelectionCatalog.Current;
            return catalog != null ? catalog.SceneOf(_mapId.Value, _raceSceneName) : _raceSceneName;
        }

        // Fills every player's loadout (empty or invalid slots take fallback skills) and writes the cache.
        // The host's own pick is read from its PlayerPrefs directly: its RPC may not have landed yet when Solo auto-starts.
        [Server]
        private void ResolveMatchSelectionServer()
        {
            ResolvedPropertySelectionCache.Clear();
            NetworkConnection local = ClientManager != null ? ClientManager.Connection : null;
            int localClientId = local != null && local.IsValid ? local.ClientId : -1;

            for (int i = 0; i < Players.Count; i++)
            {
                RoomPlayerState player = Players[i];
                if (player.PlayerId == localClientId)
                {
                    if (LoadoutResolver.TryNormalize(LocalMatchSelection.Skills, out string[] localSkills, out _))
                        player.SetSkills(localSkills);
                    player.SkinId = NormalizeSkinId(LocalMatchSelection.SkinId);
                }

                string[] skills = LoadoutResolver.Complete(player.SkillIds);
                player.SetSkills(skills);
                if (!player.Equals(Players[i]))
                    Players[i] = player;

                ResolvedPropertySelectionCache.SetPlayerSkillLoadout(player.PlayerId, skills);
                GameLog.Verbose($"[RoomStateManager] Match loadout for player {player.PlayerId}: '{skills[0]}', '{skills[1]}', '{skills[2]}', skin '{player.SkinId}'.");
            }
        }

        [Server]
        private void BeginRacePreparationServer()
        {
            StopAllCoroutines();
            _raceCountdownRoutine = null;
            _transitioningToMatch.Value = false;
            _waitingForRacePlayers.Value = true;
            _raceCountdownActive.Value = false;
            _raceCountdownSecondsRemaining.Value = 0;
            _pregameCountdownCompleted.Value = false;
            _raceStarted.Value = false;
            _gameplayMovementUnlocked.Value = false;
            _authoritativeGoIssued.Value = false;
            _matchSessionPhase.Value = MatchSessionPhase.InMatch;
            _raceStartHandshake.ResetAllReadiness();
            _latestGameplayLiveSequenceIdServer = -1;
            GameLog.Verbose($"[SceneDiag][Server] BeginRacePreparationServer scene='{RaceSceneName}' time={Time.unscaledTime:F3}");
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

            string sceneName = string.IsNullOrWhiteSpace(targetMainMenuSceneName) ? MainMenuSceneName : targetMainMenuSceneName;
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

        // FishNet does not reload a scene that is already loaded, so a rematch passes through the menu scene
        // behind a held black screen and starts the race again from there (HandleSceneLoadEnd).
        [Server]
        public void StartNextGameFromResultServer()
        {
            if (!IsServerInitialized)
                return;

            if (_returningToRoomMenu || _rematchPending)
            {
                Debug.LogWarning("[RoomStateManager] StartNextGameFromResultServer ignored because a scene change is already in progress.");
                return;
            }

            if (string.IsNullOrWhiteSpace(MainMenuSceneName) || InstanceFinder.SceneManager == null)
            {
                Debug.LogWarning("[RoomStateManager] Main menu scene name is empty or SceneManager is missing. Cannot start next game.");
                return;
            }

            CleanupMatchSessionButKeepRoomServer();
            _rematchPending = true;
            _matchSessionPhase.Value = MatchSessionPhase.TransitioningToMatch;
            NotifyMatchTransitionObserversRpc(_matchTransitionFadeSeconds);
            StartCoroutine(LoadSceneAfterFade(MainMenuSceneName, _matchTransitionFadeSeconds));
            LogDebug("Rematch: passing through the menu scene before reloading the race.");
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
            _transitioningToMatch.Value = false;
            _matchSessionPhase.Value = MatchSessionPhase.InRoom;
            _returningToRoomMenu = false;
            _matchSceneName.Value = string.Empty;
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
            _latestGameplayLiveSequenceIdServer = -1;
        }

        [Server]
        private void ResetPlayersReadyStateForRoomReturn()
        {
            Roster.ResetReadyForRoomReturn();
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
            _raceCountdownRoutine = StartCoroutine(RaceCountdownCoroutine());
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
                    _raceCountdownRoutine = null;
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

            _raceCountdownRoutine = null;
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
            // Stop the running countdown so a quick stop/restart cannot leave two coroutines decrementing.
            if (_raceCountdownRoutine != null)
            {
                StopCoroutine(_raceCountdownRoutine);
                _raceCountdownRoutine = null;
            }

            _raceCountdownActive.Value = false;
            _raceCountdownSecondsRemaining.Value = 0;
        }

        [Server]
        public void MarkAuthoritativeGoIssuedServer()
        {
            if (!IsServerInitialized || _authoritativeGoIssued.Value)
                return;

            _authoritativeGoIssued.Value = true;
            if (MatchServices.Clock != null) MatchServices.Timing?.Begin(MatchServices.Clock.Now);
            _pregameCountdownCompleted.Value = false;
            _gameplayMovementUnlocked.Value = false;
            GameLog.Verbose($"[IntroGo][Server] Authoritative go state set by intro sequence. details={BuildServerRaceReadinessSummary()}");
        }

        [Server]
        private bool ShouldMonitorRaceFlowServer()
        {
            if (!IsServerInitialized || string.IsNullOrWhiteSpace(RaceSceneName))
                return false;

            return UnitySceneManager.GetSceneByName(RaceSceneName).isLoaded;
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

            UnityEngine.SceneManagement.Scene raceScene = UnitySceneManager.GetSceneByName(RaceSceneName);
            if (!raceScene.isLoaded)
            {
                if (_enableDebugLogs && NetDebug.EnableVerboseLog)
                    LogSceneDiag($"[SceneDiag][Server] Race readiness false: scene '{RaceSceneName}' not loaded on server. details={BuildServerRaceReadinessSummary()}");
                return false;
            }

            if (!InstanceFinder.SceneManager.SceneConnections.TryGetValue(raceScene, out HashSet<NetworkConnection> sceneConnections))
            {
                if (_enableDebugLogs && NetDebug.EnableVerboseLog)
                    LogSceneDiag($"[SceneDiag][Server] Race readiness false: no SceneConnections entry for '{RaceSceneName}'. details={BuildServerRaceReadinessSummary()}");
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
                        LogSceneDiag($"[SceneDiag][Server] Race readiness false: player {playerState.PlayerId} has no owned race player in '{RaceSceneName}'. details={BuildServerRaceReadinessSummary()}");
                    return false;
                }

                if (!_raceStartHandshake.AreSceneManagersReady(playerState.PlayerId))
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

                if (!string.Equals(networkObject.gameObject.scene.name, RaceSceneName, StringComparison.Ordinal))
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

            _raceStartHandshake.MarkSceneManagersReady(caller.ClientId);
            GameLog.Verbose($"[SceneDiag][Server] Client {caller.ClientId} reported race scene managers ready. details={BuildServerRaceReadinessSummary()}");
            EvaluateRaceStartReadinessServer();
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportIntroAssignmentAppliedServerRpc(int sequenceId, NetworkConnection caller = null)
        {
            if (!TryRecordReadySequenceServer(sequenceId, caller,
                    RaceStartHandshake.Stage.Assignment))
                return;
            GameLog.Verbose($"[SceneDiag][Server] Client {caller.ClientId} reported intro assignment ready for seq={sequenceId}. details={BuildServerRaceReadinessSummary()}");
            EvaluateRaceStartReadinessServer();
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportIntroVisualPreparedServerRpc(int sequenceId, NetworkConnection caller = null)
        {
            if (!TryRecordReadySequenceServer(sequenceId, caller,
                    RaceStartHandshake.Stage.Visual))
                return;
            GameLog.Verbose($"[IntroVisual][Server] Client {caller.ClientId} reported visual prepared for seq={sequenceId}. details={BuildServerRaceReadinessSummary()}");
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportGameplayLiveServerRpc(int sequenceId, NetworkConnection caller = null)
        {
            if (!TryRecordReadySequenceServer(sequenceId, caller,
                    RaceStartHandshake.Stage.Gameplay))
                return;
            GameLog.Verbose($"[GameplayUnlock][Server] Client {caller.ClientId} reported gameplay live for seq={sequenceId}. details={BuildServerRaceReadinessSummary()}");

            if (sequenceId > _latestGameplayLiveSequenceIdServer)
                _latestGameplayLiveSequenceIdServer = sequenceId;
            TryUnlockGameplayServer(sequenceId);
        }

        [Server]
        private void TryUnlockGameplayServer(int sequenceId)
        {
            if (!AreAllClientsGameplayLiveForSequenceServer(sequenceId))
                return;

            _gameplayMovementUnlocked.Value = true;
            _raceStarted.Value = true;
            GameLog.Verbose($"[GameplayUnlock][Server] Gameplay movement unlocked for all players. seq={sequenceId} details={BuildServerRaceReadinessSummary()}");
        }

        private bool TryReportLocalSequence(int sequenceId, ref int reportedSequenceId)
        {
            return RaceStartHandshake.TryMarkLocalSequence(IsClientInitialized, sequenceId, ref reportedSequenceId);
        }

        private bool AreAllClientsReadyForSequenceServer(int sequenceId, RaceStartHandshake.Stage stage)
        {
            return _raceStartHandshake.AreAllReadyForSequence(IsServerInitialized, Players, sequenceId, stage);
        }

        private bool TryRecordReadySequenceServer(int sequenceId, NetworkConnection caller,
            RaceStartHandshake.Stage stage)
        {
            if (caller == null || !caller.IsAuthenticated)
                return false;

            _raceStartHandshake.RecordReadySequence(caller.ClientId, sequenceId, stage);
            return true;
        }

        private bool ContainsRaceScene(UnityEngine.SceneManagement.Scene[] loadedScenes)
        {
            return ContainsScene(loadedScenes, RaceSceneName);
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
        private void NotifyMatchTransitionObserversRpc(float fadeSeconds)
        {
            if (!IsServerInitialized)
                OnMatchTransitionRequested?.Invoke();

            // Black from here until the race intro releases it (IntroSequenceManager).
            SceneFadeController.HoldBlackOnNextSceneLoad();
            SceneFadeController.PlayFadeInBeforeSceneLoad(fadeSeconds);
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
        TransitioningToMatch,
        InMatch,
        TransitioningToResult,
        InResult,
        ReturningToRoom
    }
}
