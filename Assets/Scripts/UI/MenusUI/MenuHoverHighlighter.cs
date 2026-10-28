using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SteamMultiplayer.UI
{
    /// <summary>
    /// Glowing outline on the menu-environment object under the mouse, shown only while that object
    /// can actually be clicked or dragged: skill ema outside the skin view (only the inspected one while
    /// inspecting) on the skill-select shot, the whole ema wall on the main menu (it leads to skill
    /// select), the skin model inside the skin view, the bell rope in the door view, the sand table
    /// in the hall lobby, and the main hall and garden Buddha on the main menu (they press Start / 皮肤).
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuHoverHighlighter : MonoBehaviour
    {
        public enum TargetKind { SkillEma, SkinModel, BellRope, EmaWall, SandTable, MenuShortcut }

        [System.Serializable]
        public class Target
        {
            public Transform root;
            public TargetKind kind;
            [Tooltip("Hard-edged meshes (flat boards) extrude outwards from their centre so corners stay closed.")]
            [Range(0f, 1f)] public float radial;
            [Tooltip("More objects that glow and pick together with the root (the ema hanging on the wall).")]
            public Transform[] extras;
            [Tooltip("Only the root's own renderer glows, not its children (the map drawn on the sand table).")]
            public bool rootOnly;
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private MenuEmaInspector inspector;
        [SerializeField] private MenuSkinView skinView;
        [SerializeField] private MenuCameraDirector director;
        [Tooltip("Main menu: the ema wall glows as one button; skill select: single ema glow.")]
        [SerializeField] private MenuCameraProtoSwitcher shotGate;
        [SerializeField] private MenuSandTableFocus sandTable;
        [SerializeField] private MenuClickShortcuts shortcuts;
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private List<Target> targets = new List<Target>();
        [SerializeField, Min(0.1f)] private float fadeSpeed = 10f;

        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int RadialId = Shader.PropertyToID("_Radial");
        private static readonly int CenterId = Shader.PropertyToID("_CenterOS");

        private class Runtime
        {
            public Target target;
            public readonly List<Renderer> outlines = new List<Renderer>();
            public readonly List<Collider> colliders = new List<Collider>();
            public readonly List<Renderer> sources = new List<Renderer>();
            public float fade;
        }

        private readonly List<Runtime> _runtime = new List<Runtime>();
        private MaterialPropertyBlock _block;

        public void Configure(Camera cam, MenuEmaInspector insp, MenuSkinView view, MenuCameraDirector dir, Material material, List<Target> list)
        {
            targetCamera = cam; inspector = insp; skinView = view; director = dir; outlineMaterial = material; targets = list;
        }

        public void SetShotGate(MenuCameraProtoSwitcher switcher) => shotGate = switcher;

        public void SetSandTable(MenuSandTableFocus focus) => sandTable = focus;

        public void SetShortcuts(MenuClickShortcuts clickShortcuts) => shortcuts = clickShortcuts;

        // Start, not Awake: generated meshes (the bell rope) exist only after their own OnEnable.
        private void Start()
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            _block = new MaterialPropertyBlock();
            foreach (Target t in targets)
                if (t.root != null && outlineMaterial != null) _runtime.Add(Build(t));
        }

        /// <summary>Creates hidden outline renderers that share each source renderer's mesh (and bones).</summary>
        private Runtime Build(Target t)
        {
            var rt = new Runtime { target = t };
            var renderers = new List<Renderer>();
            var parts = new List<Transform> { t.root };
            if (t.extras != null) parts.AddRange(t.extras);
            foreach (Transform part in parts)
            {
                if (part == null) continue;
                if (t.rootOnly && part == t.root)
                {
                    rt.colliders.AddRange(part.GetComponents<Collider>());
                    renderers.AddRange(part.GetComponents<Renderer>());
                    continue;
                }
                rt.colliders.AddRange(part.GetComponentsInChildren<Collider>(true));
                renderers.AddRange(part.GetComponentsInChildren<Renderer>(true));
            }
            foreach (Renderer src in renderers)
            {
                if (src.name == "HoverOutline") continue;   // another target's outline on a shared object
                Mesh mesh = null;
                if (src is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                else if (src is MeshRenderer && src.TryGetComponent(out MeshFilter mf)) mesh = mf.sharedMesh;
                if (mesh == null) continue;
                rt.sources.Add(src);
                // A static-batched renderer's mesh is the combined batch: its outline would glow on other objects.
                if (src.isPartOfStaticBatch)
                {
                    Debug.LogWarning($"[MenuHoverHighlighter] {src.name} is static-batched; no outline. Clear its Batching Static flag.", src);
                    continue;
                }

                var go = new GameObject("HoverOutline");
                go.transform.SetParent(src.transform, false);
                var mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int i = 0; i < mats.Length; i++) mats[i] = outlineMaterial;
                Renderer outline;
                if (src is SkinnedMeshRenderer skinned)
                {
                    var o = go.AddComponent<SkinnedMeshRenderer>();
                    o.sharedMesh = mesh; o.bones = skinned.bones; o.rootBone = skinned.rootBone;
                    o.localBounds = skinned.localBounds; o.updateWhenOffscreen = true;
                    outline = o;
                }
                else
                {
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    outline = go.AddComponent<MeshRenderer>();
                }
                outline.sharedMaterials = mats;
                outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outline.receiveShadows = false;
                outline.enabled = false;
                outline.GetPropertyBlock(_block);
                _block.SetFloat(RadialId, t.radial);
                _block.SetVector(CenterId, mesh.bounds.center);
                _block.SetFloat(FadeId, 0f);
                outline.SetPropertyBlock(_block);
                rt.outlines.Add(outline);
            }
            return rt;
        }

        private void LateUpdate()
        {
            Runtime hovered = FindHovered();
            float dt = Time.unscaledDeltaTime;
            foreach (Runtime rt in _runtime)
            {
                float goal = rt == hovered ? 1f : 0f;
                rt.fade = Mathf.MoveTowards(rt.fade, goal, fadeSpeed * dt);
                bool visible = rt.fade > 0.001f;
                foreach (Renderer o in rt.outlines)
                {
                    if (o.enabled != visible) o.enabled = visible;
                    if (!visible) continue;
                    o.GetPropertyBlock(_block);
                    _block.SetFloat(FadeId, rt.fade);
                    o.SetPropertyBlock(_block);
                }
            }
        }

        private Runtime FindHovered()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || targetCamera == null || !Application.isFocused) return null;
            if (director != null && director.IsMoving) return null;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return null;

            // Physics runs in Script mode without auto-sync: bring colliders of moved ema up to date.
            Physics.SyncTransforms();
            Ray ray = targetCamera.ScreenPointToRay(mouse.position.ReadValue());
            // Whatever the scene ray hits first decides what is under the mouse (no outlines through walls).
            bool any = Physics.Raycast(ray, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore);
            float blocker = any ? hit.distance : float.MaxValue;

            Runtime best = null;
            float bestDistance = float.MaxValue;
            foreach (Runtime rt in _runtime)
            {
                if (!IsInteractive(rt.target)) continue;
                float d;
                switch (rt.target.kind)
                {
                    case TargetKind.BellRope: d = RopeDistance(rt, ray, mouse.position.ReadValue()); break;
                    // the wall and everything on it is one button: hovered when the first hit is any part of it
                    case TargetKind.EmaWall:
                    // main-menu shortcuts: same, through their proxy box
                    case TargetKind.MenuShortcut: d = any && rt.colliders.Contains(hit.collider) ? hit.distance : float.MaxValue; break;
                    default: d = HitDistance(rt, ray); break;
                }
                if (d < bestDistance && d <= blocker + 0.05f) { best = rt; bestDistance = d; }
            }
            return best;
        }

        private bool IsInteractive(Target t)
        {
            bool inSkinView = skinView != null && skinView.IsActive;
            switch (t.kind)
            {
                case TargetKind.SkillEma:
                    if (inSkinView || inspector == null || !inspector.isActiveAndEnabled) return false;
                    if (director != null && director.IsAway) return false;
                    if (shotGate != null && !shotGate.AtSkillSelect) return false;
                    return !inspector.IsInspecting || inspector.CurrentItem == t.root;
                case TargetKind.EmaWall:
                    return shotGate != null && shotGate.WallShortcutActive;
                case TargetKind.SkinModel:
                    return skinView != null && skinView.IsInteractive;
                case TargetKind.BellRope:
                    return t.root.GetComponentInChildren<MenuBellRope>() is MenuBellRope rope && rope.IsInteractive;
                case TargetKind.SandTable:
                    return sandTable != null && sandTable.CanFocus;
                case TargetKind.MenuShortcut:
                    return shortcuts != null && shortcuts.IsActive(t.root);
                default:
                    return false;
            }
        }

        /// <summary>The rope is thin and partly lies on the floor: pick it in screen space like the rope itself does.</summary>
        private float RopeDistance(Runtime rt, Ray ray, Vector2 pointer)
        {
            var rope = rt.target.root.GetComponentInChildren<MenuBellRope>();
            if (rope != null && rope.NodeUnder(pointer) >= 0) return 0f;
            float best = float.MaxValue;
            foreach (Renderer r in rt.sources)
                if (r.GetComponent<MenuBellRope>() == null && r.bounds.IntersectRay(ray, out float d) && d < best) best = d;
            return best;
        }

        private static float HitDistance(Runtime rt, Ray ray)
        {
            float best = float.MaxValue;
            foreach (Collider c in rt.colliders)
                if (c.enabled && c.Raycast(ray, out RaycastHit h, 200f) && h.distance < best) best = h.distance;
            if (best < float.MaxValue || rt.colliders.Count > 0) return best;
            foreach (Renderer r in rt.sources)
                if (r.bounds.IntersectRay(ray, out float d) && d < best) best = d;
            return best;
        }
    }
}
