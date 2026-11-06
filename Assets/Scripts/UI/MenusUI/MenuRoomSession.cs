using System;
using System.Collections.Generic;
using BuddahGo.Match;
using SteamMultiplayer.Network;
using Steamworks.Data;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// What the Dojo page needs from the network, in one place: the Steam lobby (create, list, join by room number,
    /// leave) and the room roster (seats, ready, start). Online means a Steam lobby whose room is spawned; without
    /// Steam the hall still opens and races solo. The page reads state every frame and calls the actions; the
    /// events tell it when to walk into or out of the hall.
    /// </summary>
    public sealed class MenuRoomSession : IDisposable
    {
        private static readonly IReadOnlyList<RoomPlayerState> NoPlayers = Array.Empty<RoomPlayerState>();

        private SteamLobbyManager _subscribed;
        private readonly List<LobbyListItemData> _listed = new List<LobbyListItemData>();
        private bool _switchingToSolo;

        /// <summary>A lobby was created or joined: walk into the hall.</summary>
        public event Action Entered;
        /// <summary>The lobby is gone (left, host left, or a failed connect): walk out. Carries a notice or "".</summary>
        public event Action<string> Exited;
        /// <summary>A create or join failed before entering: show why.</summary>
        public event Action<string> Failed;
        /// <summary>The room list came back.</summary>
        public event Action ListChanged;

        private static SteamLobbyManager Lobby => SteamLobbyManager.Instance;
        private static RoomStateManager Room => RoomStateManager.Instance != null && RoomStateManager.Instance.IsClientInitialized ? RoomStateManager.Instance : null;

        /// <summary>Steam is up: rooms can be created and joined.</summary>
        public bool CanGoOnline => SessionControl.Current != null && SessionControl.Current.IsOnlineAvailable && Lobby != null;
        public string OfflineReason => SessionControl.Current == null ? "初始化尚未完成，请稍后重试" : SessionLauncher.SteamUnavailableMessage;

        public bool InLobby => Lobby != null && Lobby.IsInLobby;
        /// <summary>In a lobby and the room's roster has reached this client.</summary>
        public bool IsOnline => InLobby && Room != null;
        public bool IsHost => !InLobby || Lobby.IsLobbyOwner;

        public IReadOnlyList<RoomPlayerState> Players => IsOnline ? (IReadOnlyList<RoomPlayerState>)Room.Players : NoPlayers;
        public int PlayerCount => IsOnline ? Math.Max(1, Room.Players.Count) : 1;
        public bool CanHostStart => IsOnline && Room.CanHostStartGame;
        public bool IsStartingMatch => IsOnline && Room.CurrentMatchSessionPhase != MatchSessionPhase.InRoom;

        public string RoomName => InLobby ? Lobby.CurrentLobbyName : string.Empty;
        public string RoomCode => InLobby ? Lobby.CurrentRoomCode : string.Empty;
        public int Capacity => InLobby ? Lobby.CurrentMaxMembers : 0;
        public LobbyVisibility Visibility => InLobby ? Lobby.CurrentVisibility : LobbyVisibility.Public;
        public string MapId => Room != null ? Room.MapId : LocalMatchSelection.MapId;

        public IReadOnlyList<LobbyListItemData> Listed => _listed;

        public bool TryGetLocalPlayer(out RoomPlayerState player)
        {
            player = default;
            return IsOnline && Room.TryGetLocalPlayer(out player);
        }

        public bool IsLocal(RoomPlayerState player) => TryGetLocalPlayer(out RoomPlayerState local) && local.PlayerId == player.PlayerId;

        /// <summary>Hooks the lobby events; call every frame (the lobby manager can come up after the page).</summary>
        public void Tick()
        {
            SteamLobbyManager lobby = Lobby;
            if (lobby == _subscribed) return;
            Unsubscribe();
            _subscribed = lobby;
            if (lobby == null) return;
            lobby.OnLobbyCreated += HandleEntered;
            lobby.OnLobbyJoined += HandleEntered;
            lobby.OnLobbyCreateFailed += HandleFailed;
            lobby.OnLobbyJoinFailed += HandleFailed;
            lobby.OnLobbyLeft += HandleLeft;
            lobby.OnHostLeft += HandleHostLeft;
            lobby.OnLobbyListDataRefreshed += HandleList;
        }

        public void Dispose() => Unsubscribe();

        private void Unsubscribe()
        {
            if (_subscribed == null) return;
            _subscribed.OnLobbyCreated -= HandleEntered;
            _subscribed.OnLobbyJoined -= HandleEntered;
            _subscribed.OnLobbyCreateFailed -= HandleFailed;
            _subscribed.OnLobbyJoinFailed -= HandleFailed;
            _subscribed.OnLobbyLeft -= HandleLeft;
            _subscribed.OnHostLeft -= HandleHostLeft;
            _subscribed.OnLobbyListDataRefreshed -= HandleList;
            _subscribed = null;
        }

        // ---------- actions ----------

        public void CreateRoom(string name, int capacity)
        {
            Tick();
            Lobby?.CreateLobby(name, LobbyVisibility.Public, capacity);
        }

        public void RefreshList()
        {
            Tick();
            if (CanGoOnline) Lobby.RefreshLobbyList();
        }

        public void JoinByCode(string code)
        {
            Tick();
            Lobby?.JoinLobbyByCode(code);
        }

        public void JoinListed(LobbyListItemData room)
        {
            Tick();
            Lobby?.JoinLobbyById(room.LobbyId);
        }

        public void Leave()
        {
            if (InLobby) Lobby.LeaveLobby();
        }

        public void ToggleReady() => Room?.RequestToggleReady();

        public void StartMatch() => Room?.RequestStartGame();

        /// <summary>
        /// Solo race. Alone in an online room the room is closed first (the Steam lobby left, the session stopped)
        /// and the solo host starts right after, without leaving the menu.
        /// </summary>
        public bool StartSolo(SoloMatchSettings settings, out string error)
        {
            error = string.Empty;
            ISessionControl session = SessionControl.Current;
            if (session == null) { error = OfflineReason; return false; }
            if (!InLobby)
            {
                if (session.StartSoloHost(settings)) return true;
                error = string.IsNullOrEmpty(session.LastError) ? "启动失败，请重试" : session.LastError;
                return false;
            }
            _switchingToSolo = true;
            session.QueueSoloAfterStop(settings);
            Lobby.LeaveLobby();
            return true;
        }

        // ---------- lobby events ----------

        private void HandleEntered(Lobby lobby) => Entered?.Invoke();

        private void HandleFailed(string reason) => Failed?.Invoke(Translate(reason));

        private void HandleLeft()
        {
            if (_switchingToSolo) { _switchingToSolo = false; return; }
            Exited?.Invoke(string.Empty);
        }

        private void HandleHostLeft() => Exited?.Invoke("房主已离开，房间解散");

        private void HandleList(List<LobbyListItemData> rooms)
        {
            _listed.Clear();
            foreach (LobbyListItemData room in rooms)
                if (room.IsJoinable) _listed.Add(room);
            ListChanged?.Invoke();
        }

        private static string Translate(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return "连接失败，请重试";
            if (reason.StartsWith("No open room")) return "没有找到这个房号的公开房间";
            if (reason.StartsWith("Room code")) return "房号是四位";
            if (reason.Contains("protocol mismatch")) return "对方的游戏版本不同，无法加入";
            if (reason.Contains("Steam is not initialized")) return SessionLauncher.SteamUnavailableMessage;
            if (reason.Contains("full") || reason.Contains("gone")) return "房间已满或已解散";
            return "连接失败：" + reason;
        }
    }
}
