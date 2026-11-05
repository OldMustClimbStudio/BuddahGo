using System;
using System.Collections.Generic;
using BuddahGo.Match;
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

        [Header("缘起 List (runtime text under the stage title, skill stage only)")]
        [SerializeField] private float yuanqiListFontSize = 26f;
        [SerializeField] private float yuanqiListGap = 6f;
        [SerializeField] private float yuanqiListHeight = 150f;
        [SerializeField] private float skillWallLookupInterval = 1f;

        [Header("Debug")]
        [SerializeField] private bool forceRefreshEveryFrame = false;

        private readonly List<PropertyGroupView> _spawnedGroups = new List<PropertyGroupView>();
        private PropertiesSelectionManager _selectionManager;
        private bool _subscribed;
        private string _lastRenderedStageKey = string.Empty;
        private float _nextManagerDiagnosticTime;
        private TextMeshProUGUI _yuanqiListText;
        private SkillWallSelectionController _skillWall;
        private float _nextSkillWallLookupTime;
        private string _yuanqiListSignature;
        private readonly List<string> _chosenSkillIds = new List<string>();

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

            RefreshSelectionVisibility();
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

        private bool RefreshSelectionVisibility()
        {
            // This UI lives on the shared HUD. Its selection labels must not persist into racing/results.
            RoomStateManager room = RoomStateManager.Instance;
            bool showSelection = room == null || (!room.IsMatchPhaseActive && !room.IsResultPhaseActive);
            SetSelectionSummaryVisible(showSelection);
            return showSelection;
        }

        private void RefreshUi()
        {
            if (!RefreshSelectionVisibility()) return;

            if (sharedUiRoot != null)
                sharedUiRoot.SetActive(true);

            SetText(pageTitleText, MatchRules.Current.IsSolo ? "Practice" : "Properties Selector");

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
                SetYuanqiListText(string.Empty);
                return;
            }

            for (int i = 0; i < _spawnedGroups.Count; i++)
                _spawnedGroups[i].RefreshView();

            RefreshStatusTexts();
            RefreshYuanqiList();
        }

        private void SetSelectionSummaryVisible(bool visible)
        {
            if (pageTitleText != null) pageTitleText.gameObject.SetActive(visible);
            if (stageTitleText != null) stageTitleText.gameObject.SetActive(visible);
            // Practice already chose its map and has no peer readiness or selection timer.
            // These legacy summaries sit over the 3D skill cards; keep the stage heading instead.
            // Recompute on refresh so returning to online selection restores the original HUD.
            bool showSessionSummary = visible && !MatchRules.Current.IsSolo;
            if (statusText != null) statusText.gameObject.SetActive(showSessionSummary);
            if (countdownText != null) countdownText.gameObject.SetActive(showSessionSummary);
            if (playersSelectionText != null) playersSelectionText.gameObject.SetActive(showSessionSummary);
            if (resolvedMapText != null) resolvedMapText.gameObject.SetActive(showSessionSummary);
            if (!visible) SetYuanqiListText(string.Empty);
        }

        // A6: the 缘起 the chosen skills can trigger (YuanqiCatalog, the same answer the server registry uses), from the
        // local draft on the skill wall (falls back to the submitted selection). Rebuilt only when the ids change.
        private void RefreshYuanqiList()
        {
            bool skillStage = _selectionManager != null && !_selectionManager.IsTransitioningToMatch
                && string.Equals(_selectionManager.CurrentStagePropertyKey, PropertiesSelectionManager.SkillLoadoutStageKey, StringComparison.Ordinal);
            if (!skillStage)
            {
                SetYuanqiListText(string.Empty);
                return;
            }

            EnsureYuanqiListText();
            if (_yuanqiListText == null)
                return;

            CollectChosenSkillIds(_chosenSkillIds);
            string signature = HudGlyphCoverage.GlobalFallbackCount() + "|" + string.Join("|", _chosenSkillIds);
            if (string.Equals(signature, _yuanqiListSignature, StringComparison.Ordinal))
                return;
            _yuanqiListSignature = signature;

            TMP_FontAsset font = _yuanqiListText.font;
            SkillDatabase database = HudSkillDatabase.Resolve();
            var entries = new List<YuanqiHudEntry>();
            if (database != null)
            {
                foreach (YuanqiAction pair in YuanqiCatalog.ForLoadout(database.GetYuanqis(), _chosenSkillIds))
                {
                    entries.Add(new YuanqiHudEntry(
                        HudSkillDatabase.YuanqiName(database, font, pair.yuanqiId),
                        HudSkillDatabase.SkillName(database, font, pair.skillA),
                        HudSkillDatabase.SkillName(database, font, pair.skillB)));
                }
            }

            SetYuanqiListText(YuanqiHudText.LoadoutList(
                HudGlyphCoverage.Resolve(font, InGameHudLabels.YuanqiLoadoutHeader),
                HudGlyphCoverage.Resolve(font, InGameHudLabels.YuanqiLoadoutNone),
                entries, _chosenSkillIds.Count));
        }

        private void CollectChosenSkillIds(List<string> results)
        {
            results.Clear();
            if (_skillWall == null && Time.unscaledTime >= _nextSkillWallLookupTime)
            {
                _nextSkillWallLookupTime = Time.unscaledTime + skillWallLookupInterval;
                _skillWall = FindFirstObjectByType<SkillWallSelectionController>();
            }

            IReadOnlyList<string> ids = _skillWall != null ? _skillWall.SelectedSkillIds : null;
            if (ids == null && _selectionManager != null && _selectionManager.TryGetLocalSkillLoadoutSelection(out string[] submitted))
                ids = submitted;
            if (ids == null)
                return;

            for (int i = 0; i < ids.Count; i++)
            {
                string id = (ids[i] ?? string.Empty).Trim();
                if (id.Length > 0 && !results.Contains(id))
                    results.Add(id);
            }
        }

        private void EnsureYuanqiListText()
        {
            if (_yuanqiListText != null || stageTitleText == null)
                return;
            RectTransform title = stageTitleText.rectTransform;
            if (!(title.parent is RectTransform parent))
                return;

            var pivot = new Vector2(title.pivot.x, 1f);
            Vector2 position = title.anchoredPosition
                + new Vector2(0f, -title.rect.height * title.pivot.y - yuanqiListGap);
            _yuanqiListText = HudRuntimeText.Create(parent, "YuanqiLoadoutList", stageTitleText.font, yuanqiListFontSize,
                stageTitleText.color, TextAlignmentOptions.TopLeft, title.anchorMin, pivot, position,
                new Vector2(title.rect.width, yuanqiListHeight));
            _yuanqiListText.horizontalAlignment = stageTitleText.horizontalAlignment;
            _yuanqiListText.verticalAlignment = VerticalAlignmentOptions.Top;
            _yuanqiListSignature = null;
        }

        private void SetYuanqiListText(string value)
        {
            if (_yuanqiListText == null)
                return;
            if (value.Length == 0)
                _yuanqiListSignature = null;
            HudRuntimeText.SetText(_yuanqiListText, value);
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
                string stageLabel = MatchRules.Current.IsSolo
                    && _selectionManager.CurrentStagePropertyKey == PropertiesSelectionManager.SkillLoadoutStageKey
                    ? $"Choose {SkillLoadout.SlotCount} skills"
                    : definition.DisplayName;
                SetText(stageTitleText, $"Step {_selectionManager.CurrentStageIndex + 1}/{_selectionManager.TotalStageCount}: {stageLabel}");
                SetText(statusText, BuildStageStatusText(definition));
                SetText(countdownText, MatchRules.Current.SelectionTimeoutEnabled
                    ? $"Time Remaining: {_selectionManager.StageCountdownSecondsRemaining}s" : string.Empty);
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
                PropertySelectionMode.Multi => MatchRules.Current.SelectionTimeoutEnabled
                    ? "Choose all desired options before the timer ends." : "Choose all desired options.",
                _ => MatchRules.Current.SelectionTimeoutEnabled
                    ? "Each player chooses one option before the timer ends." : "Each player chooses one option."
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
