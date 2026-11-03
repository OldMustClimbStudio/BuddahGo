using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Skill loadout on the hokora: each skill ema hangs either on its own spot of the ema wall or in one
    /// of the hokora's slots, never both. Drag an ema (press and move) onto the hokora to slot it, drop it
    /// anywhere else and it returns to where it came from; right-click an ema in the hokora to send it back
    /// to the wall. A short click (no drag) is left to <see cref="MenuEmaInspector"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuSkillLoadout : MonoBehaviour
    {
        [Serializable]
        public class SkillEma
        {
            public string skillId;
            public Transform item;
            [Tooltip("Objects that move with the ema (its red cord).")]
            public Transform[] attachments;
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private MenuEmaInspector inspector;
        [SerializeField] private MenuSkinView skinView;
        [SerializeField] private MenuCameraDirector director;
        [SerializeField] private MenuCameraMouseLook mouseLook;
        [Tooltip("Ema can be dragged only on the skill-select shot.")]
        [SerializeField] private MenuCameraProtoSwitcher shotGate;
        [SerializeField] private List<SkillEma> emas = new List<SkillEma>();
        [Tooltip("Hanging poses inside the hokora; the number of slots is the loadout size.")]
        [SerializeField] private Transform[] slots = Array.Empty<Transform>();
        [Tooltip("Skill hanging in each slot when the menu opens (empty = free).")]
        [SerializeField] private string[] initialLoadout = Array.Empty<string>();
        [Tooltip("Renderers of the hokora; dropping over them (on screen) puts the ema into a slot.")]
        [SerializeField] private Renderer[] hokoraRenderers = Array.Empty<Renderer>();

        [Header("Feel")]
        [SerializeField, Min(0f)] private float dragSlopPixels = 6f;
        [Tooltip("Extra margin around the hokora's screen rectangle that still counts as a drop on it.")]
        [SerializeField, Range(0f, 1f)] private float dropMargin = 0.35f;
        [SerializeField, Min(0.1f)] private float followSmoothing = 18f;
        [Tooltip("Metres the dragged ema is pulled towards the camera so it floats in front of the scene.")]
        [SerializeField, Min(0f)] private float liftTowardsCamera = 0.15f;
        [SerializeField, Min(0.01f)] private float settleDuration = 0.28f;
        [SerializeField, Min(0f)] private float settleArc = 0.08f;
        [Tooltip("On-screen size of the dragged ema relative to its size where it hangs.")]
        [SerializeField, Range(1f, 2f)] private float pickedUpScale = 1.15f;

        /// <summary>Raised with the skill ids per slot (null = empty slot) whenever the loadout changes.</summary>
        public event Action<IReadOnlyList<string>> LoadoutChanged;

        private class State
        {
            public SkillEma ema;
            public Vector3 wallPosition;
            public Quaternion wallRotation;
            public readonly List<(Transform t, Vector3 localPos, Quaternion localRot)> attached = new List<(Transform, Vector3, Quaternion)>();
            public int slot = -1;
            public Coroutine move;
            public Vector3 baseScale;
            public readonly List<Vector3> attachedScale = new List<Vector3>();
            public float scale = 1f;   // drag-time shrink so the ema keeps its on-screen size near the camera
        }

        private readonly List<State> _states = new List<State>();
        private State[] _occupant;
        private State _pressed, _dragging;
        private Vector2 _pressPoint;
        private Plane _dragPlane;
        private Vector3 _grabOffset;
        private int _previewSlot = -1;
        private float _dragDepth;
        private float _dragScale = 1f;

        public bool IsDragging => _dragging != null;

        /// <summary>Skill id per slot (null = empty).</summary>
        public IReadOnlyList<string> Loadout
        {
            get
            {
                var ids = new string[_occupant?.Length ?? 0];
                for (int i = 0; i < ids.Length; i++) ids[i] = _occupant[i]?.ema.skillId;
                return ids;
            }
        }

        public void Configure(Camera cam, MenuEmaInspector insp, MenuSkinView view, MenuCameraDirector dir, MenuCameraMouseLook look,
                              List<SkillEma> list, Transform[] slotPoses, string[] initial, Renderer[] hokora)
        {
            targetCamera = cam; inspector = insp; skinView = view; director = dir; mouseLook = look;
            emas = list; slots = slotPoses; initialLoadout = initial; hokoraRenderers = hokora;
        }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            _occupant = new State[slots.Length];
            foreach (SkillEma e in emas)
            {
                if (e.item == null) continue;
                var s = new State { ema = e, wallPosition = e.item.position, wallRotation = e.item.rotation, baseScale = e.item.localScale };
                if (e.attachments != null)
                    foreach (Transform a in e.attachments)
                        if (a != null)
                        {
                            s.attached.Add((a, e.item.InverseTransformPoint(a.position), Quaternion.Inverse(e.item.rotation) * a.rotation));
                            s.attachedScale.Add(a.localScale);
                        }
                _states.Add(s);
            }
        }

        private void Start()
        {
            for (int i = 0; i < slots.Length && i < initialLoadout.Length; i++)
            {
                State s = _states.Find(x => x.ema.skillId == initialLoadout[i]);
                if (s == null || s.slot >= 0) continue;
                Assign(s, i);
                Place(s, slots[i].position, slots[i].rotation);
            }
            LoadoutChanged?.Invoke(Loadout);
        }

        public void SetShotGate(MenuCameraProtoSwitcher switcher) => shotGate = switcher;

        private bool Available =>
            Application.isFocused
            && (shotGate == null || shotGate.AtSkillSelect)
            && (skinView == null || !skinView.IsActive)
            && (director == null || !director.IsAway)
            && (inspector == null || !inspector.IsInspecting);

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pointer = mouse.position.ReadValue();

            if (_dragging != null) { UpdateDrag(mouse, pointer); return; }
            if (!Available) { _pressed = null; return; }
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (mouse.rightButton.wasPressedThisFrame && !overUi && Pick(pointer) is State remove && remove.slot >= 0)
                ReturnToWall(remove);

            if (mouse.leftButton.wasPressedThisFrame && !overUi)
            {
                _pressed = Pick(pointer);
                _pressPoint = pointer;
            }
            if (_pressed != null && mouse.leftButton.isPressed && (pointer - _pressPoint).magnitude > dragSlopPixels)
                BeginDrag(_pressed, pointer);
            if (mouse.leftButton.wasReleasedThisFrame) _pressed = null;
        }

        private void BeginDrag(State s, Vector2 pointer)
        {
            _pressed = null;
            _dragging = s;
            if (s.move != null) { StopCoroutine(s.move); s.move = null; }
            if (mouseLook != null) mouseLook.Frozen = true;
            Transform cam = targetCamera.transform;
            // Drag in front of both the ema's start and the hokora, so the ema never passes behind the
            // hokora's roof or posts; keep it under the same screen point while it comes forward.
            float itemDepth = Vector3.Dot(s.ema.item.position - cam.position, cam.forward);
            _dragDepth = Mathf.Max(Mathf.Min(itemDepth, HokoraNearDepth()) - liftTowardsCamera, targetCamera.nearClipPlane * 4f);
            _dragScale = Mathf.Clamp01(_dragDepth / Mathf.Max(itemDepth, 1e-3f)) * pickedUpScale;
            Vector3 anchor = AtDragDepth(targetCamera.WorldToScreenPoint(s.ema.item.position));
            _dragPlane = new Plane(-cam.forward, anchor);
            _grabOffset = anchor - PointOnPlane(_pressPoint);
            _previewSlot = -1;
        }

        /// <summary>Point under <paramref name="screenPoint"/> at the drag depth (distance along the camera's forward).</summary>
        private Vector3 AtDragDepth(Vector2 screenPoint)
        {
            Ray ray = targetCamera.ScreenPointToRay(screenPoint);
            float along = Vector3.Dot(ray.direction, targetCamera.transform.forward);
            return ray.origin + ray.direction * (_dragDepth / Mathf.Max(along, 1e-3f));
        }

        /// <summary>Distance along the camera's forward to the hokora part closest to the camera.</summary>
        private float HokoraNearDepth()
        {
            Transform cam = targetCamera.transform;
            float near = float.MaxValue;
            foreach (Renderer r in hokoraRenderers)
            {
                if (r == null) continue;
                Bounds b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    near = Mathf.Min(near, Vector3.Dot(corner - cam.position, cam.forward));
                }
            }
            return near;
        }

        private void UpdateDrag(Mouse mouse, Vector2 pointer)
        {
            State s = _dragging;
            Transform item = s.ema.item;
            bool overHokora = IsOverHokora(pointer);
            _previewSlot = overHokora ? ChooseSlot(s, pointer) : -1;

            Vector3 targetPos = PointOnPlane(pointer) + _grabOffset;
            Quaternion targetRot = s.slot >= 0 ? slots[s.slot].rotation : s.wallRotation;
            if (_previewSlot >= 0)
            {
                // Preview the snap: drift in front of the slot it would land in (on the same line of sight),
                // never into the hokora itself.
                targetPos = Vector3.Lerp(targetPos, AtDragDepth(targetCamera.WorldToScreenPoint(slots[_previewSlot].position)), 0.6f);
                targetRot = slots[_previewSlot].rotation;
            }
            float k = 1f - Mathf.Exp(-followSmoothing * Time.unscaledDeltaTime);
            s.scale = Mathf.Lerp(s.scale, _dragScale, k);
            Place(s, Vector3.Lerp(item.position, targetPos, k), Quaternion.Slerp(item.rotation, targetRot, k));

            if (mouse.leftButton.isPressed) return;
            _dragging = null;
            if (mouseLook != null) mouseLook.Frozen = false;
            if (_previewSlot >= 0 && _previewSlot != s.slot)
            {
                if (s.slot >= 0) _occupant[s.slot] = null;
                Assign(s, _previewSlot);
                LoadoutChanged?.Invoke(Loadout);
            }
            SettleHome(s);
            _previewSlot = -1;
        }

        /// <summary>Free slot (or the ema's own) nearest the pointer on screen; -1 if all are taken.</summary>
        private int ChooseSlot(State s, Vector2 pointer)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < slots.Length; i++)
            {
                if (_occupant[i] != null && _occupant[i] != s) continue;
                float d = ((Vector2)targetCamera.WorldToScreenPoint(slots[i].position) - pointer).sqrMagnitude;
                if (d < bestDistance) { best = i; bestDistance = d; }
            }
            return best;
        }

        private void ReturnToWall(State s)
        {
            if (s.slot < 0) return;
            _occupant[s.slot] = null;
            s.slot = -1;
            LoadoutChanged?.Invoke(Loadout);
            SettleHome(s);
        }

        private void Assign(State s, int slot)
        {
            s.slot = slot;
            _occupant[slot] = s;
        }

        /// <summary>Animates the ema to where it now belongs: its slot, or its spot on the wall.</summary>
        private void SettleHome(State s)
        {
            Vector3 pos = s.slot >= 0 ? slots[s.slot].position : s.wallPosition;
            Quaternion rot = s.slot >= 0 ? slots[s.slot].rotation : s.wallRotation;
            if (s.move != null) StopCoroutine(s.move);
            // Into a slot: straight along the line of sight (no arc that would brush the hokora roof).
            s.move = StartCoroutine(MoveTo(s, pos, rot, s.slot >= 0 ? 0f : settleArc));
        }

        private IEnumerator MoveTo(State s, Vector3 pos, Quaternion rot, float arcHeight)
        {
            Transform item = s.ema.item;
            Vector3 p0 = item.position; Quaternion r0 = item.rotation; float s0 = s.scale;
            float time = 0f;
            while (time < settleDuration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / settleDuration));
                Vector3 arc = Vector3.up * (Mathf.Sin(t * Mathf.PI) * arcHeight);
                s.scale = Mathf.Lerp(s0, 1f, t);
                Place(s, Vector3.Lerp(p0, pos, t) + arc, Quaternion.Slerp(r0, rot, t));
                yield return null;
            }
            s.scale = 1f;
            Place(s, pos, rot);
            s.move = null;
        }

        private void Place(State s, Vector3 pos, Quaternion rot)
        {
            Transform item = s.ema.item;
            item.SetPositionAndRotation(pos, rot);
            item.localScale = s.baseScale * s.scale;
            for (int i = 0; i < s.attached.Count; i++)
            {
                var (t, localPos, localRot) = s.attached[i];
                t.SetPositionAndRotation(item.TransformPoint(localPos), rot * localRot);
                t.localScale = s.attachedScale[i] * s.scale;
            }
        }

        /// <summary>The skill ema under the pointer (first scene hit), or null.</summary>
        private State Pick(Vector2 screenPoint)
        {
            // The project runs physics in Script mode without auto-sync, so moved ema colliders lag behind.
            Physics.SyncTransforms();
            Ray ray = targetCamera.ScreenPointToRay(screenPoint);
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Collide)) return null;
            foreach (State s in _states)
                if (hit.transform.IsChildOf(s.ema.item)) return s;
            return null;
        }

        private Vector3 PointOnPlane(Vector2 screenPoint)
        {
            Ray ray = targetCamera.ScreenPointToRay(screenPoint);
            return _dragPlane.Raycast(ray, out float d) ? ray.GetPoint(d) : _dragging != null ? _dragging.ema.item.position : Vector3.zero;
        }

        private bool IsOverHokora(Vector2 pointer)
        {
            bool any = false;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (Renderer r in hokoraRenderers)
            {
                if (r == null) continue;
                Bounds b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 sp = targetCamera.WorldToScreenPoint(corner);
                    if (sp.z <= 0f) continue;
                    any = true;
                    min = Vector2.Min(min, sp); max = Vector2.Max(max, sp);
                }
            }
            if (!any) return false;
            Vector2 margin = (max - min) * dropMargin * 0.5f;
            return pointer.x >= min.x - margin.x && pointer.x <= max.x + margin.x && pointer.y >= min.y - margin.y && pointer.y <= max.y + margin.y;
        }
    }
}
