using SteamMultiplayer.Network;
using UnityEngine;

namespace BuddahGo.Match
{
    /// <summary>
    /// Puts the owner's skin (picked on the menu's skin page, synced in the room roster) on the racer's body, on
    /// every machine. Runs once the racer's identity and the owner's roster entry have arrived, then switches off.
    /// AI racers and training dummies keep the prefab's material.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RacerSkinApplier : MonoBehaviour
    {
        [Tooltip("The Buddha's body mesh. Empty: the child renderer named \"model\".")]
        [SerializeField] private Renderer body;
        [Tooltip("Seconds to wait for the identity and the roster before keeping the prefab's material.")]
        [SerializeField, Min(1f)] private float waitSeconds = 10f;

        private RacerIdentity _identity;
        private float _giveUpAt;

        public string AppliedSkinId { get; private set; }

        private void Awake()
        {
            _identity = GetComponent<RacerIdentity>();
            if (body == null)
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                    if (r.name == "model") { body = r; break; }
        }

        private void OnEnable() => _giveUpAt = Time.unscaledTime + waitSeconds;

        private void Update()
        {
            if (_identity == null || body == null || _identity.IsTrainingDummy || _identity.IsAI || Time.unscaledTime > _giveUpAt)
            {
                enabled = false;
                return;
            }
            if (!_identity.IsAssigned) return;
            RoomStateManager room = RoomStateManager.Instance;
            if (room == null || !room.TryGetPlayer(_identity.ClientId, out RoomPlayerState owner)) return;
            Apply(owner.SkinId);
            enabled = false;
        }

        /// <summary>Puts the catalog material of <paramref name="skinId"/> on every slot of the body.</summary>
        public void Apply(string skinId)
        {
            SelectionCatalog catalog = SelectionCatalog.Current;
            if (catalog == null || body == null || !catalog.TryGetSkin(skinId, out SelectionCatalog.Skin skin) || skin.material == null)
                return;
            Material[] materials = body.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = skin.material;
            body.sharedMaterials = materials;
            AppliedSkinId = skinId;
        }
    }
}
