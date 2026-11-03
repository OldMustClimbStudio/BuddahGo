using UnityEngine;
using UnityEngine.VFX;

namespace BuddahGo.VFX
{
    // One row of a skill's layer sheet (Docs/vfx/README.md 3.4) on its own GameObject: phase, tier, function, priority,
    // window and curves. SkillVfxPhaseController evaluates it every frame; the layer only applies the result to its
    // renderer, particle systems, trail or light. Name the GameObject FX_<Skill>_<Phase>_<C|S|A>_<Function>_<NN>.
    [DisallowMultipleComponent]
    public class SkillVfxLayer : MonoBehaviour
    {
        [Header("Layer sheet")]
        public SkillVfxPhase phase = SkillVfxPhase.Prj;
        public SkillVfxTier tier = SkillVfxTier.Core;
        public SkillVfxFunction function = SkillVfxFunction.Mesh;
        [Range(1, 3)] public int priority = 1;
        [Tooltip("Carries what / where / how big / whose: never dropped by tier or distance.")]
        public bool gameplayInfo;
        [Tooltip("In a burst (anti volley), only burst index 0 shows this layer, moved to the burst centre.")]
        public bool burstShared;

        [Header("Window (relative to the phase start)")]
        public SkillVfxWindowUnit startUnit = SkillVfxWindowUnit.PhaseFraction;
        public float start;
        public SkillVfxWindowUnit endUnit = SkillVfxWindowUnit.PhaseFraction;
        [Tooltip("Negative: until the phase ends.")]
        public float end = -1f;
        public float fadeInSeconds;
        public float fadeOutSeconds;
        [Tooltip("Seconds the layer keeps fading out after its phase ends early (e.g. the charge decal after launch).")]
        public float tailFadeSeconds;

        [Header("Curves over the window (0..1)")]
        public AnimationCurve alpha = AnimationCurve.Constant(0f, 1f, 1f);
        [Tooltip("Writes _FxProgress. With useChargeProgress the charge progress from gameplay is used instead.")]
        public bool driveProgress;
        public bool useChargeProgress;
        public AnimationCurve progress = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public bool driveDissolve;
        public AnimationCurve dissolve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public bool driveScale;
        public AnimationCurve scale = AnimationCurve.Constant(0f, 1f, 1f);
        [Tooltip("Light layers: intensity = curve × light_intensity × the light's authored intensity.")]
        public AnimationCurve lightIntensity = AnimationCurve.Constant(0f, 1f, 1f);

        [Header("Placement")]
        public SkillVfxAnchor anchor = SkillVfxAnchor.Self;
        [Tooltip("CasterRelative: position in the caster's frame at caster scale 1 (x right, y up, z along the launch direction).")]
        public Vector3 casterOffset;
        [Tooltip("CasterRelative: rotation relative to the launch direction.")]
        public Vector3 casterEuler;
        public bool faceCamera;
        [Tooltip("Post layers: Feel event played on the local caster only.")]
        public string feelEventId;
        [Tooltip("Particle layers whose particles outlive the window (a body made of particles): cleared when the effect dissipates.")]
        public bool clearOnDissipation;
        [Tooltip("Trail layers: trail time = trail_seconds × this.")]
        public float trailSecondsScale = 1f;
        [Tooltip("Trails under the layer keep the whole path drawn during the window: their time is raised to the window length when it starts (一念光明: everyone's route stays visible in the dark).")]
        public bool trailTimeFollowsWindow;

        [Header("Timing")]
        [Tooltip("How the content follows a window longer or shorter than at the controller's reference length: Fixed plays it as authored, Stretch slows / speeds every particle system by the same ratio, Rhythm scales only delays, emission lengths and burst times. Needs a PhaseFraction window and a reference length for the phase.")]
        public SkillVfxTimeScaling timeScaling = SkillVfxTimeScaling.Fixed;
        [Tooltip("End of the window: every particle system under the layer stops emitting and its live particles play out their own lifetime; the effect is removed only after the last one has died, instead of a fixed tail after the window.")]
        public bool particlesDieNaturally;
        [Tooltip("With particlesDieNaturally: particle systems that must be gone when the window ends instead of dying of old age after it (转向反转's yin-yang). endWithWindowSeconds before the end they stop emitting and every live particle plays the rest of its own colour-over-lifetime fade over exactly that many seconds, starting from its current alpha.")]
        public ParticleSystem[] endWithWindow;
        public float endWithWindowSeconds = 1f;

