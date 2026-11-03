using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Camera move from the current menu view up the terrace to the closed main-hall door, where
    /// rooms are created or joined (房间). One continuous glide: position along a gentle arc (so it
    /// clears the small shrine on the way), rotation and field of view blended on the same eased time,
    /// no detours or speed effects. "Back" glides to the menu view and hands control back to the
    /// mouse look.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuCameraDirector : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private MenuCameraMouseLook mouseLook;

        [Header("Poses")]
        [Tooltip("房间: the view in front of the closed hall door where the move ends.")]
        [SerializeField] private Transform roomPose;
        [SerializeField] private float roomFieldOfView = 50f;

        [Header("Move")]
        [Tooltip("Seconds for the glide (each way).")]
        [SerializeField, Min(0.1f)] private float moveDuration = 2.6f;
        [Tooltip("Metres the middle of the path rises above the straight line, to clear the shrine roof.")]
        [SerializeField, Min(0f)] private float arcHeight = 0.8f;

        [Header("UI")]
        [Tooltip("Shown at the door only (create / join room, back).")]
        [SerializeField] private GameObject roomUi;
        [Tooltip("Hidden from the moment the move starts until the camera is back (Start, skin, proto panel, ...).")]
        [SerializeField] private GameObject[] hideWhileAway;
        [Tooltip("Turned off while away from the menu views (ema inspection, ...).")]
        [SerializeField] private Behaviour[] disableWhileAway;

        [SerializeField] private UnityEvent onArrivedAtRoom;

        // Longest frame step the glide takes, so a long hitch (e.g. on the click frame) does not jump the camera.
        private const float MaxStep = 0.1f;
        private Coroutine _move;
        private Vector3 _fromPosition, _toPosition;
        private Quaternion _fromRotation, _toRotation;
        private float _fromFov, _toFov;
        private Vector3 _menuPosition;
        private Quaternion _menuRotation;
        private float _menuFov;

        public bool IsMoving => _move != null;

        /// <summary>True while at the door view.</summary>
        public bool InRoomView { get; private set; }

        /// <summary>Moving or at the door: the menu views' own interactions stay off.</summary>
        public bool IsAway => IsMoving || InRoomView;

        public void Configure(Camera cam, MenuCameraMouseLook look, Transform pose, float fov,
                              GameObject ui, GameObject[] hide, Behaviour[] disable)
        {
            targetCamera = cam; mouseLook = look; roomPose = pose; roomFieldOfView = fov;
            roomUi = ui; hideWhileAway = hide; disableWhileAway = disable;
        }

        private void Reset()
        {
            targetCamera = GetComponent<Camera>();
            mouseLook = GetComponent<MenuCameraMouseLook>();
        }

        private void Start()
        {
            if (roomUi != null) roomUi.SetActive(false);
        }

        /// <summary>Called by the Start button.</summary>
        public void PlayToRoom()
        {
            if (_move != null || InRoomView || roomPose == null) return;
            _move = StartCoroutine(MoveToRoom());
        }

        /// <summary>Called by the door view's back button: the same move in reverse.</summary>
        public void ReturnFromRoom()
        {
            if (_move != null || !InRoomView) return;
            _move = StartCoroutine(MoveBack());
        }

        private IEnumerator MoveToRoom()
        {
            Transform cam = targetCamera.transform;
            // Start from what is on screen now (the mouse look may be leaning). Disabling the mouse
            // look snaps the camera to its base pose (the menu view); that is where "back" returns to.
            Vector3 position = cam.position;
            Quaternion rotation = cam.rotation;
            float fov = targetCamera.fieldOfView;
            if (mouseLook != null) mouseLook.enabled = false;
            _menuPosition = cam.position;
            _menuRotation = cam.rotation;
            _menuFov = targetCamera.fieldOfView;
            cam.SetPositionAndRotation(position, rotation);
            targetCamera.fieldOfView = fov;

            SetAway(true);
            Prepare(position, rotation, fov, roomPose.position, roomPose.rotation, roomFieldOfView);
            yield return Glide();
            InRoomView = true;
            _move = null;
            if (roomUi != null) roomUi.SetActive(true);
            onArrivedAtRoom?.Invoke();
        }

        private IEnumerator MoveBack()
        {
            if (roomUi != null) roomUi.SetActive(false);
            InRoomView = false;
            Transform cam = targetCamera.transform;
            Prepare(cam.position, cam.rotation, targetCamera.fieldOfView, _menuPosition, _menuRotation, _menuFov);
            yield return Glide();
            _move = null;
            SetAway(false);
            // Re-enabling takes the current pose (the menu view) as its base and eases in from there.
            if (mouseLook != null) mouseLook.enabled = true;
        }

        private IEnumerator Glide()
        {
            Transform cam = targetCamera.transform;
            float time = 0f;
            while (time < moveDuration)
            {
                time += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
                Evaluate(Mathf.Clamp01(time / moveDuration), out Vector3 position, out Quaternion rotation, out float fov);
                cam.SetPositionAndRotation(position, rotation);
                targetCamera.fieldOfView = fov;
                yield return null;
            }
            cam.SetPositionAndRotation(_toPosition, _toRotation);
            targetCamera.fieldOfView = _toFov;
        }

        private void SetAway(bool away)
        {
            if (hideWhileAway != null)
                foreach (GameObject go in hideWhileAway)
                    if (go != null) go.SetActive(!away);
            if (disableWhileAway != null)
                foreach (Behaviour b in disableWhileAway)
                    if (b != null) b.enabled = !away;
        }

        /// <summary>Sets the two ends of the glide. Also usable by editor previews.</summary>
        public void Prepare(Vector3 fromPosition, Quaternion fromRotation, float fromFov,
                            Vector3 toPosition, Quaternion toRotation, float toFov)
        {
            _fromPosition = fromPosition; _fromRotation = fromRotation; _fromFov = fromFov;
            _toPosition = toPosition; _toRotation = toRotation; _toFov = toFov;
        }

        /// <summary>Camera pose at normalised time t (0..1) of the glide.</summary>
        public void Evaluate(float t, out Vector3 position, out Quaternion rotation, out float fov)
        {
            // One ease-in-out drives position, rotation and field of view together.
            float e = t * t * (3f - 2f * t);
            position = Vector3.Lerp(_fromPosition, _toPosition, e) + Vector3.up * (arcHeight * 4f * e * (1f - e));
            rotation = Quaternion.Slerp(_fromRotation, _toRotation, e);
            fov = Mathf.Lerp(_fromFov, _toFov, e);
        }
    }
}
