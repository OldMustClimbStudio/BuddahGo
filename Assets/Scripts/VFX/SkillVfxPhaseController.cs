using System;
using System.Collections.Generic;
using BuddahGo.Rendering;
using UnityEngine;

namespace BuddahGo.VFX
{
    // Shared runtime of a skill effect prefab (Docs/vfx/README.md section 4). Skill code reports phase events and the
    // gameplay values; this component starts and stops the SkillVfxLayer children, scales their windows by the VFX
    // table row, fits the body to the gameplay collider and drops layers by the client's effects tier.
    // Presentation only: it never touches gameplay state, physics, RPCs or prediction.
    [DisallowMultipleComponent]
    public class SkillVfxPhaseController : MonoBehaviour
    {
        [Tooltip("vfx_id of this prefab's row in the VFX table (Skill.xlsx / Vfx).")]
        [SerializeField] private string vfxId;
        [Tooltip("Collider size the prefab is authored for; the live collider rescales it.")]
        [SerializeField] private Vector3 referenceColliderSize = new Vector3(60f, 90f, 20f);
        [Tooltip("Scaled uniformly by collider height × scale_multiplier.")]
        [SerializeField] private Transform scaledRoot;
        [Tooltip("Inside scaledRoot; scaled so the body mesh fits inside the collider box at its own proportions.")]
        [SerializeField] private Transform bodyFitRoot;
        [Tooltip("Phase lengths this effect was tuned at (e.g. Imp 10 s for a 10 s debuff). When gameplay passes another length, PhaseFraction windows move with it and layers with timeScaling Stretch / Rhythm rescale their content by live / reference. A phase not listed is never rescaled.")]
        [SerializeField] private PhaseLength[] referenceSeconds = new PhaseLength[0];

        [Serializable]
        public struct PhaseLength
        {
            public SkillVfxPhase phase;
            [Min(0f)] public float seconds;
        }

        private readonly Dictionary<SkillVfxPhase, float> _phaseStart = new Dictionary<SkillVfxPhase, float>();
        private readonly Dictionary<SkillVfxPhase, float> _phaseEnd = new Dictionary<SkillVfxPhase, float>();
        private readonly Dictionary<SkillVfxPhase, float> _phaseDuration = new Dictionary<SkillVfxPhase, float>();
        private readonly List<GameObject> _detached = new List<GameObject>();
        private SkillVfxLayer[] _layers;
        private SkillVfxVisualValues _visual = SkillVfxVisualValues.Defaults;
        private Transform _caster;
        // Where the caster is drawn: the racer's root moves in physics / network ticks (no Rigidbody interpolation)
        // while VisualRoot is smoothed every frame, so caster-anchored layers follow VisualRoot or they stutter
        // against the model and the camera. Scale still comes from the root (PlayerScaleEffect scales the root).
        private Transform _casterVisual;
        private Collider _casterCollider;
        private Action<string> _playLocalFeel;
        private bool _isLocalCaster;
        private float _chargeProgress;
        private float _destroyAt = -1f;
        private bool _configured;

        public string VfxId => vfxId;
        public SkillVfxVisualValues Visual => _visual;
        internal SkillVfxLayer[] Layers => _layers;

        public struct Context
        {
            public Vector3 colliderSize;
            public Transform caster;
            public bool isLocalCaster;
            public Action<string> playLocalFeel;
            public int burstIndex;
            public Vector3 burstCenterOffsetWorld;
        }

