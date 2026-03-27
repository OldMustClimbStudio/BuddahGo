using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SteamMultiplayer.UI
{
    public class SkillLoadoutDragController : MonoBehaviour
    {
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private RectTransform dragGhostRoot;
        [SerializeField] private TextMeshProUGUI dragGhostText;

        public void BeginDrag(SkillCardItemView cardView, PointerEventData eventData)
        {
            if (dragGhostRoot == null || cardView == null)
                return;

            if (dragGhostText != null)
                dragGhostText.text = cardView.DisplayName;

            dragGhostRoot.gameObject.SetActive(true);
            UpdateDrag(eventData);
        }

        public void UpdateDrag(PointerEventData eventData)
        {
            if (dragGhostRoot == null || eventData == null)
                return;

            RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
            if (canvasRect == null)
            {
                dragGhostRoot.position = eventData.position;
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint);
            dragGhostRoot.localPosition = localPoint;
        }

        public void EndDrag()
        {
            if (dragGhostRoot != null)
                dragGhostRoot.gameObject.SetActive(false);
        }
    }
}
