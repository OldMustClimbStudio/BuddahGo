using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SteamMultiplayer.Network;

namespace SteamMultiplayer.UI
{
    public class SkillLoadoutSelectorUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Transform poolCardRoot;
        [SerializeField] private SkillCardItemView skillCardPrefab;
        [SerializeField] private SkillLoadoutSlotView[] slotViews;
        [SerializeField] private SkillLoadoutPoolDropZone poolDropZone;
        [SerializeField] private SkillLoadoutDragController dragController;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button clearAllButton;
        [SerializeField] private TextMeshProUGUI selectionCountText;
        [SerializeField] private TextMeshProUGUI submitHintText;
        [SerializeField] private TextMeshProUGUI feedbackText;

        private readonly List<SkillCardItemView> _poolCards = new List<SkillCardItemView>();
        private readonly Dictionary<string, SelectablePropertyOption> _optionsById = new Dictionary<string, SelectablePropertyOption>();
        private readonly string[] _draftSkillIds = new string[SkillLoadout.SlotCount];
        private PropertiesSelectionManager _selectionManager;
        private bool _draftInitialized;
        private bool _draftDirty;
        private string _lastOptionSignature = string.Empty;
        private string _lastServerSignature = string.Empty;

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(HandleConfirmClicked);
            if (clearAllButton != null)
                clearAllButton.onClick.AddListener(ClearAllSlots);

            if (slotViews != null)
            {
                for (int i = 0; i < slotViews.Length; i++)
                    slotViews[i]?.Bind(i, this);
            }