        public void Configure(Context context)
        {
            _visual = SkillVfxTable.Resolve(vfxId);
            _caster = context.caster;
            _casterCollider = _caster != null ? _caster.GetComponentInChildren<Collider>() : null;
            var visualBridge = _caster != null ? _caster.GetComponent<NewBuddah.PredictionV2.Visual.BuddahPredictionVisualRootBridge>() : null;
            _casterVisual = visualBridge != null ? visualBridge.GetVisualRoot() : _caster;
            _isLocalCaster = context.isLocalCaster;
            _playLocalFeel = context.playLocalFeel;

            Vector3 collider = context.colliderSize.sqrMagnitude > 1e-6f ? context.colliderSize : referenceColliderSize;
            float uniform = SkillVfxRules.UniformScale(collider, referenceColliderSize) * Mathf.Max(0.01f, _visual.scaleMultiplier);
            if (scaledRoot != null)
                scaledRoot.localScale = Vector3.one * uniform;
            if (bodyFitRoot != null)
            {
                float contain = SkillVfxRules.ContainScale(collider, referenceColliderSize) / SkillVfxRules.UniformScale(collider, referenceColliderSize);
                bodyFitRoot.localScale = Vector3.one * contain;
            }

            MoveToCharacterLayer(transform);

            VfxQualityTier tier = VfxQuality.Current;
            float emission = SkillVfxRules.EmissionScale(tier);
            _layers = GetComponentsInChildren<SkillVfxLayer>(true);
            for (int i = 0; i < _layers.Length; i++)
            {
                SkillVfxLayer layer = _layers[i];
                bool keep = SkillVfxRules.Keeps(tier, layer.priority, layer.gameplayInfo, layer.function);
                if (layer.burstShared && context.burstIndex != 0)
                    keep = false;
                if (layer.burstShared && context.burstIndex == 0)
                    layer.transform.position += context.burstCenterOffsetWorld;
                layer.Initialize(emission, keep);
                layer.SetTrailSeconds(_visual.trailSeconds);
                if (!keep)
                    layer.gameObject.SetActive(false);
            }
            _configured = true;
        }

