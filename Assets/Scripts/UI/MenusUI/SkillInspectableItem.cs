using SteamMultiplayer.Network;
using UnityEngine;

namespace SteamMultiplayer.UI
{
    public class SkillInspectableItem : MonoBehaviour
    {
        [Header("Inspect Visual")]
        [SerializeField] private bool allowInspect = true;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private GameObject inspectVisualPrefab;
        [SerializeField] private Transform inspectSpawnSourceOverride;
        [SerializeField] private Vector3 inspectRotationEuler = new Vector3(0f, 180f, 0f);
        private SkillWallItemView _ownerView;
        private SelectablePropertyOption _optionData;

        public SelectablePropertyOption OptionData => _optionData;
        public string SkillId => (_optionData.OptionId ?? string.Empty).Trim();
        public bool AllowInspect => allowInspect;
        public SkillWallItemView OwnerView => _ownerView;
        public Quaternion InspectRotation => Quaternion.Euler(inspectRotationEuler);

        private void Awake()
        {
            if (visualRoot == null)
                visualRoot = transform;
        }

        public void Initialize(SelectablePropertyOption optionData, SkillWallItemView ownerView)
        {
            _optionData = optionData;
            _ownerView = ownerView;

            if (visualRoot == null)
                visualRoot = transform;
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

        public GameObject CreateInspectVisualInstance(Transform parent)
        {
            GameObject source = inspectVisualPrefab != null
                ? inspectVisualPrefab
                : (visualRoot != null ? visualRoot.gameObject : gameObject);

            if (source == null)
                return null;

            GameObject instance = Instantiate(source, parent);
            instance.name = $"Inspect_{SkillId}";
            instance.transform.localScale = source.transform.localScale;
            SanitizeInstance(instance);
            return instance;
        }

        private static void SanitizeInstance(GameObject instance)
        {
            if (instance == null)
                return;

            SkillWallItemView wallView = instance.GetComponent<SkillWallItemView>();
            if (wallView != null)
                Destroy(wallView);

            SkillInspectableItem inspectable = instance.GetComponent<SkillInspectableItem>();
            if (inspectable != null)
                Destroy(inspectable);

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                Destroy(colliders[i]);

            Collider2D[] colliders2D = instance.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders2D.Length; i++)
                Destroy(colliders2D[i]);

            Rigidbody[] rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
                Destroy(rigidbodies[i]);

            Rigidbody2D[] rigidbodies2D = instance.GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < rigidbodies2D.Length; i++)
                Destroy(rigidbodies2D[i]);
        }
    }
}
