using System;
using System.Collections.Generic;
using FishNet.Transporting;
using Steamworks;
using Steamworks.Data;
using SteamMultiplayer.Network;
using UnityEngine;

// NOTE: If this file gets a "SteamMatchmaking does not contain a definition" error,
// ensure Facepunch.Steamworks.Win64.dll is referenced and the DISABLESTEAMWORKS symbol
// is NOT defined in Player Settings.

namespace SteamMultiplayer.Network
{
    /* 简介：此脚本用于管理Steam P2P大厅，提供创建、加入、离开大厅以及刷新大厅列表等功能。
        通过Facepunch.Steamworks库与Steam API进行交互，处理大厅相关的事件和状态变化。
        脚本会触发相应的事件供UI和其他系统订阅，以便在界面上显示最新的大厅信息和状态。
        同时提供流程状态管理，帮助UI显示当前的操作进度和结果。
        */
    [System.Serializable]
    public struct LobbyListItemData
    {
        public ulong LobbyId;
        public string LobbyName;
        public string HostSteamId;
        public string Version;
        public string Visibility;
        public int CurrentPlayers;
        public int MaxPlayers;
        public bool HasOpenSlot;
        public bool IsVersionMatch;
        public bool IsJoinable;
    }

    public enum LobbyFlowState
    {
        Idle,
        CreatingLobby,
        LobbyCreated,
        StartingHost,
        RefreshingLobbyList,
        LobbyListReady,
        JoiningLobby,
        LobbyJoined,
        ConnectingToHost,
        Connected,
        LeavingLobby,
        HostLeft,
        Failed
    }

    /// <summary>
    /// Visibility options when creating a Steam lobby.
    /// </summary>
    public enum LobbyVisibility
    {
        Public,
        FriendsOnly,
        Private
    }

    /// <summary>
    /// Manages Steam P2P lobbies via Facepunch.Steamworks.
    ///
    /// Responsibilities:
    ///   - Create / join / leave Steam lobbies
    ///   - Store lobby metadata (name, app version, host SteamId)
    ///   - Refresh public lobby list with version + slot filtering
    ///   - Automatically trigger StartHost / StartClient on GameNetworkManager
    ///   - Expose clean events for UI and higher-level systems
    ///
    /// Does NOT handle:
    ///   - UI rendering (see DebugLobbyTester or future LobbyUI)
    ///   - Scene flow (GameFlowManager, future)
    ///   - Player spawning (PlayerSpawnManager, future)
    ///
    /// Host Migration (placeholder):
    ///   When the host leaves, OnHostLeft fires and all clients automatically
    ///   disconnect + clear lobby state. Proper host migration (promoting
    ///   another client to host) is left for a future iteration.
    /// </summary>
    public class SteamLobbyManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        public static SteamLobbyManager Instance { get; private set; }

        // ── Metadata Key Constants ─────────────────────────────────────────────
        // Keep keys short; Steam caps lobby metadata key length at ~255 chars but
        // shorter is better for bandwidth.
        public const string KEY_LOBBY_NAME    = "l_name";
        public const string KEY_APP_VERSION   = "l_ver";
        public const string KEY_HOST_STEAM_ID = "l_host";
        public const string KEY_VISIBILITY    = "l_vis";
        public const string KEY_MAX_PLAYERS   = "l_max";

        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Lobby Defaults")]
        [Tooltip("Default maximum players per lobby.")]
        [Range(2, 6)]
        [SerializeField] private int _defaultMaxPlayers = 6;

        [Tooltip("Default lobby name displayed to other players.")]
        [SerializeField] private string _defaultLobbyName = "My Room";

        [Header("Search")]
        [Tooltip("Max number of lobbies returned per search request.")]
        [Range(1, 50)]
        [SerializeField] private int _maxSearchResults = 20;

        // ── State ──────────────────────────────────────────────────────────────
        /// <summary>The lobby this client is currently in, if any.</summary>
        public Lobby? CurrentLobby { get; private set; }

        /// <summary>True if the local player is currently inside a lobby.</summary>
        public bool IsInLobby => CurrentLobby.HasValue;

        /// <summary>True if the local player is the lobby owner.</summary>
        public bool IsLobbyOwner
            => IsInLobby && SteamClient.IsValid
               && CurrentLobby.Value.IsOwnedBy(SteamClient.SteamId);