        // Render order: the character is drawn again after the transparents (URP-HighFidelity-Renderer RenderBuddah, so
        // it stays visible over 一念光明's darkness), which painted it over every effect. Skill effects share its layer
        // and are drawn by RenderSkillVfx right after it (that layer is out of the normal transparent pass), so they
        // depth-test against the character: in front where they are in front, hidden behind it where they are behind.
        // Objects on a layer of their own (一念光明's BlackCurtainTrail) keep it. The editor applies the same rule to
        // effects in VFXtests and in prefab mode (SkillVfxCharacterLayer), so the preview draws like the race.
        public static int MoveToCharacterLayer(Transform root)
        {
            int characterLayer = LayerMask.NameToLayer(CharacterLayerName);
            if (characterLayer < 0)
                return 0;
            int moved = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.layer == 0)
                {
                    t.gameObject.layer = characterLayer;
                    moved++;
                }
            }
            return moved;
        }

        public const string CharacterLayerName = "Buddah";

        // Starts a phase now. durationSeconds is the gameplay length (charge time, lifetime, buff time); Imp and Dis
        // pass 0 to use the VFX table's own lengths.
        public void BeginPhase(SkillVfxPhase phase, float durationSeconds = 0f)
        {
            float duration = durationSeconds;
            if (phase == SkillVfxPhase.Chg)
                duration = durationSeconds * Mathf.Max(0.01f, _visual.chgRatio);
            else if (phase == SkillVfxPhase.Dis && duration <= 0f)
                duration = _visual.disSeconds;
            else if (phase == SkillVfxPhase.Lin && duration <= 0f)
                duration = _visual.linSeconds;
            _phaseStart[phase] = Time.time;
            _phaseDuration[phase] = Mathf.Max(0f, duration);
            _phaseEnd.Remove(phase);
        }

        public void EndPhase(SkillVfxPhase phase)
        {
            if (_phaseStart.ContainsKey(phase) && !_phaseEnd.ContainsKey(phase))
                _phaseEnd[phase] = Time.time;
        }

        // Charge progress from gameplay (ChargedHandProjectileRuntime's Progress, 0..1).
        public void SetChargeProgress(float value)
        {
            _chargeProgress = Mathf.Clamp01(value);
        }

        // Ends every running phase, starts Dis and destroys the effect once every layer has finished.
        public float BeginDissipation()
        {
            foreach (SkillVfxPhase phase in new List<SkillVfxPhase>(_phaseStart.Keys))
                EndPhase(phase);
            BeginPhase(SkillVfxPhase.Dis);
            for (int i = 0; i < _layers.Length; i++)
            {
                if (_layers[i] != null && _layers[i].clearOnDissipation && _layers[i].EnabledByTier)
                    _layers[i].ClearParticles();
            }
            float remaining = Mathf.Max(_visual.disSeconds, LongestLayerSeconds(SkillVfxPhase.Dis)) + MaxTailFade();
            _destroyAt = Time.time + remaining;
            return remaining;
        }

        // Buff state layers: keeps the Buf phase running until untilTime, then fades over buf_fade_seconds.
        public void ExtendBuff(float durationSeconds)
        {
            if (!_phaseStart.ContainsKey(SkillVfxPhase.Buf))
            {
                BeginPhase(SkillVfxPhase.Buf, durationSeconds);
                return;
            }
            float elapsed = Time.time - _phaseStart[SkillVfxPhase.Buf];
            _phaseDuration[SkillVfxPhase.Buf] = Mathf.Max(_phaseDuration[SkillVfxPhase.Buf], elapsed + durationSeconds);
            _phaseEnd.Remove(SkillVfxPhase.Buf);
            _destroyAt = -1f;
        }

        // Returns the seconds until the effect is destroyed.
        public float DestroyWhenFinished(float extraSeconds)
        {
            float end = Time.time;
            float tailScale = 1f;
            float naturalTail = 0f;
            foreach (KeyValuePair<SkillVfxPhase, float> pair in _phaseStart)
            {
                // A window may outlast its phase (a Seconds window longer than the gameplay length).
                end = Mathf.Max(end, pair.Value + Mathf.Max(_phaseDuration[pair.Key], LongestLayerSeconds(pair.Key)));
                for (int i = 0; _layers != null && i < _layers.Length; i++)
                {
                    if (_layers[i] == null || _layers[i].phase != pair.Key)
                        continue;
                    float k = LayerTimeScale(_layers[i], _phaseDuration[pair.Key]);
                    // Stretched particles also live k times longer after the window.
                    if (_layers[i].timeScaling == SkillVfxTimeScaling.Stretch)
                        tailScale = Mathf.Max(tailScale, k);
                    // Its last particles die of old age instead of being cut by the fixed tail.
                    if (_layers[i].particlesDieNaturally && _layers[i].EnabledByTier)
                        naturalTail = Mathf.Max(naturalTail, _layers[i].LongestParticleSeconds(k));
                }
            }
            _destroyAt = end + Mathf.Max(Mathf.Max(0f, extraSeconds) * tailScale, naturalTail) + MaxTailFade();
            return _destroyAt - Time.time;
        }

        // Internal so the EditMode tests can run a frame.
        internal void LateUpdate()
        {
            if (!_configured)
                return;

            float now = Time.time;
            Camera view = Camera.main;
            for (int i = 0; i < _layers.Length; i++)
            {
                SkillVfxLayer layer = _layers[i];
                if (layer == null || !layer.EnabledByTier)
                    continue;
                Evaluate(layer, now, view);
                // After its window the layer stops emitting, but its live particles (local space) still ride on the
                // caster until the effect is destroyed; otherwise they are left on the track where the window ended.
                if (!layer.IsActive && layer.HasStarted && FollowsCaster(layer) && _caster != null)
                    PlaceOnCaster(layer, Facing(layer));
            }

            if (_destroyAt >= 0f && now >= _destroyAt)
                Destroy(gameObject);
        }

        private void Evaluate(SkillVfxLayer layer, float now, Camera view)
        {
            if (!_phaseStart.TryGetValue(layer.phase, out float phaseStart))
            {
                layer.Deactivate();
                return;
            }

            float phaseSeconds = _phaseDuration[layer.phase];
            float t = now - phaseStart;
            LayerWindow(layer, phaseSeconds, out float start, out float end);

            float fade = 1f;
            if (_phaseEnd.TryGetValue(layer.phase, out float phaseEnded))
            {
                // Phase ended early (launch ends the charge, expiry ends the flight): fade out over the layer's tail.
                float since = now - phaseEnded;
                if (layer.tailFadeSeconds <= 0f || since >= layer.tailFadeSeconds)
                {
                    layer.Deactivate();
                    return;
                }
                fade = 1f - since / layer.tailFadeSeconds;
                t = Mathf.Min(t, phaseEnded - phaseStart);
            }

            float fadeOut = layer.phase == SkillVfxPhase.Buf ? Mathf.Max(layer.fadeOutSeconds, _visual.bufFadeSeconds) : layer.fadeOutSeconds;
            float fadeIn = layer.phase == SkillVfxPhase.Buf ? Mathf.Max(layer.fadeInSeconds, _visual.bufFadeSeconds) : layer.fadeInSeconds;
            float window = SkillVfxRules.WindowAlpha(t, start, Mathf.Max(end, start + 1e-4f), fadeIn, fadeOut);
            bool inWindow = t >= start && t < end;
            if (!inWindow && fade >= 1f)
            {
                layer.Deactivate();
                return;
            }
            if (!inWindow)
                window = 1f;

            bool firstFrame = !layer.IsActive;
            if (firstFrame)
            {
                OnLayerStarted(layer);
                if (!layer.HasStarted)
                    layer.ApplyTimeScale(LayerTimeScale(layer, phaseSeconds));
                if (layer.trailTimeFollowsWindow)
                    layer.StretchTrails(end - start);
            }

            if (layer.anchor == SkillVfxAnchor.CasterFeet && _caster != null)
                layer.transform.SetPositionAndRotation(CasterFeet(), Quaternion.identity);
            else if (FollowsCaster(layer) && _caster != null)
            {
                Vector3 before = layer.transform.position;
                PlaceOnCaster(layer, Facing(layer));
                // Respawn / teleport: the caster jumped, so a trail would draw a line across the map. Start it afresh
                // (this used to be PlayerBlackCurtainTrail / PlayerAccelerationTrail.NotifyTeleportRebase).
                if (!firstFrame && (layer.transform.position - before).sqrMagnitude > TeleportJump * TeleportJump)
                    layer.ClearTrails();
            }

            float u = Mathf.Clamp01((t - start) / Mathf.Max(1e-4f, end - start));
            layer.Apply(u, window * fade, _chargeProgress, _visual.lightIntensity, view);
            // Its endWithWindow systems fade out over the window's last seconds, gone when the rest stops emitting.
            if (fade >= 1f && end - t <= layer.endWithWindowSeconds)
                layer.BeginEnding(end - t);
        }

        public float ReferenceSeconds(SkillVfxPhase phase)
        {
            for (int i = 0; i < referenceSeconds.Length; i++)
            {
                if (referenceSeconds[i].phase == phase)
                    return referenceSeconds[i].seconds;
            }
            return 0f;
        }

        // The layer's window in seconds from its phase start for a phase of phaseSeconds (editor timing preview).
        public void LayerWindow(SkillVfxLayer layer, float phaseSeconds, out float start, out float end)
        {
            SkillVfxVisualValues visual = _configured ? _visual : SkillVfxTable.Resolve(vfxId);
            start = SkillVfxRules.ToSeconds(layer.start, layer.startUnit, phaseSeconds, visual);
            end = layer.end < 0f ? phaseSeconds : SkillVfxRules.ToSeconds(layer.end, layer.endUnit, phaseSeconds, visual);
        }

        // k of the layer's time scaling for a phase of phaseSeconds: its window then, over its window at the
        // reference length. 1 for Fixed layers, unlisted phases and windows that do not move with the phase.
        public float LayerTimeScale(SkillVfxLayer layer, float phaseSeconds)
        {
            float reference = ReferenceSeconds(layer.phase);
            if (layer.timeScaling == SkillVfxTimeScaling.Fixed || reference <= 0f)
                return 1f;
            LayerWindow(layer, phaseSeconds, out float start, out float end);
            LayerWindow(layer, reference, out float referenceStart, out float referenceEnd);
            return SkillVfxRules.TimeScale(start, end, referenceStart, referenceEnd);
        }

        private static bool FollowsCaster(SkillVfxLayer layer)
        {
            return layer.anchor == SkillVfxAnchor.CasterAttached || layer.anchor == SkillVfxAnchor.CasterFollow;
        }

        // CasterAttached turns with the caster. CasterRelative and CasterFollow keep the release direction: this root
        // is placed once at the caster's facing when the effect is played and never turns.
        private Quaternion Facing(SkillVfxLayer layer)
        {
            return layer.anchor == SkillVfxAnchor.CasterAttached ? _casterVisual.rotation : transform.rotation;
        }

        // Farther than any racer moves in one frame (units).
        private const float TeleportJump = 60f;

        private void PlaceOnCaster(SkillVfxLayer layer, Quaternion facing)
        {
            float casterScale = Mathf.Abs(_caster.lossyScale.y);
            SkillVfxRules.CasterRelativePose(_casterVisual.position, casterScale, facing,
                layer.casterOffset, layer.casterEuler, out Vector3 position, out Quaternion rotation);
            layer.transform.SetPositionAndRotation(position, rotation);
            layer.transform.localScale = Vector3.one * casterScale;
        }

        // The character's pivot is not at its feet: use the bottom of its collider (the track is flat under a racer).
        private Vector3 CasterFeet()
        {
            Vector3 feet = _casterVisual.position;
            if (_casterCollider != null && _casterCollider.enabled)
                feet.y = _casterCollider.bounds.min.y;
            return feet + Vector3.up * 0.05f;
        }

        private void OnLayerStarted(SkillVfxLayer layer)
        {
            bool onCaster = layer.anchor == SkillVfxAnchor.CasterRelative || FollowsCaster(layer);
            if (onCaster && _caster != null)
            {
                if (layer.transform.parent != null)
                {
                    layer.transform.SetParent(null, false);
                    _detached.Add(layer.gameObject);
                }
                PlaceOnCaster(layer, Facing(layer));
            }
            else if ((layer.anchor == SkillVfxAnchor.DetachAtStart || onCaster) && layer.transform.parent != null)
            {
                Vector3 worldScale = layer.transform.lossyScale;
                layer.transform.SetParent(null, true);
                layer.transform.localScale = worldScale;
                _detached.Add(layer.gameObject);
            }

            if (layer.function == SkillVfxFunction.Post && _isLocalCaster && !string.IsNullOrEmpty(layer.feelEventId))
                _playLocalFeel?.Invoke(layer.feelEventId);
        }

        private float LongestLayerSeconds(SkillVfxPhase phase)
        {
            float longest = 0f;
            float phaseSeconds = _phaseDuration.TryGetValue(phase, out float d) ? d : 0f;
            for (int i = 0; i < _layers.Length; i++)
            {
                if (_layers[i] == null || _layers[i].phase != phase)
                    continue;
                LayerWindow(_layers[i], phaseSeconds, out _, out float end);
                longest = Mathf.Max(longest, end);
            }
            return longest;
        }

        private float MaxTailFade()
        {
            float longest = 0f;
            for (int i = 0; i < _layers.Length; i++)
            {
                if (_layers[i] != null)
                    longest = Mathf.Max(longest, _layers[i].tailFadeSeconds);
            }
            return longest;
        }

        private void OnDestroy()
        {
            // Detached layers (launch flash, seal, gust) outlive the moving root only until their own window ends;
            // the controller destroys them with itself so nothing accumulates.
            for (int i = 0; i < _detached.Count; i++)
            {
                if (_detached[i] != null)
                    Destroy(_detached[i]);
            }
        }
    }
}
