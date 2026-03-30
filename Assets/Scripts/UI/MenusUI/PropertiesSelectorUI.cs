using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SteamMultiplayer.Network;

namespace SteamMultiplayer.UI
{
    public class PropertiesSelectorUI : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject sharedUiRoot;
        [SerializeField] private GameObject mapSelectorRoot;
        [SerializeField] private GameObject skillSelectorRoot;
        [SerializeField] private GameObject skinSelectorRoot;

        [Header("Current Stage")]
        [SerializeField] private Transform mapPropertyGroupListRoot;
        [SerializeField] private Transform skinPropertyGroupListRoot;
        [SerializeField] private PropertyGroupView propertyGroupPrefab;

        [Header("Summary")]
        [SerializeField] private TextMeshProUGUI pageTitleText;
        [SerializeField] private TextMeshProUGUI stageTitleText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private TextMeshProUGUI playersSelectionText;
        [SerializeField] private TextMeshProUGUI resolvedMapText;

        [Header("Legacy Optional")]
        [SerializeField] private Button readyButton;
        [SerializeField] private TextMeshProUGUI readyButtonText;

        [Header("Debug")]
        [SerializeField] private bool forceRefreshEveryFrame = false;

        private readonly List<PropertyGroupView> _spawnedGroups = new List<PropertyGroupView>();
        private PropertiesSelectionManager _selectionManager;
        private bool _subscribed;
        private string _lastRenderedStageKey = string.Empty;
        private float _nextManagerDiagnosticTime;

        private void Start()
        {
            ResolveManager();
            Subscribe();
            RebuildCurrentStageIfNeeded(force: true);
            RefreshUi();
        }

        private void Update()
        {
            ResolveManager();
            Subscribe();
            RebuildCurrentStageIfNeeded();

            if (forceRefreshEveryFrame || _selectionManager != null)
                RefreshUi();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void ResolveManager()
        {
            if (_selectionManager != null
                && (_selectionManager.IsClientInitialized || _selectionManager.IsServerInitialized))
            {
                return;
            }

            _selectionManager = null;

            PropertiesSelectionManager singleton = PropertiesSelectionManager.Instance;
            if (singleton != null && (singleton.IsClientInitialized || singleton.IsServerInitialized))
            {
                _selectionManager = singleton;
                return;
            }

            PropertiesSelectionManager sceneManager = FindFirstObjectByType<PropertiesSelectionManager>();
            if (sceneManager != null && (sceneManager.IsClientInitialized || sceneManager.IsServerInitialized))
                _selectionManager = sceneManager;
        }

        private void Subscribe()
        {
            if (_subscribed || _selectionManager == null)
                return;

            _selectionManager.OnSelectionStateChanged += HandleSelectionStateChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _selectionManager == null)
                return;

            _selectionManager.OnSelectionStateChanged -= HandleSelectionStateChanged;
            _subscribed = false;
        }

        private void HandleSelectionStateChanged()
        {
            RebuildCurrentStageIfNeeded();
            RefreshUi();
        }

        private void RebuildCurrentStageIfNeeded(bool force = false)
        {
            if (_selectionManager == null)
                return;

            string currentStageKey = _selectionManager.CurrentStagePropertyKey;
            if (!force && currentStageKey == _lastRenderedStageKey)
                return;

            ClearGroups();
            _lastRenderedStageKey = currentStageKey;
            RefreshStageRoots(currentStageKey);

            if (currentStageKey == PropertiesSelectionManager.SkillLoadoutStageKey)
                return;

            Transform propertyGroupListRoot = GetPropertyGroupListRootForStage(currentStageKey);
            if (propertyGroupListRoot == null || propertyGroupPrefab == null)
                return;

            if (!_selectionManager.TryGetCurrentStageDefinition(out SelectablePropertyDefinition definition))
                return;

            PropertyGroupView group = Instantiate(propertyGroupPrefab, propertyGroupListRoot);
            group.Bind(definition, _selectionManager);
            _spawnedGroups.Add(group);
        }

        private void RefreshUi()
        {
            if (sharedUiRoot != null)
                sharedUiRoot.SetActive(true);

            SetText(pageTitleText, "Properties Selector");

            if (readyButton != null)
                readyButton.gameObject.SetActive(false);

            SetText(readyButtonText, string.Empty);

            if (_selectionManager == null)
            {
                EmitManagerDiagnosticIfNeeded();
                SetText(stageTitleText, "Waiting for PropertiesSelectionManager...");
                SetText(statusText, string.Empty);
                SetText(countdownText, string.Empty);
                SetText(playersSelectionText, string.Empty);
                SetText(resolvedMapText, string.Empty);
                return;
            }

            for (int i = 0; i < _spawnedGroups.Count; i++)
                _spawnedGroups[i].RefreshView();

            RefreshStatusTexts();
        }

