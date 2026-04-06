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
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.Playables;
using SteamMultiplayer.UI;

namespace SteamMultiplayer.Network
{
    public class PropertiesSelectionManager : NetworkBehaviour
    {
        public const string MapStageKey = "map";
        public const string SkillLoadoutStageKey = "skill_loadout";
        public const string SkinStageKey = "skin";

        [Serializable]
        public struct PropertyDefinitionRecord : IEquatable<PropertyDefinitionRecord>
        {
            public string PropertyKey;
            public string DisplayName;
            public PropertySelectionMode SelectionMode;
            public bool Equals(PropertyDefinitionRecord other) => PropertyKey == other.PropertyKey && DisplayName == other.DisplayName && SelectionMode == other.SelectionMode;
            public override bool Equals(object obj) => obj is PropertyDefinitionRecord other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(PropertyKey, DisplayName, SelectionMode);
        }

        [Serializable]
        public struct SelectionParticipantState : IEquatable<SelectionParticipantState>
        {
            public int PlayerId;
            public string SteamId;
            public string PlayerName;
            public bool IsHost;
            public bool IsReady;
            public bool Equals(SelectionParticipantState other) => PlayerId == other.PlayerId && SteamId == other.SteamId && PlayerName == other.PlayerName && IsHost == other.IsHost && IsReady == other.IsReady;
            public override bool Equals(object obj) => obj is SelectionParticipantState other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(PlayerId, SteamId, PlayerName, IsHost, IsReady);
        }

        [Serializable]
        public struct PlayerSelectionRecord : IEquatable<PlayerSelectionRecord>
        {
            public int PlayerId;
            public string PropertyKey;
            public string SelectedOptionId;
            public bool Equals(PlayerSelectionRecord other) => PlayerId == other.PlayerId && PropertyKey == other.PropertyKey && SelectedOptionId == other.SelectedOptionId;
            public override bool Equals(object obj) => obj is PlayerSelectionRecord other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(PlayerId, PropertyKey, SelectedOptionId);
        }

        public static PropertiesSelectionManager Instance { get; private set; }

        [Header("Stage Flow")]
        [SerializeField] private bool _registerDefaultStageProperties = true;
        [SerializeField] private int _stageDurationSeconds = 30;
        [SerializeField] private string _resolvedMatchSceneName = "RaceMap";
        [SerializeField, Min(0f)] private float _sceneTransitionFadeDurationSeconds = 1f;
        [SerializeField] private bool _preferTimelineForMatchTransition = true;
        [SerializeField] private bool _enableDebugLogs = false;

        [Header("Skill Loadout Stage")]
        [SerializeField] private SkillDatabase _skillDatabase;
        [Tooltip("Legacy fallback. Config table rules override this when present.")]
        [SerializeField] private bool _allowDuplicateSkillSelections = false;
        [Tooltip("Legacy fallback. Config table fallback pool overrides this when present.")]
        [SerializeField] private string[] _fallbackSkillIds = { "acceleration", "push_projectile_hands", "blackcurtain" };

        public readonly SyncList<PropertyDefinitionRecord> PropertyDefinitions = new SyncList<PropertyDefinitionRecord>();
        public readonly SyncList<SelectablePropertyOption> PropertyOptions = new SyncList<SelectablePropertyOption>();
        public readonly SyncList<SelectionParticipantState> Participants = new SyncList<SelectionParticipantState>();
        public readonly SyncList<PlayerSelectionRecord> SubmittedSelections = new SyncList<PlayerSelectionRecord>();
        public readonly SyncList<string> StageOrder = new SyncList<string>();

        private readonly SyncVar<int> _currentStageIndex = new SyncVar<int>();
        private readonly SyncVar<int> _stageCountdownSecondsRemaining = new SyncVar<int>();
        private readonly SyncVar<bool> _stageCountdownActive = new SyncVar<bool>();
        private readonly SyncVar<bool> _transitioningToMatch = new SyncVar<bool>();

        public int CurrentStageIndex => _currentStageIndex.Value;
        public int TotalStageCount => StageOrder.Count;
        public int StageCountdownSecondsRemaining => _stageCountdownSecondsRemaining.Value;
        public bool IsStageCountdownActive => _stageCountdownActive.Value;
        public bool IsTransitioningToMatch => _transitioningToMatch.Value;
        public bool AllowDuplicateSkillSelections => _allowDuplicateSkillSelections;
        public string ResolvedMatchSceneName => _resolvedMatchSceneName;
        public string CurrentStagePropertyKey => TryGetCurrentStagePropertyKey(out string propertyKey) ? propertyKey : string.Empty;

