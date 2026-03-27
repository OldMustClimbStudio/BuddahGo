using UnityEngine;
using UnityEngine.EventSystems;

namespace SteamMultiplayer.UI
{
    public class SkillLoadoutPoolDropZone : MonoBehaviour, IDropHandler
    {
        private SkillLoadoutSelectorUI _selector;

        public void Bind(SkillLoadoutSelectorUI selector)
        {
            _selector = selector;
        }

        public void OnDrop(PointerEventData eventData)
        {
            SkillCardItemView cardView = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<SkillCardItemView>()
                : null;

            if (cardView != null)
                _selector?.HandleCardDroppedToPool(cardView);
        }
    }
}