        [Header("Caster scale (Docs/vfx/giant.md)")]
        [Tooltip("CasterAttached / CasterFollow layers on a caster whose body scale changes (巨大化 ×4, its anti ×0.3). Whole: the layer is scaled with the caster at once. Blend: Area Systems follow over the seconds below with an alpha dip, every other system (authored with ScalingMode Shape) spreads its spawn area and keeps its grain, Scale Surge marks the change.")]
        public SkillVfxCasterScaling casterScaling = SkillVfxCasterScaling.Whole;
        [Tooltip("Blend: systems that draw the area itself (ground rings, large smudges, outward pulses). They stay ScalingMode Hierarchy and grow with the layer.")]
        public ParticleSystem[] areaSystems;
        [Tooltip("Blend: a one-shot ring (one burst particle, colour-over-lifetime fade) pushed from the old edge to the new one, scaled with the area × Surge Lead. Detached from the layer when the slot is configured.")]
        public ParticleSystem scaleSurge;
        [Min(0.01f)] public float scaleUpSeconds = 1f;
        [Min(0.01f)] public float scaleDownSeconds = 0.8f;
        [Tooltip("How far the area systems' alpha dips halfway through a blend.")]
        [Range(0f, 1f)] public float scaleAlphaDip = 0.3f;
        [Tooltip("Detail particles alive when a blend starts fade out over this many seconds; new ones are born at the new spread. Detail emission pauses while shrinking so none is left outside the smaller area.")]
        [Min(0f)] public float handoffSeconds = 0.8f;
        [Tooltip("Detail particles are born at size × area scale ^ this (0 keeps the size, 1 scales it like the area).")]
        [Range(0f, 1f)] public float detailSizeExponent = 0.3f;
        [Tooltip("Detail emission follows the area (× scale²) within [1 / this, this].")]
        [Min(1f)] public float maxDetailEmission = 6f;
        [Min(1f)] public float surgeLead = 1.075f;

        static readonly int FxAlphaId = Shader.PropertyToID("_FxAlpha");
        static readonly int FxProgressId = Shader.PropertyToID("_FxProgress");
        static readonly int FxDissolveId = Shader.PropertyToID("_FxDissolve");

        Renderer[] _renderers;
        ParticleSystem[] _particles;
        float[] _particleRates;
        float[] _particleBurstScales;
        float[] _baseSimulationSpeed;
        ParticleSystem.MinMaxCurve[] _baseStartDelay;
        float[] _baseDuration;
        ParticleSystem.Burst[][] _baseBursts;
        TrailRenderer _trail;
        TrailRenderer[] _trails;
        // VFX Graph effects reused from before the slots (技能Particle / 反噬Particle in the wind-up slots).
        VisualEffect[] _graphs;
        float[] _baseGraphPlayRate;
        Light _light;
        float _lightBaseIntensity;
        Vector3 _baseScale;
        MaterialPropertyBlock _block;
        bool _active;
        bool _endingStarted;
        bool _enabledByTier = true;
        bool _played;
        float _emissionScale = 1f;

        // Caster scale blend state. _areaScale < 0 until the layer is first placed on the caster.
        bool[] _isArea;
        ParticleSystem.MinMaxCurve[] _baseStartSize;
        float _areaScale = -1f;
        float _blendFrom;
        float _blendStart;
        bool _blending;
        bool _shrinking;
        bool _handoffActive;
        float _handoffStart;
        Vector3 _surgeOffset;
        Quaternion _surgeRotation;
        float _surgeBaseScale;
        // Per particle system: alpha each particle had before this blend touched it (area dip, detail handoff).
        System.Collections.Generic.Dictionary<uint, byte>[] _blendAlpha;
        static ParticleSystem.Particle[] s_buffer = new ParticleSystem.Particle[256];

        internal bool IsActive => _active;
        internal bool HasStarted => _played;
        internal bool EnabledByTier => _enabledByTier;

