using System.Collections.Generic;

namespace BuddahGo.Rendering.Settings
{
    // Options that only take effect when another option allows it. The menu and the F9 panel grey such an option out
    // (GraphicsSettingsService.IsActive) instead of explaining it, the way most games do; the value is kept, so it
    // applies again once the other option changes. Level availability (SSAO needs the feature in the preset's renderer)
    // is the other half, in GraphicsSettingsApplier.IsLevelAvailable.
    public static class GraphicsOptionDependencies
    {
        /// <summary>
        /// The option that keeps <paramref name="optionId"/> from taking effect with these resolved levels, or null when it
        /// takes effect: <c>upscaler</c> needs a render scale below 100 %, <c>sharpening</c> needs TAA, <c>frame_rate_cap</c>
        /// needs VSync off.
        /// </summary>
        public static string InactiveBecause(IReadOnlyDictionary<string, GraphicsOptionLevelRecord> levels, string optionId)
        {
            if (levels == null)
                return null;
            switch (optionId)
            {
                case GraphicsPipelineMapper.Upscaler:
                    return levels.TryGetValue(GraphicsPipelineMapper.RenderScale, out var scale) && scale.renderScale >= 0.999f
                        ? GraphicsPipelineMapper.RenderScale : null;
                case GraphicsPipelineMapper.Sharpening:
                    return levels.TryGetValue(GraphicsPipelineMapper.AntiAliasing, out var aa)
                        && GraphicsSettingsApplier.ParsePostAntialiasing(aa.postAntialiasing) != UnityEngine.Rendering.Universal.AntialiasingMode.TemporalAntiAliasing
                        ? GraphicsPipelineMapper.AntiAliasing : null;
                case GraphicsPipelineMapper.FrameRateCap:
                    return levels.TryGetValue(GraphicsPipelineMapper.VSync, out var vSync) && vSync.vSyncCount > 0
                        ? GraphicsPipelineMapper.VSync : null;
                default:
                    return null;
            }
        }
    }
}