            poolDropZone?.Bind(this);
        }

        private void OnDestroy()
        {
            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(HandleConfirmClicked);
            if (clearAllButton != null)
                clearAllButton.onClick.RemoveListener(ClearAllSlots);
        }

        public void Bind(PropertiesSelectionManager selectionManager)
        {
            if (_selectionManager == selectionManager)
                return;

            _selectionManager = selectionManager;
            _draftInitialized = false;
            _draftDirty = false;
            _lastOptionSignature = string.Empty;
            RebuildPoolCards();
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
                root.SetActive(visible);
        }

        public void RefreshView()
        {
            if (root != null && !root.activeSelf)
                return;

            if (_selectionManager == null)
                return;

            SyncDraftFromServerIfNeeded();
            RebuildPoolCards();
            RefreshPoolCards();
            RefreshSlotCards();
            RefreshTexts();
        }

        public void HandleCardDroppedIntoSlot(SkillCardItemView cardView, int targetSlotIndex)
        {
            if (!CanEdit() || cardView == null || targetSlotIndex < 0 || targetSlotIndex >= SkillLoadout.SlotCount)
                return;

            string skillId = cardView.SkillId;
            int sourceSlotIndex = cardView.SourceSlotIndex;
            int existingSlotIndex = FindSkillInDraft(skillId);
            if (!_selectionManager.AllowDuplicateSkillSelections
                && existingSlotIndex >= 0
                && existingSlotIndex != sourceSlotIndex
                && existingSlotIndex != targetSlotIndex)
            {
                SetFeedback($"Duplicate skill '{skillId}' is not allowed.");
                return;
            }

            if (sourceSlotIndex >= 0)
            {
                string targetSkillId = _draftSkillIds[targetSlotIndex];
                _draftSkillIds[targetSlotIndex] = skillId;
                _draftSkillIds[sourceSlotIndex] = sourceSlotIndex == targetSlotIndex ? skillId : targetSkillId;
            }
            else
            {
                _draftSkillIds[targetSlotIndex] = skillId;
            }

            _draftDirty = true;
            SetFeedback($"Draft updated: slot {targetSlotIndex + 1} -> {GetDisplayName(skillId)}");
            RefreshView();
        }

        public void HandleCardDroppedToPool(SkillCardItemView cardView)
        {
            if (cardView == null || cardView.SourceSlotIndex < 0)
                return;

            ClearSlot(cardView.SourceSlotIndex);
        }

        public void ClearSlot(int slotIndex)
        {
            if (!CanEdit() || slotIndex < 0 || slotIndex >= SkillLoadout.SlotCount)
                return;

            _draftSkillIds[slotIndex] = string.Empty;
            _draftDirty = true;
            SetFeedback($"Cleared slot {slotIndex + 1}.");
            RefreshView();
        }

        public void ClearAllSlots()
        {
            if (!CanEdit())
                return;

            for (int i = 0; i < _draftSkillIds.Length; i++)
                _draftSkillIds[i] = string.Empty;

            _draftDirty = true;
            SetFeedback("Cleared all draft slots.");
            RefreshView();
        }

        private void HandleConfirmClicked()
        {
            if (!CanEdit())
                return;

            _selectionManager.SubmitSkillLoadoutSelection(_draftSkillIds);
            _draftDirty = false;
            _lastServerSignature = BuildSignature(_draftSkillIds);
            SetFeedback(GetFilledCount() >= SkillLoadout.SlotCount
                ? "Submitted 3/3 skills. Waiting for other players."
                : "Saved partial draft to server. Fill all 3 slots to complete the stage.");
            RefreshView();
        }

        private void SyncDraftFromServerIfNeeded()
        {
            string[] serverSkillIds = null;
            _selectionManager.TryGetLocalSkillLoadoutSelection(out serverSkillIds);
            string serverSignature = BuildSignature(serverSkillIds);

            if (!_draftInitialized || (!_draftDirty && serverSignature != _lastServerSignature))
            {
                for (int i = 0; i < _draftSkillIds.Length; i++)
                    _draftSkillIds[i] = serverSkillIds != null && i < serverSkillIds.Length ? serverSkillIds[i] ?? string.Empty : string.Empty;
                _lastServerSignature = serverSignature;
                _draftInitialized = true;
            }
        }

        private void RebuildPoolCards()
        {
            if (_selectionManager == null || poolCardRoot == null || skillCardPrefab == null)
                return;

            List<SelectablePropertyOption> options = _selectionManager.GetOptionsForProperty(PropertiesSelectionManager.SkillLoadoutStageKey);
            string signature = BuildOptionSignature(options);
            if (signature == _lastOptionSignature && _poolCards.Count == options.Count)
                return;

            _lastOptionSignature = signature;
            _optionsById.Clear();

            for (int i = 0; i < _poolCards.Count; i++)
            {
                if (_poolCards[i] != null)
                    Destroy(_poolCards[i].gameObject);
            }
            _poolCards.Clear();

            for (int i = 0; i < options.Count; i++)
            {
                SelectablePropertyOption option = options[i];
                _optionsById[option.OptionId] = option;
                SkillCardItemView view = Instantiate(skillCardPrefab, poolCardRoot);
                view.Bind(this, dragController, option.OptionId, option.DisplayName, -1);
                _poolCards.Add(view);
            }
        }

        private void RefreshPoolCards()
        {
            bool canEdit = CanEdit();
            for (int i = 0; i < _poolCards.Count; i++)
            {
                SkillCardItemView poolCard = _poolCards[i];
                if (poolCard == null)
                    continue;

                int slotIndex = FindSkillInDraft(poolCard.SkillId);
                string stateText = slotIndex >= 0 ? $"In Slot {slotIndex + 1}" : "Available";
                poolCard.RefreshState(slotIndex >= 0, stateText, canEdit);
            }
        }

        private void RefreshSlotCards()
        {
            bool canEdit = CanEdit();
            if (slotViews == null)
                return;

            for (int i = 0; i < slotViews.Length; i++)
            {
                SkillLoadoutSlotView slotView = slotViews[i];
                if (slotView == null)
                    continue;

                string skillId = i < _draftSkillIds.Length ? _draftSkillIds[i] : string.Empty;
                if (string.IsNullOrWhiteSpace(skillId))
                {
                    slotView.ClearCard();
                    continue;
                }

                slotView.SetCard(skillCardPrefab, dragController, skillId, GetDisplayName(skillId), canEdit);
            }
        }

        private void RefreshTexts()
        {
            int filledCount = GetFilledCount();
            if (selectionCountText != null)
                selectionCountText.text = $"Selected {filledCount}/{SkillLoadout.SlotCount}";

            if (submitHintText != null)
                submitHintText.text = filledCount >= SkillLoadout.SlotCount
                    ? "Ready to submit. All 3 slots are filled."
                    : "You can save a partial draft, but the stage is only complete at 3/3.";

            if (confirmButton != null)
                confirmButton.interactable = CanEdit();

            if (clearAllButton != null)
                clearAllButton.interactable = CanEdit();
        }

        private int GetFilledCount()
        {
            int filledCount = 0;
            for (int i = 0; i < _draftSkillIds.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(_draftSkillIds[i]))
                    filledCount++;
            }
            return filledCount;
        }

        private int FindSkillInDraft(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return -1;

            for (int i = 0; i < _draftSkillIds.Length; i++)
            {
                if (_draftSkillIds[i] == skillId)
                    return i;
            }

            return -1;
        }

        private string GetDisplayName(string skillId)
        {
            if (!string.IsNullOrWhiteSpace(skillId) && _optionsById.TryGetValue(skillId, out SelectablePropertyOption option))
                return string.IsNullOrWhiteSpace(option.DisplayName) ? skillId : option.DisplayName;

            return skillId;
        }

        private bool CanEdit()
        {
            return _selectionManager != null
                && _selectionManager.IsStageCountdownActive
                && !_selectionManager.IsTransitioningToMatch
                && _selectionManager.CurrentStagePropertyKey == PropertiesSelectionManager.SkillLoadoutStageKey;
        }

        private string BuildOptionSignature(List<SelectablePropertyOption> options)
        {
            if (options == null || options.Count == 0)
                return string.Empty;

            List<string> ids = new List<string>();
            for (int i = 0; i < options.Count; i++)
                ids.Add(options[i].OptionId);

            return string.Join("|", ids);
        }

        private static string BuildSignature(IReadOnlyList<string> values)
        {
            if (values == null)
                return string.Empty;

            List<string> parts = new List<string>();
            for (int i = 0; i < values.Count; i++)
                parts.Add(values[i] ?? string.Empty);

            return string.Join("|", parts);
        }

        private void SetFeedback(string message)
        {
            if (feedbackText != null)
                feedbackText.text = message;
        }
    }
}