        internal void Initialize(float emissionScale, bool enabledByTier)
        {
            _enabledByTier = enabledByTier;
            _emissionScale = emissionScale;
            _block = new MaterialPropertyBlock();
            bool blend = casterScaling == SkillVfxCasterScaling.Blend;
            // The surge is driven on its own (its scale leads the area's), so it leaves the layer before the layer's
            // systems are collected.
            if (blend && scaleSurge != null && scaleSurge.transform.IsChildOf(transform))
            {
                Transform surge = scaleSurge.transform;
                float layerScale = Mathf.Max(1e-4f, Mathf.Abs(transform.lossyScale.x));
                _surgeOffset = transform.InverseTransformPoint(surge.position) * layerScale;
                _surgeRotation = Quaternion.Inverse(transform.rotation) * surge.rotation;
                _surgeBaseScale = Mathf.Abs(surge.lossyScale.x) / layerScale;
                surge.SetParent(null, true);
                ParticleSystem.MainModule surgeMain = scaleSurge.main;
                surgeMain.playOnAwake = false;
                scaleSurge.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
            _trail = GetComponent<TrailRenderer>();
            // Every trail under the layer (e.g. a character trail copied into a skill effect) starts and stops with
            // the window; only the layer's own trail takes its time from the VFX table (SetTrailSeconds).
            _trails = GetComponentsInChildren<TrailRenderer>(true);
            _graphs = GetComponentsInChildren<VisualEffect>(true);
            _baseGraphPlayRate = new float[_graphs.Length];
            for (int i = 0; i < _graphs.Length; i++)
            {
                // No event on enable or reinit: the window plays it (OnPlay) and stops it (OnStop).
                _graphs[i].initialEventName = string.Empty;
                _graphs[i].Reinit();
                _baseGraphPlayRate[i] = _graphs[i].playRate;
            }
            _light = GetComponent<Light>();
            _baseScale = transform.localScale;
            if (_light != null)
                _lightBaseIntensity = _light.intensity;

            _particleRates = new float[_particles.Length];
            _particleBurstScales = new float[_particles.Length];
            _baseSimulationSpeed = new float[_particles.Length];
            _baseStartDelay = new ParticleSystem.MinMaxCurve[_particles.Length];
            _baseDuration = new float[_particles.Length];
            _baseBursts = new ParticleSystem.Burst[_particles.Length][];
            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem.EmissionModule emission = _particles[i].emission;
                _particleRates[i] = emission.rateOverTimeMultiplier;
                emission.rateOverTimeMultiplier = _particleRates[i] * emissionScale;
                _baseBursts[i] = new ParticleSystem.Burst[emission.burstCount];
                for (int b = 0; b < emission.burstCount; b++)
                {
                    ParticleSystem.Burst burst = emission.GetBurst(b);
                    burst.count = new ParticleSystem.MinMaxCurve(Mathf.Max(1f, Mathf.Round(burst.count.constantMax * emissionScale)));
                    emission.SetBurst(b, burst);
                    _baseBursts[i][b] = burst;
                }
                ParticleSystem.MainModule main = _particles[i].main;
                main.playOnAwake = false;
                _particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _baseSimulationSpeed[i] = main.simulationSpeed;
                _baseStartDelay[i] = main.startDelay;
                _baseDuration[i] = main.duration;
            }

            if (blend)
            {
                _isArea = new bool[_particles.Length];
                _baseStartSize = new ParticleSystem.MinMaxCurve[_particles.Length];
                _blendAlpha = new System.Collections.Generic.Dictionary<uint, byte>[_particles.Length];
                for (int i = 0; i < _particles.Length; i++)
                {
                    _isArea[i] = areaSystems != null && System.Array.IndexOf(areaSystems, _particles[i]) >= 0;
                    _baseStartSize[i] = _particles[i].main.startSize;
                    _blendAlpha[i] = new System.Collections.Generic.Dictionary<uint, byte>();
                }
            }

            for (int i = 0; i < _trails.Length; i++)
                _trails[i].emitting = false;
            SetVisible(false);
        }

        // Scale for this layer when the caster's body is at casterScale, at time `now` (SkillVfxPhaseController places
        // caster-anchored layers with it every frame). Whole returns the caster's scale. Blend: the first placement
        // takes the caster's scale as it is; a later change starts a blend (surge, handoff) and the area eases there.
        internal float FollowCasterScale(float casterScale, float now)
        {
            if (casterScaling != SkillVfxCasterScaling.Blend || _isArea == null)
                return casterScale;
            if (_areaScale < 0f)
            {
                _areaScale = casterScale;
                return _areaScale;
            }

            bool reversed = _blending && (casterScale < _areaScale) != _shrinking && SkillVfxRules.ScaleChanged(_areaScale, casterScale);
            if ((!_blending && SkillVfxRules.ScaleChanged(_areaScale, casterScale)) || reversed)
                StartBlend(casterScale, now);

            float u = 1f;
            if (_blending)
            {
                u = Mathf.Clamp01((now - _blendStart) / (_shrinking ? scaleDownSeconds : scaleUpSeconds));
                _areaScale = Mathf.Lerp(_blendFrom, casterScale, SkillVfxRules.BlendProgress(u, _shrinking));
            }
            else
            {
                _areaScale = casterScale;
            }

            if (_blending && u >= 1f)
                EndBlend();
            ApplyBlend(u, now);
            return _areaScale;
        }

