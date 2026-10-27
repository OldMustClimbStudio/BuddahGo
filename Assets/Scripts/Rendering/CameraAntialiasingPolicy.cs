using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BuddahGo.Rendering
{
    // Chooses the gameplay camera's post-process anti-aliasing from the active pipeline asset (quality level).
    //
    // URP 14 runs SMAA as the first post step, on the HDR scene colour before tonemapping. On RaceMap's sunset sand the
    // lit ripples are far brighter than 1, so SMAA's colour edge detection flags nearly every ripple and its blends crawl
    // under motion (measured 1.0-1.4% of pixels flickering by more than 8 levels per frame, about 12x dev). So:
    // - a pipeline that already runs MSAA gets no post AA: MSAA covers geometry edges, SMAA only adds the crawl, and URP
    //   turns TAA off with a warning every second when MSAA is on;
    // - a pipeline without MSAA gets the mode listed for its quality level, else the fallback.
    // A graphics-settings layer can call Apply with its own choice; the per-level table is the default.
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

        private Camera _camera;
        private UniversalAdditionalCameraData _data;
        private int _appliedLevel = -1;
        private AntialiasingMode? _override;

        /// <summary>Post AA the policy picks for the current quality level and pipeline.</summary>
        public AntialiasingMode Resolve()
        {
            if (_override.HasValue)
                return Sanitize(_override.Value);
            string level = QualitySettings.names[QualitySettings.GetQualityLevel()];
            for (int i = 0; i < levels.Length; i++)
                if (levels[i].qualityLevel == level)
                    return Sanitize(levels[i].mode);
            return Sanitize(fallback);
        }

        /// <summary>Forces a post AA mode (still never SMAA/TAA on top of MSAA); null returns to the per-level table.</summary>
        public void Apply(AntialiasingMode? mode)
        {
            _override = mode;
            _appliedLevel = -1;
            Refresh();
        }

        private AntialiasingMode Sanitize(AntialiasingMode mode)
        {
            return PipelineRunsMsaa() ? AntialiasingMode.None : mode;
        }

        private bool PipelineRunsMsaa()
        {
            var asset = UniversalRenderPipeline.asset;
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
            if (_appliedLevel != QualitySettings.GetQualityLevel())
                Refresh();
        }

        private void Refresh()
        {
            if (_data == null)
                return;
            _appliedLevel = QualitySettings.GetQualityLevel();
            var mode = Resolve();
            if (_data.antialiasing != mode)
                _data.antialiasing = mode;
        }
    }
}
