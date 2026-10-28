using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Menu environment: click a skill ema to bring it in front of the camera, drag to turn it,
    /// click empty space / right-click / Esc to hang it back. While inspecting, a global depth of
    /// field volume focuses on the ema and blurs the shrine behind it.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuEmaInspector : MonoBehaviour
    {
        [System.Serializable]
        public class Inspectable
        {
            public Transform item;
            [Tooltip("Objects that move with the item (its red cord).")]
            public Transform[] attachments;
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private MenuCameraMouseLook mouseLook;
        [SerializeField] private List<Inspectable> inspectables = new List<Inspectable>();
        [Tooltip("Presses that turn into a loadout drag do not open the inspector.")]
        [SerializeField] private MenuSkillLoadout loadout;
        [Tooltip("Single ema can be picked only on the skill-select shot (on the main menu the wall is one button).")]
        [SerializeField] private MenuCameraProtoSwitcher shotGate;

        [Header("Presentation")]
        [Tooltip("Share of the screen height the ema fills while inspected.")]
        [SerializeField, Range(0.1f, 0.9f)] private float screenFill = 0.5f;
        [SerializeField, Min(0.01f)] private float moveDuration = 0.35f;
        [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Degrees of rotation per screen-width of mouse drag.")]
        [SerializeField] private float dragDegrees = 360f;
        [SerializeField, Range(0f, 89f)] private float maxTilt = 70f;
        [Tooltip("A press that moves less than this many pixels counts as a click, not a drag.")]
        [SerializeField, Min(0f)] private float clickSlopPixels = 6f;

        [Header("Depth of field")]
        [Tooltip("Metres behind the ema where the blur starts / reaches full strength.")]
        [SerializeField, Min(0f)] private float blurStartOffset = 0.25f;
        [SerializeField, Min(0.01f)] private float blurEndOffset = 1.2f;
        [SerializeField, Range(0.5f, 1.5f)] private float blurRadius = 1.5f;
        [SerializeField, Min(0.01f)] private float blurFadeDuration = 0.3f;

        private enum State { Idle, Entering, Viewing, Exiting }

        private State _state;
        private Inspectable _current;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private readonly List<(Transform t, Vector3 localPos, Quaternion localRot)> _attached = new List<(Transform, Vector3, Quaternion)>();
        private Vector2 _spin;          // x = yaw, y = pitch, degrees, applied on top of the facing rotation
        private float _distance;
        private Volume _dofVolume;
        private DepthOfField _dof;
        private Coroutine _blurFade;
        private bool _pressTracked;
        private bool _pressOnItem;
        private float _dragPixels;
        private Inspectable _clickCandidate;
        private Vector2 _clickStart;

        public bool IsInspecting => _state != State.Idle;

        /// <summary>The ema being inspected, or null.</summary>
        public Transform CurrentItem => _current != null ? _current.item : null;

        public void SetInspectables(List<Inspectable> items) => inspectables = items;

        public void SetLoadout(MenuSkillLoadout skillLoadout) => loadout = skillLoadout;

        public void SetShotGate(MenuCameraProtoSwitcher switcher) => shotGate = switcher;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            if (mouseLook == null) mouseLook = GetComponent<MenuCameraMouseLook>();
            CreateDepthOfFieldVolume();
        }

        private void OnDestroy()
        {
            if (_dofVolume != null)
            {
                Destroy(_dofVolume.sharedProfile);
                Destroy(_dofVolume.gameObject);
            }
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            switch (_state)
            {
                case State.Idle:
                    if (shotGate != null && !shotGate.AtSkillSelect) { _clickCandidate = null; break; }
                    // Inspect on a click (press and release without moving); a press that moves is a loadout drag.
                    Vector2 at = mouse.position.ReadValue();
                    if (mouse.leftButton.wasPressedThisFrame)
                    {
                        _clickCandidate = !overUi && TryPick(at, out Inspectable pressed) ? pressed : null;
                        _clickStart = at;
                    }
                    if (_clickCandidate != null && ((at - _clickStart).magnitude > clickSlopPixels || (loadout != null && loadout.IsDragging)))
                        _clickCandidate = null;
                    if (mouse.leftButton.wasReleasedThisFrame && _clickCandidate != null)
                    {
                        Inspectable hit = _clickCandidate;
                        _clickCandidate = null;
                        if (TryPick(at, out Inspectable released) && released == hit)
                            StartCoroutine(Enter(hit));
                    }
                    break;

                case State.Viewing:
                    // Pressing on the ema or dragging anywhere only turns it; a short click (press and
                    // release without dragging) outside the ema puts it back.
                    if (mouse.rightButton.wasPressedThisFrame
                        || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
                    {
                        StartCoroutine(Exit());
                        break;
                    }

                    Vector2 pointer = mouse.position.ReadValue();
                    if (mouse.leftButton.wasPressedThisFrame)
                    {
                        _pressTracked = !overUi;
                        _pressOnItem = IsOverCurrent(pointer);
                        _dragPixels = 0f;
                    }
                    if (mouse.leftButton.isPressed && _pressTracked)
                    {
                        Vector2 rawDelta = mouse.delta.ReadValue();
                        _dragPixels += rawDelta.magnitude;
                        Vector2 delta = rawDelta / Mathf.Max(1f, Screen.width) * dragDegrees;
                        _spin.x -= delta.x;
                        _spin.y = Mathf.Clamp(_spin.y + delta.y, -maxTilt, maxTilt);
                    }
                    if (mouse.leftButton.wasReleasedThisFrame && _pressTracked)
                    {
                        _pressTracked = false;
                        if (!_pressOnItem && _dragPixels <= clickSlopPixels && !IsOverCurrent(pointer))
                        {
                            StartCoroutine(Exit());
                            break;
                        }
                    }
                    PlaceInFrontOfCamera(1f);
                    break;
            }
        }

        /// <summary>True when the pointer is over the ema being inspected (its colliders or visible bounds).</summary>
        private bool IsOverCurrent(Vector2 screenPoint)
        {
            if (_current == null || _current.item == null) return false;
            Physics.SyncTransforms();
            Ray ray = targetCamera.ScreenPointToRay(screenPoint);
            foreach (Collider c in _current.item.GetComponentsInChildren<Collider>())
                if (c.Raycast(ray, out _, 100f)) return true;
            foreach (Renderer r in _current.item.GetComponentsInChildren<Renderer>())
                if (r.bounds.IntersectRay(ray)) return true;
            return false;
        }

        private bool TryPick(Vector2 screenPoint, out Inspectable result)
        {
            result = null;
            // Physics runs in Script mode without auto-sync: bring colliders of moved ema up to date.
            Physics.SyncTransforms();
            Ray ray = targetCamera.ScreenPointToRay(screenPoint);
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Collide)) return false;
            foreach (Inspectable i in inspectables)
                if (i.item != null && hit.transform.IsChildOf(i.item)) { result = i; return true; }
            return false;
        }

        private IEnumerator Enter(Inspectable target)
        {
            _state = State.Entering;
            _current = target;
            if (mouseLook != null) mouseLook.Frozen = true;

            Transform item = target.item;
            _homePosition = item.position;
            _homeRotation = item.rotation;
            _attached.Clear();
            if (target.attachments != null)
                foreach (Transform a in target.attachments)
                    if (a != null) _attached.Add((a, item.InverseTransformPoint(a.position), Quaternion.Inverse(item.rotation) * a.rotation));

            _distance = FitDistance(item);
            _spin = Vector2.zero;
            _pressTracked = false;
            FadeBlur(1f);

            float time = 0f;
            while (time < moveDuration)
            {
                time += Time.unscaledDeltaTime;
                PlaceInFrontOfCamera(moveCurve.Evaluate(Mathf.Clamp01(time / moveDuration)));
                yield return null;
            }
            PlaceInFrontOfCamera(1f);
            _state = State.Viewing;
        }

        private IEnumerator Exit()
        {
            _state = State.Exiting;
            Transform item = _current.item;
            Vector3 fromPos = item.position;
            Quaternion fromRot = item.rotation;
            FadeBlur(0f);

            float time = 0f;
            while (time < moveDuration)
            {
                time += Time.unscaledDeltaTime;
                float k = moveCurve.Evaluate(Mathf.Clamp01(time / moveDuration));
                item.SetPositionAndRotation(Vector3.Lerp(fromPos, _homePosition, k), Quaternion.Slerp(fromRot, _homeRotation, k));
                SyncAttachments();
                yield return null;
            }
            item.SetPositionAndRotation(_homePosition, _homeRotation);
            SyncAttachments();
            _current = null;
            _state = State.Idle;
            if (mouseLook != null) mouseLook.Frozen = false;
        }

        /// <summary>Blends the item from its home pose (k = 0) to the inspect pose in front of the camera (k = 1).</summary>
        private void PlaceInFrontOfCamera(float k)
        {
            Transform cam = targetCamera.transform;
            Transform item = _current.item;
            Vector3 viewPos = cam.position + cam.forward * _distance;
            // The ema's painted face looks along its forward axis: turn it to face the camera, then apply the drag spin.
            Quaternion facing = Quaternion.LookRotation(-cam.forward, cam.up);
            Quaternion viewRot = Quaternion.AngleAxis(_spin.x, cam.up) * Quaternion.AngleAxis(_spin.y, cam.right) * facing;

            // Rotate around the visual centre rather than the pivot (the pivot sits at the hanging hole).
            Vector3 centreOffset = Vector3.zero;
            if (item.GetComponentInChildren<Renderer>() is Renderer r)
                centreOffset = r.bounds.center - item.position;
            Vector3 localCentre = Quaternion.Inverse(item.rotation) * centreOffset;
            Vector3 targetPos = viewPos - viewRot * localCentre;

            item.SetPositionAndRotation(Vector3.Lerp(_homePosition, targetPos, k), Quaternion.Slerp(_homeRotation, viewRot, k));
            SyncAttachments();
            if (_dof != null)
            {
                // Only what lies behind the ema blurs, so the ema stays sharp however it is turned.
                _dof.gaussianStart.value = _distance + blurStartOffset;
                _dof.gaussianEnd.value = _distance + blurStartOffset + blurEndOffset;
            }
        }

        private void SyncAttachments()
        {
            Transform item = _current.item;
            foreach (var (t, localPos, localRot) in _attached)
                t.SetPositionAndRotation(item.TransformPoint(localPos), item.rotation * localRot);
        }

        private float FitDistance(Transform item)
        {
            float size = 0.2f;
            if (item.GetComponentInChildren<Renderer>() is Renderer r)
                size = Mathf.Max(r.bounds.size.x, r.bounds.size.y, r.bounds.size.z);
            float halfFov = targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            return Mathf.Max(targetCamera.nearClipPlane * 3f, size / (2f * Mathf.Tan(halfFov) * screenFill));
        }

        private void CreateDepthOfFieldVolume()
        {
            var go = new GameObject("Inspect Depth Of Field");
            go.transform.SetParent(transform, false);
            _dofVolume = go.AddComponent<Volume>();
            _dofVolume.isGlobal = true;
            _dofVolume.priority = 50f;
            _dofVolume.weight = 0f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _dof = profile.Add<DepthOfField>(true);
            _dof.mode.Override(DepthOfFieldMode.Gaussian);
            _dof.gaussianStart.Override(1f);
            _dof.gaussianEnd.Override(3f);
            _dof.gaussianMaxRadius.Override(blurRadius);
            _dof.highQualitySampling.Override(true);
            _dofVolume.sharedProfile = profile;
        }

        private void FadeBlur(float target)
        {
            if (_dofVolume == null) return;
            if (_blurFade != null) StopCoroutine(_blurFade);
            _blurFade = StartCoroutine(FadeBlurRoutine(target));
        }

        private IEnumerator FadeBlurRoutine(float target)
        {
            float start = _dofVolume.weight, time = 0f;
            while (time < blurFadeDuration)
            {
                time += Time.unscaledDeltaTime;
                _dofVolume.weight = Mathf.Lerp(start, target, time / blurFadeDuration);
                yield return null;
            }
            _dofVolume.weight = target;
            _blurFade = null;
        }
    }
}
