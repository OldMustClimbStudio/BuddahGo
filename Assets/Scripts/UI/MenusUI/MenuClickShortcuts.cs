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
    /// never blocks other clickables (hall doors, bell rope, ema) in the other views.
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

        /// <summary>The shortcut whose proxy is the first thing under the pointer.</summary>
        private Shortcut Under(Vector2 screen)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return null;
            Physics.SyncTransforms();
            if (!Physics.Raycast(targetCamera.ScreenPointToRay(screen), out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore))
                return null;
            foreach (Shortcut s in shortcuts)
                if (s.proxy != null && hit.collider == s.proxy && Available(s)) return s;
            return null;
        }
    }
}
