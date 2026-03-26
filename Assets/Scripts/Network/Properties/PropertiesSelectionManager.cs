using System;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace SteamMultiplayer.Network
{
    /* 简介：此脚本用于管理房间属性选择流程，允许玩家在进入比赛前选择地图、角色皮肤等属性。
        作为一个网络对象，PropertiesSelectionManager在服务器上维护可选属性列表、玩家选择状态以及当前阶段等核心数据，并通过同步变量和同步列表将状态更新传递给所有客户端。
        客户端可以通过调用SubmitPlayerSelection方法来提交自己的选择，服务器会根据当前阶段的选择模式来处理这些选择，并在所有阶段完成后加载最终的比赛场景。
        脚本还提供事件OnSelectionStateChanged，供UI等系统订阅，以便在选择状态变化时更新界面显示。
        */
    public class PropertiesSelectionManager : NetworkBehaviour
    {
        [Serializable]
        public struct PropertyDefinitionRecord : IEquatable<PropertyDefinitionRecord>
        {
            public string PropertyKey;
            public string DisplayName;
            public PropertySelectionMode SelectionMode;

            public bool Equals(PropertyDefinitionRecord other)
            {
                return PropertyKey == other.PropertyKey
                    && DisplayName == other.DisplayName
                    && SelectionMode == other.SelectionMode;
            }

            public override bool Equals(object obj)
            {
                return obj is PropertyDefinitionRecord other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(PropertyKey, DisplayName, SelectionMode);
            }
        }

        [Serializable]
        public struct SelectionParticipantState : IEquatable<SelectionParticipantState>
        {
            public int PlayerId;
            public string SteamId;
            public string PlayerName;
            public bool IsHost;
            public bool IsReady;

            public bool Equals(SelectionParticipantState other)
            {
                return PlayerId == other.PlayerId
                    && SteamId == other.SteamId
                    && PlayerName == other.PlayerName
                    && IsHost == other.IsHost
                    && IsReady == other.IsReady;
            }

            public override bool Equals(object obj)
            {
                return obj is SelectionParticipantState other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(PlayerId, SteamId, PlayerName, IsHost, IsReady);
            }
        }

        [Serializable]
        public struct PlayerSelectionRecord : IEquatable<PlayerSelectionRecord>
        {
            public int PlayerId;
            public string PropertyKey;
            public string SelectedOptionId;

            public bool Equals(PlayerSelectionRecord other)
            {
                return PlayerId == other.PlayerId
                    && PropertyKey == other.PropertyKey
                    && SelectedOptionId == other.SelectedOptionId;
            }

            public override bool Equals(object obj)
            {
                return obj is PlayerSelectionRecord other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(PlayerId, PropertyKey, SelectedOptionId);
            }
        }

        public static PropertiesSelectionManager Instance { get; private set; }

        [Header("Stage Flow")]
        [SerializeField] private bool _registerDefaultStageProperties = true;
        [SerializeField] private int _stageDurationSeconds = 30;
        [SerializeField] private string _resolvedMatchSceneName = "RaceMap";
        [SerializeField] private bool _enableDebugLogs = false;

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
            _transitioningToMatch.Value = false;
            ResolvedPropertySelectionCache.Clear();
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
            for (int i = 0; i < PropertyOptions.Count; i++)
            {
                if (PropertyOptions[i].PropertyKey == propertyKey)
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

        public int GetSelectionCountForOption(string propertyKey, string optionId)
        {
            int count = 0;
            for (int i = 0; i < SubmittedSelections.Count; i++)
            {
                PlayerSelectionRecord record = SubmittedSelections[i];
                if (record.PropertyKey == propertyKey && record.SelectedOptionId == optionId)
                    count++;
            }

            return count;
        }

        public Dictionary<string, int> GetCurrentVotes(string propertyKey)
        {
            Dictionary<string, int> votes = new Dictionary<string, int>();

            for (int i = 0; i < SubmittedSelections.Count; i++)
            {
                PlayerSelectionRecord record = SubmittedSelections[i];
                if (!string.Equals(record.PropertyKey, propertyKey, StringComparison.Ordinal))
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
            if (!TryGetDefinitionRecord(propertyKey, out PropertyDefinitionRecord definition))
                return false;

            switch (definition.SelectionMode)
            {
                case PropertySelectionMode.HostOnly:
                    return TryGetHostSelectedOption(propertyKey, out optionId);
                case PropertySelectionMode.Vote:
                    return TryGetVoteWinner(propertyKey, out optionId);
                case PropertySelectionMode.Single:
                case PropertySelectionMode.Multi:
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
            if (!IsClientInitialized)
                return;

            SubmitPlayerSelectionServerRpc(propertyKey, optionId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitPlayerSelectionServerRpc(string propertyKey, string optionId, NetworkConnection caller = null)
        {
            if (caller == null || !caller.IsAuthenticated)
                return;

            if (!_stageCountdownActive.Value || _transitioningToMatch.Value)
                return;

            if (!TryGetCurrentStagePropertyKey(out string currentStagePropertyKey)
                || !string.Equals(currentStagePropertyKey, propertyKey, StringComparison.Ordinal))
            {
                return;
            }

            if (!TryGetDefinitionRecord(propertyKey, out PropertyDefinitionRecord definition))
                return;

            if (!IsSelectionAllowedForPlayer(caller.ClientId, definition.SelectionMode))
                return;

            if (!IsOptionValid(propertyKey, optionId))
                return;

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

        private void HandleAuthenticationResult(NetworkConnection conn, bool authenticated)
        {
            if (!authenticated)
                return;

            AddOrUpdateParticipant(conn);
        }

        private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                AddOrUpdateParticipant(conn);
            }
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                RemoveParticipant(conn.ClientId);
            }
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
            RegisterAvailableProperty(CreateDefaultSkinProperty(), includeInStageOrder: true);
        }

        [Server]
        private void StartCurrentStageServer()
        {
            if (_transitioningToMatch.Value)
                return;

            if (!TryGetCurrentStagePropertyKey(out _))
            {
                LoadResolvedMatchSceneServer();
                return;
            }

            StopAllCoroutines();
            _stageCountdownActive.Value = true;
            _stageCountdownSecondsRemaining.Value = Mathf.Max(1, _stageDurationSeconds);
            StartCoroutine(StageCountdownCoroutine());
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

            if (!string.IsNullOrWhiteSpace(_resolvedMatchSceneName) && InstanceFinder.SceneManager != null)
            {
                SceneLoadData sceneLoadData = new SceneLoadData(_resolvedMatchSceneName)
                {
                    ReplaceScenes = ReplaceOption.All
                };

                InstanceFinder.SceneManager.LoadGlobalScenes(sceneLoadData);
                LogDebug($"Loading match scene: {_resolvedMatchSceneName}");
            }
            else
            {
                LogDebug("Resolved match scene is empty or SceneManager missing.");
            }

            RaiseSelectionStateChanged();
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
            if (!string.IsNullOrWhiteSpace(steamId)
                && SteamLobbyManager.Instance != null
                && SteamLobbyManager.Instance.CurrentLobby.HasValue)
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
            if (!string.IsNullOrWhiteSpace(steamId)
                && SteamLobbyManager.Instance != null
                && SteamLobbyManager.Instance.CurrentLobby.HasValue)
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
            PlayerSelectionRecord record = new PlayerSelectionRecord
            {
                PlayerId = playerId,
                PropertyKey = propertyKey,
                SelectedOptionId = optionId
            };

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

                int selectionIndex = FindSelectionIndex(participant.PlayerId, definition.PropertyKey);
                if (selectionIndex < 0)
                    return false;

                string selectedOptionId = SubmittedSelections[selectionIndex].SelectedOptionId;
                if (string.IsNullOrWhiteSpace(selectedOptionId))
                    return false;
            }

            return hasRequiredParticipant;
        }

        private bool IsOptionValid(string propertyKey, string optionId)
        {
            for (int i = 0; i < PropertyOptions.Count; i++)
            {
                SelectablePropertyOption option = PropertyOptions[i];
                if (option.PropertyKey == propertyKey
                    && option.OptionId == optionId
                    && option.IsUnlocked)
                {
                    return true;
                }
            }

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
            ResolvedPropertySelectionCache.SetMatchScene(_resolvedMatchSceneName);

            for (int i = 0; i < PropertyDefinitions.Count; i++)
            {
                string propertyKey = PropertyDefinitions[i].PropertyKey;
                if (TryGetResolvedSelectionOptionId(propertyKey, out string optionId))
                    ResolvedPropertySelectionCache.SetSelection(propertyKey, optionId);
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

        private static SelectablePropertyDefinition CreateDefaultMapProperty()
        {
            return SelectablePropertyDefinition.Create(
                "map",
                "Map Selection",
                PropertySelectionMode.Vote,
                SelectablePropertyOption.Create("map", "Map_A", "Map A", "Default placeholder map A."),
                SelectablePropertyOption.Create("map", "Map_B", "Map B", "Default placeholder map B."),
                SelectablePropertyOption.Create("map", "Map_C", "Map C", "Default placeholder map C."));
        }

        private static SelectablePropertyDefinition CreateDefaultSkinProperty()
        {
            return SelectablePropertyDefinition.Create(
                "skin",
                "Skin Selection",
                PropertySelectionMode.Single,
                SelectablePropertyOption.Create("skin", "Skin_A", "Skin A", "Default placeholder skin A."),
                SelectablePropertyOption.Create("skin", "Skin_B", "Skin B", "Default placeholder skin B."),
                SelectablePropertyOption.Create("skin", "Skin_C", "Skin C", "Default placeholder skin C."));
        }

        private void LogDebug(string message)
        {
            if (_enableDebugLogs)
                Debug.Log($"[PropertiesSelectionManager] {message}");
        }
    }
}
