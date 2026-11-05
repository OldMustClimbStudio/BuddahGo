using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BuddahGo.Rendering
{
    // The renderer-feature contract shared by every quality level (Docs/rendering/graphics-settings.md, "Modules").
    // Gameplay passes carry or protect gameplay information, so every quality level's default renderer (index 0 of its
    // pipeline asset) has them, active, in this order, and keeps the gameplay layers out of its transparent pass: the
    // curtain covers everything the renderer drew, and the passes after it draw the racers, effects, edges and trails.
    // Only cosmetic features (SSAO) differ per level. Tools/BuddahGo/Rendering/Sync Gameplay Passes copies them from the reference renderer; GraphicsSettingsTests
    // checks the contract on the shipped assets, and Check() warns at startup if a level breaks it.
    public static class RenderingModules
    {
        /// <summary>Gameplay renderer features by name, in the order they sit after the cosmetic ones.</summary>
        public static readonly IReadOnlyList<string> GameplayFeatures = new[]
        {
            "BlackCurtain",     // 一念光明: the victim's darkness (BlackCurtainRenderFeature)
            "RenderBuddah",     // racers again after the curtain
            "RenderMapEdge",    // track edge ribbons (layer 8) after the curtain
            "RenderTrails",     // route trails (layer 7) after the curtain
            "RenderSkillVfx",   // skill effects on the character layer, depth-tested against the racers
        };

        /// <summary>The renderer every other default renderer copies its gameplay features from.</summary>
        public const string ReferenceRenderer = "URP-HighFidelity-Renderer";

        /// <summary>Layer the racers and skill effects live on; it is drawn by the gameplay passes, not the transparent pass.</summary>
        public const string CharacterLayer = "Buddah";

        /// <summary>Layers drawn only by the gameplay passes (after the curtain), never by the renderer's transparent pass.</summary>
        public static readonly IReadOnlyList<string> GameplayLayers = new[] { CharacterLayer, "BlackCurtainTrail", "TrackEdge" };

        static readonly FieldInfo s_RendererDataList = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>The default renderer (index 0) of a pipeline asset, or null.</summary>
        public static ScriptableRendererData DefaultRenderer(UniversalRenderPipelineAsset asset)
        {
            var list = asset != null ? s_RendererDataList?.GetValue(asset) as ScriptableRendererData[] : null;
            return list != null && list.Length > 0 ? list[0] : null;
        }

        /// <summary>
        /// What a renderer is missing of the gameplay contract: absent or inactive gameplay features, features out of
        /// order, or the character layer drawn in the transparent pass. Empty when it complies.
        /// </summary>
        public static List<string> Violations(ScriptableRendererData data)
        {
            var problems = new List<string>();
            if (data == null)
            {
                problems.Add("no default renderer");
                return problems;
            }
            int last = -1;
            foreach (string name in GameplayFeatures)
            {
                int index = data.rendererFeatures.FindIndex(f => f != null && f.name == name);
                if (index < 0)
                    problems.Add(data.name + ": missing " + name);
                else if (!data.rendererFeatures[index].isActive)
                    problems.Add(data.name + ": " + name + " is inactive");
                else if (index < last)
                    problems.Add(data.name + ": " + name + " is out of order");
                last = Mathf.Max(last, index);
            }
            if (data is UniversalRendererData universal)
                foreach (string name in GameplayLayers)
                {
                    int layer = LayerMask.NameToLayer(name);
                    if (layer >= 0 && (universal.transparentLayerMask & (1 << layer)) != 0)
                        problems.Add(data.name + ": the transparent pass draws the " + name + " layer (it would show through the curtain twice)");
                }
            return problems;
        }

        /// <summary>Contract violations of every quality level's default renderer.</summary>
        public static List<string> CheckQualityLevels()
        {
            var problems = new List<string>();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                var asset = (QualitySettings.GetRenderPipelineAssetAt(i) ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
                if (asset == null)
                    continue;
                foreach (var problem in Violations(DefaultRenderer(asset)))
                    problems.Add(QualitySettings.names[i] + " / " + problem);
            }
            return problems;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Check()
        {
            foreach (var problem in CheckQualityLevels())
                Debug.LogWarning("[RenderingModules] " + problem + " (run Tools/BuddahGo/Rendering/Sync Gameplay Passes)");
        }
    }
}
