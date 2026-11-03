using SteamMultiplayer.Network;
using UnityEngine;

namespace SteamMultiplayer.UI
{
    public class SkillInspectableItem : MonoBehaviour
    {
        [Header("Inspect Visual")]
        [SerializeField] private bool allowInspect = true;
        [SerializeField] private Transform inspectSpawnSourceOverride;
        [SerializeField] private Vector3 inspectRotationEuler = new Vector3(0f, 180f, 0f);
        private SkillWallItemView _ownerView;
        private SelectablePropertyOption _optionData;

        public SelectablePropertyOption OptionData => _optionData;
        public SkillWallItemView OwnerView => _ownerView;
        public Quaternion InspectRotation => Quaternion.Euler(inspectRotationEuler);

        public void Initialize(SelectablePropertyOption optionData, SkillWallItemView ownerView)
        {
            _optionData = optionData;
            _ownerView = ownerView;
        }

        public bool CanInspect()
        {
            return allowInspect
                && _ownerView != null
                && !_ownerView.IsInFlight
                && !_ownerView.IsSelected
                && !_ownerView.IsInteractionLocked
                && gameObject.activeInHierarchy;
        }

        public Vector3 GetInspectStartPosition()
        {
            if (inspectSpawnSourceOverride != null)
                return inspectSpawnSourceOverride.position;

            if (_ownerView != null)
                return _ownerView.GetWallWorldPosition();

            return transform.position;
        }

        public Quaternion GetInspectStartRotation()
        {
            if (inspectSpawnSourceOverride != null)
                return inspectSpawnSourceOverride.rotation;

            if (_ownerView != null)
                return _ownerView.GetWallWorldRotation();

            return transform.rotation;
        }
    }
}
