using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BuddahGo.VFX
{
    // GPU ink-smoke particles for the 如来神掌 hand (Docs/vfx/palm-mesh-tripo.md section 18). Tens of thousands of
    // particles are born on the hand's whole mesh surface (InkParticles.compute, area-weighted), live in world space
    // under a divergence-free curl-noise flow that gathers them into sheets and filaments, and are drawn by
    // FX/InkParticle as faint soft discs that add up to ink where they pack. The hand's SDF keeps them outside the
    // solid. Rendering is a procedural draw submitted per camera from beginCameraRendering, so there is no renderer
    // component and nothing in the hierarchy moves with the hand. Runs in edit mode too.
    [ExecuteAlways]
    public sealed class InkParticles : MonoBehaviour
    {
        [Header("Assets")]
        public ComputeShader compute;
        public Material material;
        [Tooltip("Emission surface, in the hand's object space (the Tripo hand mesh).")] public Mesh sourceMesh;
        [Tooltip("The hand's object space: the prefab root.")] public Transform hand;
        public Texture3D sdf;
        public Vector3 volumeOrigin = new Vector3(-41.94f, -57f, -29.23f);
        public Vector3 volumeSize = new Vector3(83.88f, 114f, 58.46f);
        public float sdfRange = 36f;

        [Header("Emission")]
        [Tooltip("Ring buffer size. Rate x longest life must fit, or the oldest particles are overwritten early.")] public int capacity = 65536;
        [Tooltip("Particles born per second over the whole surface.")] public float rate = 6000f;
        public float lifeMin = 4f;
        public float lifeMax = 7f;
        [Tooltip("Initial speed away from the surface (units / s).")] public float startSpeed = 6f;
        [Tooltip("0 = straight along the surface normal, 1 = any direction.")] [Range(0f, 1f)] public float randomDirection = 0.5f;
        [Tooltip("Share of the hand's velocity a newborn particle carries (match smoke: young smoke rides along).")] [Range(0f, 1f)] public float inheritVelocity = 0.3f;

        [Header("Motion (world space, nothing rises)")]
        [Tooltip("Acceleration along the curl-noise flow (units / s^2).")] public float noiseStrength = 30f;
        [Tooltip("Flow feature size: 1 / units. 0.02 = features ~50 units across.")] public float noiseFrequency = 0.02f;
        [Tooltip("How fast the flow field evolves in place.")] public float noiseScroll = 0.15f;
        [Range(1, 4)] public int noiseOctaves = 2;
        [Tooltip("Air resistance (1 / s).")] public float damping = 0.6f;
        public float maxSpeed = 25f;
        [Tooltip("Particles closer than this to the hand's surface (hand units) are pushed outward.")] public float pushBand = 6f;
        public float pushStrength = 40f;

        [Header("Colour (overrides the material; tweak here)")]
        [Tooltip("Colour and alpha over each particle's life (0 = birth, 1 = death). Ink tones from Docs/vfx/yixuan-palette.md: warm olive #1C1C18, wash #383428, cyan-black #111A19.")]
        public Gradient colorOverLife = DefaultGradient();
        [Tooltip("Alpha of a single particle; the ink is what many of them add up to.")] [Range(0f, 1f)] public float opacity = 0.05f;
        [Tooltip("Disc size at birth (world units).")] public float size = 1f;
        [Tooltip("Size growth over life (x).")] public float sizeGrow = 1.5f;
        [Tooltip("Disc hardness: higher = smaller, denser core.")] [Range(0.5f, 4f)] public float core = 1.2f;

        [Header("Driven by PalmInkTimeline")]
        [Range(0f, 1f)] public float emissionScale = 1f;
        [Range(0f, 1f)] public float fadeOut = 0f;

        [Header("Editor")]
        public bool simulateInEditMode = true;
        [Tooltip("Largest simulation step; larger frame times are split.")] public float maxStep = 1f / 30f;
        [Tooltip("Half size of the culling box around the hand (world units).")] public float boundsExtent = 600f;

        ComputeBuffer particleBuffer, triBuffer;
        int triCount, allocatedCapacity;
        int emitCursor, frame;
        float emitCarry;
        Vector3 lastHandPos;
        bool hasLast;
        double lastEditorTime;
        MaterialPropertyBlock block;
        Texture2D ramp;
        bool rampDirty = true;
        int kEmit, kUpdate, kClear;

        static Gradient DefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.11f, 0.11f, 0.094f), 0f), new GradientColorKey(new Color(0.22f, 0.204f, 0.157f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        // The gradient is baked into a 128x1 ramp the shader samples by life fraction.
        void BakeRamp()
        {
            const int w = 128;
            if (ramp == null)
            {
                ramp = new Texture2D(w, 1, TextureFormat.RGBA32, false, false) { name = "InkParticles ramp", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            }
            var px = new Color[w];
            for (int i = 0; i < w; i++) px[i] = colorOverLife != null ? colorOverLife.Evaluate(i / (float)(w - 1)) : Color.black;
            ramp.SetPixels(px);
            ramp.Apply(false, false);
            rampDirty = false;
        }

        public int Capacity => allocatedCapacity;
        public ComputeBuffer ParticleBuffer => particleBuffer;

        struct Tri { public Vector3 a, b, c, n; public float cum; public Vector3 pad; }   // 64 bytes, matches the compute
        const int ParticleStride = 48;
        const int TriStride = 64;

        void OnEnable()
        {
            if (hand == null && transform.parent != null) hand = transform.parent;
            Allocate();
            RenderPipelineManager.beginCameraRendering -= Draw;
            RenderPipelineManager.beginCameraRendering += Draw;
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
            if (!Application.isPlaying) { lastEditorTime = EditorApplication.timeSinceStartup; EditorApplication.update += EditorTick; }
#endif
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Draw;
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
            Release();
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
            if (dt < 1f / 30f) return;
            dt = Mathf.Min(dt, 0.1f);
            lastEditorTime = now;
            Tick(dt);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
#endif

        void OnValidate()
        {
            capacity = Mathf.Clamp(capacity, 1024, 1 << 20);
            lifeMax = Mathf.Max(lifeMax, lifeMin);
            rampDirty = true;
        }

        public void Tick(float dt)
        {
            if (compute == null || material == null || hand == null || sourceMesh == null || sdf == null) return;
            if (particleBuffer == null || allocatedCapacity != capacity) Allocate();
            if (triBuffer == null || triCount == 0) return;

            Vector3 handPos = hand.position;
            Vector3 handVel = hasLast && dt > 0f ? (handPos - lastHandPos) / dt : Vector3.zero;
            lastHandPos = handPos; hasLast = true;

            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / maxStep), 1, 4);
            float sub = dt / steps;
            for (int i = 0; i < steps; i++) Step(sub, handVel);
        }

        void Step(float dt, Vector3 handVel)
        {
            frame++;
            float time = Application.isPlaying ? Time.time : (float)(lastEditorTime % 1000.0);
            Matrix4x4 handToWorld = hand.localToWorldMatrix;

            compute.SetInt("_Capacity", allocatedCapacity);
            compute.SetInt("_TriCount", triCount);
            compute.SetInt("_Frame", frame);
            compute.SetFloat("_Dt", dt);
            compute.SetFloat("_Time", time);
            compute.SetMatrix("_HandToWorld", handToWorld);
            compute.SetMatrix("_WorldToHand", hand.worldToLocalMatrix);
            compute.SetVector("_HandVelocity", handVel);
            compute.SetFloat("_InheritVelocity", inheritVelocity);
            compute.SetFloat("_StartSpeed", startSpeed);
            compute.SetFloat("_RandomDirection", randomDirection);
            compute.SetFloat("_LifeMin", lifeMin);
            compute.SetFloat("_LifeMax", lifeMax);
            compute.SetFloat("_NoiseStrength", noiseStrength);
            compute.SetFloat("_NoiseFrequency", noiseFrequency);
            compute.SetFloat("_NoiseScroll", noiseScroll);
            compute.SetInt("_NoiseOctaves", noiseOctaves);
            compute.SetFloat("_Damping", damping);
            compute.SetFloat("_MaxSpeed", maxSpeed);
            compute.SetFloat("_PushBand", pushBand);
            compute.SetFloat("_PushStrength", pushStrength);
            compute.SetVector("_VolumeOrigin", volumeOrigin);
            compute.SetVector("_VolumeSize", volumeSize);
            compute.SetFloat("_SdfRange", sdfRange);

            // emission: rate x dt, fractional remainder carried over; the ring buffer is overwritten oldest-first
            emitCarry += rate * emissionScale * dt;
            int emitCount = Mathf.Min(Mathf.FloorToInt(emitCarry), allocatedCapacity);
            emitCarry -= emitCount;
            if (emitCount > 0)
            {
                compute.SetInt("_EmitStart", emitCursor);
                compute.SetInt("_EmitCount", emitCount);
                compute.SetBuffer(kEmit, "_Particles", particleBuffer);
                compute.SetBuffer(kEmit, "_Tris", triBuffer);
                compute.Dispatch(kEmit, Mathf.CeilToInt(emitCount / 64f), 1, 1);
                emitCursor = (emitCursor + emitCount) % allocatedCapacity;
            }

            compute.SetBuffer(kUpdate, "_Particles", particleBuffer);
            compute.SetTexture(kUpdate, "_Sdf", sdf);
            compute.Dispatch(kUpdate, Mathf.CeilToInt(allocatedCapacity / 64f), 1, 1);
        }

        void Draw(ScriptableRenderContext context, Camera cam)
        {
            if (particleBuffer == null || material == null || hand == null) return;
            if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            if (block == null) block = new MaterialPropertyBlock();
            if (ramp == null || rampDirty) BakeRamp();
            block.SetBuffer("_Particles", particleBuffer);
            block.SetTexture("_ColorRamp", ramp);
            block.SetFloat("_Opacity", opacity);
            block.SetFloat("_Size", size);
            block.SetFloat("_SizeGrow", sizeGrow);
            block.SetFloat("_Core", core);
            block.SetFloat("_FxDissolve", Mathf.Clamp01(fadeOut));
            var bounds = new Bounds(hand.position, Vector3.one * (2f * boundsExtent));
            Graphics.DrawProcedural(material, bounds, MeshTopology.Triangles, allocatedCapacity * 6, 1, cam, block, ShadowCastingMode.Off, false, gameObject.layer);
        }

        public void ResetParticles()
        {
            if (compute == null || particleBuffer == null) return;
            compute.SetInt("_Capacity", allocatedCapacity);
            compute.SetBuffer(kClear, "_Particles", particleBuffer);
            compute.Dispatch(kClear, Mathf.CeilToInt(allocatedCapacity / 64f), 1, 1);
            emitCursor = 0; emitCarry = 0f; hasLast = false;
        }

        void Allocate()
        {
            Release();
            if (compute == null) return;
            kEmit = compute.FindKernel("Emit");
            kUpdate = compute.FindKernel("Update");
            kClear = compute.FindKernel("Clear");
            allocatedCapacity = Mathf.Max(1024, Mathf.CeilToInt(capacity / 64f) * 64);
            particleBuffer = new ComputeBuffer(allocatedCapacity, ParticleStride, ComputeBufferType.Structured);
            BuildTriangles();
            ResetParticles();
        }

        // Area-weighted triangle table in hand space; the cumulative column lets the compute pick a triangle with a
        // single random number so every part of the skin smokes in proportion to its area.
        void BuildTriangles()
        {
            if (triBuffer != null) { triBuffer.Release(); triBuffer = null; }
            triCount = 0;
            if (sourceMesh == null) return;
            Vector3[] v = sourceMesh.vertices;
            int[] idx = sourceMesh.triangles;
            int n = idx.Length / 3;
            if (n == 0) return;
            var tris = new Tri[n];
            double total = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = v[idx[i * 3]], b = v[idx[i * 3 + 1]], c = v[idx[i * 3 + 2]];
                Vector3 cross = Vector3.Cross(b - a, c - a);
                float area = cross.magnitude * 0.5f;
                total += area;
                tris[i] = new Tri { a = a, b = b, c = c, n = cross.sqrMagnitude > 1e-12f ? cross.normalized : Vector3.up, cum = (float)total };
            }
            for (int i = 0; i < n; i++) tris[i].cum = (float)(tris[i].cum / total);
            tris[n - 1].cum = 1f;
            triBuffer = new ComputeBuffer(n, TriStride, ComputeBufferType.Structured);
            triBuffer.SetData(tris);
            triCount = n;
        }

        void Release()
        {
            if (particleBuffer != null) { particleBuffer.Release(); particleBuffer = null; }
            if (triBuffer != null) { triBuffer.Release(); triBuffer = null; }
            if (ramp != null) { DestroyImmediate(ramp); ramp = null; }
            triCount = 0; allocatedCapacity = 0;
        }
    }
}
