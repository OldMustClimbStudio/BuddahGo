using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Skin selection view of the menu environment: the camera glides to a close-up of the garden
    /// Buddha (the skin anchor); holding the model and dragging turns it. "Back" returns the camera to
    /// where it came from and hands control back to the mouse look.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuSkinView : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private MenuCameraMouseLook mouseLook;
        [SerializeField] private MenuEmaInspector inspector;
        [SerializeField] private MenuBuddhaTurntable turntable;
        [Tooltip("Camera pose of the skin view (in front of the garden Buddha).")]
        [SerializeField] private Transform skinPose;
        [SerializeField] private float skinFieldOfView = 40f;
        [SerializeField, Min(0.05f)] private float moveDuration = 0.9f;
        [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("UI")]
        [SerializeField] private Button enterButton;
        [SerializeField] private Button backButton;
        [Tooltip("Shown outside the skin view only (Start, the proto panel, ...).")]
        [SerializeField] private GameObject[] hideWhileActive;

        private enum State { Off, Entering, On, Leaving }
        private State _state;
        private Vector3 _returnPosition;
        private Quaternion _returnRotation;
        private float _returnFov;
        private bool _dragging;

        public bool IsActive => _state != State.Off;

        /// <summary>True once the camera has arrived and the skin model can be dragged.</summary>
        public bool IsInteractive => _state == State.On;

        public void Configure(Camera cam, MenuCameraMouseLook look, MenuEmaInspector insp, MenuBuddhaTurntable table,
                              Transform pose, float fov, Button enter, Button back, GameObject[] hide)
        {
            targetCamera = cam; mouseLook = look; inspector = insp; turntable = table;
            skinPose = pose; skinFieldOfView = fov; enterButton = enter; backButton = back; hideWhileActive = hide;
        }

        private void Start()
        {
            if (enterButton != null) enterButton.onClick.AddListener(Enter);
            if (backButton != null) { backButton.onClick.AddListener(Leave); backButton.gameObject.SetActive(false); }
        }

        public void Enter()
        {
            if (_state != State.Off || skinPose == null) return;
            if (inspector != null && inspector.IsInspecting) return;
            StartCoroutine(EnterRoutine());
        }

        public void Leave()
        {
            if (_state != State.On) return;
            StartCoroutine(LeaveRoutine());
        }

        private IEnumerator EnterRoutine()
        {
            _state = State.Entering;
            SetUi(true);
            // Disabling the mouse look puts the camera back on its own base pose first.
            if (mouseLook != null) mouseLook.enabled = false;
            if (inspector != null) inspector.enabled = false;
            Transform cam = targetCamera.transform;
            _returnPosition = cam.position; _returnRotation = cam.rotation; _returnFov = targetCamera.fieldOfView;
            yield return Move(_returnPosition, _returnRotation, _returnFov, skinPose.position, skinPose.rotation, skinFieldOfView);
            _state = State.On;
        }

        private IEnumerator LeaveRoutine()
        {
            _state = State.Leaving;
            _dragging = false;
            if (turntable != null) { turntable.ResetDrag(); turntable.FollowPointer = true; }
            Transform cam = targetCamera.transform;
            yield return Move(cam.position, cam.rotation, targetCamera.fieldOfView, _returnPosition, _returnRotation, _returnFov);
            if (mouseLook != null) mouseLook.enabled = true;
            if (inspector != null) inspector.enabled = true;
            SetUi(false);
            _state = State.Off;
        }

        private IEnumerator Move(Vector3 p0, Quaternion r0, float f0, Vector3 p1, Quaternion r1, float f1)
        {
            Transform cam = targetCamera.transform;
            float time = 0f;
            while (time < moveDuration)
            {
                time += Time.unscaledDeltaTime;
                float k = moveCurve.Evaluate(Mathf.Clamp01(time / moveDuration));
                cam.SetPositionAndRotation(Vector3.Lerp(p0, p1, k), Quaternion.Slerp(r0, r1, k));
                targetCamera.fieldOfView = Mathf.Lerp(f0, f1, k);
                yield return null;
            }
            cam.SetPositionAndRotation(p1, r1);
            targetCamera.fieldOfView = f1;
        }

        private void Update()
        {
            if (_state != State.On || turntable == null) return;
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                _dragging = !overUi && IsOverSkin(mouse.position.ReadValue());
                turntable.FollowPointer = !_dragging;
            }
            if (_dragging && mouse.leftButton.isPressed)
                turntable.Drag(mouse.delta.ReadValue().x);
            if (_dragging && mouse.leftButton.wasReleasedThisFrame)
                _dragging = false;   // keep the turned pose; following stays off until the view closes
        }

        private bool IsOverSkin(Vector2 screenPoint)
        {
            Ray ray = targetCamera.ScreenPointToRay(screenPoint);
            foreach (Renderer r in turntable.GetComponentsInChildren<Renderer>())
                if (r.bounds.IntersectRay(ray)) return true;
            return false;
        }

        private void SetUi(bool skinView)
        {
            if (enterButton != null) enterButton.gameObject.SetActive(!skinView);
            if (backButton != null) backButton.gameObject.SetActive(skinView);
            if (hideWhileActive != null)
                foreach (GameObject go in hideWhileActive)
                    if (go != null) go.SetActive(!skinView);
        }
    }
}
