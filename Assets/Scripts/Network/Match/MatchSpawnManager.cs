using System.Collections;
using BuddahGo.Match;
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

        private readonly List<NetworkObject> _spawnedAI = new List<NetworkObject>();

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

            int required = MatchRules.Current.ReturnTarget == MatchReturnTarget.MainMenuHome
                ? 1 + (SessionControl.SoloSettings?.AICount ?? 0) : _spawnedPlayers.Count + 1;
            CacheSpawnPointsIfNeeded();
            if (_spawnPoints == null || _spawnPoints.Length < required)
                throw new System.InvalidOperationException("Not enough unique racer spawn points.");
            Transform spawnPoint = GetSpawnPoint(_spawnedPlayers.Count);
            Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

            NetworkObject playerInstance = Instantiate(_playerPrefab, spawnPosition, spawnRotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(playerInstance.gameObject, gameObject.scene);
            var identity = playerInstance.GetComponent<RacerIdentity>();
            if (identity == null) throw new System.InvalidOperationException("Racer prefab requires RacerIdentity.");
            identity.AssignBeforeSpawn(RacerId.FromClient(conn.ClientId),
                MatchRules.Current.ReturnTarget == MatchReturnTarget.MainMenuHome ? MatchRules.Current.DefaultPlayerName : "Player " + conn.ClientId);
            InstanceFinder.ServerManager.Spawn(playerInstance, conn);
            ApplyResolvedSkillLoadout(playerInstance, conn.ClientId);
            _spawnedPlayers[conn.ClientId] = playerInstance;
            SpawnSoloAI();

            // Validation log for race-readiness pipeline (disabled by default).
            DebugLog($"[Spawn] Player ready for conn {conn.ClientId}");
        }

        private void SpawnSoloAI()
        {
            if (MatchRules.Current.ReturnTarget != MatchReturnTarget.MainMenuHome) return;
            var settings = SessionControl.SoloSettings;
            int count = settings?.AICount ?? 0;
            while (_spawnedAI.Count < count)
            {
                int index = _spawnedAI.Count;
                var point = GetSpawnPoint(index + 1);
                var racer = Instantiate(_playerPrefab, point.position, point.rotation);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(racer.gameObject, gameObject.scene);
                string name = index < settings.AINames.Count ? settings.AINames[index] : "AI " + (index + 1);
                racer.GetComponent<RacerIdentity>().AssignBeforeSpawn(RacerId.ForAI(index), name);
                racer.name = "AI Racer " + (index + 1);
                InstanceFinder.ServerManager.Spawn(racer); // Empty owner, never a synthetic client.
                _spawnedAI.Add(racer);
            }
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

            if (spawnIndex < 0 || spawnIndex >= _spawnPoints.Length)
                throw new System.ArgumentOutOfRangeException(nameof(spawnIndex));
            MatchSpawnPoint spawnPoint = _spawnPoints[spawnIndex];
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

        private static void DebugLog(string message)
        {
            if (NetDebug.EnableVerboseLog)
                GameLog.Verbose(message);
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

            GameLog.Verbose(
                $"[MatchSpawnManager] Applied skill loadout for player {playerId}. " +
                $"resolved={appliedResolvedLoadout}, slots=('{skillLoadout.GetSkillId(0)}','{skillLoadout.GetSkillId(1)}','{skillLoadout.GetSkillId(2)}')");
        }
    }
}
