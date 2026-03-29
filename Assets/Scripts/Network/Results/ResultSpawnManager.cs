using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using UnityEngine;

namespace SteamMultiplayer.Network.Results
{
    public class ResultSpawnManager : MonoBehaviour
    {
        [Header("Network Spawn")]
        [SerializeField] private NetworkObject _playerPrefab;
        [SerializeField] private EndFieldSpawnPoint[] _spawnPoints;

        [Header("Fallback")]
        [SerializeField] private Vector3 _overflowSpawnOffset = new Vector3(2.5f, 0f, 0f);
        [SerializeField] private Vector3 _missingResultOffset = new Vector3(0f, 0f, -3f);

        private readonly Dictionary<int, NetworkObject> _spawnedPlayers = new Dictionary<int, NetworkObject>();
        private readonly HashSet<int> _warnedMissingResultClients = new HashSet<int>();
        private bool _warnedMissingSpawnPoints;
        private bool _warnedOverflowSpawnPoints;

        private void Awake()
        {
            CacheSpawnPointsIfNeeded();
        }

        private void OnEnable()
        {
            if (InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;

            if (InstanceFinder.SceneManager != null)
                InstanceFinder.SceneManager.OnClientPresenceChangeEnd += HandleClientPresenceChangeEnd;
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
                InstanceFinder.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;

            if (InstanceFinder.SceneManager != null)
                InstanceFinder.SceneManager.OnClientPresenceChangeEnd -= HandleClientPresenceChangeEnd;
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

        private void HandleRemoteConnectionState(NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args)
        {
            if (!InstanceFinder.IsServerStarted || conn == null)
                return;

            if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Started)
            {
                TrySpawnPlayer(conn);
                return;
            }

            if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped)
                DespawnPlayer(conn.ClientId);
        }

        private void SpawnForAllConnectedPlayers()
        {
            if (!InstanceFinder.IsServerStarted || InstanceFinder.ServerManager == null)
                return;

            foreach (KeyValuePair<int, NetworkConnection> kvp in InstanceFinder.ServerManager.Clients)
                TrySpawnPlayer(kvp.Value);
        }

        private void TrySpawnPlayer(NetworkConnection conn)
        {
            if (!InstanceFinder.IsServerStarted || conn == null || !conn.IsAuthenticated)
                return;

            if (_playerPrefab == null)
            {
                Debug.LogWarning("[ResultSpawnManager] Player prefab is not assigned.");
                return;
            }

            if (_spawnedPlayers.ContainsKey(conn.ClientId))
                return;

            ResolveSpawnTransform(conn.ClientId, out Vector3 spawnPosition, out Quaternion spawnRotation);

            NetworkObject playerInstance = Instantiate(_playerPrefab, spawnPosition, spawnRotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(playerInstance.gameObject, gameObject.scene);
            InstanceFinder.ServerManager.Spawn(playerInstance, conn);
            ApplyResolvedSkillLoadout(playerInstance, conn.ClientId);
            _spawnedPlayers[conn.ClientId] = playerInstance;
        }

        private void ResolveSpawnTransform(int clientId, out Vector3 spawnPosition, out Quaternion spawnRotation)
        {
            CacheSpawnPointsIfNeeded();

            if (_spawnPoints == null || _spawnPoints.Length == 0)
            {
                if (!_warnedMissingSpawnPoints)
                {
                    Debug.LogWarning("[ResultSpawnManager] No EndFieldSpawnPoint found in ResultScene. Falling back to manager transform positions.");
                    _warnedMissingSpawnPoints = true;
                }

                int fallbackIndex = _spawnedPlayers.Count;
                spawnPosition = transform.position + (_overflowSpawnOffset * fallbackIndex);
                spawnRotation = transform.rotation;
                return;
            }

            if (!MatchResultCache.TryGetPlacementIndexForClient(clientId, out int placementIndex))
            {
                if (_warnedMissingResultClients.Add(clientId))
                {
                    Debug.LogWarning($"[ResultSpawnManager] Missing cached match result for client {clientId}. Spawning at fallback result position.");
                }

                EndFieldSpawnPoint anchor = _spawnPoints[0];
                spawnPosition = anchor.transform.position + anchor.transform.TransformDirection(_missingResultOffset * _warnedMissingResultClients.Count);
                spawnRotation = anchor.transform.rotation;
                return;
            }

            int spawnPointIndex = Mathf.Clamp(placementIndex, 0, _spawnPoints.Length - 1);
            EndFieldSpawnPoint spawnPoint = _spawnPoints[spawnPointIndex];
            spawnPosition = spawnPoint.transform.position;
            spawnRotation = spawnPoint.transform.rotation;

            if (placementIndex < _spawnPoints.Length)
                return;

            if (!_warnedOverflowSpawnPoints)
            {
                Debug.LogWarning($"[ResultSpawnManager] Result player count exceeds EndFieldSpawnPoint count. Using overflow offset from the last spawn point. players={MatchResultCache.Results.Count} spawnPoints={_spawnPoints.Length}");
                _warnedOverflowSpawnPoints = true;
            }

            int overflowIndex = placementIndex - (_spawnPoints.Length - 1);
            spawnPosition += spawnPoint.transform.TransformDirection(_overflowSpawnOffset * overflowIndex);
        }

        private void DespawnPlayer(int clientId)
        {
            if (!_spawnedPlayers.TryGetValue(clientId, out NetworkObject player) || player == null)
            {
                _spawnedPlayers.Remove(clientId);
                return;
            }

            if (player.IsSpawned && InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.Despawn(player);
            else
                Destroy(player.gameObject);

            _spawnedPlayers.Remove(clientId);
        }

        private void CacheSpawnPointsIfNeeded()
        {
            if (_spawnPoints == null || _spawnPoints.Length == 0)
                _spawnPoints = GetComponentsInChildren<EndFieldSpawnPoint>(true);

            SortSpawnPoints();
        }

        private void OnValidate()
        {
            _spawnPoints = GetComponentsInChildren<EndFieldSpawnPoint>(true);
            SortSpawnPoints();
        }

        private void SortSpawnPoints()
        {
            _spawnPoints = (_spawnPoints ?? new EndFieldSpawnPoint[0])
                .OrderBy(point => point != null ? point.SpawnOrderIndex : int.MaxValue)
                .ThenBy(point => point != null ? point.name : string.Empty)
                .ToArray();
        }

        private void ApplyResolvedSkillLoadout(NetworkObject playerInstance, int playerId)
        {
            if (playerInstance == null)
                return;

            SkillLoadout skillLoadout = playerInstance.GetComponent<SkillLoadout>();
            if (skillLoadout == null)
                skillLoadout = playerInstance.GetComponentInChildren<SkillLoadout>(true);

            if (skillLoadout == null)
                return;

            bool appliedResolvedLoadout = skillLoadout.TryApplyResolvedSelectionServer(playerId);
            if (!appliedResolvedLoadout)
                skillLoadout.ApplyDefaultSkillsServer();
        }
    }
}
