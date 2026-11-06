using System.Collections;
using BuddahGo.Match;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace SteamMultiplayer.Network.Match
{
    public class MatchSpawnManager : MonoBehaviour
    {
        [SerializeField] private NetworkObject _playerPrefab;
        [SerializeField] private MatchSpawnPoint[] _spawnPoints;

        // Practice training dummies (README A1). Separate from _spawnPoints, which stays the six racer slots. Dummy i stands
        // at d0 + start offset + spacing * i along the race direction, where d0 is the human launch point projected onto
        // TrackSplineRef, spread left / centre / right. Count, spacing, start offset, lateral offset and reset delay come
        // from the Match/ModeRule Practice row (TrainingDummyModeRuleMap); the serialized values are the fallback when the
        // table has no row. The placement probes below are engineering values and stay on the component.
        [Header("Practice Training Dummies")]
        [SerializeField, Min(0)] private int _trainingDummyCount = 3;
        [SerializeField, Min(0f)] private float _trainingDummySpacingMeters = 15f;
        [Tooltip("Track distance from the projected launch point to the first dummy. On RaceMap the launch point projects about 37 m before the start line, so 90 m puts the dummies about 53, 68 and 83 m past the line: on the first straight, clear of the intro flight (which ends at the launch point) and of the first second of the fully physical launch (about 60 m at the 60 m/s intro speed).")]
        [SerializeField] private float _trainingDummyStartOffsetMeters = 90f;
        [Tooltip("Side offset of the left and right dummies from the racing line. The racer body is about 7.8 m wide; each side is also clamped by the wall measured on that side.")]
        [SerializeField, Min(0f)] private float _trainingDummyLateralOffsetMeters = 10f;
        [Tooltip("Minimum gap between a side dummy centre and the wall on its side: body radius (about 3.9 m) plus a margin.")]
        [SerializeField, Min(0f)] private float _trainingDummyWallClearanceMeters = 5f;
        [SerializeField, Min(0f)] private float _trainingDummyResetDelaySeconds = 3f;
        [Tooltip("Height above the track spline where the downward ground probe starts.")]
        [SerializeField, Min(0f)] private float _trainingDummyGroundProbeHeight = 2f;
        [SerializeField, Min(0f)] private float _trainingDummyGroundProbeDepth = 20f;
        [Tooltip("Gap between the ground and the bottom of the dummy collider at spawn and reset.")]
        [SerializeField, Min(0f)] private float _trainingDummyGroundLift = 0.25f;
        [Tooltip("Height above the spline used when no ground is found (same as the respawn height offset).")]
        [SerializeField, Min(0f)] private float _trainingDummyFallbackLift = 3f;
        [Tooltip("Height above the ground of the sideways wall probes (TrackWallProbe.Height).")]
        [SerializeField, Min(0f)] private float _trainingDummyWallProbeHeight = 1.5f;
        [SerializeField, Min(0f)] private float _trainingDummyWallProbeDistance = 80f;
        [Tooltip("A side position whose ground lies more than this below or above the racing-line ground, or is tagged off-track, is retried closer to the line.")]
        [SerializeField, Min(0f)] private float _trainingDummyMaxGroundStepMeters = 1.5f;
        [Tooltip("Ground tagged like this is off the track (BuddahRespawn floor); a dummy is never placed on it.")]
        [SerializeField] private string _trainingDummyOffTrackTag = "RespawnFloor";
        [Tooltip("Side placement candidates: the resolved offset, then evenly closer to the racing line, the last on it.")]
        [SerializeField, Min(1)] private int _trainingDummyPlacementAttempts = 3;

        private readonly Dictionary<int, NetworkObject> _spawnedPlayers = new Dictionary<int, NetworkObject>();

        private readonly List<NetworkObject> _spawnedAI = new List<NetworkObject>();
        private readonly List<NetworkObject> _spawnedDummies = new List<NetworkObject>();
        private readonly RaycastHit[] _trainingDummyProbeHits = new RaycastHit[16];
        private BuddahGo.AI.AISkillWorld _skillWorld;

        /// <summary>Match/ModeRule Practice row → training-dummy fields ("recordField=componentField").</summary>
        public static readonly string[] TrainingDummyModeRuleMap =
        {
            "trainingDummyCount=_trainingDummyCount", "trainingDummySpacingMeters=_trainingDummySpacingMeters",
            "trainingDummyStartOffsetMeters=_trainingDummyStartOffsetMeters", "trainingDummyLateralOffsetMeters=_trainingDummyLateralOffsetMeters",
            "trainingDummyResetDelaySeconds=_trainingDummyResetDelaySeconds"
        };

        // Dummies exist only in Practice (IMatchRules.SpawnTrainingDummies), so their config is always the Practice row,
        // whatever mode is active when this Awake runs.
        private const string TrainingDummyModeId = "Practice";

        private void Awake()
        {
            CacheSpawnPointsIfNeeded();
            ApplyTrackSpawnTable();
            DesignTableOverlay.ApplyMapped(this, DesignTableOverlay.ModeRule(TrainingDummyModeId), TrainingDummyModeRuleMap);
        }

        // Match/TrackSpawn owns the spawn transforms (world position, Y rotation) for the current track.
        private void ApplyTrackSpawnTable()
        {
            ProjectConfigDatabase db = DesignTableOverlay.Db;
            if (db == null || db.trackSpawns == null || _spawnPoints == null)
                return;
            string trackId = DesignTableOverlay.CurrentTrackId(this);
            for (int i = 0; i < db.trackSpawns.Count; i++)
            {
                TrackSpawnRecord row = db.trackSpawns[i];
                if (row == null || row.trackId != trackId || row.order < 0 || row.order >= _spawnPoints.Length || _spawnPoints[row.order] == null)
                    continue;
                _spawnPoints[row.order].transform.SetPositionAndRotation(row.position, Quaternion.Euler(0f, row.rotationY, 0f));
            }
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

            int required = MatchRules.Current.IsSolo
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
                MatchRules.Current.IsSolo ? MatchRules.Current.DefaultPlayerName : DesignTableOverlay.Text("racer.online_human_prefix", "Player ") + conn.ClientId);
            InstanceFinder.ServerManager.Spawn(playerInstance, conn);
            ApplyResolvedSkillLoadout(playerInstance, conn.ClientId);
            _spawnedPlayers[conn.ClientId] = playerInstance;
            SpawnSoloAI();
            SpawnTrainingDummies();

            // Validation log for race-readiness pipeline (disabled by default).
            DebugLog($"[Spawn] Player ready for conn {conn.ClientId}");
        }

        private void SpawnSoloAI()
        {
            if (!MatchRules.Current.IsSolo) return;
            var settings = SessionControl.SoloSettings;
            int count = settings?.AICount ?? 0;
            if (count == 0) return;
            var skills = BuddahGo.AI.AISkillCatalog.Current;
            _skillWorld ??= new BuddahGo.AI.AISkillWorld(skills.Tuning);
            while (_spawnedAI.Count < count)
            {
                int index = _spawnedAI.Count;
                var point = GetSpawnPoint(index + 1);
                var racer = Instantiate(_playerPrefab, point.position, point.rotation);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(racer.gameObject, gameObject.scene);
                var identity = racer.GetComponent<RacerIdentity>();
                var driver = racer.GetComponent<BuddahGo.AI.AIRacerDriver>();
                var loadout = racer.GetComponent<SkillLoadout>();
                var caster = racer.GetComponent<BuddahGo.AI.AISkillCaster>();
                if (identity == null || driver == null || loadout == null || caster == null)
                    throw new System.InvalidOperationException("Racer prefab requires RacerIdentity, a loadout, driver and skill caster.");
                var personality = ResolveRosterPersonality(skills, index);
                identity.AssignBeforeSpawn(RacerId.ForAI(index), index < settings.AINames.Count ? settings.AINames[index] : ResolveRosterName(index, personality.DisplayName));
                racer.name = "AI Racer " + (index + 1);
                driver.AdoptProfile(BuddahGo.AI.AIDifficultyProfiles.Resolve(settings.Difficulty, index));
                InstanceFinder.ServerManager.Spawn(racer); // Empty owner, never a synthetic client.
                loadout.SetSlotsServer(personality.Loadout);
                caster.Configure(_skillWorld, personality, skills.ForDifficulty(settings.Difficulty), skills.Tuning, index, driver.Profile.NoiseSeed);
                _spawnedAI.Add(racer);
            }
        }

        // Practice only (IMatchRules.SpawnTrainingDummies). Ownerless instances of the racer prefab whose racer id stays
        // unassigned, so they never reach the leaderboard, finish, results, catch-up, RacerDirectory or the intro. They get
        // no AI profile, loadout override or caster: they never drive and never cast. Dummies die with the scene.
        private void SpawnTrainingDummies()
        {
            if (!MatchRules.Current.SpawnTrainingDummies || _spawnedDummies.Count >= _trainingDummyCount)
                return;
            TrackSplineRef track = TrackSplineRef.Instance;
            if (track == null || track.container == null || track.TrackLength <= 1e-3f)
            {
                Debug.LogWarning("[MatchSpawnManager] Practice training dummies need a TrackSplineRef; none spawned.");
                return;
            }

            float startDistance = ProjectOntoTrack(track, ResolveHumanStartPosition());
            while (_spawnedDummies.Count < _trainingDummyCount)
            {
                int index = _spawnedDummies.Count;
                float distance = BuddahGo.Match.TrainingDummyLayout.DistanceAlongTrack(
                    startDistance, _trainingDummyStartOffsetMeters, _trainingDummySpacingMeters, index, track.TrackLength);
                float progress01 = distance / track.TrackLength;
                if (!track.TryEvaluateWorldPoseAtProgress01(progress01, out Vector3 trackPoint, out Vector3 tangent))
                {
                    Debug.LogWarning($"[MatchSpawnManager] No track pose for training dummy {index + 1}; remaining dummies skipped.");
                    return;
                }

                Quaternion rotation = BuddahGo.Match.TrainingDummyLayout.FacingAlong(tangent);
                NetworkObject dummy = Instantiate(_playerPrefab, trackPoint, rotation);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(dummy.gameObject, gameObject.scene);
                var identity = dummy.GetComponent<RacerIdentity>();
                if (identity == null) throw new System.InvalidOperationException("Racer prefab requires RacerIdentity.");
                Vector3 position = ResolveTrainingDummyPosition(dummy, trackPoint, tangent, index);
                dummy.transform.SetPositionAndRotation(position, rotation);
                string displayName = "Training Dummy " + (index + 1);
                identity.AssignTrainingDummyBeforeSpawn(displayName);
                dummy.name = displayName;
                // Added before Spawn so the motor's OnStartNetwork refresh sees the steering override.
                var behaviour = dummy.gameObject.AddComponent<BuddahGo.Match.TrainingDummy>();
                behaviour.ConfigureBeforeSpawn(position, rotation, progress01, _trainingDummyResetDelaySeconds);
                dummy.gameObject.AddComponent<BuddahGo.Match.TrainingDummyReadout>();
                InstanceFinder.ServerManager.Spawn(dummy); // Empty owner, never a synthetic client.
                _spawnedDummies.Add(dummy);
                GameLog.Verbose($"[MatchSpawnManager] Spawned {displayName} at distance={distance:0.0}m progress01={progress01:0.0000} pos={position}");
            }
        }

        // Where the human actually starts: the intro launch slot (Practice: Layout_1P Slot 0), else the first spawn point.
        // The intro manager is a scene NetworkObject that FishNet keeps inactive until it is spawned, which is usually
        // after this runs, so the lookup must include inactive objects (its layouts are plain serialized transforms).
        private Vector3 ResolveHumanStartPosition()
        {
            IntroSequenceManager intro = FindFirstObjectByType<IntroSequenceManager>(FindObjectsInactive.Include);
            if (intro != null && intro.TryGetLaunchPosition(Mathf.Max(1, _spawnedPlayers.Count), 0, out Vector3 launch))
                return launch;
            Transform spawnPoint = GetSpawnPoint(0);
            return spawnPoint != null ? spawnPoint.position : transform.position;
        }

        // Same projection as SplineProgressTracker.SnapToWorldPosition: nearest spline point, as track distance.
        private static float ProjectOntoTrack(TrackSplineRef track, Vector3 worldPosition)
        {
            Vector3 local = track.container.transform.InverseTransformPoint(worldPosition);
            SplineUtility.GetNearestPoint(track.container.Spline, (float3)local, out _, out float t);
            return track.DistanceAtT(Mathf.Repeat(t, 1f));
        }

        private Vector3 ResolveTrainingDummyPosition(NetworkObject dummy, Vector3 trackPoint, Vector3 tangent, int index)
        {
            Vector3 right = BuddahGo.Match.TrainingDummyLayout.RightOf(tangent);
            bool centreGrounded = TryProbeGround(trackPoint, out float centreGroundY);
            Vector3 wallOrigin = new Vector3(trackPoint.x, (centreGrounded ? centreGroundY : trackPoint.y) + _trainingDummyWallProbeHeight, trackPoint.z);
            float leftWall = ProbeStaticDistance(wallOrigin, -right, _trainingDummyWallProbeDistance);
            float rightWall = ProbeStaticDistance(wallOrigin, right, _trainingDummyWallProbeDistance);
            float lateral = BuddahGo.Match.TrainingDummyLayout.ResolveLateralOffset(index, _trainingDummyCount,
                _trainingDummyLateralOffsetMeters, leftWall, rightWall, _trainingDummyWallClearanceMeters);
            float pivotHeight = PivotAboveColliderBottom(dummy) + _trainingDummyGroundLift;
            // A side spot off the road (no ground, a step away from the line, or off-track floor) is retried closer to the line.
            for (int attempt = 0; attempt < _trainingDummyPlacementAttempts; attempt++)
            {
                float candidate = BuddahGo.Match.TrainingDummyLayout.LateralCandidate(lateral, attempt, _trainingDummyPlacementAttempts);
                Vector3 position = BuddahGo.Match.TrainingDummyLayout.Place(trackPoint, tangent, candidate);
                if (!TryProbeGround(position, out float groundY, out bool offTrack) || offTrack)
                    continue;
                if (centreGrounded && Mathf.Abs(groundY - centreGroundY) > _trainingDummyMaxGroundStepMeters)
                    continue;
                position.y = groundY + pivotHeight;
                return position;
            }

            Vector3 fallback = trackPoint;
            fallback.y = centreGrounded ? centreGroundY + pivotHeight : trackPoint.y + _trainingDummyFallbackLift;
            return fallback;
        }

        private bool TryProbeGround(Vector3 trackPoint, out float groundY)
        {
            return TryProbeGround(trackPoint, out groundY, out _);
        }

        private bool TryProbeGround(Vector3 trackPoint, out float groundY, out bool offTrack)
        {
            Vector3 origin = trackPoint + Vector3.up * _trainingDummyGroundProbeHeight;
            bool hit = TryProbeStatic(origin, Vector3.down, _trainingDummyGroundProbeHeight + _trainingDummyGroundProbeDepth, out RaycastHit ground);
            groundY = hit ? ground.point.y : origin.y;
            offTrack = hit && !string.IsNullOrEmpty(_trainingDummyOffTrackTag) && ground.collider.CompareTag(_trainingDummyOffTrackTag);
            return hit;
        }

        private float ProbeStaticDistance(Vector3 origin, Vector3 direction, float maxDistance)
        {
            return TryProbeStatic(origin, direction, maxDistance, out RaycastHit hit) ? hit.distance : float.PositiveInfinity;
        }

        // Nearest solid, rigidbody-free hit (track geometry): racers, dummies and push boxes are ignored.
        private bool TryProbeStatic(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _trainingDummyProbeHits, maxDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            nearest = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _trainingDummyProbeHits[i];
                if (hit.rigidbody != null || (found && hit.distance >= nearest.distance))
                    continue;
                nearest = hit;
                found = true;
            }
            return found;
        }

        // Height of the racer pivot above the bottom of its solid capsule (the table-applied collider, about 1.3 m).
        private static float PivotAboveColliderBottom(NetworkObject racer)
        {
            foreach (CapsuleCollider capsule in racer.GetComponentsInChildren<CapsuleCollider>(true))
            {
                if (capsule.isTrigger)
                    continue;
                Vector3 bottom = capsule.center - Vector3.up * (capsule.height * 0.5f);
                return racer.transform.position.y - capsule.transform.TransformPoint(bottom).y;
            }
            return 0f;
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

        // AI/Roster maps AI slot → personality (and an optional display-name override); JSON order is the fallback.
        private static BuddahGo.AI.AISkillPersonality ResolveRosterPersonality(BuddahGo.AI.AISkillCatalog skills, int index)
        {
            ProjectConfigDatabase db = DesignTableOverlay.Db;
            RosterRecord slot = db != null ? DesignTableOverlay.Single(db.rosters, r => r.slotIndex == index) : null;
            if (slot != null && !string.IsNullOrEmpty(slot.personalityId))
            {
                foreach (var personality in skills.Personalities)
                    if (personality.Id == slot.personalityId) return personality;
            }
            return skills.Personalities[index];
        }

        private static string ResolveRosterName(int index, string fallback)
        {
            ProjectConfigDatabase db = DesignTableOverlay.Db;
            RosterRecord slot = db != null ? DesignTableOverlay.Single(db.rosters, r => r.slotIndex == index) : null;
            return slot != null && !string.IsNullOrEmpty(slot.displayNameOverrideKey) ? DesignTableOverlay.Text(slot.displayNameOverrideKey, fallback) : fallback;
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
