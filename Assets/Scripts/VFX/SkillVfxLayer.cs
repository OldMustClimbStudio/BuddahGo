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
        bool _enabledByTier = true;
        bool _played;

        internal bool IsActive => _active;
        internal bool HasStarted => _played;
        internal bool EnabledByTier => _enabledByTier;

        internal void Initialize(float emissionScale, bool enabledByTier)
        {
            _enabledByTier = enabledByTier;
            _block = new MaterialPropertyBlock();
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

            for (int i = 0; i < _trails.Length; i++)
                _trails[i].emitting = false;
            SetVisible(false);
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
