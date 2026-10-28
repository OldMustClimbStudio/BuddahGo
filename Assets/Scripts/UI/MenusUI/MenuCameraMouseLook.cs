using UnityEngine;
using UnityEngine.InputSystem;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Menu background camera that drifts with the mouse pointer around the pose it starts with.
    /// Pointer left/right leans the camera towards <see cref="leftFocus"/> / <see cref="rightFocus"/>
    /// (turn, a short dolly and a slight zoom); pointer up/down adds a small pitch. The total turn
    /// never exceeds <see cref="maxAngle"/> degrees from the starting direction.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuCameraMouseLook : MonoBehaviour
    {
        [Header("Focus targets")]
        [Tooltip("What the camera leans towards when the pointer is on the left side of the screen.")]
        [SerializeField] private Transform leftFocus;
        [Tooltip("What the camera leans towards when the pointer is on the right side of the screen.")]
        [SerializeField] private Transform rightFocus;

        [Header("Amounts at the screen edge")]
        [Tooltip("Share of the way the camera turns to face the focus target (0 = no turn, 1 = faces it).")]
        [SerializeField, Range(0f, 1f)] private float focusTurn = 0.35f;
        [Tooltip("Metres the camera moves towards the focus target.")]
        [SerializeField, Min(0f)] private float focusMove = 0.35f;
        [Tooltip("Degrees the field of view narrows while focusing.")]
        [SerializeField, Range(0f, 20f)] private float focusZoom = 4f;
        [Tooltip("Vertical look in degrees when the pointer is at the top/bottom edge.")]
        [SerializeField, Range(0f, 30f)] private float maxPitch = 6f;
        [Tooltip("Largest angle (degrees) the camera may turn away from its starting direction.")]
        [SerializeField, Range(0f, 90f)] private float maxAngle = 30f;

        [Tooltip("How quickly the camera catches up with the pointer (higher is snappier).")]
        [SerializeField, Min(0.1f)] private float smoothing = 3f;
        [SerializeField] private bool invertVertical;

        private Camera _camera;
        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private float _baseFov;
        private Vector2 _pointer;   // smoothed pointer offset, each axis in [-1, 1]

        /// <summary>Holds the current pose (e.g. while an item is being inspected).</summary>
        public bool Frozen { get; set; }

        public void SetFocusTargets(Transform left, Transform right)
        {
            leftFocus = left;
            rightFocus = right;
        }

        private void OnEnable()
        {
            _camera = GetComponent<Camera>();
            _basePosition = transform.position;
            _baseRotation = transform.rotation;
            _baseFov = _camera != null ? _camera.fieldOfView : 60f;
            _pointer = Vector2.zero;
        }

        private void OnDisable()
        {
            transform.SetPositionAndRotation(_basePosition, _baseRotation);
            if (_camera != null) _camera.fieldOfView = _baseFov;
        }

        private void LateUpdate()
        {
            if (Frozen) return;
            Vector2 target = Application.isFocused ? PointerOffset() : _pointer;
            float t = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            _pointer = Vector2.Lerp(_pointer, target, t);
            Apply(_pointer);
        }

        private void Apply(Vector2 p)
        {
            Transform focus = p.x < 0f ? leftFocus : rightFocus;
            float w = Mathf.SmoothStep(0f, 1f, Mathf.Abs(p.x));

            Quaternion rotation = _baseRotation;
            Vector3 position = _basePosition;
            if (focus != null)
            {
                Vector3 toFocus = focus.position - _basePosition;
                Quaternion facing = Quaternion.LookRotation(toFocus, Vector3.up);
                rotation = Quaternion.Slerp(_baseRotation, facing, w * focusTurn);
                position = _basePosition + toFocus.normalized * (focusMove * w);
            }
            else
            {
                rotation = _baseRotation * Quaternion.Euler(0f, p.x * maxAngle * focusTurn, 0f);
            }

            float pitch = p.y * maxPitch * (invertVertical ? -1f : 1f);
            rotation *= Quaternion.Euler(-pitch, 0f, 0f);

            float angle = Quaternion.Angle(_baseRotation, rotation);
            if (angle > maxAngle)
                rotation = Quaternion.Slerp(_baseRotation, rotation, maxAngle / angle);

            transform.SetPositionAndRotation(position, rotation);
            if (_camera != null) _camera.fieldOfView = _baseFov - focusZoom * w;
        }

        private Vector2 PointerOffset()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || Screen.width <= 0 || Screen.height <= 0)
                return _pointer;

            Vector2 pointer = mouse.position.ReadValue();
            return new Vector2(
                Mathf.Clamp(pointer.x / Screen.width * 2f - 1f, -1f, 1f),
                Mathf.Clamp(pointer.y / Screen.height * 2f - 1f, -1f, 1f));
        }
    }
}
