using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Main-menu objects that act as their button: clicking the main hall presses Start, clicking the
    /// garden Buddha presses 皮肤. A click presses the button itself (same handlers, same behaviour)
    /// and works only on the main-menu shot while that button is shown and nothing else is going on.
    /// Each object is picked by a proxy box that is enabled only while its click is available, so it
    /// never blocks other clickables (hall doors, bell rope, ema) in the other views. The box fills the
    /// gaps between the parts; a pick also needs the pointer over one of the parts' own bounds, so the
    /// air inside the box (between the eaves, beside the deck) is not part of the button.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuClickShortcuts : MonoBehaviour
    {
        [System.Serializable]
        public class Shortcut
        {
            public string label;
            [Tooltip("The button this object stands in for; its onClick runs on a click.")]
            public Button button;
            [Tooltip("Outline root (the hover outline target uses the same root).")]
            public Transform root;
            [Tooltip("More objects that belong to it (the rest of the hall); they glow and pick together with the root.")]
            public Transform[] parts;
            [Tooltip("Box over the whole object, enabled only while the click is available.")]
            public Collider proxy;
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private MenuCameraProtoSwitcher shotGate;
        [SerializeField] private MenuCameraDirector director;
        [SerializeField] private MenuSkinView skinView;
        [SerializeField] private MenuEmaInspector inspector;
        [SerializeField] private List<Shortcut> shortcuts = new List<Shortcut>();
        [SerializeField, Min(0f)] private float clickSlopPixels = 6f;

        private Shortcut _pressed;
        private Vector2 _pressedAt;
        private readonly List<Renderer> _renderers = new List<Renderer>();

        public IReadOnlyList<Shortcut> Shortcuts => shortcuts;

        public void Configure(Camera cam, MenuCameraProtoSwitcher gate, MenuCameraDirector dir, MenuSkinView view,
                              MenuEmaInspector insp, List<Shortcut> list)
        {
            targetCamera = cam; shotGate = gate; director = dir; skinView = view; inspector = insp; shortcuts = list;
        }

        /// <summary>Settled on the main menu with no move, close-up or inspection going on.</summary>
        private bool MenuIdle =>
            Application.isFocused
            && (shotGate == null || shotGate.AtStartShot)
            && (director == null || !director.IsAway)
            && (skinView == null || !skinView.IsActive)
            && (inspector == null || !inspector.IsInspecting);

        private static bool Available(Shortcut s) =>
            s.button != null && s.button.IsActive() && s.button.interactable;

        /// <summary>The object under root can be clicked now (used by the hover outline).</summary>
        public bool IsActive(Transform root)
        {
            if (!MenuIdle) return false;
            foreach (Shortcut s in shortcuts)
                if (s.root == root) return Available(s);
            return false;
        }

        private void Update()
        {
            bool idle = MenuIdle;
            foreach (Shortcut s in shortcuts)
            {
                bool on = idle && Available(s);
                if (s.proxy != null && s.proxy.enabled != on) s.proxy.enabled = on;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !idle) { _pressed = null; return; }
            Vector2 at = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) { _pressed = Under(at); _pressedAt = at; }
            if (_pressed != null && (at - _pressedAt).magnitude > clickSlopPixels) _pressed = null;
            if (_pressed != null && mouse.leftButton.wasReleasedThisFrame)
            {
                Shortcut clicked = _pressed;
                _pressed = null;
                if (Under(at) == clicked && Available(clicked)) clicked.button.onClick.Invoke();
            }
        }

        /// <summary>
        /// The object under root is what the ray points at, given the scene's first hit along it: its proxy is
        /// that hit and the ray crosses one of its parts (used by the hover outline, so glow and click agree).
        /// </summary>
        public bool IsUnder(Transform root, Ray ray, RaycastHit firstHit)
        {
            foreach (Shortcut s in shortcuts)
                if (s.root == root) return Picks(s, ray, firstHit);
            return false;
        }

        /// <summary>The shortcut picked under the pointer.</summary>
        private Shortcut Under(Vector2 screen)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return null;
            // Physics runs in Script mode without auto-sync.
            Physics.SyncTransforms();
            Ray ray = targetCamera.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore)) return null;
            foreach (Shortcut s in shortcuts)
                if (Available(s) && Picks(s, ray, hit)) return s;
            return null;
        }

        private bool Picks(Shortcut s, Ray ray, RaycastHit firstHit) =>
            s.proxy != null && firstHit.collider == s.proxy && (Crosses(s.root, ray) || CrossesAny(s.parts, ray));

        private bool CrossesAny(Transform[] roots, Ray ray)
        {
            if (roots == null) return false;
            foreach (Transform t in roots)
                if (Crosses(t, ray)) return true;
            return false;
        }

        /// <summary>The ray crosses the bounds of a shown renderer under root (read each time: the skin model changes).</summary>
        private bool Crosses(Transform root, Ray ray)
        {
            if (root == null) return false;
            root.GetComponentsInChildren(false, _renderers);
            foreach (Renderer r in _renderers)
                if (r.enabled && r.name != "HoverOutline" && r.bounds.IntersectRay(ray)) return true;
            return false;
        }
    }
}
