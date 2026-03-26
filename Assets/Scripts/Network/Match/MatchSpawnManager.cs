using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace SteamMultiplayer.Network.Match
{
    public class MatchSpawnManager : MonoBehaviour
    {
        [SerializeField] private NetworkObject _playerPrefab;
        [SerializeField] private MatchSpawnPoint[] _spawnPoints;

        private readonly Dictionary<int, NetworkObject> _spawnedPlayers = new Dictionary<int, NetworkObject>();

        private void Awake()
        {
            CacheSpawnPointsIfNeeded();
        }

        private void OnEnable()
        {
            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
            }

            if (InstanceFinder.SceneManager != null)
            {
                InstanceFinder.SceneManager.OnClientPresenceChangeEnd += HandleClientPresenceChangeEnd;
            }
        }

        private void Start()
        {
            if (!InstanceFinder.IsServerStarted)
                return;

            StartCoroutine(SpawnExistingPlayersNextFrame());
        }

        private void OnDisable()
        {
            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
            }

            if (InstanceFinder.SceneManager != null)
            {
                InstanceFinder.SceneManager.OnClientPresenceChangeEnd -= HandleClientPresenceChangeEnd;
            }
        }

        private IEnumerator SpawnExistingPlayersNextFrame()
        {
            yield return null;
            SpawnForAllConnectedPlayers();
        }

        private void HandleClientPresenceChangeEnd(ClientPresenceChangeEventArgs args)
        {
            if (!InstanceFinder.IsServerStarted || !args.Added)
                return;

            if (args.Scene != gameObject.scene)
                return;

            TrySpawnPlayer(args.Connection);
        }

        private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (!InstanceFinder.IsServerStarted || conn == null)
                return;

            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                TrySpawnPlayer(conn);
                return;
            }

            if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                DespawnPlayer(conn.ClientId);
            }
        }

        private void SpawnForAllConnectedPlayers()
        {
            if (!InstanceFinder.IsServerStarted || InstanceFinder.ServerManager == null)
                return;

            foreach (KeyValuePair<int, NetworkConnection> kvp in InstanceFinder.ServerManager.Clients)
            {
                TrySpawnPlayer(kvp.Value);
            }
        }

        private void TrySpawnPlayer(NetworkConnection conn)
        {
            if (!InstanceFinder.IsServerStarted || conn == null || !conn.IsAuthenticated)
                return;

            if (_playerPrefab == null)
            {
                Debug.LogWarning("[MatchSpawnManager] Placeholder player prefab is not assigned.");
                return;
            }

            if (_spawnedPlayers.ContainsKey(conn.ClientId))
                return;

            Transform spawnPoint = GetSpawnPoint(_spawnedPlayers.Count);
            Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

            NetworkObject playerInstance = Instantiate(_playerPrefab, spawnPosition, spawnRotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(playerInstance.gameObject, gameObject.scene);
            InstanceFinder.ServerManager.Spawn(playerInstance, conn);
            _spawnedPlayers[conn.ClientId] = playerInstance;
        }

        private void DespawnPlayer(int clientId)
        {
            if (!_spawnedPlayers.TryGetValue(clientId, out NetworkObject player) || player == null)
            {
                _spawnedPlayers.Remove(clientId);
                return;
            }

            if (player.IsSpawned && InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.Despawn(player);
            }
            else
            {
                Destroy(player.gameObject);
            }

            _spawnedPlayers.Remove(clientId);
        }

        private Transform GetSpawnPoint(int spawnIndex)
        {
            CacheSpawnPointsIfNeeded();

            if (_spawnPoints == null || _spawnPoints.Length == 0)
                return null;

            MatchSpawnPoint spawnPoint = _spawnPoints[spawnIndex % _spawnPoints.Length];
            return spawnPoint != null ? spawnPoint.transform : null;
        }

        private void CacheSpawnPointsIfNeeded()
        {
            if (_spawnPoints != null && _spawnPoints.Length > 0)
                return;

            _spawnPoints = GetComponentsInChildren<MatchSpawnPoint>(true);
        }

        private void OnValidate()
        {
            _spawnPoints = GetComponentsInChildren<MatchSpawnPoint>(true);
        }
    }
}
