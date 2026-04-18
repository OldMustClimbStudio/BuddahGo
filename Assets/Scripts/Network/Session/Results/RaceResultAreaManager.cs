using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using SteamMultiplayer.Network;
using UnityEngine;

namespace SteamMultiplayer.Network.Results
{
    [RequireComponent(typeof(NetworkObject))]
    public class RaceResultAreaManager : NetworkBehaviour
    {
        public static RaceResultAreaManager Instance { get; private set; }

        [Header("Result Area Anchors")]
        [SerializeField] private Transform anchorRoot;
        [SerializeField] private bool autoCollectAnchorsFromChildren = true;
        [SerializeField] private EndFieldSpawnPoint[] resultAnchors;

        [Header("Fallback")]
        [SerializeField] private Vector3 overflowOffset = new Vector3(2.5f, 0f, 0f);
        [SerializeField] private Vector3 missingPlayerOffset = new Vector3(0f, 0f, -3f);

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        private readonly HashSet<int> _warnedMissingPlayers = new HashSet<int>();
        private readonly HashSet<int> _placedClientIds = new HashSet<int>();
        private bool _warnedMissingAnchors;
        private bool _warnedOverflowAnchors;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[RaceResultAreaManager] Duplicate instance detected during Awake. Keeping scene NetworkObject alive and allowing network lifecycle to resolve the active instance.");
                return;
            }

            CacheAnchorsIfNeeded();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Instance = this;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _placedClientIds.Clear();
            _warnedMissingAnchors = false;
            _warnedOverflowAnchors = false;
            _warnedMissingPlayers.Clear();
            if (Instance == this)
                Instance = null;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Instance == this)
                Instance = null;
        }

        [Server]
        public bool TryPlacePlayersInResultAreaServer(IReadOnlyList<FinalMatchResultEntry> finalResults)
        {
            if (!IsServerInitialized)
                return false;

            CacheAnchorsIfNeeded();

            int placedCount = 0;
            int resultCount = finalResults != null ? finalResults.Count : 0;
            for (int i = 0; i < resultCount; i++)
            {
                FinalMatchResultEntry result = finalResults[i];
                if (result == null)
                    continue;

                if (!TryFindReporter(result.ClientId, out PlayerProgressReporter reporter))
                {
                    if (_warnedMissingPlayers.Add(result.ClientId))
                    {
                        Debug.LogWarning($"[RaceResultAreaManager] Could not find live player object for client {result.ClientId} during result-area placement.");
                    }

                    continue;
                }

                if (ApplyPlacementToPlayer(reporter, i))
                    placedCount++;
            }

            DebugLog($"Placed {placedCount}/{resultCount} players into the in-scene result area.");

            return true;
        }

        [Server]
        public bool TryGetPlacementTransform(int placementIndex, out Vector3 worldPosition, out Quaternion worldRotation)
        {
            if (!IsServerInitialized)
            {
                worldPosition = default;
                worldRotation = default;
                return false;
            }

            ResolvePlacementTransform(placementIndex, out worldPosition, out worldRotation);
            return true;
        }

        [Server]
        public bool ApplyPlacementToPlayer(PlayerProgressReporter reporter, int placementIndex)
        {
            if (!IsServerInitialized || reporter == null)
                return false;

            if (_placedClientIds.Contains(reporter.OwnerId))
                return false;

            ResolvePlacementTransform(placementIndex, out Vector3 worldPosition, out Quaternion worldRotation);
            reporter.EnterResultAreaServer(worldPosition, worldRotation);
            _placedClientIds.Add(reporter.OwnerId);
            DebugLog($"Placed clientId={reporter.OwnerId} placementIndex={placementIndex}");
            return true;
        }

        [Server]
        public void NotifyPlacementApplied(int clientId)
        {
            if (!IsServerInitialized)
                return;

            _placedClientIds.Add(clientId);
        }

        [Server]
        public bool HasPlacementApplied(int clientId)
        {
            return IsServerInitialized && _placedClientIds.Contains(clientId);
        }

        private bool TryFindReporter(int clientId, out PlayerProgressReporter reporter)
        {
            PlayerProgressReporter[] reporters = FindObjectsByType<PlayerProgressReporter>(FindObjectsSortMode.None);
            for (int i = 0; i < reporters.Length; i++)
            {
                PlayerProgressReporter candidate = reporters[i];
                if (candidate == null || !candidate.IsSpawned || candidate.OwnerId != clientId)
                    continue;

                reporter = candidate;
                return true;
            }

            reporter = null;
            return false;
        }

        private void ResolvePlacementTransform(int placementIndex, out Vector3 worldPosition, out Quaternion worldRotation)
        {
            CacheAnchorsIfNeeded();

            if (resultAnchors == null || resultAnchors.Length == 0)
            {
                if (!_warnedMissingAnchors)
                {
                    Debug.LogWarning("[RaceResultAreaManager] No EndFieldSpawnPoint found in RaceMap result area. Falling back to manager transform.");
                    _warnedMissingAnchors = true;
                }

                worldPosition = transform.position + (overflowOffset * placementIndex);
                worldRotation = transform.rotation;
                return;
            }

            int anchorIndex = Mathf.Clamp(placementIndex, 0, resultAnchors.Length - 1);
            EndFieldSpawnPoint anchor = resultAnchors[anchorIndex];
            if (anchor == null)
            {
                worldPosition = transform.position + (missingPlayerOffset * (placementIndex + 1));
                worldRotation = transform.rotation;
                return;
            }

            worldPosition = anchor.transform.position;
            worldRotation = anchor.transform.rotation;

            if (placementIndex < resultAnchors.Length)
                return;

            if (!_warnedOverflowAnchors)
            {
                Debug.LogWarning($"[RaceResultAreaManager] Result count exceeds result anchors. Applying overflow offset. placements={placementIndex + 1} anchors={resultAnchors.Length}");
                _warnedOverflowAnchors = true;
            }

            int overflowIndex = placementIndex - (resultAnchors.Length - 1);
            worldPosition += anchor.transform.TransformDirection(overflowOffset * overflowIndex);
        }

        private void CacheAnchorsIfNeeded()
        {
            if ((resultAnchors == null || resultAnchors.Length == 0) && autoCollectAnchorsFromChildren)
            {
                Transform searchRoot = anchorRoot != null ? anchorRoot : transform;
                resultAnchors = searchRoot.GetComponentsInChildren<EndFieldSpawnPoint>(true);
            }

            resultAnchors = (resultAnchors ?? new EndFieldSpawnPoint[0])
                .Where(anchor => anchor != null)
                .OrderBy(anchor => anchor.SpawnOrderIndex)
                .ThenBy(anchor => anchor.name)
                .ToArray();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (autoCollectAnchorsFromChildren)
            {
                Transform searchRoot = anchorRoot != null ? anchorRoot : transform;
                resultAnchors = searchRoot.GetComponentsInChildren<EndFieldSpawnPoint>(true);
            }

            CacheAnchorsIfNeeded();
        }

        private void DebugLog(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[RaceResultAreaManager] {message}");
        }
    }
}
