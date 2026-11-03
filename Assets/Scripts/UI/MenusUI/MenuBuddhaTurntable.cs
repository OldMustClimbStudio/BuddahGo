using UnityEngine;
using UnityEngine.InputSystem;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Pivot under the menu skin anchor. Plays the skin's idle slowly, turns the Buddha slightly
    /// towards the mouse pointer, and adds a drag rotation while the skin is being inspected.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuBuddhaTurntable : MonoBehaviour
    {
        [SerializeField] private Animator skinAnimator;
        [SerializeField, Range(0f, 2f)] private float idleSpeed = 0.3f;

        [Header("Follow the pointer")]
        [SerializeField] private Camera viewCamera;
        [Tooltip("Largest turn (degrees) towards the pointer.")]
        [SerializeField, Range(0f, 45f)] private float maxFollowYaw = 10f;
        [SerializeField, Min(0.1f)] private float followSmoothing = 2.5f;

        [Header("Drag (skin inspection)")]
        [Tooltip("Degrees of turn per screen-width of drag.")]
        [SerializeField] private float dragDegrees = 360f;
        [SerializeField, Min(0.1f)] private float dragSmoothing = 10f;
        [SerializeField, Min(0.1f)] private float returnSmoothing = 4f;

        private Quaternion _baseRotation;
        private float _followYaw;
        private float _dragTarget;
        private float _dragYaw;

        /// <summary>When false the Buddha stops looking towards the pointer (e.g. while it is dragged).</summary>
        public bool FollowPointer { get; set; } = true;

        public void Configure(Animator animator, Camera cam)
        {
            skinAnimator = animator;
            viewCamera = cam;
        }

        private void Awake()
        {
            _baseRotation = transform.localRotation;
        }

        private void OnEnable()
        {
            if (skinAnimator != null) skinAnimator.speed = idleSpeed;
        }

        private void OnValidate()
        {
            if (Application.isPlaying && skinAnimator != null) skinAnimator.speed = idleSpeed;
        }

        /// <summary>Turns the Buddha by a mouse drag of <paramref name="pixels"/> horizontally.</summary>
        public void Drag(float pixels) => _dragTarget -= pixels / Mathf.Max(1f, Screen.width) * dragDegrees;

        /// <summary>Eases the drag rotation back to the front-facing pose.</summary>
        public void ResetDrag() => _dragTarget = Mathf.Round(_dragYaw / 360f) * 360f;

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            float followTarget = FollowPointer && Application.isFocused ? PointerYaw() : 0f;
            _followYaw = Mathf.Lerp(_followYaw, followTarget, 1f - Mathf.Exp(-followSmoothing * dt));

            float k = Mathf.Abs(_dragTarget - _dragYaw) > 0.01f && FollowPointer ? returnSmoothing : dragSmoothing;
            _dragYaw = Mathf.Lerp(_dragYaw, _dragTarget, 1f - Mathf.Exp(-k * dt));
            if (FollowPointer && Mathf.Abs(_dragTarget - _dragYaw) < 0.05f)
            {
                _dragYaw = _dragTarget = 0f;   // settled back at the front: drop whole turns
            }

            transform.localRotation = _baseRotation * Quaternion.Euler(0f, _followYaw + _dragYaw, 0f);
        }

        /// <summary>Yaw that turns the Buddha's front towards the point under the mouse, clamped.</summary>
        private float PointerYaw()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || viewCamera == null || Screen.width <= 0) return 0f;
            float px = Mathf.Clamp(mouse.position.ReadValue().x / Screen.width * 2f - 1f, -1f, 1f);
            Transform cam = viewCamera.transform;
            Vector3 toCamera = cam.position - transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 1e-4f) return 0f;
            Vector3 right = cam.right; right.y = 0f;
            Vector3 lookAt = toCamera + right.normalized * (px * toCamera.magnitude * 0.6f);
            float yaw = Vector3.SignedAngle(toCamera, lookAt, Vector3.up);
            return Mathf.Clamp(yaw, -maxFollowYaw, maxFollowYaw);
        }
    }
}