        /// <summary>Current member count (0 if not in a lobby).</summary>
        public int MemberCount => IsInLobby ? CurrentLobby.Value.MemberCount : 0;

        /// <summary>Display name of the current lobby (empty if not in one).</summary>
        public string CurrentLobbyName
            => IsInLobby ? CurrentLobby.Value.GetData(KEY_LOBBY_NAME) : string.Empty;

        /// <summary>Most recent create-room visibility selection, for debug UIs before a lobby exists.</summary>
        public LobbyVisibility PendingVisibility { get; private set; } = LobbyVisibility.Public;

        /// <summary>Most recent create-room max player selection, for debug UIs before a lobby exists.</summary>
        public int PendingMaxPlayers { get; private set; } = 6;

        public LobbyFlowState CurrentFlowState { get; private set; } = LobbyFlowState.Idle;
        public string CurrentFlowMessage { get; private set; } = "Idle";

        // ── Events ─────────────────────────────────────────────────────────────

        /// <summary>Fired upon successfully creating a lobby (Host side).</summary>
        public event Action<Lobby> OnLobbyCreated;

        /// <summary>Fired upon successfully joining a lobby (Client side).</summary>
        public event Action<Lobby> OnLobbyJoined;

        /// <summary>Fired after leaving or being removed from the current lobby.</summary>
        public event Action OnLobbyLeft;

        /// <summary>Fired when lobby creation fails. Arg = reason string.</summary>
        public event Action<string> OnLobbyCreateFailed;

        /// <summary>Fired when joining a lobby fails. Arg = reason string.</summary>
        public event Action<string> OnLobbyJoinFailed;

        /// <summary>Fired when another member enters our current lobby.</summary>
        public event Action<Friend> OnMemberJoined;

        /// <summary>Fired when a member leaves or disconnects from our current lobby.</summary>
        public event Action<Friend> OnMemberLeft;

        /// <summary>Fired after RefreshLobbyList() completes. Arg = discovered lobbies.</summary>
        public event Action<List<Lobby>> OnLobbyListRefreshed;
        public event Action<List<LobbyListItemData>> OnLobbyListDataRefreshed;
        public event Action<string> OnDebugLog;
        public event Action<LobbyFlowState, string> OnFlowStateChanged;

        /// <summary>
        /// Fired when the current HOST player leaves the lobby.
        /// Placeholder behaviour: all remaining players are told to disconnect
        /// and return to the main menu.
        /// Future: replace with proper Host Migration.
        /// </summary>
        public event Action OnHostLeft;
        private bool _networkEventsSubscribed;

