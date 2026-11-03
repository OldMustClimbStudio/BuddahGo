using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Prototype camera switcher for the menu environment: a named list of shots (title-screen
    /// candidates, skill select) that proto buttons jump between. Switching fades to black,
    /// cuts and fades back in, so the camera never clips through the shrine on the way.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuCameraProtoSwitcher : MonoBehaviour
    {
        public enum TransitionMode { FadeCut, Smooth }

        [System.Serializable]
        public class Shot
        {
            public string label;
            public Transform pose;
            public float fieldOfView = 60f;
            [Tooltip("Mouse look leans towards the ema wall / hokora on this shot (skill select).")]
            public bool useFocusTargets;
            public Button button;
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private MenuCameraMouseLook mouseLook;
        [SerializeField] private MenuCameraDirector director;
        [SerializeField] private MenuEmaInspector inspector;
        [SerializeField] private List<Shot> shots = new List<Shot>();
        [Tooltip("Shot shown when the game starts (the title / idle screen).")]
        [SerializeField] private int startShot;

        [Header("Transition")]
        [SerializeField] private TransitionMode mode = TransitionMode.FadeCut;
        [SerializeField, Min(0.01f)] private float fadeOut = 0.25f;
        [SerializeField, Min(0.01f)] private float fadeIn = 0.4f;
        [SerializeField, Min(0.01f)] private float smoothDuration = 1.2f;
        [SerializeField] private Image fadeImage;

        [Header("Mouse look focus (skill select)")]
        [SerializeField] private Transform leftFocus;
        [SerializeField] private Transform rightFocus;

        [Header("Ema wall shortcut (main menu → skill select)")]
        [Tooltip("The ema wall and everything hanging on it: a click on any of them on the start shot goes to wallShot.")]
        [SerializeField] private Transform[] wallParts = new Transform[0];
        [Tooltip("Box over the whole wall, enabled only while the shortcut is active so it never blocks single ema.")]
        [SerializeField] private Collider wallProxy;
        [SerializeField] private int wallShot = -1;
        [SerializeField] private MenuSkinView skinView;
        [SerializeField, Min(0f)] private float clickSlopPixels = 6f;

        [Header("Button colours")]
        [SerializeField] private Color normalColor = new Color(0.12f, 0.08f, 0.06f, 0.72f);
        [SerializeField] private Color activeColor = new Color(0.62f, 0.34f, 0.14f, 0.9f);

        private Coroutine _switch;
        private bool _wallPress;
        private Vector2 _wallPressAt;
        public int CurrentShot { get; private set; } = -1;

        /// <summary>Settled on the start shot (the main menu).</summary>
        public bool AtStartShot => _switch == null && CurrentShot == startShot;

        /// <summary>Settled on the skill-select shot (the only one whose mouse look leans to the ema wall and hokora).</summary>
        public bool AtSkillSelect => _switch == null && CurrentShot >= 0 && CurrentShot < shots.Count && shots[CurrentShot].useFocusTargets;

        /// <summary>The ema wall can be clicked to go to skill select (main menu, nothing else going on).</summary>
        public bool WallShortcutActive =>
            AtStartShot && wallShot >= 0 && Application.isFocused
            && (director == null || !director.IsAway) && (skinView == null || !skinView.IsActive);

        public int IndexOf(string label) => shots.FindIndex(s => s.label == label);

        public void ConfigureWallShortcut(Transform[] parts, Collider proxy, int shot, MenuSkinView view)
        {
            wallParts = parts; wallProxy = proxy; wallShot = shot; skinView = view;
        }

        /// <summary>True when t is the ema wall or hangs on it (or is a child of either).</summary>
        public bool IsWallPart(Transform t)
        {
            for (; t != null; t = t.parent)
                if (System.Array.IndexOf(wallParts, t) >= 0) return true;
            return false;
        }

        private void Update()
        {
            bool active = WallShortcutActive;
            if (wallProxy != null && wallProxy.enabled != active) wallProxy.enabled = active;
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || !active) { _wallPress = false; return; }
            Vector2 at = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) { _wallPress = PointerOnWall(at); _wallPressAt = at; }
            if (_wallPress && (at - _wallPressAt).magnitude > clickSlopPixels) _wallPress = false;
            if (_wallPress && mouse.leftButton.wasReleasedThisFrame)
            {
                _wallPress = false;
                if (PointerOnWall(at)) SwitchTo(wallShot);
            }
        }

        private bool PointerOnWall(Vector2 screen)
        {
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return false;
            // Physics runs in Script mode without auto-sync: bring colliders of moved ema up to date.
            Physics.SyncTransforms();
            return Physics.Raycast(targetCamera.ScreenPointToRay(screen), out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore)
                   && IsWallPart(hit.collider.transform);
        }

        public void Configure(Camera cam, MenuCameraMouseLook look, MenuCameraDirector dir, MenuEmaInspector insp,
                              List<Shot> list, int start, Image fade, Transform left, Transform right)
        {
            targetCamera = cam; mouseLook = look; director = dir; inspector = insp;
            shots = list; startShot = start; fadeImage = fade; leftFocus = left; rightFocus = right;
        }

        private void Start()
        {
            for (int i = 0; i < shots.Count; i++)
            {
                int index = i;
                if (shots[i].button != null) shots[i].button.onClick.AddListener(() => SwitchTo(index));
            }
            if (fadeImage != null) SetFade(0f);
            if (startShot >= 0 && startShot < shots.Count) Apply(startShot);
        }

        public void SwitchTo(int index)
        {
            if (index < 0 || index >= shots.Count || shots[index].pose == null) return;
            if (_switch != null || (director != null && director.IsAway) || (inspector != null && inspector.IsInspecting)) return;
            _switch = StartCoroutine(mode == TransitionMode.FadeCut ? FadeCut(index) : Smooth(index));
        }

        private IEnumerator FadeCut(int index)
        {
            yield return Fade(0f, 1f, fadeOut);
            Apply(index);
            yield return Fade(1f, 0f, fadeIn);
            _switch = null;
        }

        private IEnumerator Smooth(int index)
        {
            Shot shot = shots[index];
            if (mouseLook != null) mouseLook.enabled = false;
            Transform cam = targetCamera.transform;
            Vector3 p0 = cam.position, p2 = shot.pose.position;
            Quaternion r0 = cam.rotation, r2 = shot.pose.rotation;
            float f0 = targetCamera.fieldOfView;
            // Lift the middle of the move so it arcs over the roofs instead of cutting through them.
            Vector3 p1 = (p0 + p2) * 0.5f + Vector3.up * (Vector3.Distance(p0, p2) * 0.35f);
            float time = 0f;
            while (time < smoothDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / smoothDuration));
                Vector3 a = Vector3.Lerp(p0, p1, t), b = Vector3.Lerp(p1, p2, t);
                cam.SetPositionAndRotation(Vector3.Lerp(a, b, t), Quaternion.Slerp(r0, r2, t));
                targetCamera.fieldOfView = Mathf.Lerp(f0, shot.fieldOfView, t);
                yield return null;
            }
            Apply(index);
            _switch = null;
        }

        private void Apply(int index)
        {
            Shot shot = shots[index];
            if (mouseLook != null)
            {
                // The mouse look captures its base pose when enabled, so re-enable it after placing the camera.
                mouseLook.enabled = false;
                mouseLook.SetFocusTargets(shot.useFocusTargets ? leftFocus : null, shot.useFocusTargets ? rightFocus : null);
            }
            targetCamera.transform.SetPositionAndRotation(shot.pose.position, shot.pose.rotation);
            targetCamera.fieldOfView = shot.fieldOfView;
            if (mouseLook != null) mouseLook.enabled = true;
            CurrentShot = index;
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            for (int i = 0; i < shots.Count; i++)
                if (shots[i].button != null && shots[i].button.targetGraphic != null)
                    shots[i].button.targetGraphic.color = i == CurrentShot ? activeColor : normalColor;
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
