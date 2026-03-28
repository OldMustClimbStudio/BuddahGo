using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SteamMultiplayer.UI
{
    public class SkillCardItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI stateText;
        [SerializeField] private GameObject selectedHighlight;
        [SerializeField] private CanvasGroup canvasGroup;

        private SkillLoadoutSelectorUI _selector;
        private SkillLoadoutDragController _dragController;
        private bool _dragEnabled;

        public string SkillId { get; private set; } = string.Empty;
        public string DisplayName { get; private set; } = string.Empty;
        public int SourceSlotIndex { get; private set; } = -1;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        public void Bind(
            SkillLoadoutSelectorUI selector,
            SkillLoadoutDragController dragController,
            string skillId,
            string displayName,
            int sourceSlotIndex)
        {
            _selector = selector;
            _dragController = dragController;
            SkillId = skillId ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? SkillId : displayName;
            SourceSlotIndex = sourceSlotIndex;

            if (titleText != null)
                titleText.text = DisplayName;
        }

        public void RefreshState(bool isSelected, string currentStateText, bool dragEnabled)
        {
            _dragEnabled = dragEnabled;
            if (selectedHighlight != null)
                selectedHighlight.SetActive(isSelected);
            if (stateText != null)
                stateText.text = currentStateText ?? string.Empty;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_dragEnabled)
                return;

            if (canvasGroup != null)
                canvasGroup.blocksRaycasts = false;

            _dragController?.BeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragEnabled)
                _dragController?.UpdateDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (canvasGroup != null)
                canvasGroup.blocksRaycasts = true;

            _dragController?.EndDrag();
        }
    }
}
