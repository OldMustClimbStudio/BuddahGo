using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BuddahGo.VFX
{
    // Gather / hold / dissipate timeline for the ink palm prefab (Docs/vfx/palm-mesh-tripo.md section 15).
    // Over `duration` seconds: the fog gathers out of noise during the first `gatherSeconds` (volume _FxDissolve 1 -> 0,
    // _FxAlpha 0 -> 1, smoke emission 0 -> 1), holds, then dissipates over the last `dissipateSeconds` along
    // `dissipateCurve` (volume _FxDissolve 0 -> 1, smoke emission off and the fluid's density fades). Drives the Volume
    // renderer through a MaterialPropertyBlock, the InkFluidVolume children, and the particle children's emission.
    // Plays on enable at runtime; in the editor, tick Preview to loop it.
    // Section 20: the launch motion belongs to the skill (Docs/vfx/push-projectile-hands.md, "飞行速度"): in the race
    // ChargedHandProjectileRuntime moves this object along Skill_PushProjectileHands' speed shape and calls
    // ConfigureForProjectile, which turns Drive Motion off and takes the gather and length from the skill. Drive Motion
    // is the editor preview of that same flight (ChargedProjectileFlight, read from `flightSource`): from the anchor
    // captured at Play(), still while gathering, then the skill's flight along the forward captured at the same moment.
    // The 3D fluid (InkFluidVolume) is driven like the sheets (emission, fade, pull, band) and smokes only while moving.
    [ExecuteAlways]
    public sealed class PalmInkTimeline : MonoBehaviour
    {
        public float duration = 4f;
        public float gatherSeconds = 1f;
        public float dissipateSeconds = 1.5f;
        public AnimationCurve gatherCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("0 = fully present, 1 = gone; evaluated over the last dissipateSeconds.")]
        public AnimationCurve dissipateCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(0.6f, 0.35f), new Keyframe(1f, 1f, 2.5f, 0f));
        public bool playOnEnable = true;
        [Tooltip("Editor only: loop the timeline in edit mode.")] public bool preview = false;
        [Tooltip("Editor only: when not playing, hold the fully-gathered state so you can drag the hand around by hand (turn Drive Motion off) and watch the smoke.")] public bool holdVisible = false;
        public bool loop = false;

        [Header("Gather / scatter feel")]
        [Tooltip("Fluid: pull toward the surface while gathering, push away while dissipating (cells / s^2).")] public float smokePull = 60f;
        [Tooltip("Fluid: emission band width multiplier at the start of the gather.")] public float gatherBandScale = 5f;
        [Tooltip("Volume: spill (fog far from the surface) at the start of the gather.")] public float gatherSpill = 0.6f;
        [Tooltip("Volume: spill falloff distance at the start of the gather.")] public float gatherSpillFalloff = 14f;
        [Tooltip("Volume: domain warp at the end of the dissipate (the fog tears apart).")] public float dissipateWarp = 40f;
        [Tooltip("Volume: spill at the end of the dissipate.")] public float dissipateSpill = 0.5f;
        [Tooltip("Volume: alpha ceiling (the fog body never gets more opaque than this).")] [Range(0f, 1f)] public float volumeAlphaMax = 0.7f;
        [Tooltip("Volume: alpha reaches its ceiling at this fraction of the gather (small = the body is there early, the gather is then the shape forming, not a fade-in).")] [Range(0.05f, 1f)] public float volumeFadeInFraction = 0.4f;

        [Header("Launch motion (preview of the skill's flight; the race moves the real projectile)")]
        [Tooltip("Preview: move this object along the skill's flight from the anchor captured at Play(). The race turns it off (ConfigureForProjectile).")] public bool driveMotion = true;
        [Tooltip("Skill whose flight the preview plays: still for buildUpSeconds, then projectileSpeed x projectileSpeedCurve over projectileLifetimeSeconds. Empty = the palm skill asset.")] public Skill_PushProjectileHands flightSource;
        [Tooltip("Edit-mode tick rate (Hz).")] public float editorHz = 60f;

        [Header("Gather from the air (3D fluid)")]
        [Tooltip("At Play(), faint ink is scattered in a shell this far from the hand's surface (hand units)...")] public float fluidSeedInner = 12f;
        [Tooltip("...out to this far.")] public float fluidSeedOuter = 60f;
        [Tooltip("Density of the scattered clouds.")] public float fluidSeedDensity = 0.18f;
        [Tooltip("During the gather the scattered ink is carried toward the surface at this speed (cells / s), fading to 0 as the gather completes.")] public float fluidGatherSpeed = 70f;

        [Header("State (read-only)")]
        public float time;
        public bool playing;
        public float speed;
        [Tooltip("The skill's peak speed (preview).")] public float peakSpeed;
        [Tooltip("The skill's range: peak x flight seconds x the shape's area (preview).")] public float range;

        Vector3 anchorPos, anchorForward;
        bool anchorSet;
        Skill_PushProjectileHands flight;
        float[] flightDistance;

        static readonly int FxAlphaId = Shader.PropertyToID("_FxAlpha");
        static readonly int FxDissolveId = Shader.PropertyToID("_FxDissolve");
        static readonly int SpillId = Shader.PropertyToID("_Spill");
        static readonly int SpillFalloffId = Shader.PropertyToID("_SpillFalloff");
        static readonly int WarpId = Shader.PropertyToID("_Warp");
        float baseSpill = -1f, baseSpillFalloff, baseWarp;
        MaterialPropertyBlock block;
        double lastEditorTime;
        float[] baseRates;
        ParticleSystem[] particles;
        // cached by Collect(): Apply runs every frame on every flying palm, so no per-frame hierarchy searches
        InkFluidVolume[] fluids;
        MeshRenderer volumeRenderer;
        InkVolumeColor volumeColor;
        bool collected;

        public float Progress => duration > 0f ? Mathf.Clamp01(time / duration) : 1f;

        // Race: the projectile carrying this palm moves it; the timeline only plays the visual phases over the skill's
        // gather (buildUpSeconds) and flight. Called once the projectile has placed and turned the palm; alreadyElapsed
        // is how far into its timeline a late copy starts (the server spawned its hitbox that long ago).
        public void ConfigureForProjectile(float gather, float flightSeconds, float alreadyElapsed = 0f)
        {
            driveMotion = false; loop = false; preview = false;
            gatherSeconds = Mathf.Max(0f, gather);
            duration = gatherSeconds + Mathf.Max(0.05f, flightSeconds);
            Play();
            time = Mathf.Clamp(alreadyElapsed, 0f, duration);
            Apply();
        }

        Skill_PushProjectileHands ResolveFlight()
        {
            if (flightSource != null) return flightSource;
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<Skill_PushProjectileHands>("Assets/Skills_Assets/Skills/Skill_PushProjectileHands.asset");
#else
            return null;
#endif
        }

        public void Play()
        {
            if (driveMotion)
            {
                if (anchorSet) transform.position = anchorPos;     // restart from the same anchor
                anchorPos = transform.position; anchorForward = transform.forward; anchorSet = true;
                flight = ResolveFlight();
                flightDistance = null;
                if (flight != null && flight.projectileSpeedCurve != null)
                {
                    // the preview takes its timing from the skill too: still for buildUpSeconds, then the flight
                    gatherSeconds = flight.buildUpSeconds;
                    duration = flight.buildUpSeconds + flight.projectileLifetimeSeconds;
                    flightDistance = ChargedProjectileFlight.Cumulative(flight.projectileSpeedCurve);
                    peakSpeed = flight.projectileSpeed;
                    range = ChargedProjectileFlight.Range(flight.projectileSpeedCurve, flight.projectileSpeed, flight.projectileLifetimeSeconds);
                }
            }
            if (!collected) Collect();
            foreach (var vol in fluids) if (vol != null) { vol.Recentre(); vol.SeedGather(fluidSeedInner, fluidSeedOuter, fluidSeedDensity); }
            time = 0f; playing = true; speed = 0f; Apply();
        }

        public void Stop()
        {
            playing = false; speed = 0f;
            if (driveMotion && anchorSet) transform.position = anchorPos;
        }

        void OnEnable()
        {
            Collect();
            if (Application.isPlaying && playOnEnable) Play();
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
        }

        void OnValidate() { if (!Application.isPlaying) Apply(); }

        void Update()
        {
            if (!Application.isPlaying) return;
            Tick(Time.deltaTime);
        }

