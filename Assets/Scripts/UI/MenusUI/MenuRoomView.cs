using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Inside of the room view (房间): from the closed hall door, "创建房间" swings both doors inwards and
    /// then cuts (through a short dip to black) to the lobby view inside the hall, doors shut behind.
    /// "返回" cuts back out to the door with the doors open and closes them. The door view itself
    /// (getting there and back) belongs to <see cref="MenuCameraDirector"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuRoomView : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private MenuCameraDirector director;

        [Header("Doors (pivot on the hinge, built closed)")]
        [SerializeField] private Transform[] doors;
        [SerializeField, Range(30f, 150f)] private float openAngle = 105f;
        [SerializeField, Min(0.05f)] private float doorDuration = 0.9f;

        [Header("Camera")]
        [Tooltip("房间内: the lobby view inside the hall.")]
        [SerializeField] private Transform insidePose;
        [SerializeField] private float insideFieldOfView = 55f;

        [Header("Cut")]
        [Tooltip("Full-screen image the cut dips through.")]
        [SerializeField] private Image fadeImage;
        [SerializeField, Min(0.01f)] private float fadeOut = 0.18f;
        [SerializeField, Min(0.01f)] private float fadeIn = 0.35f;

        [Header("UI")]
        [SerializeField] private Button createButton;
        [SerializeField] private GameObject doorUi;
        [SerializeField] private GameObject insideUi;
        [SerializeField] private Button leaveButton;

        private enum State { Outside, Entering, Inside, Leaving }
        private State _state;
        private Quaternion[] _closed;
        private float[] _openSign;
        private Vector3 _outsidePosition;
        private Quaternion _outsideRotation;
        private float _outsideFov;

        public bool IsInside => _state != State.Outside;

        /// <summary>The room is open: the lobby view has faded in and its UI is up.</summary>
        public event System.Action Entered;
        /// <summary>The room is closed: raised while the screen is black, before the view cuts back to the door.</summary>
        public event System.Action Left;

        public void Configure(Camera cam, MenuCameraDirector dir, Transform[] doorList, Transform inside, float insideFov,
                              Image fade, Button create, GameObject doorPanel, GameObject insidePanel, Button leave)
        {
            targetCamera = cam; director = dir; doors = doorList; insidePose = inside; insideFieldOfView = insideFov;
            fadeImage = fade; createButton = create; doorUi = doorPanel; insideUi = insidePanel; leaveButton = leave;
        }

        private void Awake()
        {
            _closed = new Quaternion[doors.Length];
            _openSign = new float[doors.Length];
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] == null) continue;
                _closed[i] = doors[i].localRotation;
                _openSign[i] = InwardSign(doors[i]);
            }
        }

        private void Start()
        {
            if (createButton != null) createButton.onClick.AddListener(Enter);
            if (leaveButton != null) leaveButton.onClick.AddListener(Leave);
            if (insideUi != null) insideUi.SetActive(false);
        }

        /// <summary>Which way round the hinge swings the leaf into the hall (towards what the lobby view looks at).</summary>
        private float InwardSign(Transform door)
        {
            if (insidePose == null) return 1f;
            Renderer r = door.GetComponentInChildren<Renderer>();
            Vector3 leaf = (r != null ? r.bounds.center : door.position) - door.position;
            leaf.y = 0f;
            Vector3 inside = insidePose.position + insidePose.forward * 3f;
            float Score(float sign) => Vector3.Distance(door.position + Quaternion.AngleAxis(sign * 60f, Vector3.up) * leaf, inside);
            return Score(1f) < Score(-1f) ? 1f : -1f;
        }

        public void Enter()
        {
            if (_state != State.Outside || director == null || !director.InRoomView || director.IsMoving) return;
            StartCoroutine(EnterRoutine());
        }

        public void Leave()
        {
            if (_state != State.Inside) return;
            StartCoroutine(LeaveRoutine());
        }

        private IEnumerator EnterRoutine()
        {
            _state = State.Entering;
            if (doorUi != null) doorUi.SetActive(false);
            Transform cam = targetCamera.transform;
            _outsidePosition = cam.position; _outsideRotation = cam.rotation; _outsideFov = targetCamera.fieldOfView;

            yield return SwingDoors(0f, 1f);
            yield return Fade(0f, 1f, fadeOut);
            // Cut to the lobby; the doors are shut behind us.
            cam.SetPositionAndRotation(insidePose.position, insidePose.rotation);
            targetCamera.fieldOfView = insideFieldOfView;
            SetDoors(0f);
            yield return Fade(1f, 0f, fadeIn);
            if (insideUi != null) insideUi.SetActive(true);
            _state = State.Inside;
            Entered?.Invoke();
        }

        private IEnumerator LeaveRoutine()
        {
            _state = State.Leaving;
            if (insideUi != null) insideUi.SetActive(false);
            yield return Fade(0f, 1f, fadeOut);
            Left?.Invoke();
            // Cut back out to the door, just stepped through it, then close up.
            targetCamera.transform.SetPositionAndRotation(_outsidePosition, _outsideRotation);
            targetCamera.fieldOfView = _outsideFov;
            SetDoors(1f);
            yield return Fade(1f, 0f, fadeIn);
            yield return SwingDoors(1f, 0f);
            if (doorUi != null) doorUi.SetActive(true);
            _state = State.Outside;
        }

        private IEnumerator SwingDoors(float from, float to)
        {
            float time = 0f;
            while (time < doorDuration)
            {
                time += Time.deltaTime;
                // Heavy doors: slow to start, settle softly.
                SetDoors(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / doorDuration))));
                yield return null;
            }
            SetDoors(to);
        }

        private void SetDoors(float open)
        {
            for (int i = 0; i < doors.Length; i++)
                if (doors[i] != null)
                    doors[i].localRotation = Quaternion.AngleAxis(_openSign[i] * openAngle * open, Vector3.up) * _closed[i];
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (fadeImage == null) yield break;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                SetFade(Mathf.Lerp(from, to, time / duration));
                yield return null;
            }
            SetFade(to);
        }

        private void SetFade(float alpha)
        {
            Color c = fadeImage.color;
            c.a = alpha;
            fadeImage.color = c;
            fadeImage.raycastTarget = alpha > 0.01f;
        }
    }
}
