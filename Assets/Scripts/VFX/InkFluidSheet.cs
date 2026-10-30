using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BuddahGo.VFX
{
    // Ink-in-water smoke for the 如来神掌 hand (Docs/vfx/palm-mesh-tripo.md sections 14-17): a 2D Stable Fluids solver
    // (InkFluid.compute) runs on a sheet through the hand; every step the hand's mesh surface (sampled from its SDF where
    // it crosses the sheet) injects ink and velocity, vorticity confinement keeps the filaments, random drift wanders it.
    // WorldFixed (default, section 17): the grid is anchored in the world and the hand sweeps across it, so the ink stays
    // where it was left with no per-frame resampling (no jitter, no blur); when the hand nears the border the grid is
    // re-centred by a whole number of cells (an exact texel copy). The quad is drawn by a hidden, unparented runtime
    // object so the parent's motion never touches it. HandLocal / FaceCamera are the older modes (the sheet rides on the
    // hand and the field is shifted the other way every tick). Runs in edit mode too.
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class InkFluidSheet : MonoBehaviour
    {
        [Header("Simulation")]
        public ComputeShader compute;
        [Tooltip("World units covered by the sheet (x along its right axis, y up); the trail lives inside it.")] public Vector2 sheetSize = new Vector2(280f, 280f);
        [Tooltip("Sheet centre offset from the hand, in sheet units (x right, y up): shift the side sheet backwards so the trail fits.")] public Vector2 centreOffset = Vector2.zero;
        [Tooltip("World units per simulation cell; the grid is sheet size divided by this.")] public float cellSize = 1.1f;
        [Tooltip("Half the march length through the hand along the sheet normal (hand units).")] public float halfDepth = 40f;
        [Tooltip("SDF value counted as inside the hand (hand units).")] public float sourceInside = -0.5f;
        [Range(0, 40)] public int pressureIterations = 24;
        [Tooltip("How fast cells under the hand reach the target density (per second).")] public float injectRate = 15f;
        [Tooltip("Density the source drives cells toward: the sparseness knob (0.3 thin, 1 solid).")] [Range(0.05f, 1f)] public float targetDensity = 0.45f;
        public enum Source { MeshSurface, SilhouetteRim }
        [Tooltip("MeshSurface: ink leaves where the hand's mesh surface crosses this sheet's plane. SilhouetteRim: the projected silhouette through the hand's depth (edgeOnly blends its rim / fill).")] public Source source = Source.MeshSurface;
        [Tooltip("MeshSurface: thickness of the emission band around the surface (hand units).")] public float surfaceBand = 4f;
        [Tooltip("SilhouetteRim: 0 = ink fills the whole silhouette, 1 = only its rim. Also gates the sparse patches in both modes.")] [Range(0f, 1f)] public float edgeOnly = 1f;
        [Tooltip("Fraction of rim patches emitting at any moment.")] [Range(0.05f, 1f)] public float sparsity = 0.35f;
        [Tooltip("Size of the random emission patches (cells).")] public float sparsityScale = 6f;
        [Tooltip("Upward velocity of fresh ink (cells / s). Keep 0: the smoke has no rise, only random drift.")] public float injectVelocity = 0f;
        [Tooltip("Random sideways velocity at the source (cells / s).")] public float swirl = 30f;
        [Tooltip("How much of the hand's own velocity fresh ink inherits.")] public float dragInject = 0.6f;
        [Tooltip("Upward acceleration per unit density (cells / s^2). Keep 0: no rise.")] public float buoyancy = 0f;
        [Tooltip("Vorticity confinement: higher = finer, longer-lived filaments.")] public float vorticity = 8f;
        [Tooltip("Random ambient momentum per unit density (cells / s^2): smoke wanders instead of rising.")] public float drift = 20f;
        [Tooltip("Blend toward the neighbour mean per step: smoke spreads and thins instead of keeping its stamp.")] [Range(0f, 0.6f)] public float diffusion = 0.2f;
        [Tooltip("Driven by PalmInkTimeline: > 0 pulls smoke toward the surface (gather), < 0 pushes it away (scatter).")] public float pull = 0f;
        [Tooltip("Driven by PalmInkTimeline: widens the emission band while gathering.")] public float bandScale = 1f;
        [Tooltip("Driven by PalmInkTimeline: scales the injection (0 = no new smoke).")] [Range(0f, 1f)] public float emissionScale = 1f;
        [Tooltip("Driven by PalmInkTimeline: extra fade per second applied to the density (0 = none).")] [Range(0f, 1f)] public float fadeOut = 0f;
        [Range(0.9f, 1f)] public float velocityDissipation = 0.995f;
        [Range(0.9f, 1f)] public float densityDissipation = 0.992f;
        [Tooltip("Largest simulation step; larger frame times are split.")] public float maxStep = 1f / 30f;
        public bool simulateInEditMode = true;

        [Header("Hand volume (same values as Mat_FX_Palm_InkVolume)")]
        [Tooltip("The hand's object space: the prefab root.")] public Transform hand;
        public Texture3D sdf;
        public Vector3 volumeOrigin = new Vector3(-41.94f, -57f, -29.23f);
        public Vector3 volumeSize = new Vector3(83.88f, 114f, 58.46f);
        public float sdfRange = 36f;

        public enum Orientation { HandLocal, FaceCamera, WorldFixed }

        [Header("Orientation")]
        [Tooltip("WorldFixed: the grid is anchored in the world (facing taken from Local Normal in hand space when it is placed) and the hand sweeps across it; re-centred by whole cells when the hand nears the border. HandLocal: the sheet rides on the hand and the field is shifted back every tick (jitters while dragging). FaceCamera: billboard.")]
        public Orientation orientation = Orientation.WorldFixed;
        [Tooltip("Sheet normal in hand space (+Z = across the palm, +X = the side sheet along the flight direction). WorldFixed samples it once when the sheet is placed.")]
        public Vector3 localNormal = Vector3.forward;
        [Tooltip("WorldFixed: re-centre the grid (by whole cells) once the hand has drifted this fraction of the sheet from where it was placed.")] [Range(0.05f, 0.45f)] public float recenterFraction = 0.25f;

        [Header("Rendering")]
        public Material material;
        [Tooltip("Face this camera instead of the main / scene view camera (tools and tests).")] public Camera overrideCamera;

        RenderTexture velA, velB, denA, denB, prsA, prsB, div, curl;
        MaterialPropertyBlock block;
        Mesh sheetMesh;
        Matrix4x4 sheetToHandLocal = Matrix4x4.identity;   // HandLocal: sheet space (-0.5..0.5) -> hand space
        Vector3 lastHandPos;
        Vector2 emitOffset;
        Vector3 anchorPos;                 // WorldFixed: sheet centre in the world
        Quaternion anchorRot = Quaternion.identity;
        bool anchorValid;
        GameObject worldQuad;              // WorldFixed: hidden unparented renderer
        bool hasLast;
        double lastEditorTime;
        int kSource, kAdvectV, kCurl, kForces, kDiv, kJacobi, kGrad, kAdvectD, kClear;

        public RenderTexture DensityTexture => denA;
        Vector2Int Resolution => new Vector2Int(Mathf.Max(8, Mathf.CeilToInt(sheetSize.x / Mathf.Max(cellSize, 0.01f) / 8f) * 8), Mathf.Max(8, Mathf.CeilToInt(sheetSize.y / Mathf.Max(cellSize, 0.01f) / 8f) * 8));

        void OnEnable()
        {
            if (hand == null && transform.parent != null) hand = transform.parent;
            Allocate();
            hasLast = false;
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
            Release();
            if (worldQuad != null) { DestroyImmediate(worldQuad); worldQuad = null; }
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
            // edit mode runs at 30 Hz at most, so the editor stays responsive while dragging
            if (dt < 1f / 30f) return;
            dt = Mathf.Min(dt, 0.1f);
            lastEditorTime = now;
            Tick(dt);
            // the Game view does not repaint by itself in edit mode; ask every view to
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
#endif

        void Tick(float dt)
        {
            if (compute == null || sdf == null || hand == null || material == null) return;
            Vector2Int res = Resolution;
            if (velA == null || velA.width != res.x || velA.height != res.y) Allocate();
            FaceCamera();
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / maxStep), 1, 4);
            float sub = dt / steps;
            Vector3 delta = hasLast ? hand.position - lastHandPos : Vector3.zero;
            lastHandPos = hand.position;
            hasLast = true;
            Vector2 recenter = orientation == Orientation.WorldFixed ? Recenter() : Vector2.zero;
            for (int i = 0; i < steps; i++) Step(sub, delta / steps, i == 0 ? recenter : Vector2.zero);
            Bind();
        }

        // WorldFixed: once the hand has drifted recenterFraction of the sheet from its placed spot, move the anchor by a
        // whole number of cells so the hand is back where it started relative to the grid. Returns the cells to shift the
        // field by (integer, so the advection samples texel centres exactly: nothing is blurred).
        Vector2 Recenter()
        {
            float cell = Mathf.Max(cellSize, 0.01f);
            Vector3 o = Quaternion.Inverse(anchorRot) * (hand.position - anchorPos);
            Vector2 err = new Vector2(o.x, o.y) + centreOffset;        // hand placed at -centreOffset from the centre
            if (Mathf.Abs(err.x) < recenterFraction * sheetSize.x && Mathf.Abs(err.y) < recenterFraction * sheetSize.y) return Vector2.zero;
            Vector2 cells = new Vector2(Mathf.Round(err.x / cell), Mathf.Round(err.y / cell));
            anchorPos += anchorRot * new Vector3(cells.x * cell, cells.y * cell, 0f);
            emitOffset += cells;
            if (emitOffset.sqrMagnitude > 1e8f) emitOffset = Vector2.zero;
            PlaceWorldQuad();
            return cells;
        }

        // WorldFixed placement: anchor the sheet at the hand (facing from localNormal in hand space) the first time, then
        // leave it alone; the hidden quad renders at the anchor and this child draws nothing itself.
        public void PlaceWorld()
        {
            if (!anchorValid)
            {
                Vector3 n = localNormal.sqrMagnitude > 1e-6f ? localNormal.normalized : Vector3.forward;
                Vector3 up = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
                anchorRot = hand.rotation * Quaternion.LookRotation(n, up);
                anchorPos = hand.position + anchorRot * new Vector3(centreOffset.x, centreOffset.y, 0f);
                anchorValid = true;
            }
            if (transform.parent == hand)
            {
                if (transform.localPosition != Vector3.zero) transform.localPosition = Vector3.zero;
                if (transform.localRotation != Quaternion.identity) transform.localRotation = Quaternion.identity;
                if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;
            }
            var mf = GetComponent<MeshFilter>();
            if (mf.sharedMesh != null) mf.sharedMesh = null;
            PlaceWorldQuad();
        }

        void PlaceWorldQuad()
        {
            if (worldQuad == null)
            {
                worldQuad = new GameObject(name + " (world quad)") { hideFlags = HideFlags.HideAndDontSave };
                if (gameObject.scene.IsValid() && worldQuad.scene != gameObject.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(worldQuad, gameObject.scene);
                worldQuad.AddComponent<MeshFilter>();
                var r = worldQuad.AddComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
#if UNITY_EDITOR
                SceneVisibilityManager.instance.DisablePicking(worldQuad, true);
#endif
            }
            if (sheetMesh == null || sheetMesh.vertexCount == 0) BuildMesh(Matrix4x4.identity);
            var wf = worldQuad.GetComponent<MeshFilter>();
            if (wf.sharedMesh != sheetMesh) wf.sharedMesh = sheetMesh;
            var t = worldQuad.transform;
            t.SetPositionAndRotation(anchorPos, anchorRot);
            t.localScale = new Vector3(sheetSize.x, sheetSize.y, 1f);
        }

        Matrix4x4 SheetToWorldFixed => Matrix4x4.TRS(anchorPos, anchorRot, new Vector3(sheetSize.x, sheetSize.y, 1f));

        // Hand-local placement is pure local transform data (the sheet is a child of the hand), so dragging the hand
        // moves it instantly and nothing writes world positions every frame.
        public void PlaceLocal()
        {
            // The sheet object keeps an identity local transform; its placement (offset, facing, size) is baked into
            // a generated quad in hand space, so the hierarchy reads clean and nothing moves the child.
            Vector3 n = localNormal.sqrMagnitude > 1e-6f ? localNormal.normalized : Vector3.forward;
            Vector3 up = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            Quaternion rot = Quaternion.LookRotation(n, up);
            Vector3 localPos = rot * new Vector3(centreOffset.x, centreOffset.y, 0f);
            sheetToHandLocal = Matrix4x4.TRS(localPos, rot, new Vector3(sheetSize.x, sheetSize.y, 1f));
            if (transform.parent == hand)
            {
                if (transform.localPosition != Vector3.zero) transform.localPosition = Vector3.zero;
                if (transform.localRotation != Quaternion.identity) transform.localRotation = Quaternion.identity;
                if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;
            }
            BuildMesh(sheetToHandLocal);
        }

        void BuildMesh(Matrix4x4 toHand)
        {
            if (sheetMesh == null)
            {
                sheetMesh = new Mesh { name = "InkFluidSheet (generated)", hideFlags = HideFlags.HideAndDontSave };
                sheetMesh.MarkDynamic();
            }
            var v = new Vector3[4];
            var nrm = new Vector3[4];
            Vector3 normal = toHand.MultiplyVector(Vector3.forward).normalized;
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -0.5f : 0.5f;
                float y = i < 2 ? -0.5f : 0.5f;
                v[i] = toHand.MultiplyPoint3x4(new Vector3(x, y, 0f));
                nrm[i] = normal;
            }
            sheetMesh.Clear();
            sheetMesh.vertices = v;
            sheetMesh.normals = nrm;
            sheetMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            sheetMesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            sheetMesh.RecalculateBounds();
            var mf = GetComponent<MeshFilter>();
            if (orientation == Orientation.WorldFixed) { if (mf.sharedMesh != null) mf.sharedMesh = null; }
            else if (mf.sharedMesh != sheetMesh) mf.sharedMesh = sheetMesh;
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            // mesh assignment is not allowed inside OnValidate; do it right after
            EditorApplication.delayCall += () =>
            {
                if (this == null || hand == null) return;
                if (orientation == Orientation.HandLocal) PlaceLocal();
                else if (orientation == Orientation.WorldFixed) { if (worldQuad != null) { DestroyImmediate(worldQuad); worldQuad = null; } PlaceWorld(); }
            };
#endif
        }

        void FaceCamera()
        {
            if (orientation == Orientation.HandLocal) { PlaceLocal(); return; }
            if (orientation == Orientation.WorldFixed) { PlaceWorld(); return; }
            Camera cam = overrideCamera != null ? overrideCamera : Camera.main;
#if UNITY_EDITOR
            if (overrideCamera == null && !Application.isPlaying && SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
                cam = SceneView.lastActiveSceneView.camera;
#endif
            if (cam != null) transform.rotation = cam.transform.rotation;
            transform.position = hand.position + transform.right * centreOffset.x + transform.up * centreOffset.y;
            transform.localScale = new Vector3(sheetSize.x, sheetSize.y, 1f);
            BuildMesh(Matrix4x4.identity);
        }

        void Step(float dt, Vector3 handDelta, Vector2 recenterShift)
        {
            float cell = Mathf.Max(cellSize, 0.01f);
            Vector2Int res = Resolution;
            bool local = orientation == Orientation.HandLocal;
            bool fixedGrid = orientation == Orientation.WorldFixed;
            Vector3 right = fixedGrid ? anchorRot * Vector3.right : local ? hand.TransformDirection(sheetToHandLocal.MultiplyVector(Vector3.right)).normalized : transform.right;
            Vector3 upDir = fixedGrid ? anchorRot * Vector3.up : local ? hand.TransformDirection(sheetToHandLocal.MultiplyVector(Vector3.up)).normalized : transform.up;
            Vector2 deltaSheet = new Vector2(Vector3.Dot(handDelta, right), Vector3.Dot(handDelta, upDir)) / cell;
            Vector2 shift;
            if (fixedGrid)
            {
                // the grid does not move with the hand; only a re-centre moves it, by whole cells
                shift = recenterShift;
            }
            else
            {
                // the sheet rides on the hand: move the field the other way so the ink stays in the world
                shift = deltaSheet;
                emitOffset += deltaSheet;
                if (emitOffset.sqrMagnitude > 1e8f) emitOffset = Vector2.zero;
            }
            Vector2 handVel = dt > 0f ? deltaSheet / dt : Vector2.zero;

            Matrix4x4 sheetToHand = fixedGrid ? hand.worldToLocalMatrix * SheetToWorldFixed : local ? sheetToHandLocal : hand.worldToLocalMatrix * transform.localToWorldMatrix;
            Vector3 normalDir = sheetToHand.MultiplyVector(Vector3.forward).normalized;

            compute.SetInts("_Size", res.x, res.y);
            compute.SetFloat("_Dt", dt);
            compute.SetFloat("_Time", Application.isPlaying ? Time.time : (float)(lastEditorTime % 1000.0));
            compute.SetVector("_Shift", shift);
            compute.SetFloat("_VelDissipation", velocityDissipation);
            compute.SetFloat("_DensityDissipation", densityDissipation * Mathf.Pow(1f - fadeOut, dt * 4f));
            compute.SetFloat("_Drift", drift);
            compute.SetFloat("_Pull", pull);
            compute.SetFloat("_Diffusion", diffusion);
            compute.SetFloat("_Buoyancy", buoyancy);
            compute.SetFloat("_Vorticity", vorticity);
            compute.SetFloat("_InjectRate", injectRate * emissionScale);
            compute.SetFloat("_InjectTarget", targetDensity);
            compute.SetFloat("_EdgeOnly", edgeOnly);
            compute.SetFloat("_Sparsity", sparsity);
            compute.SetFloat("_SparsityScale", sparsityScale);
            compute.SetVector("_EmitOffset", emitOffset);
            compute.SetFloat("_SurfaceMode", source == Source.MeshSurface ? 1f : 0f);
            compute.SetFloat("_SurfaceBand", surfaceBand * Mathf.Max(bandScale, 0.1f));
            compute.SetFloat("_InjectVelocity", injectVelocity);
            compute.SetFloat("_Swirl", swirl);
            compute.SetVector("_HandVelocity", handVel);
            compute.SetFloat("_DragInject", dragInject);
            compute.SetMatrix("_SheetToHand", sheetToHand);
            compute.SetVector("_HandNormalDir", normalDir);
            compute.SetFloat("_HalfDepth", halfDepth);
            compute.SetVector("_VolumeOrigin", volumeOrigin);
            compute.SetVector("_VolumeSize", volumeSize);
            compute.SetFloat("_SdfRange", sdfRange);
            compute.SetFloat("_SourceInside", sourceInside);
            compute.SetTexture(kSource, "_Sdf", sdf);

            int g = Mathf.CeilToInt(res.x / 8f);
            int gy = Mathf.CeilToInt(res.y / 8f);

            // source: velA/denA -> velB/denB
            compute.SetTexture(kSource, "_VelocityIn", velA); compute.SetTexture(kSource, "_VelocityOut", velB);
            compute.SetTexture(kSource, "_DensityIn", denA); compute.SetTexture(kSource, "_DensityOut", denB);
            compute.Dispatch(kSource, g, gy, 1);
            Swap(ref velA, ref velB); Swap(ref denA, ref denB);

            // advect velocity
            compute.SetTexture(kAdvectV, "_VelocityIn", velA); compute.SetTexture(kAdvectV, "_VelocityOut", velB);
            compute.Dispatch(kAdvectV, g, gy, 1);
            Swap(ref velA, ref velB);

            // curl + forces
            compute.SetTexture(kCurl, "_VelocityIn", velA); compute.SetTexture(kCurl, "_CurlOut", curl);
            compute.Dispatch(kCurl, g, gy, 1);
            compute.SetTexture(kForces, "_VelocityIn", velA); compute.SetTexture(kForces, "_VelocityOut", velB);
            compute.SetTexture(kForces, "_CurlIn", curl); compute.SetTexture(kForces, "_DensityIn", denA);
            compute.Dispatch(kForces, g, gy, 1);
            Swap(ref velA, ref velB);

            // projection
            compute.SetTexture(kDiv, "_VelocityIn", velA); compute.SetTexture(kDiv, "_DivergenceOut", div); compute.SetTexture(kDiv, "_PressureOut", prsA);
            compute.Dispatch(kDiv, g, gy, 1);
            for (int i = 0; i < pressureIterations; i++)
            {
                compute.SetTexture(kJacobi, "_PressureIn", prsA); compute.SetTexture(kJacobi, "_PressureOut", prsB); compute.SetTexture(kJacobi, "_DivergenceIn", div);
                compute.Dispatch(kJacobi, g, gy, 1);
                Swap(ref prsA, ref prsB);
            }
            compute.SetTexture(kGrad, "_VelocityIn", velA); compute.SetTexture(kGrad, "_VelocityOut", velB); compute.SetTexture(kGrad, "_PressureIn", prsA);
            compute.Dispatch(kGrad, g, gy, 1);
            Swap(ref velA, ref velB);

            // advect density
            compute.SetTexture(kAdvectD, "_VelocityIn", velA); compute.SetTexture(kAdvectD, "_DensityIn", denA); compute.SetTexture(kAdvectD, "_DensityOut", denB);
            compute.Dispatch(kAdvectD, g, gy, 1);
            Swap(ref denA, ref denB);
        }

        void Bind()
        {
            var own = GetComponent<MeshRenderer>();
            var mr = orientation == Orientation.WorldFixed && worldQuad != null ? worldQuad.GetComponent<MeshRenderer>() : own;
            if (block == null) block = new MaterialPropertyBlock();
            mr.GetPropertyBlock(block);
            block.SetTexture("_DensityTex", denA);
            mr.SetPropertyBlock(block);
            if (mr.sharedMaterial != material) mr.sharedMaterial = material;
            if (mr != own && mr.enabled != own.enabled) mr.enabled = own.enabled;   // PalmInkLayerSwitch toggles the child
        }

        // WorldFixed: put the sheet back on the hand (the ink in it is cleared too).
        public void Recentre()
        {
            anchorValid = false;
            ResetFluid();
            if (hand != null && orientation == Orientation.WorldFixed) PlaceWorld();
        }

        public void ResetFluid()
        {
            if (compute == null || velA == null) return;
            int g = Mathf.CeilToInt(velA.width / 8f), gy = Mathf.CeilToInt(velA.height / 8f);
            compute.SetTexture(kClear, "_VelocityOut", velA); compute.SetTexture(kClear, "_DensityOut", denA); compute.SetTexture(kClear, "_PressureOut", prsA);
            compute.Dispatch(kClear, g, gy, 1);
            compute.SetTexture(kClear, "_VelocityOut", velB); compute.SetTexture(kClear, "_DensityOut", denB); compute.SetTexture(kClear, "_PressureOut", prsB);
            compute.Dispatch(kClear, g, gy, 1);
            hasLast = false;
        }

        void Allocate()
        {
            Release();
            if (compute == null) return;
            kSource = compute.FindKernel("Source");
            kAdvectV = compute.FindKernel("AdvectVelocity");
            kCurl = compute.FindKernel("Curl");
            kForces = compute.FindKernel("Forces");
            kDiv = compute.FindKernel("Divergence");
            kJacobi = compute.FindKernel("Jacobi");
            kGrad = compute.FindKernel("Gradient");
            kAdvectD = compute.FindKernel("AdvectDensity");
            kClear = compute.FindKernel("Clear");
            velA = Make(RenderTextureFormat.RGFloat, "InkFluid.velA"); velB = Make(RenderTextureFormat.RGFloat, "InkFluid.velB");
            denA = Make(RenderTextureFormat.RFloat, "InkFluid.denA"); denB = Make(RenderTextureFormat.RFloat, "InkFluid.denB");
            prsA = Make(RenderTextureFormat.RFloat, "InkFluid.prsA"); prsB = Make(RenderTextureFormat.RFloat, "InkFluid.prsB");
            div = Make(RenderTextureFormat.RFloat, "InkFluid.div"); curl = Make(RenderTextureFormat.RFloat, "InkFluid.curl");
            ResetFluid();
            if (hand != null && orientation == Orientation.HandLocal) PlaceLocal();
            else if (hand != null && orientation == Orientation.WorldFixed) PlaceWorld();
            else BuildMesh(Matrix4x4.identity);
        }

        RenderTexture Make(RenderTextureFormat format, string name)
        {
            Vector2Int res = Resolution;
            var rt = new RenderTexture(res.x, res.y, 0, format, RenderTextureReadWrite.Linear)
            {
                name = name, enableRandomWrite = true, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
            };
            rt.Create();
            return rt;
        }

        void Release()
        {
            if (worldQuad != null) { var wf = worldQuad.GetComponent<MeshFilter>(); if (wf != null) wf.sharedMesh = null; }
            if (sheetMesh != null) { DestroyImmediate(sheetMesh); sheetMesh = null; }
            foreach (var rt in new[] { velA, velB, denA, denB, prsA, prsB, div, curl })
                if (rt != null) { rt.Release(); DestroyImmediate(rt); }
            velA = velB = denA = denB = prsA = prsB = div = curl = null;
        }

        static void Swap(ref RenderTexture a, ref RenderTexture b) { var t = a; a = b; b = t; }
    }
}
