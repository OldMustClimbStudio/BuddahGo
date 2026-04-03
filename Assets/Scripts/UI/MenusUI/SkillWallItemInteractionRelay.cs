using UnityEngine;

namespace SteamMultiplayer.UI
{
    public class SkillWallItemInteractionRelay : MonoBehaviour
    {
        private SkillWallItemView _owner;

        public void Initialize(SkillWallItemView owner)
        {
            _owner = owner;
        }

        private void OnMouseEnter()
        {
            if (_owner != null)
                _owner.HandlePointerEnter();
        }

        private void OnMouseExit()
        {
            if (_owner != null)
                _owner.HandlePointerExit();
        }

        private void OnMouseDown()
        {
            if (_owner != null)
                _owner.HandlePrimaryPointerDown();
        }
    }
}