#if UNITY_EDITOR
        void EditorTick()
        {
            if (this == null || !isActiveAndEnabled || Application.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - lastEditorTime);
            if (dt < 1f / Mathf.Max(editorHz, 1f)) return;
            dt = Mathf.Min(dt, 0.1f);
            lastEditorTime = now;
            if (!preview) { if (playing) Stop(); return; }    // unticking Preview mid-flight puts the hand back on its anchor
            if (!playing) Play();
            Tick(dt);
        }
#endif

        public void Tick(float dt)
        {
            if (!playing) return;
            time += dt;
            if (time >= duration)
            {
                if (loop || (!Application.isPlaying && preview)) { Play(); return; }
                time = duration; playing = false; speed = 0f;
            }
            if (driveMotion && anchorSet && flightDistance != null)
            {
                // same flight as ChargedHandProjectileRuntime: position = the integral of the skill's speed shape
                float flightSeconds = Mathf.Max(0.05f, duration - gatherSeconds);
                float u = Mathf.Clamp01((time - gatherSeconds) / flightSeconds);
                speed = time < gatherSeconds ? 0f : flight.projectileSpeed * Mathf.Max(0f, flight.projectileSpeedCurve.Evaluate(u));
                transform.position = anchorPos + anchorForward * (flight.projectileSpeed * flightSeconds * ChargedProjectileFlight.DistanceFraction(flightDistance, u));
            }
            Apply();
        }

        void Collect()
        {
            particles = GetComponentsInChildren<ParticleSystem>(true);
            baseRates = new float[particles.Length];
            for (int i = 0; i < particles.Length; i++) baseRates[i] = particles[i].emission.rateOverTime.constant;
            fluids = GetComponentsInChildren<InkFluidVolume>(true);
            Transform vol = transform.Find("Volume");
            volumeRenderer = vol != null ? vol.GetComponent<MeshRenderer>() : null;
            volumeColor = vol != null ? vol.GetComponent<InkVolumeColor>() : null;
            collected = true;
        }

        public void Apply()
        {
            float gather = gatherSeconds > 0f ? gatherCurve.Evaluate(Mathf.Clamp01(time / gatherSeconds)) : 1f;
            float dStart = duration - dissipateSeconds;
            float dissipate = dissipateSeconds > 0f && time > dStart ? dissipateCurve.Evaluate(Mathf.Clamp01((time - dStart) / dissipateSeconds)) : 0f;
            dissipate = Mathf.Clamp01(dissipate);
            bool hold = holdVisible && !playing && !Application.isPlaying;
            if (hold) { gather = 1f; dissipate = 0f; }
            float present = gather * (1f - dissipate);

            if (!collected || !Application.isPlaying) Collect();   // edit mode: the hierarchy may be edited under us
            {
                var mr = volumeRenderer;
                if (mr != null)
                {
                    if (block == null) block = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(block);
                    float fadeIn = hold ? 1f : gatherSeconds > 0f ? Mathf.Clamp01(time / (gatherSeconds * Mathf.Max(volumeFadeInFraction, 0.01f))) : 1f;
                    block.SetFloat(FxAlphaId, Mathf.Clamp01(volumeAlphaMax * fadeIn * (1f - dissipate)));   // 0 at both ends
                    block.SetFloat(FxDissolveId, Mathf.Max(1f - gather, dissipate));
                    // resting values: from InkVolumeColor when the Volume has one (live-editable), else from the material
                    var ivc = volumeColor;
                    if (ivc != null)
                    {
                        baseSpill = ivc.outerDensity; baseSpillFalloff = ivc.outerFalloff;
                        if (mr.sharedMaterial != null) baseWarp = mr.sharedMaterial.GetFloat(WarpId);
                    }
                    else if (baseSpill < 0f && mr.sharedMaterial != null)
                    {
                        baseSpill = mr.sharedMaterial.GetFloat(SpillId);
                        baseSpillFalloff = mr.sharedMaterial.GetFloat(SpillFalloffId);
                        baseWarp = mr.sharedMaterial.GetFloat(WarpId);
                    }
                    if (baseSpill >= 0f)
                    {
                        // gather: fog starts spilled far out and converges; dissipate: it spills again and tears
                        block.SetFloat(SpillId, Mathf.Lerp(Mathf.Lerp(gatherSpill, baseSpill, gather), dissipateSpill, dissipate));
                        block.SetFloat(SpillFalloffId, Mathf.Lerp(gatherSpillFalloff, baseSpillFalloff, gather));
                        block.SetFloat(WarpId, Mathf.Lerp(baseWarp, dissipateWarp, dissipate));
                    }
                    mr.SetPropertyBlock(block);
                }
            }

            foreach (var fluid3d in fluids)
            {
                if (fluid3d == null) continue;
                fluid3d.emissionScale = present;
                fluid3d.fadeOut = dissipate;
                fluid3d.pull = -smokePull * dissipate;
                fluid3d.bandScale = 1f;
                fluid3d.gatherSpeed = !hold && time < gatherSeconds ? fluidGatherSpeed * (1f - gather) : 0f;
            }

            if (particles == null || baseRates == null || baseRates.Length != particles.Length) Collect();
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] == null) continue;
                var em = particles[i].emission;
                em.rateOverTime = baseRates[i] * present;
            }
        }
    }
}
