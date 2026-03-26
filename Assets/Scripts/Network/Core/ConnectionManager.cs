using System.Text;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
// Add the appropriate using statement for LobbyManager below

public class ConnectionManager : MonoBehaviour
{
    [SerializeField] private NetworkManager _networkManager;

    [Header("Host UI")]
    [SerializeField] private GameObject[] buttonsToHideOnHost;
    [SerializeField] private GameObject hostPanelRoot;
    [SerializeField] private TextMeshProUGUI playersListText;
    [SerializeField] private bool enableDebugLogs = false;

    private bool _serverCallbacksRegistered;
    private bool _lobbySubscribed;

    private void Awake()
    {
        ShowHostPanel(false);
    }

    private void Update()
    {
        TrySubscribeLobbyList();
    }

    private void OnDisable()
    {
        UnsubscribeLobbyList();
    }

    public void StartHost()
    {
        StartServer();
        StartClient();
        ShowHostPanel(true);
        RegisterServerCallbacks();
        RefreshPlayersList();
        StartCoroutine(WaitForLocalAuthAndRefresh());
    }

    public void StartServer()
    {
        _networkManager.ServerManager.StartConnection();
    }

    public void StartClient()
    {
        _networkManager.ClientManager.StartConnection();
        ShowHostPanel(true);
        RefreshPlayersList();
        StartCoroutine(WaitForLobbyAndRefreshClient());
    }

    public void StartGame()
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.RequestStartGame();
    }

    public void Back()
    {
        if (_networkManager != null)
        {
            _networkManager.ClientManager.StopConnection();
            _networkManager.ServerManager.StopConnection(true);
        }

        UnregisterServerCallbacks();
        ShowHostPanel(false);
    }

    public void SetIPAddress(string text)
    {
        _networkManager.TransportManager.Transport.SetClientAddress(text);
    }

    private void RegisterServerCallbacks()
    {
        if (_serverCallbacksRegistered || _networkManager == null)
            return;

        _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        _networkManager.ServerManager.OnAuthenticationResult += OnAuthenticationResult;
        _serverCallbacksRegistered = true;
    }

    private void TrySubscribeLobbyList()
    {
        if (_lobbySubscribed)
            return;

        if (LobbyManager.Instance == null)
            return;

        if (LobbyManager.Instance.NetworkObject == null || !LobbyManager.Instance.NetworkObject.IsSpawned)
        {
            LogDebug("LobbyManager exists but NOT spawned yet.", true);
            return;
        }

        LobbyManager.Instance.Players.OnChange += HandleLobbyPlayersChanged;
        _lobbySubscribed = true;
        LogDebug("Subscribed to LobbyManager.Players.OnChange.", false);
        RefreshPlayersList();
    }


    private void UnsubscribeLobbyList()
    {
        if (!_lobbySubscribed)
            return;

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.Players.OnChange -= HandleLobbyPlayersChanged;
        }

        _lobbySubscribed = false;
    }

    private void HandleLobbyPlayersChanged(SyncListOperation op, int index, LobbyPlayer oldItem, LobbyPlayer newItem, bool asServer)
    {
        LogDebug($"Lobby list changed. Op={op} Count={LobbyManager.Instance?.Players.Count ?? -1}", false);
        RefreshPlayersList();
    }

    private void UnregisterServerCallbacks()
    {
        if (!_serverCallbacksRegistered || _networkManager == null)
            return;

        _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        _networkManager.ServerManager.OnAuthenticationResult -= OnAuthenticationResult;
        _serverCallbacksRegistered = false;
    }

    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started
            || args.ConnectionState == RemoteConnectionState.Stopped)
        {
            Debug.Log($"Connection state changed: {args.ConnectionState} for ClientId={conn.ClientId}");
            RefreshPlayersList();
        }
    }

    private void OnAuthenticationResult(NetworkConnection conn, bool authenticated)
    {
        if (authenticated)
            RefreshPlayersList();
    }

    private void RefreshPlayersList()
    {
        if (playersListText == null || _networkManager == null)
            return;

        bool isServer = _networkManager.IsServerStarted;
        LogDebug($"RefreshPlayersList. isServer={isServer} isClient={_networkManager.IsClientStarted}", true);
        if (LobbyManager.Instance != null)
        {
            bool hasNetworkObject = LobbyManager.Instance.NetworkObject != null;
            string spawnedState = hasNetworkObject ? LobbyManager.Instance.NetworkObject.IsSpawned.ToString() : "NoNetObj";
            LogDebug($"LobbyManager: Spawned={spawnedState} Count={LobbyManager.Instance.Players.Count}", true);
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Players");

        int count = 0;
        if (isServer)
        {
            foreach (NetworkConnection conn in _networkManager.ServerManager.Clients.Values)
            {
                if (conn == null || !conn.IsAuthenticated)
                    continue;

                count++;
                sb.AppendLine($"Player {conn.ClientId}");
            }

            NetworkConnection localConn = _networkManager.ClientManager.Connection;
            if (localConn != null && localConn.IsAuthenticated)
            {
                bool alreadyListed = _networkManager.ServerManager.Clients.ContainsKey(localConn.ClientId);
                if (!alreadyListed)
                {
                    count++;
                    sb.AppendLine($"Player {localConn.ClientId}");
                }
            }
        }
        else if (LobbyManager.Instance != null)
        {
            string summary = LobbyManager.Instance.PlayersListText;
            if (!string.IsNullOrWhiteSpace(summary))
            {
                playersListText.text = summary;
                return;
            }

            LogDebug($"Client lobby count={LobbyManager.Instance.Players.Count}", true);
            for (int i = 0; i < LobbyManager.Instance.Players.Count; i++)
            {
                LobbyPlayer p = LobbyManager.Instance.Players[i];
                count++;
                sb.AppendLine($"{p.DisplayName}");
            }
        }

        if (count == 0)
        {
            sb.AppendLine("(none)");
        }

        playersListText.text = sb.ToString();
    }

    private void ShowHostPanel(bool visible)
    {
        if (buttonsToHideOnHost != null)
        {
            for (int i = 0; i < buttonsToHideOnHost.Length; i++)
            {
                if (buttonsToHideOnHost[i] != null)
                    buttonsToHideOnHost[i].SetActive(!visible);
            }
        }

        if (hostPanelRoot != null)
            hostPanelRoot.SetActive(visible);
    }

    private System.Collections.IEnumerator WaitForLocalAuthAndRefresh()
    {
        float timeout = 3f;
        float startTime = Time.time;

        while (_networkManager != null
            && _networkManager.ClientManager.Connection != null
            && !_networkManager.ClientManager.Connection.IsAuthenticated
            && Time.time - startTime < timeout)
        {
            yield return null;
        }

        RefreshPlayersList();
    }

    private System.Collections.IEnumerator WaitForLobbyAndRefreshClient()
    {
        float timeout = 5f;
        float startTime = Time.time;

        while (Time.time - startTime < timeout)
        {
            bool hasLobby = LobbyManager.Instance != null;
            int count = hasLobby ? LobbyManager.Instance.Players.Count : -1;
            LogDebug($"Client wait: Lobby={hasLobby} Count={count}", false);
            RefreshPlayersList();
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void LogDebug(string message, bool throttled)
    {
        if (!enableDebugLogs)
            return;

        if (throttled && (Time.frameCount % 60) != 0)
            return;

        Debug.Log($"[ConnectionManager] {message}");
    }
}
