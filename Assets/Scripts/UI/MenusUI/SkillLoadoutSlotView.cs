using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    public class SkillLoadoutSlotView : MonoBehaviour, IDropHandler
    {
        [SerializeField] private TextMeshProUGUI slotTitleText;
        [SerializeField] private TextMeshProUGUI emptyStateText;
        [SerializeField] private Transform cardRoot;
        [SerializeField] private Button clearButton;

        private SkillLoadoutSelectorUI _selector;
        private SkillCardItemView _cardView;

        public int SlotIndex { get; private set; }

        private void Awake()
        {
            if (clearButton != null)
                clearButton.onClick.AddListener(HandleClearClicked);
        }

        private void OnDestroy()
        {
            if (clearButton != null)
                clearButton.onClick.RemoveListener(HandleClearClicked);
        }

        public void Bind(int slotIndex, SkillLoadoutSelectorUI selector)
        {
            SlotIndex = slotIndex;
            _selector = selector;
            if (slotTitleText != null)
                slotTitleText.text = $"Slot {slotIndex + 1}";
        }

        public void SetCard(SkillCardItemView prefab, SkillLoadoutDragController dragController, string skillId, string displayName, bool dragEnabled)
        {
            if (prefab == null || cardRoot == null)
                return;

            if (_cardView == null)
                _cardView = Instantiate(prefab, cardRoot);

            _cardView.Bind(_selector, dragController, skillId, displayName, SlotIndex);
            _cardView.RefreshState(true, "Selected", dragEnabled);

            if (emptyStateText != null)
                emptyStateText.gameObject.SetActive(false);
        }

        public void ClearCard()
        {
            if (_cardView != null)
                Destroy(_cardView.gameObject);
            _cardView = null;

            if (emptyStateText != null)
                emptyStateText.gameObject.SetActive(true);
        }

        public void OnDrop(PointerEventData eventData)
        {
            SkillCardItemView cardView = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<SkillCardItemView>()
                : null;
            if (cardView != null)
                _selector?.HandleCardDroppedIntoSlot(cardView, SlotIndex);
        }

        private void HandleClearClicked()
        {
            _selector?.ClearSlot(SlotIndex);
        }
    }
}