        void StartBlend(float casterScale, float now)
        {
            if (_blending)
                EndBlend();
            _blending = true;
            _shrinking = casterScale < _areaScale;
            _blendFrom = _areaScale;
            _blendStart = now;
            // Detail handoff: the particles alive now fade out; new ones are born at the new spread.
            FinishHandoff();
            for (int i = 0; i < _particles.Length; i++)
            {
                if (_isArea[i])
                    continue;
                int count = Particles(_particles[i]);
                for (int p = 0; p < count; p++)
                    _blendAlpha[i][s_buffer[p].randomSeed] = s_buffer[p].startColor.a;
            }
            _handoffActive = true;
            _handoffStart = now;
            if (scaleSurge != null && _played && _enabledByTier)
            {
                scaleSurge.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                scaleSurge.Play(true);
            }
        }

        void ApplyBlend(float u, float now)
        {
            float dip = SkillVfxRules.BlendAlpha(u, scaleAlphaDip);
            float handoff = SkillVfxRules.HandoffAlpha(now - _handoffStart, handoffSeconds);
            float size = SkillVfxRules.DetailSize(_areaScale, detailSizeExponent);
            float emission = _blending && _shrinking ? 0f : SkillVfxRules.DetailEmission(_areaScale, maxDetailEmission);
            for (int i = 0; i < _particles.Length; i++)
            {
                if (_isArea[i])
                {
                    if (!_blending)
                        continue;
                    // Area dip: every particle, at the alpha it had the first time this blend saw it.
                    int count = Particles(_particles[i]);
                    for (int p = 0; p < count; p++)
                    {
                        byte alpha;
                        if (!_blendAlpha[i].TryGetValue(s_buffer[p].randomSeed, out alpha))
                            _blendAlpha[i][s_buffer[p].randomSeed] = alpha = s_buffer[p].startColor.a;
                        SetAlpha(p, alpha * dip);
                    }
                    _particles[i].SetParticles(s_buffer, count);
                    continue;
                }

                ParticleSystem.MainModule main = _particles[i].main;
                main.startSize = ScaleCurve(_baseStartSize[i], size);
                ParticleSystem.EmissionModule rate = _particles[i].emission;
                rate.rateOverTimeMultiplier = _particleRates[i] * _emissionScale * emission;
                if (_handoffActive && _blendAlpha[i].Count > 0)
                {
                    int count = Particles(_particles[i]);
                    for (int p = 0; p < count; p++)
                    {
                        byte alpha;
                        if (!_blendAlpha[i].TryGetValue(s_buffer[p].randomSeed, out alpha))
                            continue;
                        SetAlpha(p, alpha * handoff);
                        if (handoff <= 0f)
                            s_buffer[p].remainingLifetime = 0f;
                    }
                    _particles[i].SetParticles(s_buffer, count);
                }
            }
            if (_handoffActive && handoff <= 0f)
                FinishHandoff();

            if (scaleSurge != null)
            {
                scaleSurge.transform.SetPositionAndRotation(transform.position + transform.rotation * (_surgeOffset * _areaScale),
                    transform.rotation * _surgeRotation);
                scaleSurge.transform.localScale = Vector3.one * (_surgeBaseScale * _areaScale * surgeLead);
                scaleSurge.gameObject.layer = gameObject.layer;
            }
        }

