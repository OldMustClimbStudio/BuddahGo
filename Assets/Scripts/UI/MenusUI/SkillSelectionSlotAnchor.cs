using UnityEngine;

namespace SteamMultiplayer.UI
{
    public class SkillSelectionSlotAnchor : MonoBehaviour
    {
        [SerializeField] private int slotIndex;
        [SerializeField] private Transform snapAnchor;

        private SkillWallSelectionController _controller;

        public int SlotIndex => slotIndex;
        public Transform SnapAnchor => snapAnchor != null ? snapAnchor : transform;
        public bool IsOccupied { get; private set; }
        public SkillWallItemView OccupyingItem { get; private set; }

        public void Initialize(SkillWallSelectionController controller)
        {
            _controller = controller;
        }

        public void SetOccupied(SkillWallItemView item)
        {
            OccupyingItem = item;
            IsOccupied = item != null;
        }

        public void ClearOccupancy()
        {
            OccupyingItem = null;
            IsOccupied = false;
        }

        private void OnMouseDown()
        {
            if (_controller == null || !IsOccupied)
                return;

            if (!_controller.WasMenuLeftClickThisFrame())
                return;

            _controller.RemoveSkillFromSlot(slotIndex);
        }
    }
}
