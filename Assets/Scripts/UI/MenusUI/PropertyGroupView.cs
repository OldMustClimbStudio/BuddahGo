using System.Collections.Generic;
using TMPro;
using UnityEngine;
using SteamMultiplayer.Network;

namespace SteamMultiplayer.UI
{
    public class PropertyGroupView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI propertyNameText;
        [SerializeField] private TextMeshProUGUI localSelectionText;
        [SerializeField] private TextMeshProUGUI sharedStateText;
        [SerializeField] private GameObject listLayoutRoot;
        [SerializeField] private Transform optionListRoot;
        [SerializeField] private PropertyOptionItemView optionItemPrefab;
        [SerializeField] private PropertyCarouselView carouselView;

        private readonly List<PropertyOptionItemView> _spawnedItems = new List<PropertyOptionItemView>();
        private SelectablePropertyDefinition _definition;
        private PropertiesSelectionManager _selectionManager;

        public string PropertyKey => _definition.PropertyKey;

        public void Bind(SelectablePropertyDefinition definition, PropertiesSelectionManager selectionManager)
        {
            _definition = definition;
            _selectionManager = selectionManager;

            if (propertyNameText != null)
                propertyNameText.text = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.PropertyKey : definition.DisplayName;

            ConfigureLayoutMode();
            RebuildOptions();
            RefreshView();
        }

        public void RefreshView()
        {
            if (_selectionManager == null)
                return;

            string localSelectedOptionId = string.Empty;
            bool hasLocalSelection = _selectionManager.TryGetLocalPlayerSelection(out PlayerPropertySelection localSelection)
                && localSelection.TryGetSelectedOptionId(_definition.PropertyKey, out localSelectedOptionId);

            if (localSelectionText != null)
                localSelectionText.text = hasLocalSelection
                    ? $"Selected: {GetOptionDisplayName(localSelectedOptionId)}"
                    : "Selected: None";

            if (sharedStateText != null)
                sharedStateText.text = BuildGroupStateText();

            if (carouselView != null && ShouldUseCarouselLayout())
                carouselView.RefreshState();

            bool isHost = _selectionManager.TryGetLocalParticipant(out PropertiesSelectionManager.SelectionParticipantState participant)
                && participant.IsHost;
            bool hasConfirmedSelection = hasLocalSelection && !string.IsNullOrWhiteSpace(localSelectedOptionId);
            bool interactableForLocalPlayer = _selectionManager.IsStageCountdownActive
                && !_selectionManager.IsTransitioningToMatch
                && !hasConfirmedSelection
                && (_definition.SelectionMode != PropertySelectionMode.HostOnly || isHost);

            List<SelectablePropertyOption> options = _selectionManager.GetOptionsForProperty(_definition.PropertyKey);
            for (int i = 0; i < _spawnedItems.Count && i < options.Count; i++)
            {
                SelectablePropertyOption option = options[i];
                bool isSelected = hasLocalSelection && localSelectedOptionId == option.OptionId;
                bool interactable = option.IsUnlocked && interactableForLocalPlayer;

                _spawnedItems[i].RefreshState(isSelected, BuildOptionStateText(option), interactable);
            }
        }

        private void RebuildOptions()
        {
            ClearSpawnedItems();

            if (ShouldUseCarouselLayout())
            {
                if (carouselView != null)
                    carouselView.Bind(_definition, _selectionManager, optionItemPrefab);

                return;
            }

            if (optionListRoot == null || optionItemPrefab == null)
                return;

            List<SelectablePropertyOption> options = _selectionManager.GetOptionsForProperty(_definition.PropertyKey);
            for (int i = 0; i < options.Count; i++)
            {
                PropertyOptionItemView item = Instantiate(optionItemPrefab, optionListRoot);
                item.Bind(_definition, options[i], _selectionManager);
                _spawnedItems.Add(item);
            }
        }

        private string BuildGroupStateText()
        {
            return _definition.SelectionMode switch
            {
                PropertySelectionMode.Vote => "Mode: Vote",
                PropertySelectionMode.HostOnly => "Mode: Host Only",
                PropertySelectionMode.Multi => "Mode: Multi Select",
                _ => "Mode: Single Select"
            };
        }

        private string BuildOptionStateText(SelectablePropertyOption option)
        {
            if (!option.IsUnlocked)
                return "Locked";

            int selectedCount = _selectionManager.GetSelectionCountForOption(_definition.PropertyKey, option.OptionId);
            return _definition.SelectionMode == PropertySelectionMode.Vote
                ? $"Votes: {selectedCount}"
                : $"Players: {selectedCount}";
        }

        private string GetOptionDisplayName(string optionId)
        {
            List<SelectablePropertyOption> options = _selectionManager.GetOptionsForProperty(_definition.PropertyKey);
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].OptionId == optionId)
                    return string.IsNullOrWhiteSpace(options[i].DisplayName) ? optionId : options[i].DisplayName;
            }

            return optionId;
        }

        private void ConfigureLayoutMode()
        {
            bool useCarousel = ShouldUseCarouselLayout();

            if (listLayoutRoot != null)
                listLayoutRoot.SetActive(!useCarousel);

            if (carouselView != null)
                carouselView.gameObject.SetActive(useCarousel);
        }

        private bool ShouldUseCarouselLayout()
        {
            return string.Equals(_definition.PropertyKey, "map", System.StringComparison.Ordinal)
                || string.Equals(_definition.PropertyKey, "skin", System.StringComparison.Ordinal);
        }

        private void ClearSpawnedItems()
        {
            for (int i = 0; i < _spawnedItems.Count; i++)
            {
                if (_spawnedItems[i] != null)
                    Destroy(_spawnedItems[i].gameObject);
            }

            _spawnedItems.Clear();
        }
    }
}
