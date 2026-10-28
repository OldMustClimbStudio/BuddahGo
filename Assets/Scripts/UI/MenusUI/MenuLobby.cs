using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Room lobby inside the main hall: six player spots around the sand table and on the balcony. A
    /// player who joins takes the first free spot (a model pops up there, posed as the spot); one who
    /// leaves sinks away. Creating a room seats the host (房主) on the first spot; the host stays until
    /// the room is left, which clears the lobby. Two temporary buttons simulate other players joining and
    /// leaving until the lobby is wired to the real room roster.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuLobby : MonoBehaviour
    {
        [Tooltip("Player spots in fill order; a model stands at the spot's position facing its forward.")]
        [SerializeField] private Transform[] slots = new Transform[0];
        [SerializeField] private GameObject playerPrefab;
        [Tooltip("Height of a player model standing on its spot (metres).")]
        [SerializeField, Min(0.1f)] private float playerHeight = 1.0f;
        [SerializeField, Min(0.01f)] private float popDuration = 0.45f;
        [SerializeField, Range(0.1f, 2f)] private float idleSpeed = 0.6f;
        [Tooltip("Opens the room (host takes the first spot) on entering the hall and clears it on leaving.")]
        [SerializeField] private MenuRoomView roomView;

        [Header("UI (temporary)")]
        [SerializeField] private Button joinButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Text countLabel;

        private GameObject[] _occupants;
        private readonly List<int> _joinOrder = new List<int>();
        private bool _hasHost;

        public int PlayerCount => _joinOrder.Count;
        /// <summary>Players that "模拟退出" may remove: everyone but the host.</summary>
        private int GuestCount => PlayerCount - (_hasHost ? 1 : 0);
        public int Capacity => slots.Length;

        public void Configure(Transform[] spots, GameObject prefab, Button join, Button leave, Text count, MenuRoomView room)
        {
            slots = spots; playerPrefab = prefab; joinButton = join; leaveButton = leave; countLabel = count; roomView = room;
        }

        /// <summary>Name prefix of the editor-only stand-ins placed on the spots for arranging them in the Scene view.</summary>
        public const string PreviewPrefix = "Preview_LobbyPlayer_";

        private void Awake()
        {
            _occupants = new GameObject[slots.Length];
            foreach (Transform slot in slots)
                if (slot != null)
                    for (int i = slot.childCount - 1; i >= 0; i--)
                        if (slot.GetChild(i).name.StartsWith(PreviewPrefix)) Destroy(slot.GetChild(i).gameObject);
        }

        private void Start()
        {
            if (joinButton != null) joinButton.onClick.AddListener(() => Join());
            if (leaveButton != null) leaveButton.onClick.AddListener(() => Leave());
            if (roomView != null) { roomView.Entered += OpenRoom; roomView.Left += CloseRoom; }
            Refresh();
        }

        private void OnDestroy()
        {
            if (roomView != null) { roomView.Entered -= OpenRoom; roomView.Left -= CloseRoom; }
        }

        /// <summary>A new room: the host pops up on the first spot.</summary>
        public void OpenRoom()
        {
            CloseRoom();
            _hasHost = Join() >= 0;
            Refresh();
        }

        /// <summary>The room is gone: everyone, host included, is removed at once (the screen is dark).</summary>
        public void CloseRoom()
        {
            StopAllCoroutines();
            for (int i = 0; i < _occupants.Length; i++)
            {
                if (_occupants[i] != null) Destroy(_occupants[i]);
                _occupants[i] = null;
            }
            _joinOrder.Clear();
            _hasHost = false;
            Refresh();
        }

        /// <summary>Puts a player on the first free spot; returns the spot index or -1 when full.</summary>
        public int Join()
        {
            int i = System.Array.IndexOf(_occupants, null);
            if (i < 0 || playerPrefab == null || slots[i] == null) return -1;
            Transform spot = slots[i];
            GameObject player = Instantiate(playerPrefab, spot.position, spot.rotation, spot);
            player.name = $"LobbyPlayer_{i + 1}";
            float scale = playerHeight / Mathf.Max(ModelHeight(player), 1e-3f);
            if (player.GetComponentInChildren<Animator>() is Animator animator)
            {
                animator.speed = idleSpeed * Random.Range(0.85f, 1.15f);
                animator.Update(Random.value * 3f);   // not everyone in step
            }
            _occupants[i] = player;
            _joinOrder.Add(i);
            StartCoroutine(Pop(player.transform, scale, true));
            Refresh();
            return i;
        }

        /// <summary>The most recent guest leaves (never the host); returns their spot index or -1 when none.</summary>
        public int Leave()
        {
            if (GuestCount <= 0) return -1;
            int i = _joinOrder[_joinOrder.Count - 1];
            _joinOrder.RemoveAt(_joinOrder.Count - 1);
            GameObject player = _occupants[i];
            _occupants[i] = null;
            if (player != null) StartCoroutine(Pop(player.transform, player.transform.localScale.x, false));
            Refresh();
            return i;
        }

        private static float ModelHeight(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 1f;
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b.size.y / Mathf.Max(model.transform.lossyScale.y, 1e-4f);
        }

        /// <summary>Joining: rises and grows in with a little overshoot. Leaving: shrinks into the floor, then gone.</summary>
        private IEnumerator Pop(Transform t, float scale, bool appear)
        {
            Vector3 home = t.localPosition;
            float time = 0f;
            while (time < popDuration)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / popDuration);
                float s = appear ? BackOut(k) : 1f - k * k;
                if (t == null) yield break;
                t.localScale = Vector3.one * (scale * Mathf.Max(s, 0.001f));
                t.localPosition = home + Vector3.down * ((appear ? 1f - k : k) * 0.15f);
                yield return null;
            }
            if (t == null) yield break;
            if (appear) { t.localScale = Vector3.one * scale; t.localPosition = home; }
            else Destroy(t.gameObject);
        }

        private static float BackOut(float k)
        {
            const float c = 1.6f;
            float x = k - 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }

        private void Refresh()
        {
            if (countLabel != null) countLabel.text = $"玩家 {PlayerCount}/{Capacity}";
            if (joinButton != null) joinButton.interactable = PlayerCount < Capacity;
            if (leaveButton != null) leaveButton.interactable = GuestCount > 0;
        }
    }
}