        public event Action OnSelectionStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                LogDebug("Duplicate PropertiesSelectionManager detected; destroying duplicate instance.");
                Destroy(gameObject);
                return;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Instance = this;
            Debug.Log("[PropertySelection] OnStartClient - client received manager");
            SubscribeSyncCollections();
            NotifySelectionStateChanged();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            UnsubscribeSyncCollections();
            if (Instance == this)
                Instance = null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Instance = this;
            ResolvedPropertySelectionCache.Clear();
            ResolvedPropertySelectionCache.SetMatchScene(_resolvedMatchSceneName);
            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.OnAuthenticationResult += HandleAuthenticationResult;
                InstanceFinder.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
                foreach (KeyValuePair<int, NetworkConnection> kvp in InstanceFinder.ServerManager.Clients)
                {
                    NetworkConnection conn = kvp.Value;
                    if (conn != null && conn.IsAuthenticated)
                        AddOrUpdateParticipant(conn);
                }
            }
            if (_registerDefaultStageProperties)
                RegisterDefaultStageProperties();
            StartCurrentStageServer();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.OnAuthenticationResult -= HandleAuthenticationResult;
                InstanceFinder.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
            }
            StopAllCoroutines();
            PropertyDefinitions.Clear();
            PropertyOptions.Clear();
            Participants.Clear();
            SubmittedSelections.Clear();
            StageOrder.Clear();
            _currentStageIndex.Value = 0;
            _stageCountdownSecondsRemaining.Value = 0;
            _stageCountdownActive.Value = false;
            bool preserveResolvedSelectionsForMatchTransition = _transitioningToMatch.Value;
            _transitioningToMatch.Value = false;

            // Keep resolved selections alive while the property-selection scene is unloading into the match scene.
            // MatchSpawnManager reads this cache on the server when spawning race players.
            if (!preserveResolvedSelectionsForMatchTransition)
                ResolvedPropertySelectionCache.Clear();
        }

        public static string GetSkillLoadoutSlotPropertyKey(int slotIndex) => $"{SkillLoadoutStageKey}_{slotIndex}";

        public static bool TryParseSkillLoadoutSlotPropertyKey(string propertyKey, out int slotIndex)
        {
            slotIndex = -1;
            const string prefix = SkillLoadoutStageKey + "_";
            return !string.IsNullOrWhiteSpace(propertyKey)
                && propertyKey.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(propertyKey.Substring(prefix.Length), out slotIndex)
                && slotIndex >= 0
                && slotIndex < SkillLoadout.SlotCount;
        }

        public List<SelectablePropertyDefinition> GetRegisteredProperties()
        {
            List<SelectablePropertyDefinition> result = new List<SelectablePropertyDefinition>();
            for (int i = 0; i < PropertyDefinitions.Count; i++)
            {
                PropertyDefinitionRecord record = PropertyDefinitions[i];
                result.Add(new SelectablePropertyDefinition
                {
                    PropertyKey = record.PropertyKey,
                    DisplayName = record.DisplayName,
                    SelectionMode = record.SelectionMode,
                    AvailableOptions = GetOptionsForProperty(record.PropertyKey)
                });
            }
            return result;
        }

        public bool TryGetCurrentStageDefinition(out SelectablePropertyDefinition definition)
        {
            definition = default;
            if (!TryGetCurrentStagePropertyKey(out string propertyKey))
                return false;
            List<SelectablePropertyDefinition> definitions = GetRegisteredProperties();
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].PropertyKey == propertyKey)
                {
                    definition = definitions[i];
                    return true;
                }
            }
            return false;
        }

        public bool TryGetCurrentStagePropertyKey(out string propertyKey)
        {
            if (_currentStageIndex.Value < 0 || _currentStageIndex.Value >= StageOrder.Count)
            {
                propertyKey = string.Empty;
                return false;
            }
            propertyKey = StageOrder[_currentStageIndex.Value];
            return !string.IsNullOrWhiteSpace(propertyKey);
        }

        public List<SelectablePropertyOption> GetOptionsForProperty(string propertyKey)
        {
            List<SelectablePropertyOption> result = new List<SelectablePropertyOption>();
            string lookupKey = NormalizeOptionsPropertyKey(propertyKey);
            for (int i = 0; i < PropertyOptions.Count; i++)
            {
                if (PropertyOptions[i].PropertyKey == lookupKey)
                    result.Add(PropertyOptions[i]);
            }
            return result;
        }

        public bool TryGetPlayerSelection(int playerId, out PlayerPropertySelection selection)
        {
            selection = default;
            int participantIndex = FindParticipantIndex(playerId);
            if (participantIndex < 0)
                return false;

            SelectionParticipantState participant = Participants[participantIndex];
            selection = new PlayerPropertySelection
            {
                PlayerId = participant.PlayerId,
                SteamId = participant.SteamId,
                PlayerName = participant.PlayerName,
                Selections = new List<PropertySelectionEntry>()
            };

            for (int i = 0; i < SubmittedSelections.Count; i++)
            {
                PlayerSelectionRecord record = SubmittedSelections[i];
                if (record.PlayerId != playerId)
                    continue;
                selection.Selections.Add(new PropertySelectionEntry
                {
                    PropertyKey = record.PropertyKey,
                    SelectedOptionId = record.SelectedOptionId
                });
            }

            return true;
        }

        public bool TryGetLocalPlayerSelection(out PlayerPropertySelection selection)
        {
            int localPlayerId = GetLocalClientId();
            if (localPlayerId < 0)
            {
                selection = default;
                return false;
            }
            return TryGetPlayerSelection(localPlayerId, out selection);
        }

        public bool TryGetLocalParticipant(out SelectionParticipantState participant)
        {
            int localPlayerId = GetLocalClientId();
            int participantIndex = FindParticipantIndex(localPlayerId);
            if (participantIndex >= 0)
            {
                participant = Participants[participantIndex];
                return true;
            }
            participant = default;
            return false;
        }

        public bool TryGetPlayerSkillLoadoutSelection(int playerId, out string[] skillIds)
        {
            skillIds = new string[SkillLoadout.SlotCount];
            bool hasAnySelection = false;
            for (int slotIndex = 0; slotIndex < SkillLoadout.SlotCount; slotIndex++)
            {
                int selectionIndex = FindSelectionIndex(playerId, GetSkillLoadoutSlotPropertyKey(slotIndex));
                if (selectionIndex < 0)
                    continue;
                skillIds[slotIndex] = SubmittedSelections[selectionIndex].SelectedOptionId ?? string.Empty;
                hasAnySelection = true;
            }
            return hasAnySelection;
        }

        public bool TryGetLocalSkillLoadoutSelection(out string[] skillIds)
        {
            int localPlayerId = GetLocalClientId();
            if (localPlayerId < 0)
            {
                skillIds = null;
                return false;
            }
            return TryGetPlayerSkillLoadoutSelection(localPlayerId, out skillIds);
        }

        public int GetFilledSkillLoadoutSlotCount(int playerId)
        {
            if (!TryGetPlayerSkillLoadoutSelection(playerId, out string[] skillIds) || skillIds == null)
                return 0;
            int count = 0;
            for (int i = 0; i < skillIds.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(skillIds[i]))
                    count++;
            }
            return count;
        }

        public int GetSelectionCountForOption(string propertyKey, string optionId)
        {
            int count = 0;
            string lookupKey = NormalizeOptionsPropertyKey(propertyKey);
            for (int i = 0; i < SubmittedSelections.Count; i++)
            {
                PlayerSelectionRecord record = SubmittedSelections[i];
                if (NormalizeOptionsPropertyKey(record.PropertyKey) == lookupKey && record.SelectedOptionId == optionId)
                    count++;
            }
            return count;
        }

        public Dictionary<string, int> GetCurrentVotes(string propertyKey)
        {
            Dictionary<string, int> votes = new Dictionary<string, int>();
            string lookupKey = NormalizeOptionsPropertyKey(propertyKey);
            for (int i = 0; i < SubmittedSelections.Count; i++)
            {
                PlayerSelectionRecord record = SubmittedSelections[i];
                if (!string.Equals(NormalizeOptionsPropertyKey(record.PropertyKey), lookupKey, StringComparison.Ordinal))
                    continue;
                if (string.IsNullOrWhiteSpace(record.SelectedOptionId))
                    continue;
                votes.TryGetValue(record.SelectedOptionId, out int currentCount);
                votes[record.SelectedOptionId] = currentCount + 1;
            }
            return votes;
        }

        public bool TryGetResolvedSelectionOptionId(string propertyKey, out string optionId)
        {
            optionId = string.Empty;
            if (string.Equals(propertyKey, SkillLoadoutStageKey, StringComparison.Ordinal))
                return false;
            if (!TryGetDefinitionRecord(propertyKey, out PropertyDefinitionRecord definition))
                return false;
            switch (definition.SelectionMode)
            {
                case PropertySelectionMode.HostOnly:
                    return TryGetHostSelectedOption(propertyKey, out optionId);
                case PropertySelectionMode.Vote:
                    return TryGetVoteWinner(propertyKey, out optionId);
                default:
                    return TryGetFirstSubmittedOption(propertyKey, out optionId);
            }
        }

        [Server]
        public void RegisterAvailableProperty(SelectablePropertyDefinition definition, bool includeInStageOrder = false)
        {
            if (string.IsNullOrWhiteSpace(definition.PropertyKey))
                return;
            int existingDefinitionIndex = FindDefinitionIndex(definition.PropertyKey);
            PropertyDefinitionRecord record = new PropertyDefinitionRecord
            {
                PropertyKey = definition.PropertyKey,
                DisplayName = definition.DisplayName,
                SelectionMode = definition.SelectionMode
            };
            if (existingDefinitionIndex >= 0)
                PropertyDefinitions[existingDefinitionIndex] = record;
            else
                PropertyDefinitions.Add(record);
            RemoveOptionsForProperty(definition.PropertyKey);
            foreach (SelectablePropertyOption option in definition.GetSafeOptions())
                PropertyOptions.Add(option);
            if (includeInStageOrder && FindStageOrderIndex(definition.PropertyKey) < 0)
                StageOrder.Add(definition.PropertyKey);
            RaiseSelectionStateChanged();
        }

        public void SubmitPlayerSelection(string propertyKey, string optionId)
        {
            if (IsClientInitialized)
                SubmitPlayerSelectionServerRpc(propertyKey, optionId);
        }

        public void SubmitSkillLoadoutSelection(IReadOnlyList<string> skillIds)
        {
            if (!IsClientInitialized)
                return;
            SubmitSkillLoadoutSelectionServerRpc(
                skillIds != null && skillIds.Count > 0 ? skillIds[0] : string.Empty,
                skillIds != null && skillIds.Count > 1 ? skillIds[1] : string.Empty,
                skillIds != null && skillIds.Count > 2 ? skillIds[2] : string.Empty);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitPlayerSelectionServerRpc(string propertyKey, string optionId, NetworkConnection caller = null)
        {
            if (caller == null || !caller.IsAuthenticated || !_stageCountdownActive.Value || _transitioningToMatch.Value)
                return;
            if (!TryGetCurrentStagePropertyKey(out string currentStagePropertyKey)
                || !string.Equals(currentStagePropertyKey, propertyKey, StringComparison.Ordinal))
            {
                Debug.LogWarning($"[PropertySelection] Rejected non-current stage submission. player={caller.ClientId}, property={propertyKey}, currentStage={currentStagePropertyKey}");
                return;
            }
            if (IsSkillLoadoutStage(propertyKey))
            {
                Debug.LogWarning($"[PropertySelection] Rejected generic submit for skill stage. player={caller.ClientId}, property={propertyKey}");
                return;
            }
            if (!TryGetDefinitionRecord(propertyKey, out PropertyDefinitionRecord definition)
                || !IsSelectionAllowedForPlayer(caller.ClientId, definition.SelectionMode)
                || !IsOptionValid(propertyKey, optionId))
            {
                return;
            }
            UpsertSelection(caller.ClientId, propertyKey, optionId);
            LogDebug($"Selection submitted: player={caller.ClientId}, property={propertyKey}, option={optionId}");
            if (AreAllRequiredSelectionsSubmittedForCurrentStage(definition))
            {
                StopAllCoroutines();
                _stageCountdownActive.Value = false;
                _stageCountdownSecondsRemaining.Value = 0;
                RaiseSelectionStateChanged();
                LogDebug($"All required selections submitted for stage '{propertyKey}'. Advancing immediately.");
                AdvanceToNextStageServer();
                return;
            }
            RaiseSelectionStateChanged();
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitSkillLoadoutSelectionServerRpc(string slot0SkillId, string slot1SkillId, string slot2SkillId, NetworkConnection caller = null)
        {
            if (caller == null || !caller.IsAuthenticated || !_stageCountdownActive.Value || _transitioningToMatch.Value)
                return;
            if (!TryGetCurrentStagePropertyKey(out string currentStagePropertyKey) || !IsSkillLoadoutStage(currentStagePropertyKey))
            {
                Debug.LogWarning($"[PropertySelection] Rejected skill loadout submit outside of skill stage. player={caller.ClientId}, currentStage={currentStagePropertyKey}");
                return;
            }
            if (!TryGetDefinitionRecord(SkillLoadoutStageKey, out PropertyDefinitionRecord definition)
                || !IsSelectionAllowedForPlayer(caller.ClientId, definition.SelectionMode))
            {
                return;
            }

            string[] skillIds = new string[SkillLoadout.SlotCount]
            {
                slot0SkillId ?? string.Empty,
                slot1SkillId ?? string.Empty,
                slot2SkillId ?? string.Empty
            };
            if (!ValidateSkillLoadoutSubmission(caller.ClientId, skillIds, out string validationError))
            {
                Debug.LogWarning($"[PropertySelection] Skill loadout submit rejected for player {caller.ClientId}: {validationError}");
                return;
            }

            for (int slotIndex = 0; slotIndex < SkillLoadout.SlotCount; slotIndex++)
            {
                UpsertSelection(caller.ClientId, GetSkillLoadoutSlotPropertyKey(slotIndex), skillIds[slotIndex]);
                Debug.Log($"[PropertySelection] Player {caller.ClientId} submitted skill slot {slotIndex} -> '{skillIds[slotIndex]}'");
            }

            Debug.Log($"[PropertySelection] Player {caller.ClientId} current submitted loadout: 0='{skillIds[0]}', 1='{skillIds[1]}', 2='{skillIds[2]}'");
            if (AreAllRequiredSelectionsSubmittedForCurrentStage(definition))
            {
                StopAllCoroutines();
                _stageCountdownActive.Value = false;
                _stageCountdownSecondsRemaining.Value = 0;
                RaiseSelectionStateChanged();
                Debug.Log("[PropertySelection] All players completed skill loadout stage. Advancing immediately.");
                AdvanceToNextStageServer();
                return;
            }
            RaiseSelectionStateChanged();
        }

        private void HandleAuthenticationResult(NetworkConnection conn, bool authenticated)
        {
            if (authenticated)
                AddOrUpdateParticipant(conn);
        }

        private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
                AddOrUpdateParticipant(conn);
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
                RemoveParticipant(conn.ClientId);
        }

        [Server]
        private void AddOrUpdateParticipant(NetworkConnection conn)
        {
            if (conn == null)
                return;
            SelectionParticipantState participant = BuildParticipantState(conn);
            int index = FindParticipantIndex(conn.ClientId);
            if (index >= 0)
                Participants[index] = participant;
            else
                Participants.Add(participant);
            RaiseSelectionStateChanged();
        }

        [Server]
        private void RemoveParticipant(int playerId)
        {
            int index = FindParticipantIndex(playerId);
            if (index >= 0)
                Participants.RemoveAt(index);
            for (int i = SubmittedSelections.Count - 1; i >= 0; i--)
            {
                if (SubmittedSelections[i].PlayerId == playerId)
                    SubmittedSelections.RemoveAt(i);
            }
            RaiseSelectionStateChanged();
        }

        [Server]
        private void RegisterDefaultStageProperties()
        {
            RegisterAvailableProperty(CreateDefaultMapProperty(), includeInStageOrder: true);
            RegisterAvailableProperty(CreateSkillLoadoutProperty(), includeInStageOrder: true);
            RegisterAvailableProperty(CreateDefaultSkinProperty(), includeInStageOrder: true);
        }

        [Server]
        private void StartCurrentStageServer()
        {
            if (_transitioningToMatch.Value)
                return;
            if (!TryGetCurrentStagePropertyKey(out string stageKey))
            {
                LoadResolvedMatchSceneServer();
                return;
            }
            StopAllCoroutines();
            _stageCountdownActive.Value = true;
            _stageCountdownSecondsRemaining.Value = Mathf.Max(1, _stageDurationSeconds);
            StartCoroutine(StageCountdownCoroutine());
            if (IsSkillLoadoutStage(stageKey))
                Debug.Log("[PropertySelection] Skill loadout selection stage started.");
            else
                LogDebug($"Stage started: '{stageKey}'");
            RaiseSelectionStateChanged();
        }

        [Server]
        private IEnumerator StageCountdownCoroutine()
        {
            while (_stageCountdownSecondsRemaining.Value > 0)
            {
                yield return new WaitForSeconds(1f);
                if (!_stageCountdownActive.Value || _transitioningToMatch.Value)
                    yield break;
                _stageCountdownSecondsRemaining.Value--;
                RaiseSelectionStateChanged();
            }
            if (TryGetCurrentStagePropertyKey(out string stageKey))
                FinalizeStageOnCountdownServer(stageKey);
            _stageCountdownActive.Value = false;
            AdvanceToNextStageServer();
        }

        [Server]
        private void AdvanceToNextStageServer()
        {
            _currentStageIndex.Value++;
            if (_currentStageIndex.Value >= StageOrder.Count)
            {
                LoadResolvedMatchSceneServer();
                return;
            }
            StartCurrentStageServer();
        }

        [Server]
        private void LoadResolvedMatchSceneServer()
        {
            if (_transitioningToMatch.Value)
                return;
            StopAllCoroutines();
            _stageCountdownActive.Value = false;
            _stageCountdownSecondsRemaining.Value = 0;
            _transitioningToMatch.Value = true;
            UpdateResolvedSelectionCache();
            StartCoroutine(LoadResolvedMatchSceneWithFadeCoroutine());
            RaiseSelectionStateChanged();
        }

        [Server]
        private IEnumerator LoadResolvedMatchSceneWithFadeCoroutine()
        {
            if (string.IsNullOrWhiteSpace(_resolvedMatchSceneName) || InstanceFinder.SceneManager == null)
            {
                LogDebug("Resolved match scene is empty or SceneManager missing.");
                yield break;
            }

            float transitionDurationSeconds = ResolveMatchTransitionDurationSeconds();
            TriggerSceneFadeObserversRpc(transitionDurationSeconds);

            if (transitionDurationSeconds > 0f)
                yield return new WaitForSeconds(transitionDurationSeconds);

            SceneLoadData sceneLoadData = new SceneLoadData(_resolvedMatchSceneName) { ReplaceScenes = ReplaceOption.All };
            InstanceFinder.SceneManager.LoadGlobalScenes(sceneLoadData);
            LogDebug($"Loading match scene: {_resolvedMatchSceneName}");
        }

        [ObserversRpc]
        private void TriggerSceneFadeObserversRpc(float fadeDurationSeconds)
        {
            if (_preferTimelineForMatchTransition && TryPlayMatchTransitionTimeline())
                return;

            SceneFadeController.PlayFadeInBeforeSceneLoad(fadeDurationSeconds);
        }

        private float ResolveMatchTransitionDurationSeconds()
        {
            if (!_preferTimelineForMatchTransition)
                return _sceneTransitionFadeDurationSeconds;

            PropertySelectionMatchTransitionTimeline timeline = FindFirstObjectByType<PropertySelectionMatchTransitionTimeline>(FindObjectsInactive.Include);
            if (timeline == null || !timeline.IsConfigured)
                return _sceneTransitionFadeDurationSeconds;

            return timeline.GetDurationSeconds();
        }

        private bool TryPlayMatchTransitionTimeline()
        {
            PropertySelectionMatchTransitionTimeline timeline = FindFirstObjectByType<PropertySelectionMatchTransitionTimeline>(FindObjectsInactive.Include);
            if (timeline == null)
                return false;

            return timeline.PlayTransition();
        }

        private SelectionParticipantState BuildParticipantState(NetworkConnection conn)
        {
            string steamId = GetSteamIdForConnection(conn);
            bool wasReady = TryGetParticipantState(conn.ClientId, out SelectionParticipantState existing) && existing.IsReady;
            return new SelectionParticipantState
            {
                PlayerId = conn.ClientId,
                SteamId = steamId,
                PlayerName = ResolvePlayerName(steamId, conn.ClientId),
                IsHost = IsHostConnection(conn, steamId),
                IsReady = wasReady
            };
        }

        private bool TryGetParticipantState(int playerId, out SelectionParticipantState participant)
        {
            int index = FindParticipantIndex(playerId);
            if (index >= 0)
            {
                participant = Participants[index];
                return true;
            }
            participant = default;
            return false;
        }

        private string GetSteamIdForConnection(NetworkConnection conn)
        {
            if (GameNetworkManager.Instance?.FishNetManager?.TransportManager?.Transport == null || conn == null)
                return string.Empty;
            string address = GameNetworkManager.Instance.FishNetManager.TransportManager.Transport.GetConnectionAddress(conn.ClientId);
            return string.IsNullOrWhiteSpace(address) ? string.Empty : address;
        }

        private bool IsHostConnection(NetworkConnection conn, string steamId)
        {
            if (!string.IsNullOrWhiteSpace(steamId) && SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.CurrentLobby.HasValue)
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
            if (!string.IsNullOrWhiteSpace(steamId) && SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.CurrentLobby.HasValue)
            {
                Lobby lobby = SteamLobbyManager.Instance.CurrentLobby.Value;
                foreach (Friend member in lobby.Members)
                {
                    if (member.Id.Value.ToString() == steamId)
                        return member.Name;
                }
            }
            if (SteamClient.IsValid && SteamClient.SteamId.Value.ToString() == steamId)
                return SteamClient.Name;
            return $"Player {clientId}";
        }

        private void SubscribeSyncCollections()
        {
            PropertyDefinitions.OnChange += HandleSyncCollectionChanged;
            PropertyOptions.OnChange += HandleSyncCollectionChanged;
            Participants.OnChange += HandleSyncCollectionChanged;
            SubmittedSelections.OnChange += HandleSyncCollectionChanged;
            StageOrder.OnChange += HandleSyncCollectionChanged;
        }

        private void UnsubscribeSyncCollections()
        {
            PropertyDefinitions.OnChange -= HandleSyncCollectionChanged;
            PropertyOptions.OnChange -= HandleSyncCollectionChanged;
            Participants.OnChange -= HandleSyncCollectionChanged;
            SubmittedSelections.OnChange -= HandleSyncCollectionChanged;
            StageOrder.OnChange -= HandleSyncCollectionChanged;
        }

        private void HandleSyncCollectionChanged<T>(SyncListOperation op, int index, T oldItem, T newItem, bool asServer)
        {
            RaiseSelectionStateChanged();
        }

        [Server]
        private void UpsertSelection(int playerId, string propertyKey, string optionId)
        {
            int index = FindSelectionIndex(playerId, propertyKey);
            PlayerSelectionRecord record = new PlayerSelectionRecord { PlayerId = playerId, PropertyKey = propertyKey, SelectedOptionId = optionId ?? string.Empty };
            if (index >= 0)
                SubmittedSelections[index] = record;
            else
                SubmittedSelections.Add(record);
        }

        [Server]
        private void RemoveOptionsForProperty(string propertyKey)
        {
            for (int i = PropertyOptions.Count - 1; i >= 0; i--)
            {
                if (PropertyOptions[i].PropertyKey == propertyKey)
                    PropertyOptions.RemoveAt(i);
            }
        }

        private bool TryGetDefinitionRecord(string propertyKey, out PropertyDefinitionRecord record)
        {
            int index = FindDefinitionIndex(propertyKey);
            if (index >= 0)
            {
                record = PropertyDefinitions[index];
                return true;
            }
            record = default;
            return false;
        }

        private bool IsSelectionAllowedForPlayer(int playerId, PropertySelectionMode selectionMode)
        {
            if (selectionMode != PropertySelectionMode.HostOnly)
                return true;
            int participantIndex = FindParticipantIndex(playerId);
            return participantIndex >= 0 && Participants[participantIndex].IsHost;
        }

        private bool AreAllRequiredSelectionsSubmittedForCurrentStage(PropertyDefinitionRecord definition)
        {
            if (!TryGetCurrentStagePropertyKey(out string currentStagePropertyKey)
                || !string.Equals(currentStagePropertyKey, definition.PropertyKey, StringComparison.Ordinal))
            {
                return false;
            }
            bool hasRequiredParticipant = false;
            for (int i = 0; i < Participants.Count; i++)
            {
                SelectionParticipantState participant = Participants[i];
                if (!IsSelectionAllowedForPlayer(participant.PlayerId, definition.SelectionMode))
                    continue;
                hasRequiredParticipant = true;
                if (IsSkillLoadoutStage(definition.PropertyKey))
                {
                    if (!HasCompleteSkillLoadoutSelection(participant.PlayerId))
                        return false;
                    continue;
                }
                int selectionIndex = FindSelectionIndex(participant.PlayerId, definition.PropertyKey);
                if (selectionIndex < 0)
                    return false;
                if (string.IsNullOrWhiteSpace(SubmittedSelections[selectionIndex].SelectedOptionId))
                    return false;
            }
            return hasRequiredParticipant;
        }

        private bool IsOptionValid(string propertyKey, string optionId)
        {
            string lookupKey = NormalizeOptionsPropertyKey(propertyKey);
            for (int i = 0; i < PropertyOptions.Count; i++)
            {
                SelectablePropertyOption option = PropertyOptions[i];
                if (option.PropertyKey == lookupKey && option.OptionId == optionId && option.IsUnlocked)
                    return true;
            }
            Debug.LogWarning($"[PropertySelection] Invalid option submit. property={propertyKey}, option={optionId}");
            return false;
        }

        private int FindDefinitionIndex(string propertyKey)
        {
            for (int i = 0; i < PropertyDefinitions.Count; i++)
            {
                if (PropertyDefinitions[i].PropertyKey == propertyKey)
                    return i;
            }
            return -1;
        }

        private int FindParticipantIndex(int playerId)
        {
            for (int i = 0; i < Participants.Count; i++)
            {
                if (Participants[i].PlayerId == playerId)
                    return i;
            }
            return -1;
        }

        private int FindStageOrderIndex(string propertyKey)
        {
            for (int i = 0; i < StageOrder.Count; i++)
            {
                if (StageOrder[i] == propertyKey)
                    return i;
            }
            return -1;
        }

        private int FindSelectionIndex(int playerId, string propertyKey)
        {
            for (int i = 0; i < SubmittedSelections.Count; i++)
            {
                PlayerSelectionRecord record = SubmittedSelections[i];
                if (record.PlayerId == playerId && record.PropertyKey == propertyKey)
                    return i;
            }
            return -1;
        }

        private void RaiseSelectionStateChanged()
        {
            UpdateResolvedSelectionCache();
            OnSelectionStateChanged?.Invoke();
        }

        public void NotifySelectionStateChanged()
        {
            RaiseSelectionStateChanged();
        }

        private void UpdateResolvedSelectionCache()
        {
            ResolvedPropertySelectionCache.Clear();
            ResolvedPropertySelectionCache.SetMatchScene(_resolvedMatchSceneName);
            for (int i = 0; i < PropertyDefinitions.Count; i++)
            {
                string propertyKey = PropertyDefinitions[i].PropertyKey;
                if (IsSkillLoadoutStage(propertyKey))
                    continue;
                if (TryGetResolvedSelectionOptionId(propertyKey, out string optionId))
                    ResolvedPropertySelectionCache.SetSelection(propertyKey, optionId);
            }
            for (int i = 0; i < Participants.Count; i++)
            {
                int playerId = Participants[i].PlayerId;
                if (TryGetPlayerSkillLoadoutSelection(playerId, out string[] skillIds))
                    ResolvedPropertySelectionCache.SetPlayerSkillLoadout(playerId, skillIds);
            }
        }

        private bool TryGetHostSelectedOption(string propertyKey, out string optionId)
        {
            optionId = string.Empty;
            for (int i = 0; i < Participants.Count; i++)
            {
                if (!Participants[i].IsHost)
                    continue;
                int selectionIndex = FindSelectionIndex(Participants[i].PlayerId, propertyKey);
                if (selectionIndex >= 0)
                {
                    optionId = SubmittedSelections[selectionIndex].SelectedOptionId;
                    return !string.IsNullOrWhiteSpace(optionId);
                }
            }
            return false;
        }

        private bool TryGetVoteWinner(string propertyKey, out string optionId)
        {
            optionId = string.Empty;
            Dictionary<string, int> votes = GetCurrentVotes(propertyKey);
            if (votes.Count == 0)
                return false;
            int bestVotes = int.MinValue;
            string bestOptionId = string.Empty;
            List<SelectablePropertyOption> orderedOptions = GetOptionsForProperty(propertyKey);
            for (int i = 0; i < orderedOptions.Count; i++)
            {
                SelectablePropertyOption option = orderedOptions[i];
                votes.TryGetValue(option.OptionId, out int currentVotes);
                if (currentVotes > bestVotes)
                {
                    bestVotes = currentVotes;
                    bestOptionId = option.OptionId;
                }
            }
            optionId = bestOptionId;
            return !string.IsNullOrWhiteSpace(optionId);
        }

        private bool TryGetFirstSubmittedOption(string propertyKey, out string optionId)
        {
            optionId = string.Empty;
            for (int i = 0; i < SubmittedSelections.Count; i++)
            {
                if (SubmittedSelections[i].PropertyKey == propertyKey)
                {
                    optionId = SubmittedSelections[i].SelectedOptionId;
                    return !string.IsNullOrWhiteSpace(optionId);
                }
            }
            return false;
        }

        private int GetLocalClientId()
        {
            if (GameNetworkManager.Instance?.FishNetManager?.ClientManager?.Connection == null)
                return -1;
            return GameNetworkManager.Instance.FishNetManager.ClientManager.Connection.ClientId;
        }

        [Server]
        private void FinalizeStageOnCountdownServer(string stageKey)
        {
            if (!IsSkillLoadoutStage(stageKey))
                return;
            Debug.Log("[PropertySelection] Skill loadout timer expired. Auto-filling incomplete selections.");
            if (!TryGetDefinitionRecord(stageKey, out PropertyDefinitionRecord definition))
                return;
            for (int i = 0; i < Participants.Count; i++)
            {
                SelectionParticipantState participant = Participants[i];
                if (!IsSelectionAllowedForPlayer(participant.PlayerId, definition.SelectionMode))
                    continue;
                AutoFillMissingSkillSelectionsServer(participant.PlayerId);
            }
            RaiseSelectionStateChanged();
        }

        [Server]
        private void AutoFillMissingSkillSelectionsServer(int playerId)
        {
            TryGetPlayerSkillLoadoutSelection(playerId, out string[] currentSkillIds);
            if (currentSkillIds == null)
                currentSkillIds = new string[SkillLoadout.SlotCount];
            List<string> candidateSkillIds = BuildSkillAutoFillCandidates();
            HashSet<string> used = new HashSet<string>();
            if (!IsDuplicateSkillSelectionAllowed())
            {
                for (int i = 0; i < currentSkillIds.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(currentSkillIds[i]))
                        used.Add(currentSkillIds[i]);
                }
            }
            for (int slotIndex = 0; slotIndex < SkillLoadout.SlotCount; slotIndex++)
            {
                if (!string.IsNullOrWhiteSpace(currentSkillIds[slotIndex]))
                    continue;
                string fillSkillId = FindNextAutoFillSkillId(candidateSkillIds, used);
                if (string.IsNullOrWhiteSpace(fillSkillId))
                {
                    Debug.LogWarning($"[PropertySelection] Could not auto-fill skill slot {slotIndex} for player {playerId}; leaving empty.");
                    fillSkillId = string.Empty;
                }
                else if (!IsDuplicateSkillSelectionAllowed())
                {
                    used.Add(fillSkillId);
                }
                currentSkillIds[slotIndex] = fillSkillId;
                UpsertSelection(playerId, GetSkillLoadoutSlotPropertyKey(slotIndex), fillSkillId);
            }
            Debug.Log($"[PropertySelection] Auto-filled player {playerId} final skill loadout: 0='{currentSkillIds[0]}', 1='{currentSkillIds[1]}', 2='{currentSkillIds[2]}'");
        }

        private bool ValidateSkillLoadoutSubmission(int playerId, string[] skillIds, out string validationError)
        {
            validationError = string.Empty;
            if (skillIds == null || skillIds.Length != SkillLoadout.SlotCount)
            {
                validationError = "slot array size mismatch";
                return false;
            }
            HashSet<string> seen = new HashSet<string>();
            for (int slotIndex = 0; slotIndex < skillIds.Length; slotIndex++)
            {
                string skillId = (skillIds[slotIndex] ?? string.Empty).Trim();
                skillIds[slotIndex] = skillId;
                if (string.IsNullOrWhiteSpace(skillId))
                    continue;
                if (!IsOptionValid(SkillLoadoutStageKey, skillId))
                {
                    validationError = $"invalid skillId '{skillId}' in slot {slotIndex}";
                    return false;
                }
                if (!IsDuplicateSkillSelectionAllowed() && !seen.Add(skillId))
                {
                    validationError = $"duplicate skill '{skillId}' is not allowed";
                    return false;
                }
            }
            LogDebug($"Validated skill loadout submission for player={playerId}");
            return true;
        }

        private bool HasCompleteSkillLoadoutSelection(int playerId)
        {
            if (!TryGetPlayerSkillLoadoutSelection(playerId, out string[] skillIds) || skillIds == null)
                return false;
            for (int i = 0; i < skillIds.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(skillIds[i]) || !IsOptionValid(SkillLoadoutStageKey, skillIds[i]))
                    return false;
            }
            if (IsDuplicateSkillSelectionAllowed())
                return true;
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < skillIds.Length; i++)
            {
                if (!seen.Add(skillIds[i]))
                    return false;
            }
            return true;
        }

        private List<string> BuildSkillAutoFillCandidates()
        {
            List<string> candidates = new List<string>();
            HashSet<string> seen = new HashSet<string>();
            List<SelectablePropertyOption> options = GetOptionsForProperty(SkillLoadoutStageKey);
            for (int i = 0; i < options.Count; i++)
            {
                string optionId = options[i].OptionId;
                if (!string.IsNullOrWhiteSpace(optionId) && seen.Add(optionId))
                    candidates.Add(optionId);
            }
            if (_skillDatabase != null)
            {
                List<string> configuredFallbackSkillIds = _skillDatabase.GetFallbackSkillIds();
                for (int i = 0; i < configuredFallbackSkillIds.Count; i++)
                {
                    string skillId = (configuredFallbackSkillIds[i] ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(skillId) && seen.Add(skillId))
                        candidates.Add(skillId);
                }
            }
            for (int i = 0; i < _fallbackSkillIds.Length; i++)
            {
                string skillId = (_fallbackSkillIds[i] ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(skillId) && seen.Add(skillId))
                    candidates.Add(skillId);
            }
            return candidates;
        }

        private string FindNextAutoFillSkillId(List<string> candidateSkillIds, HashSet<string> used)
        {
            for (int i = 0; i < candidateSkillIds.Count; i++)
            {
                string candidate = candidateSkillIds[i];
                if (!string.IsNullOrWhiteSpace(candidate) && (IsDuplicateSkillSelectionAllowed() || !used.Contains(candidate)))
                    return candidate;
            }
            return string.Empty;
        }

        private string NormalizeOptionsPropertyKey(string propertyKey)
        {
            return TryParseSkillLoadoutSlotPropertyKey(propertyKey, out _) ? SkillLoadoutStageKey : propertyKey;
        }

        private bool IsSkillLoadoutStage(string propertyKey)
        {
            return string.Equals(propertyKey, SkillLoadoutStageKey, StringComparison.Ordinal);
        }

        private SelectablePropertyDefinition CreateSkillLoadoutProperty()
        {
            return new SelectablePropertyDefinition
            {
                PropertyKey = SkillLoadoutStageKey,
                DisplayName = "Skill Loadout Selection",
                SelectionMode = PropertySelectionMode.Multi,
                AvailableOptions = BuildSkillLoadoutOptions()
            };
        }

        private List<SelectablePropertyOption> BuildSkillLoadoutOptions()
        {
            List<SelectablePropertyOption> results = new List<SelectablePropertyOption>();
            if (_skillDatabase == null)
            {
                Debug.LogWarning("[PropertySelection] Skill loadout stage has no SkillDatabase assigned.");
                return results;
            }
            results = _skillDatabase.BuildSelectableSkillOptions();
            if (results.Count > 0)
                return results;

            List<SkillAction> selectableSkills = _skillDatabase.GetSelectableNormalSkills();
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < selectableSkills.Count; i++)
            {
                SkillAction skill = selectableSkills[i];
                if (skill == null)
                    continue;
                string skillId = (skill.skillId ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(skillId) || !seen.Add(skillId))
                    continue;
                results.Add(SelectablePropertyOption.Create(SkillLoadoutStageKey, skillId, _skillDatabase.GetResolvedDisplayName(skillId, skill), string.Empty));
            }
            return results;
        }

        private bool IsDuplicateSkillSelectionAllowed()
        {
            if (_skillDatabase != null)
                return _skillDatabase.GetAllowDuplicateSkillSelections(_allowDuplicateSkillSelections);

            return _allowDuplicateSkillSelections;
        }

        private static SelectablePropertyDefinition CreateDefaultMapProperty()
        {
            return SelectablePropertyDefinition.Create(MapStageKey, "Map Selection", PropertySelectionMode.Vote,
                SelectablePropertyOption.Create(MapStageKey, "Map_A", "Map A", "Default placeholder map A."),
                SelectablePropertyOption.Create(MapStageKey, "Map_B", "Map B", "Default placeholder map B."),
                SelectablePropertyOption.Create(MapStageKey, "Map_C", "Map C", "Default placeholder map C."));
        }

        private static SelectablePropertyDefinition CreateDefaultSkinProperty()
        {
            return SelectablePropertyDefinition.Create(SkinStageKey, "Skin Selection", PropertySelectionMode.Single,
                SelectablePropertyOption.Create(SkinStageKey, "Skin_A", "Skin A", "Default placeholder skin A."),
                SelectablePropertyOption.Create(SkinStageKey, "Skin_B", "Skin B", "Default placeholder skin B."),
                SelectablePropertyOption.Create(SkinStageKey, "Skin_C", "Skin C", "Default placeholder skin C."));
        }

        private void LogDebug(string message)
        {
            if (_enableDebugLogs)
                Debug.Log($"[PropertiesSelectionManager] {message}");
        }
    }
}
