using System;
using System.Collections.Generic;
using System.Text;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using FishNet;
using UnityEngine;

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("Scene")]
    [SerializeField] private string raceSceneName = "RaceMap";

    [Header("Rules")]
    [SerializeField] private bool requireAllReady = false;
    [SerializeField] private bool enableDebugLogs = false;

    public readonly SyncList<LobbyPlayer> Players = new SyncList<LobbyPlayer>();
    private readonly SyncVar<bool> _gameStarted = new SyncVar<bool>();
    private readonly SyncVar<string> _playersListText = new SyncVar<string>();

    public bool GameStarted => _gameStarted.Value;
    public string PlayersListText => _playersListText.Value;

    private void Awake()
    {
        // Avoid destroying network objects in Awake.
    }
    public override void OnStartClient()
    {
        base.OnStartClient();
        Instance = this; // 客户端以网络对象为准
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (Instance == this) Instance = null;
    }


    public override void OnStartServer()
    {
        base.OnStartServer();
        Instance = this; // Server-side singleton reference.
        LogDebug("OnStartServer", false);

        if (InstanceFinder.ServerManager != null)
        {
            InstanceFinder.ServerManager.OnAuthenticationResult += OnAuthenticationResult;
            InstanceFinder.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;

            foreach (KeyValuePair<int, NetworkConnection> kvp in InstanceFinder.ServerManager.Clients)
            {
                NetworkConnection conn = kvp.Value;
                if (conn != null && conn.IsAuthenticated)
                {
                    AddOrUpdatePlayer(conn);
                }
            }
        }

        _gameStarted.Value = false;
        UpdatePlayersListText();

        InvokeRepeating(nameof(RescanAuthedClients), 0.2f, 0.5f);
    }

    public override void OnStopServer()
    {
        base.OnStopServer();

        if (InstanceFinder.ServerManager != null)
        {
            InstanceFinder.ServerManager.OnAuthenticationResult -= OnAuthenticationResult;
            InstanceFinder.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        }

        CancelInvoke(nameof(RescanAuthedClients));

        Players.Clear();
        _gameStarted.Value = false;
        _playersListText.Value = string.Empty;
    }

    public void RequestStartGame()
    {
        if (!IsClientInitialized)
            return;

        StartGameServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartGameServerRpc(NetworkConnection caller = null)
    {
        if (_gameStarted.Value)
            return;

        if (requireAllReady && !AreAllPlayersReady())
            return;

        LoadRaceScene();
        _gameStarted.Value = true;
    }

    public void RequestToggleReady()
    {
        if (!IsClientInitialized)
            return;

        ToggleReadyServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleReadyServerRpc(NetworkConnection caller = null)
    {
        if (caller == null || !caller.IsAuthenticated)
            return;

        int index = FindPlayerIndex(caller.ClientId);
        if (index == -1)
        {
            AddOrUpdatePlayer(caller);
            index = FindPlayerIndex(caller.ClientId);
        }

        if (index == -1)
            return;

        LobbyPlayer player = Players[index];
        player.Ready = !player.Ready;
        Players[index] = player;
    }

    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            AddOrUpdatePlayer(conn);
        }
        else if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            RemovePlayer(conn.ClientId);
        }
    }

    private void OnAuthenticationResult(NetworkConnection conn, bool authenticated)
    {
        if (!authenticated)
            return;

        AddOrUpdatePlayer(conn);
    }

    [Server]
    private void RescanAuthedClients()
    {
        if (InstanceFinder.ServerManager == null)
            return;

        foreach (KeyValuePair<int, NetworkConnection> kvp in InstanceFinder.ServerManager.Clients)
        {
            NetworkConnection conn = kvp.Value;
            if (conn != null && conn.IsAuthenticated)
            {
                AddOrUpdatePlayer(conn);
            }
        }
    }

    [Server]
    private void AddOrUpdatePlayer(NetworkConnection conn)
    {
        if (conn == null)
            return;

        int index = FindPlayerIndex(conn.ClientId);
        LobbyPlayer player = new LobbyPlayer
        {
            ClientId = conn.ClientId,
            DisplayName = $"Player {conn.ClientId}",
            Ready = false
        };

        if (index == -1)
        {
            Players.Add(player);
            LogDebug($"AddPlayer {conn.ClientId}. Count={Players.Count}", false);
        }
        else
        {
            Players[index] = player;
            LogDebug($"UpdatePlayer {conn.ClientId}. Count={Players.Count}", false);
        }

        UpdatePlayersListText();
    }

    [Server]
    private void RemovePlayer(int clientId)
    {
        int index = FindPlayerIndex(clientId);
        if (index != -1)
        {
            Players.RemoveAt(index);
            LogDebug($"RemovePlayer {clientId}. Count={Players.Count}", false);
            UpdatePlayersListText();
        }
    }

    private void LogDebug(string message, bool throttled)
    {
        if (!enableDebugLogs)
            return;

        if (throttled && (Time.frameCount % 60) != 0)
            return;

        Debug.Log($"[LobbyManager] {message}");
    }

    private bool AreAllPlayersReady()
    {
        if (Players.Count == 0)
            return false;

        for (int i = 0; i < Players.Count; i++)
        {
            if (!Players[i].Ready)
                return false;
        }

        return true;
    }

    private int FindPlayerIndex(int clientId)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].ClientId == clientId)
                return i;
        }

        return -1;
    }

    private void LoadRaceScene()
    {
        if (!IsServerInitialized)
            return;

        if (InstanceFinder.SceneManager == null)
            return;

        SceneLoadData sld = new SceneLoadData(raceSceneName)
        {
            ReplaceScenes = ReplaceOption.All
        };

        InstanceFinder.SceneManager.LoadGlobalScenes(sld);
    }

    private void UpdatePlayersListText()
    {
        if (!IsServerInitialized)
            return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Players");

        if (Players.Count == 0)
        {
            sb.AppendLine("(none)");
            _playersListText.Value = sb.ToString();
            return;
        }

        for (int i = 0; i < Players.Count; i++)
        {
            sb.AppendLine(Players[i].DisplayName);
        }

        _playersListText.Value = sb.ToString();
    }
}

[Serializable]
public struct LobbyPlayer : IEquatable<LobbyPlayer>
{
    public int ClientId;
    public string DisplayName;
    public bool Ready;

    public bool Equals(LobbyPlayer other)
    {
        return ClientId == other.ClientId
            && DisplayName == other.DisplayName
            && Ready == other.Ready;
    }

    public override bool Equals(object obj)
    {
        return obj is LobbyPlayer other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ClientId, DisplayName, Ready);
    }
}
