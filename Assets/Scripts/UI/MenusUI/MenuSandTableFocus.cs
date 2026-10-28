using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Sand table in the hall lobby (房间内): clicking it glides the camera to a view over the table and
    /// softens everything else with a gentle depth of field. "返回", Esc or right-click glide back to the
    /// lobby view while the blur fades out. Only clickable while the lobby view is up.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuSandTableFocus : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private MenuRoomView roomView;
        [Tooltip("The table; its own renderer is what gets clicked.")]
        [SerializeField] private Renderer table;

        [Header("Camera")]
        [Tooltip("沙盘: the angle of the view over the table. The distance is fitted to the table and screen shape on each click.")]
        [SerializeField] private Transform focusPose;
        [SerializeField] private float focusFieldOfView = 45f;
        [Tooltip("Share of the screen left free around the table top.")]
        [SerializeField, Range(0f, 0.3f)] private float framePadding = 0.05f;
        [Tooltip("Seconds for the glide (each way).")]
        [SerializeField, Min(0.1f)] private float moveDuration = 1.4f;

        [Header("Depth of field (focused view only)")]
        [Tooltip("Lens focal length at full focus (mm). Blur grows with its square; lower is softer.")]
        [SerializeField, Range(10f, 120f)] private float focalLength = 70f;
        [Tooltip("Lens f-number at full focus. Higher keeps more in focus.")]
        [SerializeField, Range(1f, 16f)] private float aperture = 2.8f;

        [Header("UI")]
        [Tooltip("Shown only while focused.")]
        [SerializeField] private Button backButton;
        [Tooltip("Hidden while focused (the lobby buttons).")]
        [SerializeField] private GameObject[] hideWhileFocused;

        // Longest frame step the glide takes, so a long hitch does not jump the camera.
        private const float MaxStep = 0.1f;
        private enum State { Idle, Entering, Focused, Leaving }
        private State _state;
        private bool _inLobby;
        private Coroutine _move;
        private Vector3 _lobbyPosition;
        private Quaternion _lobbyRotation;
        private float _lobbyFov;
        private Volume _dofVolume;
        private DepthOfField _dof;

        /// <summary>The table can be clicked now (lobby view up, not focused or moving).</summary>
        public bool CanFocus => _inLobby && _state == State.Idle;

        /// <summary>Focused on the table or moving to or from it.</summary>
        public bool IsActive => _state != State.Idle;

        public void Configure(Camera cam, MenuRoomView room, Renderer tableRenderer, Transform pose, float fov,
                              Button back, GameObject[] hide)
        {
            targetCamera = cam; roomView = room; table = tableRenderer; focusPose = pose; focusFieldOfView = fov;
            backButton = back; hideWhileFocused = hide;
        }

        private void Reset() => targetCamera = GetComponent<Camera>();

        private void OnEnable()
        {
            if (roomView == null) return;
            roomView.Entered += OnEntered;
            roomView.Left += OnLeft;
        }

        private void OnDisable()
        {
            if (roomView != null)
            {
                roomView.Entered -= OnEntered;
                roomView.Left -= OnLeft;
            }
            _inLobby = false;
        }

        private void Start()
        {
            if (backButton != null)
            {
                backButton.onClick.AddListener(Unfocus);
                backButton.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_dofVolume == null) return;
            Destroy(_dofVolume.sharedProfile);
            Destroy(_dofVolume.gameObject);
        }

        private void OnEntered() => _inLobby = true;

        /// <summary>The room is closing (screen already black): drop the focus at once.</summary>
        private void OnLeft()
        {
            _inLobby = false;
            if (_state == State.Idle) return;
            if (_move != null) StopCoroutine(_move);
            _move = null;
            SetDof(0f);
            SetFocusedUi(false);
            _state = State.Idle;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (CanFocus)
            {
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && !OverUi() && IsUnderPointer(mouse.position.ReadValue()))
                    Focus();
            }
            else if (_state == State.Focused)
            {
                bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
                bool right = mouse != null && mouse.rightButton.wasPressedThisFrame;
                if (esc || right) Unfocus();
            }
        }

        public void Focus()
        {
            if (!CanFocus || focusPose == null) return;
            Transform cam = targetCamera.transform;
            _lobbyPosition = cam.position; _lobbyRotation = cam.rotation; _lobbyFov = targetCamera.fieldOfView;
            _move = StartCoroutine(Glide(State.Entering, FitPosition(focusPose.rotation), focusPose.rotation, focusFieldOfView, 1f));
        }

        public void Unfocus()
        {
            if (_state != State.Focused) return;
            _move = StartCoroutine(Glide(State.Leaving, _lobbyPosition, _lobbyRotation, _lobbyFov, 0f));
        }

        private IEnumerator Glide(State moving, Vector3 toPosition, Quaternion toRotation, float toFov, float toBlur)
        {
            _state = moving;
            if (moving == State.Leaving) SetFocusedUi(false);
            else SetHidden(true);

            Transform cam = targetCamera.transform;
            Vector3 fromPosition = cam.position;
            Quaternion fromRotation = cam.rotation;
            float fromFov = targetCamera.fieldOfView;
            float fromBlur = 1f - toBlur;
            float time = 0f;
            while (time < moveDuration)
            {
                time += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
                float t = Mathf.Clamp01(time / moveDuration);
                float e = t * t * (3f - 2f * t);
                cam.SetPositionAndRotation(Vector3.Lerp(fromPosition, toPosition, e), Quaternion.Slerp(fromRotation, toRotation, e));
                targetCamera.fieldOfView = Mathf.Lerp(fromFov, toFov, e);
                SetDof(Mathf.Lerp(fromBlur, toBlur, e));
                yield return null;
            }
            cam.SetPositionAndRotation(toPosition, toRotation);
            targetCamera.fieldOfView = toFov;
            SetDof(toBlur);

            _move = null;
            if (moving == State.Entering)
            {
                _state = State.Focused;
                SetFocusedUi(true);
            }
            else
            {
                _state = State.Idle;
                SetHidden(false);
            }
        }

        private void SetFocusedUi(bool focused)
        {
            if (backButton != null) backButton.gameObject.SetActive(focused);
            if (!focused && _state == State.Idle) SetHidden(false);
        }

        private void SetHidden(bool hidden)
        {
            if (hideWhileFocused == null) return;
            foreach (GameObject go in hideWhileFocused)
                if (go != null) go.SetActive(!hidden);
        }

        /// <summary>
        /// Blur amount 0..1. The focus plane follows the table as the camera moves; the blur grows with the
        /// square of the focal length, so the focal length follows the square root to fade in evenly.
        /// </summary>
        private void SetDof(float amount)
        {
            if (amount <= 0.001f)
            {
                if (_dofVolume != null) _dofVolume.enabled = false;
                return;
            }
            EnsureDofVolume();
            _dofVolume.enabled = true;
            Transform cam = targetCamera.transform;
            float distance = Vector3.Dot(table.bounds.center - cam.position, cam.forward);
            _dof.focusDistance.Override(Mathf.Max(0.1f, distance));
            _dof.focalLength.Override(Mathf.Max(1f, focalLength * Mathf.Sqrt(amount)));
        }

        private void EnsureDofVolume()
        {
            if (_dofVolume != null) return;
            var go = new GameObject("Sand Table DOF");
            go.transform.SetParent(transform, false);
            _dofVolume = go.AddComponent<Volume>();
            _dofVolume.isGlobal = true;
            _dofVolume.priority = 55f;   // above the ema inspection blur (50)
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _dof = profile.Add<DepthOfField>(true);
            _dof.mode.Override(DepthOfFieldMode.Bokeh);
            _dof.aperture.Override(aperture);
            _dof.focalLength.Override(1f);
            _dofVolume.sharedProfile = profile;
        }

        /// <summary>
        /// Closest camera position, looking along the given rotation at the table, that keeps the whole
        /// table top (rim and corner caps) on screen at the focus field of view and the current aspect.
        /// </summary>
        public Vector3 FitPosition(Quaternion rotation)
        {
            Bounds b = table.bounds;
            Vector3 centre = new Vector3(b.center.x, b.max.y, b.center.z);
            Vector3 forward = rotation * Vector3.forward, right = rotation * Vector3.right, up = rotation * Vector3.up;
            float tanV = Mathf.Tan(focusFieldOfView * 0.5f * Mathf.Deg2Rad) * (1f - framePadding);
            float tanH = tanV * targetCamera.aspect;
            float distance = 0.5f;
            for (int i = 0; i < 8; i++)
            {
                // Corners of the top slab (the legs below hide under it from this angle).
                Vector3 corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, (i / 2) % 2 == 0 ? b.max.y : b.max.y - 0.15f,
                                             i / 4 == 0 ? b.min.z : b.max.z) - centre;
                float depth = Vector3.Dot(corner, forward);
                distance = Mathf.Max(distance,
                    Mathf.Abs(Vector3.Dot(corner, right)) / tanH - depth,
                    Mathf.Abs(Vector3.Dot(corner, up)) / tanV - depth);
            }
            return centre - forward * distance;
        }

        private bool IsUnderPointer(Vector2 pointer)
        {
            if (table == null || targetCamera == null) return false;
            Ray ray = targetCamera.ScreenPointToRay(pointer);
            return table.bounds.IntersectRay(ray);
        }

        private static bool OverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
