using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SteamMultiplayer.Network;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Infinite looping carousel driven entirely by anchoredPosition.
    /// This does not rely on ScrollRect dragging.
    /// </summary>
    public class PropertyCarouselView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject root;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private Button leftArrowButton;
        [SerializeField] private Button rightArrowButton;
        [SerializeField] private Button selectButton;
        [SerializeField] private TextMeshProUGUI currentSelectionText;

        [Header("Layout")]
        [Tooltip("Horizontal distance between item centers.")]
        [SerializeField] private float spacing = 320f;
        [Tooltip("Vertical anchored position applied to every item.")]
        [SerializeField] private float itemYOffset = 0f;

        [Header("Animation")]
        [Tooltip("How long one left/right step takes.")]
        [SerializeField] private float animationDuration = 0.25f;
        [Tooltip("Extra smoothing applied while approaching the target step.")]
        [SerializeField] private float animationSharpness = 12f;

        [Header("Scaling")]
        [Tooltip("Scale for items furthest from center.")]
        [SerializeField] private float minScale = 0.8f;
        [Tooltip("Scale for the centered item.")]
        [SerializeField] private float maxScale = 1.2f;
        [Tooltip("How quickly scale falls off away from center.")]
        [SerializeField] private float scaleFalloff = 0.65f;

        private readonly List<PropertyOptionItemView> _spawnedItems = new List<PropertyOptionItemView>();
        private SelectablePropertyDefinition _definition;
        private PropertiesSelectionManager _selectionManager;
        private float _animatedCenterIndex;
        private float _targetCenterIndex;
        private bool _isAnimating;
        private float _stepVelocity;
        private string _lastSubmittedOptionId = string.Empty;

        private int CenteredItemIndex => _spawnedItems.Count == 0
            ? -1
            : PositiveMod(Mathf.RoundToInt(_animatedCenterIndex), _spawnedItems.Count);

        private void Awake()
        {
            if (scrollRect != null)
            {
                // Legacy field kept for prefab compatibility, but this carousel is fully script-driven.
                scrollRect.enabled = false;
            }

            NormalizeViewportAndContent();
            DisableLegacyLayoutDrivers();

            if (leftArrowButton != null)
                leftArrowButton.onClick.AddListener(MoveLeft);

            if (rightArrowButton != null)
                rightArrowButton.onClick.AddListener(MoveRight);

            if (selectButton != null)
                selectButton.onClick.AddListener(ConfirmCenteredSelection);
        }

        private void Update()
        {
            if (root != null && !root.activeInHierarchy)
                return;

            if (_spawnedItems.Count == 0)
                return;

            UpdateAnimation();
            UpdateItemLayoutAndScaling();
            UpdateCurrentSelectionLabel();
        }

        private void OnDestroy()
        {
            if (leftArrowButton != null)
                leftArrowButton.onClick.RemoveListener(MoveLeft);

            if (rightArrowButton != null)
                rightArrowButton.onClick.RemoveListener(MoveRight);

            if (selectButton != null)
                selectButton.onClick.RemoveListener(ConfirmCenteredSelection);
        }

        public void Bind(
            SelectablePropertyDefinition definition,
            PropertiesSelectionManager selectionManager,
            PropertyOptionItemView optionItemPrefab)
        {
            _definition = definition;
            _selectionManager = selectionManager;

            if (root != null)
                root.SetActive(true);

            NormalizeViewportAndContent();
            RebuildItems(optionItemPrefab);
            SnapToLocalSelectionOrFirst();
            RefreshState();
        }

        public void RefreshState()
        {
            if (_selectionManager == null)
                return;

            string localSelectedOptionId = string.Empty;
            bool hasLocalSelection = _selectionManager.TryGetLocalPlayerSelection(out PlayerPropertySelection selection)
                && selection.TryGetSelectedOptionId(_definition.PropertyKey, out localSelectedOptionId);
            _lastSubmittedOptionId = hasLocalSelection ? localSelectedOptionId : string.Empty;

            bool isHost = _selectionManager.TryGetLocalParticipant(out PropertiesSelectionManager.SelectionParticipantState participant)
                && participant.IsHost;
            bool interactable = CanInteract(isHost);

            for (int i = 0; i < _spawnedItems.Count; i++)
            {
                PropertyOptionItemView item = _spawnedItems[i];
                bool isSelected = hasLocalSelection && item.OptionId == localSelectedOptionId;
                item.RefreshState(isSelected, BuildOptionStateText(item.OptionId), interactable);
            }

            if (leftArrowButton != null)
                leftArrowButton.interactable = interactable && _spawnedItems.Count > 1;

            if (rightArrowButton != null)
                rightArrowButton.interactable = interactable && _spawnedItems.Count > 1;

            if (selectButton != null)
                selectButton.interactable = interactable && CenteredItemIndex >= 0;
        }

        private void RebuildItems(PropertyOptionItemView optionItemPrefab)
        {
            ClearItems();

            if (content == null || optionItemPrefab == null || _selectionManager == null)
                return;

            List<SelectablePropertyOption> options = _selectionManager.GetOptionsForProperty(_definition.PropertyKey);
            for (int i = 0; i < options.Count; i++)
            {
                PropertyOptionItemView item = Instantiate(optionItemPrefab, content);
                RectTransform itemRect = item.transform as RectTransform;
                if (itemRect != null)
                {
                    itemRect.anchorMin = new Vector2(0.5f, 0.5f);
                    itemRect.anchorMax = new Vector2(0.5f, 0.5f);
                    itemRect.pivot = new Vector2(0.5f, 0.5f);
                }

                item.Bind(_definition, options[i], _selectionManager, HandleItemClicked);
                _spawnedItems.Add(item);
            }
        }

        private void HandleItemClicked(PropertyOptionItemView item)
        {
            if (!CanInteractForCurrentPlayer())
                return;

            int itemIndex = _spawnedItems.IndexOf(item);
            if (itemIndex < 0)
                return;

            FocusItem(itemIndex);
        }

        private void MoveLeft()
        {
            if (_spawnedItems.Count <= 1 || !CanInteractForCurrentPlayer())
                return;

            _targetCenterIndex -= 1f;
            _isAnimating = true;
        }

        private void MoveRight()
        {
            if (_spawnedItems.Count <= 1 || !CanInteractForCurrentPlayer())
                return;

            _targetCenterIndex += 1f;
            _isAnimating = true;
        }

        private void ConfirmCenteredSelection()
        {
            if (!CanInteractForCurrentPlayer())
                return;

            int centeredIndex = CenteredItemIndex;
            if (centeredIndex < 0 || centeredIndex >= _spawnedItems.Count)
                return;

            string optionId = _spawnedItems[centeredIndex].OptionId;
            _lastSubmittedOptionId = optionId;
            _selectionManager?.SubmitPlayerSelection(_definition.PropertyKey, optionId);
            RefreshState();
        }

        private void FocusItem(int targetItemIndex)
        {
            int count = _spawnedItems.Count;
            if (count == 0)
                return;

            int currentCenteredIndex = CenteredItemIndex;
            if (currentCenteredIndex < 0)
                currentCenteredIndex = 0;

            int delta = targetItemIndex - currentCenteredIndex;
            int half = count / 2;
            if (delta > half)
                delta -= count;
            else if (delta < -half)
                delta += count;

            _targetCenterIndex += delta;
            _isAnimating = true;
        }

        private void SnapToLocalSelectionOrFirst()
        {
            if (_spawnedItems.Count == 0)
                return;

            string localSelectedOptionId = string.Empty;
            bool hasLocalSelection = _selectionManager.TryGetLocalPlayerSelection(out PlayerPropertySelection selection)
                && selection.TryGetSelectedOptionId(_definition.PropertyKey, out localSelectedOptionId);

            int targetIndex = 0;
            if (hasLocalSelection)
            {
                for (int i = 0; i < _spawnedItems.Count; i++)
                {
                    if (_spawnedItems[i].OptionId == localSelectedOptionId)
                    {
                        targetIndex = i;
                        break;
                    }
                }
            }

            _animatedCenterIndex = targetIndex;
            _targetCenterIndex = targetIndex;
            _isAnimating = false;
            _stepVelocity = 0f;
            UpdateItemLayoutAndScaling();
            UpdateCurrentSelectionLabel();
        }

        private void UpdateAnimation()
        {
            if (!_isAnimating)
                return;

            float smoothTime = Mathf.Max(0.01f, animationDuration);
            _animatedCenterIndex = Mathf.SmoothDamp(
                _animatedCenterIndex,
                _targetCenterIndex,
                ref _stepVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.deltaTime * Mathf.Max(1f, animationSharpness));

            if (Mathf.Abs(_animatedCenterIndex - _targetCenterIndex) <= 0.001f)
            {
                _animatedCenterIndex = _targetCenterIndex;
                _isAnimating = false;
                _stepVelocity = 0f;
            }
        }

        private void UpdateItemLayoutAndScaling()
        {
            int count = _spawnedItems.Count;
            if (count == 0)
                return;

            List<(PropertyOptionItemView item, float distance)> ordering = new List<(PropertyOptionItemView, float)>(count);

            for (int i = 0; i < count; i++)
            {
                PropertyOptionItemView item = _spawnedItems[i];
                RectTransform itemRect = item.transform as RectTransform;
                if (itemRect == null)
                    continue;

                float relativeIndex = GetWrappedRelativeIndex(i, _animatedCenterIndex, count);
                itemRect.anchoredPosition = new Vector2(relativeIndex * spacing, itemYOffset);

                float normalizedDistance = Mathf.Clamp01(Mathf.Abs(relativeIndex) * scaleFalloff);
                float scale = Mathf.Lerp(maxScale, minScale, normalizedDistance);
                itemRect.localScale = Vector3.one * scale;

                ordering.Add((item, Mathf.Abs(relativeIndex)));
            }

            ordering.Sort((a, b) => b.distance.CompareTo(a.distance));
            for (int i = 0; i < ordering.Count; i++)
                ordering[i].item.transform.SetSiblingIndex(i);
        }

        private void UpdateCurrentSelectionLabel()
        {
            if (currentSelectionText == null)
                return;

            int centeredIndex = CenteredItemIndex;
            if (centeredIndex < 0 || centeredIndex >= _spawnedItems.Count)
            {
                currentSelectionText.text = "Focused: None";
                return;
            }

            PropertyOptionItemView item = _spawnedItems[centeredIndex];
            string confirmedSuffix = string.IsNullOrWhiteSpace(_lastSubmittedOptionId)
                ? "Confirmed: None"
                : $"Confirmed: {_lastSubmittedOptionId}";
            currentSelectionText.text = $"Focused: {item.OptionId}\n{confirmedSuffix}";
        }

        private string BuildOptionStateText(string optionId)
        {
            int count = _selectionManager.GetSelectionCountForOption(_definition.PropertyKey, optionId);
            return _definition.SelectionMode == PropertySelectionMode.Vote
                ? $"Votes: {count}"
                : $"Selected: {count}";
        }

        private static float GetWrappedRelativeIndex(int itemIndex, float centerIndex, int count)
        {
            float delta = itemIndex - centerIndex;
            float half = count / 2f;

            while (delta > half)
                delta -= count;

            while (delta < -half)
                delta += count;

            return delta;
        }

        private static int PositiveMod(int value, int modulus)
        {
            if (modulus <= 0)
                return 0;

            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private void ClearItems()
        {
            for (int i = 0; i < _spawnedItems.Count; i++)
            {
                if (_spawnedItems[i] != null)
                    Destroy(_spawnedItems[i].gameObject);
            }

            _spawnedItems.Clear();
        }

        private void DisableLegacyLayoutDrivers()
        {
            if (content == null)
                return;

            LayoutGroup layoutGroup = content.GetComponent<LayoutGroup>();
            if (layoutGroup != null)
                layoutGroup.enabled = false;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                fitter.enabled = false;
        }

        private void NormalizeViewportAndContent()
        {
            if (viewport != null)
            {
                viewport.anchorMin = new Vector2(0f, 0f);
                viewport.anchorMax = new Vector2(1f, 1f);
                viewport.pivot = new Vector2(0.5f, 0.5f);
                viewport.anchoredPosition = Vector2.zero;
                viewport.sizeDelta = Vector2.zero;
            }

            if (content != null)
            {
                content.anchorMin = new Vector2(0.5f, 0.5f);
                content.anchorMax = new Vector2(0.5f, 0.5f);
                content.pivot = new Vector2(0.5f, 0.5f);
                content.anchoredPosition = Vector2.zero;

                if (viewport != null)
                    content.sizeDelta = new Vector2(viewport.rect.width, viewport.rect.height);
            }
        }

        private bool CanInteractForCurrentPlayer()
        {
            if (_selectionManager != null
                && _selectionManager.TryGetLocalPlayerSelection(out PlayerPropertySelection selection)
                && selection.TryGetSelectedOptionId(_definition.PropertyKey, out string selectedOptionId)
                && !string.IsNullOrWhiteSpace(selectedOptionId))
            {
                return false;
            }

            bool isHost = _selectionManager != null
                && _selectionManager.TryGetLocalParticipant(out PropertiesSelectionManager.SelectionParticipantState participant)
                && participant.IsHost;
            return CanInteract(isHost);
        }

        private bool CanInteract(bool isHost)
        {
            if (_selectionManager == null)
                return false;

            return !_selectionManager.IsTransitioningToMatch
                && _selectionManager.IsStageCountdownActive
                && (_definition.SelectionMode != PropertySelectionMode.HostOnly || isHost);
        }
    }
}
