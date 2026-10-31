using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BuddahGo.VFX
{
    // 3D ink-smoke simulation for the 如来神掌 hand (Docs/vfx/palm-mesh-tripo.md section 19): a 3D Stable Fluids
    // solver (InkFluid3D.compute) on an axis-aligned grid that is anchored in the world; the hand sweeps through it
    // and its whole mesh surface (a thin SDF shell) injects ink, so the smoke is born everywhere on the skin and lives
    // in three dimensions. When the hand drifts recenterCells from where it was placed, the grid is re-centred by a
    // whole number of cells (an exact texel copy: no blur). The density is ray-marched by FX/InkFluid3D on a hidden,
    // unparented world box, so nothing in the hierarchy moves with the hand. Edit mode too.
    //
    // Cost (Docs/vfx/push-projectile-hands.md section 15): only velocity and density are state; the six other grids
    // (scratch velocity / density, two pressures, divergence, curl) are one static set shared by every instance, since
    // the instances step one after another on the same command stream. State sets come from a pool, so a palm spawning
    // mid-race takes a set that an earlier palm returned instead of allocating. A fast hand is painted along its whole
    // path inside one step (the Source kernel sweeps the shell) rather than with sub-steps. At most simulationBudget
    // instances simulate at once (the ones nearest the camera); the rest hold no grids and draw no smoke.
    [ExecuteAlways]
    public sealed class InkFluidVolume : MonoBehaviour
    {
        [Header("Grid (world-fixed, axis-aligned)")]
        public ComputeShader compute;
        [Tooltip("Cells per axis (multiples of 8 / 8 / 4 are used as given).")] public Vector3Int resolution = new Vector3Int(80, 80, 128);
        [Tooltip("World units per cell.")] public float cellSize = 2f;
        [Tooltip("Box centre relative to the hand, in the hand's axes at placement (push it behind the hand so the trail fits: the hand flies along +Z).")] public Vector3 centreOffset = new Vector3(0f, 0f, -80f);
        [Tooltip("Re-centre the grid (an exact whole-cell copy) once the hand has drifted this many cells from where it was placed. Small is fine: the copy is exact, and the hand must never reach the box's front wall.")] public float recenterCells = 4f;

        [Header("Budget (shared by every instance)")]
        [Tooltip("How many InkFluidVolumes may simulate at once on this client: the ones nearest the main camera. The others hold no grids and draw no smoke (their hand still draws). Memory = budget x (10 bytes x cells) + one shared scratch set (24 bytes x cells).")] public int simulationBudget = 8;

        [Header("Simulation (velocity in cells / s)")]
        [Tooltip("Jacobi passes of the pressure solve. With Coarse Pressure on they run on a half-resolution grid (1/8 of the cost each) and Pressure Fine Iterations full-resolution passes follow.")] [Range(0, 40)] public int pressureIterations = 12;
        [Tooltip("Solve the pressure on a half-resolution grid first (cheap, converges faster), then a few fine passes. Off = every pass at full resolution (the original solver).")] public bool coarsePressure = true;
        [Tooltip("Full-resolution Jacobi passes after the coarse solve (Coarse Pressure on).")] [Range(0, 8)] public int pressureFineIterations = 2;
        [Tooltip("How fast cells in the source shell reach the target density (per second).")] public float injectRate = 12f;
        [Tooltip("Hand speed (world units / s) at which emission is full; below it emission scales down linearly. 0 = always full. The projectile only smokes while it moves.")] public float emissionSpeedRef = 60f;
        [Tooltip("Emission fraction when the hand is still.")] [Range(0f, 1f)] public float emissionAtRest = 0f;
        [Tooltip("Density the source drives cells toward.")] [Range(0.05f, 1f)] public float targetDensity = 0.45f;
        [Tooltip("Thickness of the emission shell around the mesh surface (hand units).")] public float surfaceBand = 3f;
        [Tooltip("SDF value counted as the surface (hand units).")] public float sourceInside = -0.5f;
        [Tooltip("Fraction of the surface emitting at any moment.")] [Range(0.05f, 1f)] public float sparsity = 0.4f;
        [Tooltip("Size of the random emission patches (cells).")] public float sparsityScale = 4f;
        [Tooltip("Random velocity of fresh ink (cells / s).")] public float swirl = 25f;
        [Tooltip("Upward velocity of fresh ink. Keep 0: no rise.")] public float injectVelocity = 0f;
        [Tooltip("How much of the hand's own velocity fresh ink inherits.")] public float dragInject = 0.1f;
        [Tooltip("Upward acceleration per unit density. Keep 0.")] public float buoyancy = 0f;
        [Tooltip("Vorticity confinement: higher = finer, longer-lived filaments.")] public float vorticity = 6f;
        [Tooltip("Random ambient momentum per unit density (cells / s^2).")] public float drift = 30f;
        [Tooltip("Blend toward the 6-neighbour mean per step.")] [Range(0f, 0.6f)] public float diffusion = 0.12f;
        [Tooltip("Velocity kept per 1/30 s (rate-independent: scaled to the actual step).")] [Range(0.9f, 1f)] public float velocityDissipation = 0.985f;
        [Tooltip("Density kept per 1/30 s (rate-independent: scaled to the actual step).")] [Range(0.9f, 1f)] public float densityDissipation = 0.975f;
        [Tooltip("Frames longer than this run two simulation steps instead of one (never more: a slow frame must not cost more fluid work, or it only gets slower). The solver is stable at any step; the hand's path is covered by the sweep.")] public float maxStep = 1f / 25f;
        [Tooltip("The hand is painted at several poses along its path inside one step, spaced at most this many cells apart (up to 8 poses), so a fast hand leaves a continuous trail.")] public float maxCellsPerStep = 2f;
        [Tooltip("Edit-mode simulation rate (Hz).")] public float editorHz = 60f;
        public bool simulateInEditMode = true;

        [Header("Colour (overrides the material; tweak here)")]
        [Tooltip("Thick ink. Palette: warm olive #1C1C18, cyan-black #111A19.")] public Color inkColor = new Color(0.11f, 0.11f, 0.094f, 1f);
        [Tooltip("Thin wash. Palette: #383428.")] public Color washColor = new Color(0.22f, 0.204f, 0.157f, 1f);
        [Tooltip("Density below this is not drawn (crisp edge).")] [Range(0f, 0.5f)] public float threshold = 0.03f;
        [Tooltip("Density contrast (pow).")] [Range(0.3f, 3f)] public float contrast = 1.1f;
        [Tooltip("Edge softness: density range over which the threshold fades in (bigger = softer, less stair-stepping).")] [Range(0.005f, 0.3f)] public float softness = 0.06f;
        [Tooltip("Extinction per world unit at full density: higher = blacker.")] public float extinction = 0.35f;

        [Header("Driven by PalmInkTimeline")]
        public float pull = 0f;
        public float bandScale = 1f;
        [Range(0f, 1f)] public float emissionScale = 1f;
        [Range(0f, 1f)] public float fadeOut = 0f;
        [Tooltip("Gather: while > 0, ink anywhere in the box is carried straight toward the surface at this speed (cells / s).")] public float gatherSpeed = 0f;

        [Header("Hand volume (same values as Mat_FX_Palm_InkVolume)")]
        [Tooltip("The hand's object space: the prefab root.")] public Transform hand;
        public Texture3D sdf;
        public Vector3 volumeOrigin = new Vector3(-41.94f, -57f, -29.23f);
        public Vector3 volumeSize = new Vector3(83.88f, 114f, 58.46f);
        public float sdfRange = 36f;

        [Header("Rendering")]
        public Material material;

        // ---------- shared GPU memory ----------

        // Per-instance state: what survives from one step to the next.
        sealed class StateSet
        {
            public Vector3Int size;
            public RenderTexture vel, den;
            public ComputeBuffer bounds;    // 6 uints: occupied AABB in cells, written by AdvectDensity, read by the shader
            public bool Alive => vel != null && den != null && bounds != null && bounds.IsValid();
            public void Destroy()
            {
                DestroyRt(vel); DestroyRt(den); vel = den = null;
                if (bounds != null) { bounds.Release(); bounds = null; }
            }
        }

        // One set for everybody: inputs and outputs of the passes inside a step. Instances step sequentially, so no two
        // ever use it at the same time.
        sealed class ScratchSet
        {
            public Vector3Int size;
            public RenderTexture vel, den, prs0, prs1, div, curl, divC, prsC0, prsC1;
            public Vector3Int sizeC;
            public bool Alive => vel != null && den != null && prs0 != null && prs1 != null && div != null && curl != null && divC != null && prsC0 != null && prsC1 != null;
            public void Destroy()
            {
                foreach (var rt in new[] { vel, den, prs0, prs1, div, curl, divC, prsC0, prsC1 }) DestroyRt(rt);
                vel = den = prs0 = prs1 = div = curl = divC = prsC0 = prsC1 = null;
            }
        }

        static ScratchSet s_scratch;
        static readonly Dictionary<Vector3Int, Stack<StateSet>> s_pool = new Dictionary<Vector3Int, Stack<StateSet>>();
        static readonly List<InkFluidVolume> s_active = new List<InkFluidVolume>();
        static int s_scheduledFrame = -1;
        static int s_scratchBoundFrame = -1;
        static int s_poolKeep = 8;
        static bool s_hooked;
        static readonly Matrix4x4[] s_sweep = new Matrix4x4[8];
        static readonly List<InkFluidVolume> s_rank = new List<InkFluidVolume>();
        static readonly System.Comparison<InkFluidVolume> s_byScore = (a, b) => a.score.CompareTo(b.score);
        static readonly uint[] s_zero6 = new uint[6];

        public static int ActiveCount => s_active.Count;
        public static int SimulatingCount { get { int n = 0; foreach (var v in s_active) if (v.state != null) n++; return n; } }
        public static int PooledCount { get { int n = 0; foreach (var kv in s_pool) n += kv.Value.Count; return n; } }
        // Bytes held by the shared scratch set and the state sets (simulating + pooled).
        public static long GpuBytes
        {
            get
            {
                long b = 0;
                if (s_scratch != null) b += (long)Cells(s_scratch.size) * 24 + (long)Cells(s_scratch.sizeC) * 6;
                foreach (var v in s_active) if (v.state != null) b += (long)Cells(v.state.size) * 10;
                foreach (var kv in s_pool) b += (long)kv.Value.Count * Cells(kv.Key) * 10;
                return b;
            }
        }
        static int Cells(Vector3Int s) => s.x * s.y * s.z;

        static void HookTeardown()
        {
            if (s_hooked) return;
            s_hooked = true;
            Application.quitting += DestroyShared;
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += DestroyShared;
#endif
        }

        // Frees everything static (the scratch set and the pooled state sets). Instances still active lose nothing they
        // own; they re-acquire on their next Tick.
        public static void DestroyShared()
        {
            if (s_scratch != null) { s_scratch.Destroy(); s_scratch = null; }
            foreach (var kv in s_pool) foreach (var set in kv.Value) set.Destroy();
            s_pool.Clear();
        }

        static ScratchSet Scratch(Vector3Int size)
        {
            var sizeC = new Vector3Int((size.x + 1) / 2, (size.y + 1) / 2, (size.z + 1) / 2);
            if (s_scratch != null && (s_scratch.size != size || !s_scratch.Alive)) { s_scratch.Destroy(); s_scratch = null; }
            if (s_scratch == null)
            {
                HookTeardown();
                s_scratch = new ScratchSet
                {
                    size = size,
                    vel = Make(size, RenderTextureFormat.ARGBHalf, "InkFluid3D.scratch.vel"), den = Make(size, RenderTextureFormat.RHalf, "InkFluid3D.scratch.den"),
                    prs0 = Make(size, RenderTextureFormat.RHalf, "InkFluid3D.scratch.prs0"), prs1 = Make(size, RenderTextureFormat.RHalf, "InkFluid3D.scratch.prs1"),
                    div = Make(size, RenderTextureFormat.RHalf, "InkFluid3D.scratch.div"), curl = Make(size, RenderTextureFormat.ARGBHalf, "InkFluid3D.scratch.curl"),
                    sizeC = sizeC, divC = Make(sizeC, RenderTextureFormat.RHalf, "InkFluid3D.scratch.divC"),
                    prsC0 = Make(sizeC, RenderTextureFormat.RHalf, "InkFluid3D.scratch.prsC0"), prsC1 = Make(sizeC, RenderTextureFormat.RHalf, "InkFluid3D.scratch.prsC1")
                };
                s_scratchBoundFrame = -1;
            }
            return s_scratch;
        }

        static StateSet TakeState(Vector3Int size)
        {
            HookTeardown();
            if (s_pool.TryGetValue(size, out var stack))
                while (stack.Count > 0)
                {
                    var set = stack.Pop();
                    if (set.Alive) { if (!set.vel.IsCreated()) set.vel.Create(); if (!set.den.IsCreated()) set.den.Create(); return set; }
                    set.Destroy();
                }
            return new StateSet
            {
                size = size,
                vel = Make(size, RenderTextureFormat.ARGBHalf, "InkFluid3D.vel"), den = Make(size, RenderTextureFormat.RHalf, "InkFluid3D.den"),
                bounds = new ComputeBuffer(6, sizeof(uint))
            };
        }

        static void ReturnState(StateSet set)
        {
            if (set == null) return;
            if (!set.Alive) { set.Destroy(); return; }
            if (!s_pool.TryGetValue(set.size, out var stack)) s_pool[set.size] = stack = new Stack<StateSet>();
            if (stack.Count >= Mathf.Max(s_poolKeep, 1)) { set.Destroy(); return; }
            stack.Push(set);
        }

        static RenderTexture Make(Vector3Int size, RenderTextureFormat format, string name)
        {
            var rt = new RenderTexture(size.x, size.y, 0, format, RenderTextureReadWrite.Linear)
            {
                name = name, dimension = TextureDimension.Tex3D, volumeDepth = size.z, enableRandomWrite = true,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
            };
            rt.Create();
            return rt;
        }

        static void DestroyRt(RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            if (Application.isPlaying) Destroy(rt); else DestroyImmediate(rt);
        }

        // Once per frame: which instances simulate this frame. Under the budget everybody does; over it the ones
        // nearest the main camera, with 20% hysteresis in distance so two palms at the edge do not swap every frame.
        static void Schedule()
        {
            int frame = Time.frameCount;
            if (Application.isPlaying && s_scheduledFrame == frame) return;
            s_scheduledFrame = frame;
            Camera cam = Camera.main;
            s_poolKeep = Mathf.Clamp(PickSimulating(s_active, cam != null, cam != null ? cam.transform.position : Vector3.zero), 1, 32);
        }

        // Sets InBudget on each volume and returns the budget (the smallest simulationBudget among them). Without an
        // eye (no main camera) the oldest volumes keep simulating.
        internal static int PickSimulating(List<InkFluidVolume> volumes, bool hasEye, Vector3 eye)
        {
            int budget = int.MaxValue;
            foreach (var v in volumes) budget = Mathf.Min(budget, Mathf.Max(v.simulationBudget, 0));
            if (volumes.Count <= budget) { foreach (var v in volumes) v.wanted = true; return budget; }
            s_rank.Clear();
            foreach (var v in volumes)
            {
                Vector3 p = v.hand != null ? v.hand.position : v.transform.position;
                v.score = hasEye ? RankScore((p - eye).sqrMagnitude, v.state != null) : v.spawnOrder;
                s_rank.Add(v);
            }
            s_rank.Sort(s_byScore);
            for (int i = 0; i < s_rank.Count; i++) s_rank[i].wanted = i < budget;
            return budget;
        }

        // Lower ranks first. A volume already simulating counts as 20% nearer (0.8 squared), so it keeps its grids
        // until a newcomer is clearly closer.
        internal static float RankScore(float sqrDistance, bool simulating) => sqrDistance * (simulating ? 0.64f : 1f);

        // ---------- instance ----------

        StateSet state;
        ComputeBuffer stats;
        MaterialPropertyBlock block;
        GameObject worldBox;
        MeshRenderer boxRenderer;
        Vector3 anchorCentre, offsetWorld;
        Vector3 emitOffset;
        bool anchorValid, hasLast;
        Vector3 lastHandPos;
        double lastEditorTime;
        Vector3Int allocated;
        int kSource, kAdvectV, kCurl, kForces, kDiv, kJacobiA, kJacobiB, kGrad, kAdvectD, kClear, kStats, kSeed, kResetBounds, kRestrict, kJacobiCA, kJacobiCB, kProlong;
        bool kernelsFound;
        bool wanted = true;
        float score;
        int spawnOrder;
        static int s_spawnCounter;
        float speedFactor = 1f;

        public RenderTexture DensityTexture => state?.den;
        public bool IsSimulating => state != null;
        internal bool InBudget => wanted;
        public float SpeedFactor => speedFactor;
        public Vector3 BoxMin => anchorCentre - BoxSize * 0.5f;
        public Vector3 BoxSize => new Vector3(resolution.x, resolution.y, resolution.z) * Mathf.Max(cellSize, 0.01f);
        public Vector3 AnchorCentre => anchorCentre;

        void OnEnable()
        {
            if (hand == null && transform.parent != null) hand = transform.parent;
            spawnOrder = s_spawnCounter++;
            if (!s_active.Contains(this)) s_active.Add(this);
            wanted = true;
            Allocate();
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
            if (!Application.isPlaying) { lastEditorTime = EditorApplication.timeSinceStartup; EditorApplication.update += EditorTick; }
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
            s_active.Remove(this);
            Release();
            if (worldBox != null) { DestroyImmediate(worldBox); worldBox = null; boxRenderer = null; }
            anchorValid = false;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            Tick(Time.deltaTime);
        }

#if UNITY_EDITOR
        void EditorTick()
        {
            if (this == null || !isActiveAndEnabled || !simulateInEditMode || Application.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - lastEditorTime);
            if (dt < 1f / Mathf.Max(editorHz, 1f)) return;
            dt = Mathf.Min(dt, 0.1f);
            lastEditorTime = now;
            Tick(dt);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
#endif

        void OnValidate()
        {
            resolution = new Vector3Int(Mathf.Max(8, resolution.x), Mathf.Max(8, resolution.y), Mathf.Max(4, resolution.z));
#if UNITY_EDITOR
            EditorApplication.delayCall += () => { if (this != null && worldBox != null) PlaceBox(); };
#endif
        }

        public void Tick(float dt)
        {
            if (compute == null || sdf == null || hand == null || material == null) return;
            Schedule();
            if (!wanted) { if (state != null) Suspend(); return; }
            if (state == null || allocated != resolution || !state.Alive) Allocate();
            if (state == null) return;
            if (!anchorValid) Place();
            Vector3 delta = hasLast ? hand.position - lastHandPos : Vector3.zero;
            lastHandPos = hand.position; hasLast = true;
            float speed = dt > 0f ? delta.magnitude / dt : 0f;
            // below the reference speed emission scales down (still = emissionAtRest); above it emission scales UP with
            // speed, so a fast hand deposits as much ink per unit of path as a slow one (it spends less time per cell)
            float rel = emissionSpeedRef > 0f ? speed / emissionSpeedRef : 1f;
            speedFactor = emissionSpeedRef <= 0f ? 1f : rel < 1f ? Mathf.Lerp(emissionAtRest, 1f, rel) : Mathf.Min(rel, 40f);
            Vector3Int shift = Recenter();
            float cell = Mathf.Max(cellSize, 0.01f);
            // one step per frame, two for a long frame; the path split is handled inside the Source kernel (the sweep)
            int steps = dt > Mathf.Max(maxStep, 1e-4f) ? 2 : 1;
            float sub = dt / steps;
            Vector3 deltaStep = delta / steps;
            float cellsPerStep = deltaStep.magnitude / cell;
            int sweep = Mathf.Clamp(Mathf.CeilToInt(cellsPerStep / Mathf.Max(maxCellsPerStep, 0.25f)), 1, s_sweep.Length);
            Vector3 start = hand.position - delta;
            for (int i = 0; i < steps; i++)
            {
                Vector3 stepStart = start + deltaStep * i;
                for (int k = 0; k < sweep; k++)
                {
                    // the hand is placed along its path, so a fast hand paints a continuous trail
                    Vector3 posSub = stepStart + deltaStep * ((k + 1f) / sweep);
                    s_sweep[k] = Matrix4x4.TRS(posSub, hand.rotation, hand.lossyScale).inverse;
                }
                Step(sub, deltaStep, i == 0 ? shift : Vector3Int.zero, Matrix4x4.TRS(stepStart + deltaStep, hand.rotation, hand.lossyScale), sweep);
            }
            Bind();
        }

        // First placement: box centre = hand + offset (offset given in the hand's axes, frozen in world axes from then on).
        void Place()
        {
            offsetWorld = hand.rotation * centreOffset;
            anchorCentre = hand.position + offsetWorld;
            anchorValid = true;
            emitOffset = Vector3.zero;
            if (transform.parent == hand)
            {
                if (transform.localPosition != Vector3.zero) transform.localPosition = Vector3.zero;
                if (transform.localRotation != Quaternion.identity) transform.localRotation = Quaternion.identity;
                if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;
            }
            PlaceBox();
        }

        // Whole-cell re-centre once the hand has drifted too far from its placed spot. Returns the cells to shift the
        // field by (field moves opposite to the anchor, read exactly on texel centres by the Source kernel).
        Vector3Int Recenter()
        {
            float cell = Mathf.Max(cellSize, 0.01f);
            Vector3 err = hand.position + offsetWorld - anchorCentre;
            float limit = Mathf.Max(recenterCells, 1f) * cell;
            if (Mathf.Abs(err.x) < limit && Mathf.Abs(err.y) < limit && Mathf.Abs(err.z) < limit) return Vector3Int.zero;
            var cells = new Vector3Int(Mathf.RoundToInt(err.x / cell), Mathf.RoundToInt(err.y / cell), Mathf.RoundToInt(err.z / cell));
            anchorCentre += new Vector3(cells.x, cells.y, cells.z) * cell;
            emitOffset += new Vector3(cells.x, cells.y, cells.z);
            if (emitOffset.sqrMagnitude > 1e8f) emitOffset = Vector3.zero;
            PlaceBox();
            return cells;
        }

        void PlaceBox()
        {
#if UNITY_EDITOR
            // Prefab Mode / preview scenes must not get a second root (LoadPrefabContents refuses a prefab with two
            // roots), so the hidden box is only made for real scenes; the prefab still renders once instantiated.
            if (!Application.isPlaying && UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(gameObject)) return;
#endif
            if (worldBox == null)
            {
                worldBox = new GameObject(name + " (world box)") { hideFlags = HideFlags.HideAndDontSave };
                if (gameObject.scene.IsValid() && worldBox.scene != gameObject.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(worldBox, gameObject.scene);
                var mf = worldBox.AddComponent<MeshFilter>();
                mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                boxRenderer = worldBox.AddComponent<MeshRenderer>();
                boxRenderer.shadowCastingMode = ShadowCastingMode.Off; boxRenderer.receiveShadows = false; boxRenderer.lightProbeUsage = LightProbeUsage.Off; boxRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
#if UNITY_EDITOR
                SceneVisibilityManager.instance.DisablePicking(worldBox, true);
#endif
            }
            if (worldBox.layer != gameObject.layer) worldBox.layer = gameObject.layer;   // follow SkillVfxPhaseController's layer moves
            var t = worldBox.transform;
            t.SetPositionAndRotation(anchorCentre, Quaternion.identity);
            t.localScale = BoxSize;
        }

        // The tuning constants are the same for every instance (one prefab), so they are only re-sent when they differ
        // from what the compute shader was last given; the per-instance values (hand pose, shift, timeline) always are.
        struct SharedParams
        {
            public Vector3Int size; public float cell, dragInject, injectTarget, sourceInside, sparsity, sparsityScale, swirl, injectVelocity, buoyancy, vorticity, drift, diffusion, sdfRange, boundsMin;
            public Vector3 volumeOrigin, volumeSize;
            public Texture3D sdf;
            public bool Same(in SharedParams o) =>
                size == o.size && cell == o.cell && dragInject == o.dragInject && injectTarget == o.injectTarget && sourceInside == o.sourceInside
                && sparsity == o.sparsity && sparsityScale == o.sparsityScale && swirl == o.swirl && injectVelocity == o.injectVelocity && buoyancy == o.buoyancy
                && vorticity == o.vorticity && drift == o.drift && diffusion == o.diffusion && sdfRange == o.sdfRange && boundsMin == o.boundsMin
                && volumeOrigin == o.volumeOrigin && volumeSize == o.volumeSize && sdf == o.sdf;
        }
        static SharedParams s_lastShared;
        static bool s_sharedValid;

        void Step(float dt, Vector3 handDelta, Vector3Int shift, Matrix4x4 handToWorld, int sweep)
        {
            float cell = Mathf.Max(cellSize, 0.01f);
            Vector3 handVel = dt > 0f ? handDelta / dt / cell : Vector3.zero;
            var sc = Scratch(allocated);
            bool rebind = !Application.isPlaying || s_scratchBoundFrame != Time.frameCount;

            var shared = new SharedParams
            {
                size = allocated, cell = cell, dragInject = dragInject, injectTarget = targetDensity, sourceInside = sourceInside, sparsity = sparsity,
                sparsityScale = sparsityScale, swirl = swirl, injectVelocity = injectVelocity, buoyancy = buoyancy, vorticity = vorticity, drift = drift,
                diffusion = diffusion, sdfRange = sdfRange, boundsMin = threshold, volumeOrigin = volumeOrigin, volumeSize = volumeSize, sdf = sdf
            };
            if (rebind || !s_sharedValid || !s_lastShared.Same(shared))
            {
                s_lastShared = shared; s_sharedValid = true;
                compute.SetInts("_Size", allocated.x, allocated.y, allocated.z);
                compute.SetInts("_SizeC", sc.sizeC.x, sc.sizeC.y, sc.sizeC.z);
                compute.SetFloat("_Cell", cell);
                compute.SetFloat("_DragInject", dragInject);
                compute.SetFloat("_InjectTarget", targetDensity);
                compute.SetFloat("_SourceInside", sourceInside);
                compute.SetFloat("_Sparsity", sparsity);
                compute.SetFloat("_SparsityScale", sparsityScale);
                compute.SetFloat("_Swirl", swirl);
                compute.SetFloat("_InjectVelocity", injectVelocity);
                compute.SetFloat("_Buoyancy", buoyancy);
                compute.SetFloat("_Vorticity", vorticity);
                compute.SetFloat("_Drift", drift);
                compute.SetFloat("_Diffusion", diffusion);
                compute.SetVector("_VolumeOrigin", volumeOrigin);
                compute.SetVector("_VolumeSize", volumeSize);
                compute.SetFloat("_SdfRange", sdfRange);
                compute.SetFloat("_BoundsMin", threshold);
                compute.SetTexture(kSource, "_Sdf", sdf);
                compute.SetTexture(kForces, "_Sdf", sdf);
            }
            if (rebind)
            {
                // scratch-only bindings: identical for every instance and every step
                s_scratchBoundFrame = Time.frameCount;
                compute.SetTexture(kSource, "_VelocityOut", sc.vel); compute.SetTexture(kSource, "_DensityOut", sc.den);
                compute.SetTexture(kAdvectV, "_VelocityIn", sc.vel);
                compute.SetTexture(kCurl, "_CurlOut", sc.curl);
                compute.SetTexture(kForces, "_VelocityOut", sc.vel); compute.SetTexture(kForces, "_CurlIn", sc.curl); compute.SetTexture(kForces, "_DensityIn", sc.den);
                compute.SetTexture(kDiv, "_VelocityIn", sc.vel); compute.SetTexture(kDiv, "_DivergenceOut", sc.div); compute.SetTexture(kDiv, "_PressureOut", sc.prs0);
                compute.SetTexture(kJacobiA, "_PressureIn", sc.prs0); compute.SetTexture(kJacobiA, "_PressureOut", sc.prs1); compute.SetTexture(kJacobiA, "_DivergenceIn", sc.div);
                compute.SetTexture(kJacobiB, "_PressureIn2", sc.prs1); compute.SetTexture(kJacobiB, "_PressureOut2", sc.prs0); compute.SetTexture(kJacobiB, "_DivergenceIn", sc.div);
                compute.SetTexture(kGrad, "_VelocityIn", sc.vel);
                compute.SetTexture(kAdvectD, "_DensityIn", sc.den);
                compute.SetTexture(kRestrict, "_DivergenceIn", sc.div); compute.SetTexture(kRestrict, "_DivergenceCOut", sc.divC); compute.SetTexture(kRestrict, "_PressureCOut", sc.prsC0);
                compute.SetTexture(kJacobiCA, "_PressureCIn", sc.prsC0); compute.SetTexture(kJacobiCA, "_PressureCOut", sc.prsC1); compute.SetTexture(kJacobiCA, "_DivergenceC", sc.divC);
                compute.SetTexture(kJacobiCB, "_PressureCIn2", sc.prsC1); compute.SetTexture(kJacobiCB, "_PressureCOut2", sc.prsC0); compute.SetTexture(kJacobiCB, "_DivergenceC", sc.divC);
                compute.SetTexture(kProlong, "_PressureOut", sc.prs0);
            }

            // per instance / per step
            compute.SetFloat("_Dt", dt);
            compute.SetFloat("_Time", Application.isPlaying ? Time.time : (float)(lastEditorTime % 1000.0));
            compute.SetInts("_Shift", shift.x, shift.y, shift.z);
            compute.SetVector("_GridOrigin", BoxMin);
            compute.SetMatrix("_WorldToHand", handToWorld.inverse);
            compute.SetMatrix("_HandToWorld", handToWorld);
            compute.SetMatrixArray("_WorldToHandSweep", s_sweep);
            compute.SetInt("_SweepCount", sweep);
            compute.SetVector("_HandVelocity", handVel);
            compute.SetFloat("_InjectRate", injectRate * emissionScale * speedFactor);
            compute.SetFloat("_SurfaceBand", surfaceBand * Mathf.Max(bandScale, 0.1f));
            compute.SetVector("_EmitOffset", emitOffset);
            compute.SetFloat("_Pull", pull);
            compute.SetFloat("_VelDissipation", Mathf.Pow(velocityDissipation, dt * 30f));
            compute.SetFloat("_DensityDissipation", Mathf.Pow(densityDissipation, dt * 30f) * Mathf.Pow(1f - fadeOut, dt * 4f));
            compute.SetFloat("_GatherSpeed", gatherSpeed);

            int gx = Mathf.CeilToInt(allocated.x / 8f), gy = Mathf.CeilToInt(allocated.y / 8f), gz = Mathf.CeilToInt(allocated.z / 4f);

            // state -> scratch -> state: velocity passes Source, AdvectVelocity, Forces, Gradient (even), density Source,
            // AdvectDensity (even), so the step ends in the instance's own textures
            compute.SetTexture(kSource, "_VelocityIn", state.vel); compute.SetTexture(kSource, "_DensityIn", state.den);
            compute.Dispatch(kSource, gx, gy, gz);

            compute.SetTexture(kAdvectV, "_VelocityOut", state.vel);
            compute.Dispatch(kAdvectV, gx, gy, gz);

            compute.SetTexture(kCurl, "_VelocityIn", state.vel);
            compute.Dispatch(kCurl, gx, gy, gz);
            compute.SetTexture(kForces, "_VelocityIn", state.vel);
            compute.Dispatch(kForces, gx, gy, gz);

            compute.Dispatch(kDiv, gx, gy, gz);
            int fineIters = pressureIterations;
            if (coarsePressure)
            {
                int cx = Mathf.CeilToInt(sc.sizeC.x / 8f), cy = Mathf.CeilToInt(sc.sizeC.y / 8f), cz = Mathf.CeilToInt(sc.sizeC.z / 4f);
                compute.Dispatch(kRestrict, cx, cy, cz);
                for (int i = 0; i < pressureIterations; i++) compute.Dispatch((i & 1) == 0 ? kJacobiCA : kJacobiCB, cx, cy, cz);
                compute.SetTexture(kProlong, "_PressureCIn", (pressureIterations & 1) == 1 ? sc.prsC1 : sc.prsC0);
                compute.Dispatch(kProlong, gx, gy, gz);      // -> prs0, the fine passes continue from it
                fineIters = pressureFineIterations;
            }
            for (int i = 0; i < fineIters; i++) compute.Dispatch((i & 1) == 0 ? kJacobiA : kJacobiB, gx, gy, gz);
            compute.SetTexture(kGrad, "_PressureIn", (fineIters & 1) == 1 ? sc.prs1 : sc.prs0);
            compute.SetTexture(kGrad, "_VelocityOut", state.vel);
            compute.Dispatch(kGrad, gx, gy, gz);

            compute.SetBuffer(kResetBounds, "_Bounds", state.bounds);
            compute.Dispatch(kResetBounds, 1, 1, 1);
            compute.SetTexture(kAdvectD, "_VelocityIn", state.vel); compute.SetTexture(kAdvectD, "_DensityOut", state.den);
            compute.SetBuffer(kAdvectD, "_Bounds", state.bounds);
            compute.Dispatch(kAdvectD, gx, gy, gz);
        }

        void Bind()
        {
            if (worldBox == null) PlaceBox();
            if (worldBox == null || boxRenderer == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            boxRenderer.GetPropertyBlock(block);
            block.SetTexture("_DensityTex", state.den);
            block.SetBuffer("_Bounds", state.bounds);
            block.SetVector("_BoxMin", BoxMin);
            block.SetVector("_BoxSize", BoxSize);
            block.SetColor("_InkColor", inkColor);
            block.SetColor("_WashColor", washColor);
            block.SetFloat("_Threshold", threshold);
            block.SetFloat("_Contrast", contrast);
            block.SetFloat("_Softness", softness);
            block.SetVector("_GridSize", new Vector3(allocated.x, allocated.y, allocated.z));
            block.SetFloat("_Extinction", extinction);
            boxRenderer.SetPropertyBlock(block);
            if (boxRenderer.sharedMaterial != material) boxRenderer.sharedMaterial = material;
            if (!boxRenderer.enabled) boxRenderer.enabled = true;
        }

        // Mass and centroid of the density field (cell units), for self-checks: (mass, x, y, z).
        public Vector4 ReadStats()
        {
            if (compute == null || state == null) return Vector4.zero;
            if (stats == null) stats = new ComputeBuffer(5, sizeof(uint));
            stats.SetData(new uint[5]);
            compute.SetInts("_Size", allocated.x, allocated.y, allocated.z);
            compute.SetTexture(kStats, "_DensityIn", state.den);
            compute.SetBuffer(kStats, "_Stats", stats);
            compute.Dispatch(kStats, Mathf.CeilToInt(allocated.x / 8f), Mathf.CeilToInt(allocated.y / 8f), Mathf.CeilToInt(allocated.z / 4f));
            var r = new uint[5];
            stats.GetData(r);
            float m = r[0] / 256f;
            return m > 0f && r[4] > 0 ? new Vector4(m, r[1] / (float)r[4], r[2] / (float)r[4], r[3] / (float)r[4]) : Vector4.zero;
        }

        // Occupied AABB of the ink in cells (min, max), as the renderer sees it; max < min when empty. Readback: self-checks only.
        public bool ReadBounds(out Vector3Int min, out Vector3Int max)
        {
            min = Vector3Int.zero; max = Vector3Int.zero;
            if (state == null) return false;
            var r = new uint[6];
            state.bounds.GetData(r);
            if (r[0] == uint.MaxValue) return false;
            min = new Vector3Int((int)r[0], (int)r[1], (int)r[2]); max = new Vector3Int((int)r[3], (int)r[4], (int)r[5]);
            return true;
        }

        public Vector3 CellToWorld(Vector3 cell) => BoxMin + (cell + Vector3.one * 0.5f) * Mathf.Max(cellSize, 0.01f);

        // Gather start: scatter faint ink clouds in a shell [inner, outer] (hand-space SDF distance) around the hand.
        public void SeedGather(float inner, float outer, float density)
        {
            if (compute == null || hand == null || sdf == null) return;
            if (state == null || allocated != resolution || !state.Alive) Allocate();
            if (state == null) return;
            if (!anchorValid) Place();
            float cell = Mathf.Max(cellSize, 0.01f);
            compute.SetInts("_Size", allocated.x, allocated.y, allocated.z);
            compute.SetVector("_GridOrigin", BoxMin);
            compute.SetFloat("_Cell", cell);
            compute.SetMatrix("_WorldToHand", hand.worldToLocalMatrix);
            compute.SetVector("_EmitOffset", emitOffset);
            compute.SetVector("_VolumeOrigin", volumeOrigin);
            compute.SetVector("_VolumeSize", volumeSize);
            compute.SetFloat("_SdfRange", sdfRange);
            compute.SetFloat("_SeedInner", inner);
            compute.SetFloat("_SeedOuter", outer);
            compute.SetFloat("_SeedDensity", density);
            compute.SetTexture(kSeed, "_Sdf", sdf);
            compute.SetTexture(kSeed, "_DensityOut", state.den);
            compute.SetTexture(kSeed, "_VelocityOut", state.vel);
            compute.Dispatch(kSeed, Mathf.CeilToInt(allocated.x / 8f), Mathf.CeilToInt(allocated.y / 8f), Mathf.CeilToInt(allocated.z / 4f));
            s_sharedValid = false;      // _Size etc. were set piecemeal here
            Bind();
        }

        public void ResetFluid()
        {
            if (compute == null || state == null) return;
            int gx = Mathf.CeilToInt(allocated.x / 8f), gy = Mathf.CeilToInt(allocated.y / 8f), gz = Mathf.CeilToInt(allocated.z / 4f);
            compute.SetInts("_Size", allocated.x, allocated.y, allocated.z);
            compute.SetTexture(kClear, "_VelocityOut", state.vel); compute.SetTexture(kClear, "_DensityOut", state.den);
            compute.Dispatch(kClear, gx, gy, gz);
            state.bounds.SetData(s_zero6);
            compute.SetBuffer(kResetBounds, "_Bounds", state.bounds);
            compute.Dispatch(kResetBounds, 1, 1, 1);
            s_sharedValid = false;
            hasLast = false;
        }

        // Put the box back on the hand (clears the ink).
        public void Recentre()
        {
            anchorValid = false;
            ResetFluid();
            if (hand != null) Place();
        }

        void FindKernels()
        {
            if (kernelsFound || compute == null) return;
            kSource = compute.FindKernel("Source");
            kAdvectV = compute.FindKernel("AdvectVelocity");
            kCurl = compute.FindKernel("Curl");
            kForces = compute.FindKernel("Forces");
            kDiv = compute.FindKernel("Divergence");
            kJacobiA = compute.FindKernel("JacobiA");
            kJacobiB = compute.FindKernel("JacobiB");
            kGrad = compute.FindKernel("Gradient");
            kAdvectD = compute.FindKernel("AdvectDensity");
            kClear = compute.FindKernel("Clear");
            kStats = compute.FindKernel("Stats");
            kSeed = compute.FindKernel("Seed");
            kResetBounds = compute.FindKernel("ResetBounds");
            kRestrict = compute.FindKernel("Restrict");
            kJacobiCA = compute.FindKernel("JacobiCA");
            kJacobiCB = compute.FindKernel("JacobiCB");
            kProlong = compute.FindKernel("Prolong");
            kernelsFound = true;
        }

        // Take a state set (from the pool when one is free) and start from empty.
        // No GPU (batch mode with -nographics, a headless server): the compute kernels do not exist and FindKernel would
        // log errors, so the fluid stays off and callers see no state. The volume body still draws where there is a GPU.
        static bool ComputeUsable => !ForceNoCompute && SystemInfo.supportsComputeShaders && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
        // EditMode tests: behave as on a machine without compute shaders.
        internal static bool ForceNoCompute;

        void Allocate()
        {
            Release();
            if (compute == null || !ComputeUsable) return;
            FindKernels();
            allocated = resolution;
            state = TakeState(allocated);
            ResetFluid();
            anchorValid = false;
            if (hand != null) Place();
        }

        // Out of the simulation budget: give the grids back and stop drawing; the next Tick in budget starts from empty on the hand.
        void Suspend()
        {
            ReturnState(state); state = null;
            if (boxRenderer != null) boxRenderer.enabled = false;
            hasLast = false;
        }

        void Release()
        {
            ReturnState(state); state = null;
            if (stats != null) { stats.Release(); stats = null; }
        }
    }
}