        // ── Unity Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SubscribeSteamEvents();
            SubscribeNetworkEvents();
        }

        private void OnDisable()
        {
            UnsubscribeSteamEvents();
            UnsubscribeNetworkEvents();
        }

        private void OnDestroy()
        {
            // Attempt a clean leave when this manager is destroyed
            // (e.g. during application quit or scene reload without DontDestroyOnLoad)
            if (IsInLobby && SteamClient.IsValid)
            {
                CurrentLobby.Value.Leave();
                CurrentLobby = null;
            }

            if (Instance == this)
                Instance = null;
        }

        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Create a new Steam lobby and (on success) automatically start hosting.
        /// </summary>
        /// <param name="lobbyName">Room name visible to other players. Null = use Inspector default.</param>
        /// <param name="visibility">Public / FriendsOnly / Private.</param>
        /// <param name="maxPlayers">Max players. -1 = use Inspector default.</param>
        public void CreateLobby(
            string lobbyName = null,
            LobbyVisibility visibility = LobbyVisibility.Public,
            int maxPlayers = -1)
        {
            SubscribeNetworkEvents();

            if (!SteamClient.IsValid)
            {
                FireCreateFailed("Steam is not initialized.");
                return;
            }

            if (IsInLobby)
            {
                NetLog.Warn("[Lobby] Already in a lobby – leave first before creating a new one.");
                SetFlowState(LobbyFlowState.Failed, "Already in a lobby.");
                return;
            }

            string name     = string.IsNullOrWhiteSpace(lobbyName) ? _defaultLobbyName : lobbyName;
            int    capacity = maxPlayers > 0 ? maxPlayers : _defaultMaxPlayers;
            SetPendingCreateSettings(visibility, capacity);

            CreateLobbyAsync(name, visibility, capacity);
        }

        /// <summary>
        /// Join a lobby from the lobby list and (on success) automatically connect to the host.
        /// </summary>
        public void JoinLobby(Lobby lobby)
        {
            SubscribeNetworkEvents();

            if (!SteamClient.IsValid)
            {
                FireJoinFailed("Steam is not initialized.");
                return;
            }

            if (IsInLobby)
            {
                NetLog.Warn("[Lobby] Already in a lobby – leave first before joining another.");
                SetFlowState(LobbyFlowState.Failed, "Already in a lobby.");
                return;
            }

            JoinLobbyAsync(lobby.Id);
        }

        /// <summary>
        /// Join a lobby directly by its raw SteamId value (ulong).
        /// Useful for invite codes and debug testing.
        /// </summary>
        public void JoinLobbyById(ulong lobbyId)
        {
            SubscribeNetworkEvents();

            if (!SteamClient.IsValid)
            {
                FireJoinFailed("Steam is not initialized.");
                return;
            }

            if (IsInLobby)
            {
                NetLog.Warn("[Lobby] Already in a lobby – leave first.");
                SetFlowState(LobbyFlowState.Failed, "Already in a lobby.");
                return;
            }

            JoinLobbyAsync(new SteamId { Value = lobbyId });
        }

        /// <summary>
        /// Leave the current lobby and stop all network connections.
        /// Safe to call when not in a lobby.
        /// </summary>
        public void LeaveLobby()
        {
            SubscribeNetworkEvents();

            if (!IsInLobby) return;

            NetLog.Info($"[Lobby] Leaving \"{CurrentLobbyName}\"...");
            SetFlowState(LobbyFlowState.LeavingLobby, $"Leaving \"{CurrentLobbyName}\"...");

            // Leave the Steam lobby first
            CurrentLobby.Value.Leave();
            CurrentLobby = null;

            // Stop networking
            if (GameNetworkManager.Instance != null)
                GameNetworkManager.Instance.StopConnection();

            OnLobbyLeft?.Invoke();
            SetFlowState(LobbyFlowState.Idle, "Left lobby.");
        }

        /// <summary>
        /// Fetch the list of public lobbies that match the current app version
        /// and have at least one open slot. Results come back via OnLobbyListRefreshed.
        /// </summary>
        public void RefreshLobbyList()
        {
            if (!SteamClient.IsValid)
            {
                NetLog.Error("[Lobby] Steam is not initialized. Cannot fetch lobby list.");
                SetFlowState(LobbyFlowState.Failed, "Steam is not initialized.");
                return;
            }

            RefreshLobbyListAsync();
        }

        public void PublishDebugLog(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                OnDebugLog?.Invoke(message);
        }

        public void SetPendingCreateSettings(LobbyVisibility visibility, int maxPlayers)
        {
            PendingVisibility = visibility;
            PendingMaxPlayers = Mathf.Clamp(maxPlayers, 2, 6);
        }

        public bool UpdateCurrentLobbySettings(LobbyVisibility visibility, int maxPlayers, string lobbyName = null)
        {
            if (!IsInLobby || !CurrentLobby.HasValue || !SteamClient.IsValid)
                return false;

            Lobby lobby = CurrentLobby.Value;
            int clampedMaxPlayers = Mathf.Clamp(maxPlayers, 2, 6);

            switch (visibility)
            {
                case LobbyVisibility.Public: lobby.SetPublic(); break;
                case LobbyVisibility.FriendsOnly: lobby.SetFriendsOnly(); break;
                case LobbyVisibility.Private: lobby.SetPrivate(); break;
            }

            lobby.MaxMembers = clampedMaxPlayers;
            lobby.SetData(KEY_VISIBILITY, visibility.ToString());
            lobby.SetData(KEY_MAX_PLAYERS, clampedMaxPlayers.ToString());

            if (!string.IsNullOrWhiteSpace(lobbyName))
            {
                lobby.SetData(KEY_LOBBY_NAME, lobbyName);
            }

            CurrentLobby = lobby;
            PendingVisibility = visibility;
            PendingMaxPlayers = clampedMaxPlayers;
            PublishDebugLog($"Lobby settings updated: visibility={visibility}, maxPlayers={clampedMaxPlayers}");
            return true;
        }

        // ── Private Async Implementations ─────────────────────────────────────

        private async void CreateLobbyAsync(string name, LobbyVisibility visibility, int maxPlayers)
        {
            NetLog.Info($"[Lobby] Creating \"{name}\" | {visibility} | max={maxPlayers}...");
            SetFlowState(LobbyFlowState.CreatingLobby, $"Creating lobby \"{name}\"...");

            Lobby? result = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);

            if (!result.HasValue)
            {
                FireCreateFailed("SteamMatchmaking.CreateLobbyAsync returned null (Steam error).");
                return;
            }

            Lobby lobby = result.Value;

            // Apply visibility (lobby starts invisible by default)
            switch (visibility)
            {
                case LobbyVisibility.Public:      lobby.SetPublic();      break;
                case LobbyVisibility.FriendsOnly: lobby.SetFriendsOnly(); break;
                case LobbyVisibility.Private:     lobby.SetPrivate();     break;
            }

            // Keep the lobby's capacity and metadata aligned so every UI can read the same value.
            lobby.MaxMembers = maxPlayers;

            // Allow joining
            lobby.SetJoinable(true);

            // Write metadata
            lobby.SetData(KEY_LOBBY_NAME,    name);
            lobby.SetData(KEY_APP_VERSION,   Application.version);
            lobby.SetData(KEY_HOST_STEAM_ID, SteamClient.SteamId.Value.ToString());
            lobby.SetData(KEY_VISIBILITY, visibility.ToString());
            lobby.SetData(KEY_MAX_PLAYERS, maxPlayers.ToString());

            CurrentLobby = lobby;
            NetLog.Info($"[Lobby] Created — ID={lobby.Id.Value}  name={name}");
            SetFlowState(LobbyFlowState.LobbyCreated, $"Lobby created: {name}");

            // Auto-start hosting
            if (GameNetworkManager.Instance != null)
            {
                SetFlowState(LobbyFlowState.StartingHost, "Starting host...");
                GameNetworkManager.Instance.StartHost();
            }
            else
            {
                NetLog.Warn("[Lobby] GameNetworkManager not found – StartHost() skipped.");
                SetFlowState(LobbyFlowState.Failed, "GameNetworkManager not found.");
            }

            PublishDebugLog($"Lobby created: {lobby.Id.Value}, visibility={visibility}, maxPlayers={maxPlayers}");
            OnLobbyCreated?.Invoke(lobby);
        }

        private async void JoinLobbyAsync(SteamId steamId)
        {
            NetLog.Info($"[Lobby] Joining {steamId.Value}...");
            SetFlowState(LobbyFlowState.JoiningLobby, $"Joining lobby {steamId.Value}...");

            Lobby? result = await SteamMatchmaking.JoinLobbyAsync(steamId);

            if (!result.HasValue)
            {
                FireJoinFailed($"JoinLobbyAsync({steamId.Value}) returned null – lobby may be full or gone.");
                return;
            }

            Lobby lobby = result.Value;
            string lobbyName  = lobby.GetData(KEY_LOBBY_NAME);
            string hostSteamId = lobby.GetData(KEY_HOST_STEAM_ID);

            // Treat lobbies missing core metadata as invalid join targets.
            // This prevents the UI from entering RoomUI for dead/invalid lobby ids.
            if (string.IsNullOrWhiteSpace(hostSteamId))
            {
                lobby.Leave();
                FireJoinFailed($"Lobby {steamId.Value} is invalid or missing host metadata.");
                return;
            }

            CurrentLobby = lobby;
            SetFlowState(LobbyFlowState.LobbyJoined, $"Joined lobby \"{lobbyName}\".");

            NetLog.Info($"[Lobby] Joined \"{lobbyName}\" | Host={hostSteamId}");

            // Auto-connect to the host
            if (!string.IsNullOrEmpty(hostSteamId))
            {
                if (GameNetworkManager.Instance != null)
                {
                    SetFlowState(LobbyFlowState.ConnectingToHost, $"Connecting to host {hostSteamId}...");
                    GameNetworkManager.Instance.StartClient(hostSteamId);
                }
                else
                {
                    NetLog.Warn("[Lobby] GameNetworkManager not found – StartClient() skipped.");
                    SetFlowState(LobbyFlowState.Failed, "GameNetworkManager not found.");
                }
            }

            PublishDebugLog($"Joined lobby: {lobby.Id.Value}, visibility={lobby.GetData(KEY_VISIBILITY)}, maxPlayers={lobby.GetData(KEY_MAX_PLAYERS)}");
            OnLobbyJoined?.Invoke(lobby);
        }

        private async void RefreshLobbyListAsync()
        {
            NetLog.Info("[Lobby] Refreshing lobby list...");
            SetFlowState(LobbyFlowState.RefreshingLobbyList, "Refreshing lobby list...");

            // Build query: match app version, at least 1 open slot, capped result count
            Lobby[] lobbies = await new LobbyQuery()
                .WithKeyValue(KEY_APP_VERSION, Application.version)
                .WithSlotsAvailable(1)
                .WithMaxResults(_maxSearchResults)
                .RequestAsync();

            var list = lobbies != null ? new List<Lobby>(lobbies) : new List<Lobby>();
            List<LobbyListItemData> dtoList = BuildLobbyListItemData(list);
            NetLog.Info($"[Lobby] List refreshed: {list.Count} lobbies found.");
            PublishDebugLog($"Lobby list refreshed: {list.Count}");
            OnLobbyListRefreshed?.Invoke(list);
            OnLobbyListDataRefreshed?.Invoke(dtoList);
            SetFlowState(LobbyFlowState.LobbyListReady, $"Lobby list ready: {list.Count} found.");
        }

        // ── Steam Event Subscriptions ──────────────────────────────────────────

        private void SubscribeSteamEvents()
        {
            SteamMatchmaking.OnLobbyMemberJoined       += Steam_OnMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave        += Steam_OnMemberLeave;
            SteamMatchmaking.OnLobbyMemberDisconnected += Steam_OnMemberDisconnected;
        }

        private void UnsubscribeSteamEvents()
        {
            SteamMatchmaking.OnLobbyMemberJoined       -= Steam_OnMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave        -= Steam_OnMemberLeave;
            SteamMatchmaking.OnLobbyMemberDisconnected -= Steam_OnMemberDisconnected;
        }

        private void SubscribeNetworkEvents()
        {
            if (_networkEventsSubscribed || GameNetworkManager.Instance == null)
                return;

            GameNetworkManager.Instance.OnClientStateChanged += HandleClientStateChanged;
            GameNetworkManager.Instance.OnServerStateChanged += HandleServerStateChanged;
            _networkEventsSubscribed = true;
        }

        private void UnsubscribeNetworkEvents()
        {
            if (!_networkEventsSubscribed || GameNetworkManager.Instance == null)
                return;

            GameNetworkManager.Instance.OnClientStateChanged -= HandleClientStateChanged;
            GameNetworkManager.Instance.OnServerStateChanged -= HandleServerStateChanged;
            _networkEventsSubscribed = false;
        }

        // ── Steam Callbacks ────────────────────────────────────────────────────

        private void Steam_OnMemberJoined(Lobby lobby, Friend friend)
        {
            if (!IsCurrentLobby(lobby)) return;
            NetLog.Info($"[Lobby] {friend.Name} joined.");
            PublishDebugLog($"+ {friend.Name} joined. steamId={friend.Id.Value}");
            OnMemberJoined?.Invoke(friend);
        }

        private void Steam_OnMemberLeave(Lobby lobby, Friend friend)
        {
            if (!IsCurrentLobby(lobby)) return;
            NetLog.Info($"[Lobby] {friend.Name} left.");
            PublishDebugLog($"- {friend.Name} left. steamId={friend.Id.Value}");
            OnMemberLeft?.Invoke(friend);
            HandlePotentialHostLeft(lobby, friend);
        }

        private void Steam_OnMemberDisconnected(Lobby lobby, Friend friend)
        {
            if (!IsCurrentLobby(lobby)) return;
            NetLog.Info($"[Lobby] {friend.Name} disconnected.");
            PublishDebugLog($"- {friend.Name} disconnected. steamId={friend.Id.Value}");
            OnMemberLeft?.Invoke(friend);   // treat disconnect same as leave for UI
            HandlePotentialHostLeft(lobby, friend);
        }

        // ── Host Migration Placeholder ─────────────────────────────────────────

        /// <summary>
        /// Detects if the player who left was the host, and if so notifies all
        /// clients to disconnect.
        ///
        /// CURRENT LIMITATION: No host migration. All clients return to menu.
        /// TO UPGRADE: Assign a new owner with lobby.Owner = newOwner; and
        /// update the KEY_HOST_STEAM_ID metadata, then have the new host call
        /// GameNetworkManager.Instance.StartHost().
        /// </summary>
        private void HandlePotentialHostLeft(Lobby lobby, Friend friend)
        {
            string hostIdStr = lobby.GetData(KEY_HOST_STEAM_ID);
            if (string.IsNullOrEmpty(hostIdStr)) return;

            // Check whether the leaving member was the recorded host
            if (friend.Id.Value.ToString() != hostIdStr) return;

            NetLog.Warn("[Lobby] Host has left. Disconnecting all clients (no host migration implemented).");
            PublishDebugLog("Host left.");
            SetFlowState(LobbyFlowState.HostLeft, "Host has left the room.");

            // Local cleanup
            Lobby leaving = CurrentLobby.Value;
            CurrentLobby = null;
            leaving.Leave();

            // Stop networking — let UI / GameFlowManager handle scene transition
            if (GameNetworkManager.Instance != null)
                GameNetworkManager.Instance.StopConnection();

            OnHostLeft?.Invoke();   // UI should navigate back to main menu on this event
            OnLobbyLeft?.Invoke();
            SetFlowState(LobbyFlowState.Idle, "Waiting in menu.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private bool IsCurrentLobby(Lobby lobby)
            => IsInLobby && lobby.Id == CurrentLobby.Value.Id;

        private List<LobbyListItemData> BuildLobbyListItemData(List<Lobby> lobbies)
        {
            List<LobbyListItemData> list = new List<LobbyListItemData>(lobbies.Count);

            foreach (Lobby lobby in lobbies)
            {
                int maxPlayers = lobby.MaxMembers;
                string maxPlayersMetadata = lobby.GetData(KEY_MAX_PLAYERS);
                if (int.TryParse(maxPlayersMetadata, out int parsedMaxPlayers))
                    maxPlayers = parsedMaxPlayers;

                string version = lobby.GetData(KEY_APP_VERSION);
                bool isVersionMatch = string.Equals(version, Application.version, StringComparison.Ordinal);
                bool hasOpenSlot = lobby.MemberCount < maxPlayers;

                list.Add(new LobbyListItemData
                {
                    LobbyId = lobby.Id.Value,
                    LobbyName = lobby.GetData(KEY_LOBBY_NAME),
                    HostSteamId = lobby.GetData(KEY_HOST_STEAM_ID),
                    Version = version,
                    Visibility = lobby.GetData(KEY_VISIBILITY),
                    CurrentPlayers = lobby.MemberCount,
                    MaxPlayers = maxPlayers,
                    HasOpenSlot = hasOpenSlot,
                    IsVersionMatch = isVersionMatch,
                    IsJoinable = hasOpenSlot && isVersionMatch
                });
            }

            return list;
        }

        private void HandleClientStateChanged(LocalConnectionState state)
        {
            switch (state)
            {
                case LocalConnectionState.Started:
                    SetFlowState(LobbyFlowState.Connected, IsLobbyOwner ? "Host connection started." : "Connected to host.");
                    break;
                case LocalConnectionState.Stopped:
                    if (CurrentFlowState == LobbyFlowState.ConnectingToHost)
                        SetFlowState(LobbyFlowState.Failed, "Failed to connect to host.");
                    else if (CurrentFlowState == LobbyFlowState.LeavingLobby || CurrentFlowState == LobbyFlowState.HostLeft)
                        SetFlowState(LobbyFlowState.Idle, "Waiting in menu.");
                    break;
            }
        }

        private void HandleServerStateChanged(LocalConnectionState state)
        {
            if (state == LocalConnectionState.Started && IsLobbyOwner)
                SetFlowState(LobbyFlowState.StartingHost, "Host server started.");
        }

        private void SetFlowState(LobbyFlowState state, string message)
        {
            CurrentFlowState = state;
            CurrentFlowMessage = string.IsNullOrWhiteSpace(message) ? state.ToString() : message;
            PublishDebugLog($"State: {CurrentFlowState} | {CurrentFlowMessage}");
            OnFlowStateChanged?.Invoke(CurrentFlowState, CurrentFlowMessage);
        }

        private void FireCreateFailed(string reason)
        {
            NetLog.Error($"[Lobby] Create failed: {reason}");
            PublishDebugLog($"Create failed: {reason}");
            SetFlowState(LobbyFlowState.Failed, reason);
            OnLobbyCreateFailed?.Invoke(reason);
        }

        private void FireJoinFailed(string reason)
        {
            NetLog.Error($"[Lobby] Join failed: {reason}");
            PublishDebugLog($"Join failed: {reason}");
            SetFlowState(LobbyFlowState.Failed, reason);
            OnLobbyJoinFailed?.Invoke(reason);
        }
    }
}