        // The area dip is lifted: back to each particle's own alpha.
        void EndBlend()
        {
            _blending = false;
            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_isArea[i] || _blendAlpha[i].Count == 0)
                    continue;
                int count = Particles(_particles[i]);
                for (int p = 0; p < count; p++)
                {
                    byte alpha;
                    if (_blendAlpha[i].TryGetValue(s_buffer[p].randomSeed, out alpha))
                        SetAlpha(p, alpha);
                }
                _particles[i].SetParticles(s_buffer, count);
                _blendAlpha[i].Clear();
            }
        }

        // The particles still fading from the last change are gone at once.
        void FinishHandoff()
        {
            if (!_handoffActive)
                return;
            _handoffActive = false;
            for (int i = 0; i < _particles.Length; i++)
            {
                if (_isArea[i] || _blendAlpha[i].Count == 0)
                    continue;
                int count = Particles(_particles[i]);
                for (int p = 0; p < count; p++)
                {
                    if (_blendAlpha[i].ContainsKey(s_buffer[p].randomSeed))
                        s_buffer[p].remainingLifetime = 0f;
                }
                _particles[i].SetParticles(s_buffer, count);
                _blendAlpha[i].Clear();
            }
        }

        static int Particles(ParticleSystem system)
        {
            if (s_buffer.Length < system.particleCount)
                s_buffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(system.particleCount)];
            return system.GetParticles(s_buffer);
        }

        static void SetAlpha(int index, float alpha)
        {
            Color32 colour = s_buffer[index].startColor;
            colour.a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha), 0, 255);
            s_buffer[index].startColor = colour;
        }

        void OnDestroy()
        {
            // The surge left the layer when it was configured; it goes with the layer.
            if (scaleSurge != null && !scaleSurge.transform.IsChildOf(transform))
            {
                if (Application.isPlaying)
                    Destroy(scaleSurge.gameObject);
                else
                    DestroyImmediate(scaleSurge.gameObject);
            }
        }

        internal void ClearTrails()
        {
            for (int i = 0; i < _trails.Length; i++)
                _trails[i].Clear();
        }

        internal void StretchTrails(float seconds)
        {
            for (int i = 0; i < _trails.Length; i++)
                _trails[i].time = Mathf.Max(_trails[i].time, seconds);
        }

        // Applied once when the window starts, before the particles play (main.duration can only change while stopped).
        // k = live window / window at the reference length (SkillVfxRules.TimeScale); timeScaling picks what it moves.
        internal void ApplyTimeScale(float k)
        {
            float simulation = timeScaling == SkillVfxTimeScaling.Stretch ? k : 1f;
            float schedule = timeScaling == SkillVfxTimeScaling.Rhythm ? k : 1f;
            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem.MainModule main = _particles[i].main;
                main.simulationSpeed = _baseSimulationSpeed[i] / simulation;
                main.startDelay = ScaleCurve(_baseStartDelay[i], schedule);
                if (!_particles[i].isPlaying)
                    main.duration = Mathf.Max(0.05f, _baseDuration[i] * schedule);
                ParticleSystem.EmissionModule emission = _particles[i].emission;
                for (int b = 0; b < _baseBursts[i].Length; b++)
                {
                    ParticleSystem.Burst burst = _baseBursts[i][b];
                    burst.time *= schedule;
                    burst.repeatInterval *= schedule;
                    emission.SetBurst(b, burst);
                }
            }
            if (simulation != 1f)
            {
                for (int i = 0; i < _trails.Length; i++)
                    _trails[i].time *= simulation;
            }
            // A graph's own schedule cannot be rescaled from outside, so Rhythm leaves it as authored.
            for (int i = 0; i < _graphs.Length; i++)
                _graphs[i].playRate = _baseGraphPlayRate[i] / simulation;
        }

        // Real seconds a particle born at the end of the window can still live, at time scale k (Stretch runs the
        // particles at 1 / k speed, so they live k times longer).
        internal float LongestParticleSeconds(float k)
        {
            float simulation = timeScaling == SkillVfxTimeScaling.Stretch ? k : 1f;
            float longest = 0f;
            for (int i = 0; _particles != null && i < _particles.Length; i++)
            {
                float speed = Mathf.Max(1e-3f, _baseSimulationSpeed[i]);
                // Ended inside the window; endWithWindowSeconds covers a window that ended before the ending began.
                if (EndsWithWindow(_particles[i]))
                    longest = Mathf.Max(longest, endWithWindowSeconds);
                else
                    longest = Mathf.Max(longest, SkillVfxRules.MaxLifetime(_particles[i].main.startLifetime) * simulation / speed);
            }
            return longest;
        }

        static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve curve, float k)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return new ParticleSystem.MinMaxCurve(curve.constant * k);
                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(curve.constantMin * k, curve.constantMax * k);
                default:
                    curve.curveMultiplier *= k;
                    return curve;
            }
        }

        internal void SetTrailSeconds(float trailSeconds)
        {
            if (_trail != null)
                _trail.time = Mathf.Max(0.01f, trailSeconds * trailSecondsScale);
        }

        // u = position in the window (0..1); fade = window fade × tail fade.
        internal void Apply(float u, float fade, float chargeProgress, float lightScale, Camera view)
        {
            if (!_enabledByTier)
                return;

            if (!_active)
            {
                _active = true;
                SetVisible(true);
                if (!_played)
                {
                    _played = true;
                    for (int i = 0; i < _particles.Length; i++)
                        _particles[i].Play(false);
                    for (int i = 0; i < _trails.Length; i++)
                    {
                        _trails[i].enabled = true;
                        _trails[i].Clear();
                        _trails[i].emitting = true;
                    }
                    for (int i = 0; i < _graphs.Length; i++)
                        _graphs[i].Play();
                }
            }

            float a = alpha.Evaluate(u) * fade;
            _block.Clear();
            _block.SetFloat(FxAlphaId, a);
            if (driveProgress)
                _block.SetFloat(FxProgressId, useChargeProgress ? chargeProgress : progress.Evaluate(u));
            if (driveDissolve)
                _block.SetFloat(FxDissolveId, dissolve.Evaluate(u));
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] is ParticleSystemRenderer)
                    continue;
                _renderers[i].SetPropertyBlock(_block);
            }

            if (driveScale)
                transform.localScale = _baseScale * scale.Evaluate(u);
            if (_light != null)
                _light.intensity = _lightBaseIntensity * lightIntensity.Evaluate(u) * lightScale * fade;
            if (faceCamera && view != null)
                transform.rotation = Quaternion.LookRotation(transform.position - view.transform.position, view.transform.up);
        }

        internal void Deactivate()
        {
            if (!_active)
                return;
            _active = false;
            for (int i = 0; i < _particles.Length; i++)
                _particles[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
            // The window ended before the ending began (shorter than endWithWindowSeconds, or cut early): play it now.
            BeginEnding(endWithWindowSeconds);
            for (int i = 0; i < _trails.Length; i++)
                _trails[i].emitting = false;
            for (int i = 0; i < _graphs.Length; i++)
                _graphs[i].Stop();

            // Particles and trails finish on their own; everything else disappears with the window.
            bool keepsDrawing = _particles.Length > 0 || _trails.Length > 0 || _graphs.Length > 0;
            if (!keepsDrawing)
                SetVisible(false);
            if (_light != null)
                _light.enabled = false;
        }

        bool EndsWithWindow(ParticleSystem system)
        {
            return particlesDieNaturally && endWithWindow != null && System.Array.IndexOf(endWithWindow, system) >= 0;
        }

        // The endWithWindow systems stop emitting and their live particles end within `seconds` (real time).
        internal void BeginEnding(float seconds)
        {
            if (_endingStarted || !particlesDieNaturally || endWithWindow == null || endWithWindow.Length == 0)
                return;
            _endingStarted = true;
            for (int i = 0; i < _particles.Length; i++)
            {
                if (!EndsWithWindow(_particles[i]))
                    continue;
                _particles[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
                PlayEnding(_particles[i], seconds);
            }
        }

        // Each particle jumps to the point of its colour-over-lifetime fade with its current alpha and its lifetime is
        // set so that the rest of the fade takes `seconds`; one that would die sooner on its own is left alone.
        static void PlayEnding(ParticleSystem system, float seconds)
        {
            float rest = Mathf.Max(1e-3f, seconds * system.main.simulationSpeed);
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            Gradient fade = colour.enabled && colour.color.mode == ParticleSystemGradientMode.Gradient ? colour.color.gradient : null;
            var live = new ParticleSystem.Particle[system.particleCount];
            int count = system.GetParticles(live);
            for (int i = 0; i < count; i++)
            {
                if (live[i].remainingLifetime <= rest)
                    continue;
                float age = 1f - live[i].remainingLifetime / Mathf.Max(1e-4f, live[i].startLifetime);
                float from = Mathf.Min(fade != null ? SkillVfxRules.EndingAge(fade, age) : age, 0.999f);
                live[i].startLifetime = rest / (1f - from);
                live[i].remainingLifetime = rest;
            }
            system.SetParticles(live, count);
        }

        internal void ClearParticles()
        {
            for (int i = 0; i < _particles.Length; i++)
                _particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            for (int i = 0; i < _graphs.Length; i++)
                _graphs[i].Reinit();
        }

        void SetVisible(bool visible)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] is ParticleSystemRenderer || _renderers[i] is TrailRenderer)
                    continue;
                _renderers[i].enabled = visible;
            }
            if (_light != null)
                _light.enabled = visible;
        }
    }
}