        private void RefreshStatusTexts()
        {
            if (_selectionManager.IsTransitioningToMatch)
            {
                SetText(stageTitleText, "Loading Match");
                SetText(statusText, $"Loading {_selectionManager.ResolvedMatchSceneName}...");
                SetText(countdownText, string.Empty);
            }
            else if (_selectionManager.TryGetCurrentStageDefinition(out SelectablePropertyDefinition definition))
            {
                SetText(stageTitleText, $"Step {_selectionManager.CurrentStageIndex + 1}/{_selectionManager.TotalStageCount}: {definition.DisplayName}");
                SetText(statusText, BuildStageStatusText(definition));
                SetText(countdownText, $"Time Remaining: {_selectionManager.StageCountdownSecondsRemaining}s");
            }
            else if (_selectionManager.TotalStageCount <= 0)
            {
                SetText(stageTitleText, "Syncing stage data");
                SetText(statusText, "Waiting for host stage configuration...");
                SetText(countdownText, string.Empty);
            }
            else
            {
                SetText(stageTitleText, "All stages complete");
                SetText(statusText, "Waiting to enter match...");
                SetText(countdownText, string.Empty);
            }

            SetText(playersSelectionText, BuildPlayersSelectionText());

            string resolvedMapLabel = _selectionManager.TryGetResolvedSelectionOptionId("map", out string mapOptionId)
                ? $"Resolved Map: {mapOptionId}"
                : "Resolved Map: pending";
            SetText(resolvedMapText, resolvedMapLabel);
        }

        private string BuildStageStatusText(SelectablePropertyDefinition definition)
        {
            return definition.SelectionMode switch
            {
                PropertySelectionMode.Vote => "Everyone is voting on the current stage option.",
                PropertySelectionMode.HostOnly => "Only the host can choose during this stage.",
                PropertySelectionMode.Multi => "Choose all desired options before the timer ends.",
                _ => "Each player chooses one option before the timer ends."
            };
        }

        private string BuildPlayersSelectionText()
        {
            if (!_selectionManager.TryGetCurrentStagePropertyKey(out string propertyKey))
                return "No active stage.";

            List<string> lines = new List<string>();
            bool isSkillStage = propertyKey == PropertiesSelectionManager.SkillLoadoutStageKey;
            for (int i = 0; i < _selectionManager.Participants.Count; i++)
            {
                PropertiesSelectionManager.SelectionParticipantState participant = _selectionManager.Participants[i];
                string selectedOptionId = "Choosing...";

                if (isSkillStage)
                {
                    _selectionManager.TryGetPlayerSkillLoadoutSelection(participant.PlayerId, out string[] skillIds);
                    if (skillIds == null)
                        skillIds = new string[SkillLoadout.SlotCount];
                    int filledCount = 0;
                    List<string> names = new List<string>();
                    for (int slotIndex = 0; slotIndex < skillIds.Length; slotIndex++)
                    {
                        if (string.IsNullOrWhiteSpace(skillIds[slotIndex]))
                            continue;

                        filledCount++;
                        names.Add(skillIds[slotIndex]);
                    }

                    selectedOptionId = filledCount > 0
                        ? $"{filledCount}/{SkillLoadout.SlotCount} [{string.Join(", ", names)}]"
                        : "Choosing...";
                    lines.Add($"{participant.PlayerName}: {selectedOptionId}");
                    continue;
                }

                if (_selectionManager.TryGetPlayerSelection(participant.PlayerId, out PlayerPropertySelection selection)
                    && selection.TryGetSelectedOptionId(propertyKey, out string currentSelection)
                    && !string.IsNullOrWhiteSpace(currentSelection))
                {
                    selectedOptionId = currentSelection;
                }

                lines.Add($"{participant.PlayerName}: {selectedOptionId}");
            }

            return lines.Count > 0 ? string.Join("\n", lines) : "No players connected.";
        }

        private void ClearGroups()
        {
            for (int i = 0; i < _spawnedGroups.Count; i++)
            {
                if (_spawnedGroups[i] != null)
                    Destroy(_spawnedGroups[i].gameObject);
            }

            _spawnedGroups.Clear();
        }

        private void RefreshStageRoots(string currentStageKey)
        {
            bool isSkillStage = string.Equals(currentStageKey, PropertiesSelectionManager.SkillLoadoutStageKey, System.StringComparison.Ordinal);
            bool isSkinStage = string.Equals(currentStageKey, PropertiesSelectionManager.SkinStageKey, System.StringComparison.Ordinal);
            bool isMapStage = !isSkillStage && !isSkinStage;

            SetActiveSafe(mapSelectorRoot, isMapStage);
            SetActiveSafe(skillSelectorRoot, isSkillStage);
            SetActiveSafe(skinSelectorRoot, isSkinStage);
        }

        private Transform GetPropertyGroupListRootForStage(string currentStageKey)
        {
            if (string.Equals(currentStageKey, PropertiesSelectionManager.SkinStageKey, System.StringComparison.Ordinal))
                return skinPropertyGroupListRoot;

            if (string.Equals(currentStageKey, PropertiesSelectionManager.MapStageKey, System.StringComparison.Ordinal))
                return mapPropertyGroupListRoot;

            return null;
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
                target.text = value;
        }

        private static void SetActiveSafe(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }

        private void EmitManagerDiagnosticIfNeeded()
        {
            if (Time.unscaledTime < _nextManagerDiagnosticTime)
                return;

            _nextManagerDiagnosticTime = Time.unscaledTime + 2f;

            PropertiesSelectionManager sceneManager = FindFirstObjectByType<PropertiesSelectionManager>();
            bool hasSceneManager = sceneManager != null;
            bool isSpawned = hasSceneManager
                && sceneManager.NetworkObject != null
                && sceneManager.NetworkObject.IsSpawned;

            Debug.LogWarning($"[PropertiesSelectorUI] Waiting for network-ready PropertiesSelectionManager. sceneManagerFound={hasSceneManager}, spawned={isSpawned}, singletonNull={PropertiesSelectionManager.Instance == null}");
        }
    }
}
