using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SteamMultiplayer.Network;
using System;

namespace SteamMultiplayer.UI
{
    public class PropertyOptionItemView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button selectButton;
        [SerializeField] private TextMeshProUGUI optionNameText;
        [SerializeField] private TextMeshProUGUI optionDescriptionText;
        [SerializeField] private TextMeshProUGUI optionStateText;
        [SerializeField] private GameObject selectedHighlight;

        private SelectablePropertyDefinition _definition;
        private SelectablePropertyOption _option;
        private PropertiesSelectionManager _selectionManager;
        private Action<PropertyOptionItemView> _clickOverride;

        public string OptionId => _option.OptionId;

        private void Awake()
        {
            EnsureButtonReference();

            if (selectButton != null)
                selectButton.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            if (selectButton != null)
                selectButton.onClick.RemoveListener(HandleClicked);
        }

        public void Bind(
            SelectablePropertyDefinition definition,
            SelectablePropertyOption option,
            PropertiesSelectionManager selectionManager,
            Action<PropertyOptionItemView> clickOverride = null)
        {
            _definition = definition;
            _option = option;
            _selectionManager = selectionManager;
            _clickOverride = clickOverride;

            if (optionNameText != null)
                optionNameText.text = string.IsNullOrWhiteSpace(option.DisplayName) ? option.OptionId : option.DisplayName;

            if (optionDescriptionText != null)
                optionDescriptionText.text = option.Description;
        }

        public void RefreshState(bool isSelected, string stateText, bool interactable)
        {
            if (selectedHighlight != null)
                selectedHighlight.SetActive(isSelected);

            if (optionStateText != null)
                optionStateText.text = stateText;

            if (selectButton != null)
            {
                selectButton.interactable = interactable;
                SetButtonVisible(interactable);
            }
        }

        private void EnsureButtonReference()
        {
            if (selectButton == null)
                selectButton = GetComponent<Button>();

            if (selectButton == null)
                selectButton = gameObject.AddComponent<Button>();

            Image image = GetComponent<Image>();
            if (image == null)
            {
                image = gameObject.AddComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0f);
            }

            if (selectButton.targetGraphic == null)
                selectButton.targetGraphic = image;
        }

        private void HandleClicked()
        {
            if (_clickOverride != null)
            {
                _clickOverride(this);
                return;
            }

            _selectionManager?.SubmitPlayerSelection(_definition.PropertyKey, _option.OptionId);
        }

        private void SetButtonVisible(bool visible)
        {
            if (selectButton == null)
                return;

            if (selectButton.gameObject != gameObject)
            {
                selectButton.gameObject.SetActive(visible);
                return;
            }

            selectButton.enabled = visible;
        }
    }
}
