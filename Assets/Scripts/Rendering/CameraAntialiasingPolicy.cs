using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BuddahGo.Rendering
{
    // Chooses the gameplay camera's post-process anti-aliasing from the active pipeline asset (quality level).
    //
    // RaceMap's sunset turns the sand's normal-mapped ripples into dense, high-contrast stripes. SMAA treats every ripple
    // boundary as an edge and its blends crawl as the stripes move (1.0-1.4% of pixels flickering by more than 8 levels
    // per frame, about 12x dev), wherever it runs: SMAA on the tonemapped image crawled the same. So:
    // - SMAA and TAA never run on top of MSAA: SMAA only adds the crawl, and URP turns TAA off with a warning every second
    //   when MSAA is on. FXAA may: MSAA averages edge samples before tonemapping, so bright-sand-against-dark edges and the
    //   ripple bands' shading stay stepped; FXAA after tonemapping halves the visible flicker (0.13-0.15% -> 0.05-0.06% of
    //   pixels changing by more than 8 levels in a sub-pixel pan, measured with real 8x MSAA on 2026-10-28) at some
    //   softness. High Fidelity has no table entry, so it gets the fallback, FXAA, on top of its 8x MSAA;
    // - otherwise the mode listed for the quality level, else the fallback.
    // The graphics settings (GraphicsSettingsApplier) set SettingsMode and SettingsSharpening for every camera with this
    // component; Apply overrides one camera. The per-level table is the default when no settings are applied.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraAntialiasingPolicy : MonoBehaviour
    {
        [System.Serializable]
        public struct LevelMode
        {
            [Tooltip("Quality level name as listed in Project Settings > Quality.")]
            public string qualityLevel;
            public AntialiasingMode mode;
        }

        [Tooltip("Post AA per quality level, used when the level's pipeline runs without MSAA.")]
        [SerializeField] private LevelMode[] levels =
        {
            new LevelMode { qualityLevel = "Balanced", mode = AntialiasingMode.TemporalAntiAliasing },
            new LevelMode { qualityLevel = "Performant", mode = AntialiasingMode.FastApproximateAntialiasing },
        };

        [Tooltip("Post AA for a level without an entry, when its pipeline runs without MSAA.")]
        [SerializeField] private AntialiasingMode fallback = AntialiasingMode.FastApproximateAntialiasing;

        private static AntialiasingMode? s_SettingsMode;
        private static float? s_SettingsSharpening;
        private static int s_SettingsVersion;

        /// <summary>Post AA chosen in the graphics settings for all policy cameras; null returns to the per-level table.</summary>
        public static AntialiasingMode? SettingsMode
        {
            get => s_SettingsMode;
            set { s_SettingsMode = value; s_SettingsVersion++; }
        }

        /// <summary>TAA contrast-adaptive sharpening (0-1) from the graphics settings; null keeps the camera's own value.</summary>
        public static float? SettingsSharpening
        {
            get => s_SettingsSharpening;
            set { s_SettingsSharpening = value; s_SettingsVersion++; }
        }

        private Camera _camera;
        private UniversalAdditionalCameraData _data;
        private int _appliedLevel = -1;
        private int _appliedVersion = -1;
        private float _ownSharpening = -1f;
        private AntialiasingMode? _override;

        /// <summary>Post AA the policy picks for the current quality level and pipeline.</summary>
        public AntialiasingMode Resolve()
        {
            if (_override.HasValue)
                return Sanitize(_override.Value);
            if (s_SettingsMode.HasValue)
                return Sanitize(s_SettingsMode.Value);
            string level = QualitySettings.names[QualitySettings.GetQualityLevel()];
            for (int i = 0; i < levels.Length; i++)
                if (levels[i].qualityLevel == level)
                    return Sanitize(levels[i].mode);
            return Sanitize(fallback);
        }

        /// <summary>Forces a post AA mode (still never SMAA or TAA on top of MSAA); null returns to the per-level table.</summary>
        public void Apply(AntialiasingMode? mode)
        {
            _override = mode;
            _appliedLevel = -1;
            Refresh();
        }

        private AntialiasingMode Sanitize(AntialiasingMode mode)
        {
            bool needsSingleSample = mode == AntialiasingMode.TemporalAntiAliasing || mode == AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            return needsSingleSample && PipelineRunsMsaa() ? AntialiasingMode.None : mode;
        }

        private bool PipelineRunsMsaa()
        {
            var asset = UniversalRenderPipeline.asset;
            if (_camera == null)
                _camera = GetComponent<Camera>();
            return asset != null && asset.msaaSampleCount > 1 && _camera != null && _camera.allowMSAA;
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _data = GetComponent<UniversalAdditionalCameraData>();
        }

        private void OnEnable() => Refresh();

        private void LateUpdate()
        {
            if (_appliedLevel != QualitySettings.GetQualityLevel() || _appliedVersion != s_SettingsVersion)
                Refresh();
        }

        private void Refresh()
        {
            if (_data == null)
                return;
            _appliedLevel = QualitySettings.GetQualityLevel();
            _appliedVersion = s_SettingsVersion;
            var mode = Resolve();
            if (_data.antialiasing != mode)
                _data.antialiasing = mode;
            ref var taa = ref _data.taaSettings;
            if (_ownSharpening < 0f)
                _ownSharpening = taa.contrastAdaptiveSharpening;
            taa.contrastAdaptiveSharpening = s_SettingsSharpening ?? _ownSharpening;
        }
    }
}
