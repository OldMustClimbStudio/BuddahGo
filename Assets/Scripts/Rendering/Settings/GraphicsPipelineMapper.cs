using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BuddahGo.Rendering.Settings
{
    // Maps resolved option levels onto a URP pipeline asset and its renderer features. Only ever called on runtime
    // clones (GraphicsSettingsApplier); comparing against the original asset decides whether a clone is needed at all.
    // URP 14 keeps cascade splits, shadow-map resolution, soft and main-light shadows behind internal setters, and its
    // SSAO feature is internal; those are set through their serialized fields. The SSR feature lives in
    // Assembly-CSharp-firstpass, which this assembly cannot reference, so it is found by type name.
    public static class GraphicsPipelineMapper
    {
        public const string RenderScale = "render_scale";
        public const string Upscaler = "upscaler";
        public const string AntiAliasing = "anti_aliasing";
        public const string Sharpening = "sharpening";
        public const string ShadowQuality = "shadow_quality";
        public const string TextureQuality = "texture_quality";
        public const string AnisotropicFiltering = "anisotropic_filtering";
        public const string Reflections = "reflections";
        public const string AmbientOcclusion = "ambient_occlusion";
        public const string VfxQuality = "vfx_quality";
        public const string FrameRateCap = "frame_rate_cap";

        const string SsrTypeName = "ScreenSpaceReflectionURP";
        const string SsaoTypeName = "ScreenSpaceAmbientOcclusion";
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static readonly FieldInfo RendererDataListField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", Instance);
        static readonly FieldInfo MainLightShadowsField = typeof(UniversalRenderPipelineAsset).GetField("m_MainLightShadowsSupported", Instance);
        static readonly FieldInfo SoftShadowsField = typeof(UniversalRenderPipelineAsset).GetField("m_SoftShadowsSupported", Instance);
        static readonly FieldInfo ShadowResolutionField = typeof(UniversalRenderPipelineAsset).GetField("m_MainLightShadowmapResolution", Instance);
        static readonly FieldInfo Cascade2Field = typeof(UniversalRenderPipelineAsset).GetField("m_Cascade2Split", Instance);
        static readonly FieldInfo Cascade3Field = typeof(UniversalRenderPipelineAsset).GetField("m_Cascade3Split", Instance);
        static readonly FieldInfo Cascade4Field = typeof(UniversalRenderPipelineAsset).GetField("m_Cascade4Split", Instance);

        // ---------------------------------------------------------------- pipeline asset

        /// <summary>True when applying <paramref name="levels"/> would change anything on <paramref name="asset"/> or its features.</summary>
        public static bool DiffersFrom(UniversalRenderPipelineAsset asset, IReadOnlyDictionary<string, GraphicsOptionLevelRecord> levels)
        {
            var probe = UnityEngine.Object.Instantiate(asset);
            try
            {
                ApplyToPipeline(probe, levels);
                if (!SamePipeline(asset, probe))
                    return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
            foreach (var data in RendererData(asset))
            {
                if (data == null)
                    continue;
                foreach (var feature in data.rendererFeatures)
                    if (feature != null && FeatureDiffers(feature, levels))
                        return true;
            }
            return false;
        }

        public static void ApplyToPipeline(UniversalRenderPipelineAsset asset, IReadOnlyDictionary<string, GraphicsOptionLevelRecord> levels)
        {
            if (levels.TryGetValue(RenderScale, out var scale))
                asset.renderScale = Mathf.Clamp(scale.renderScale, 0.1f, 2f);
            if (levels.TryGetValue(Upscaler, out var upscaler) && Enum.TryParse(upscaler.upscalingFilter, true, out UpscalingFilterSelection filter))
                asset.upscalingFilter = filter;
            if (levels.TryGetValue(AntiAliasing, out var aa) && aa.msaaSamples >= 1)
                asset.msaaSampleCount = aa.msaaSamples;
            if (levels.TryGetValue(ShadowQuality, out var shadows))
                ApplyShadows(asset, shadows);
        }

        static void ApplyShadows(UniversalRenderPipelineAsset asset, GraphicsOptionLevelRecord level)
        {
            MainLightShadowsField?.SetValue(asset, level.shadowsEnabled);
            if (!level.shadowsEnabled)
                return;
            asset.shadowDistance = level.shadowDistance;
            asset.shadowCascadeCount = Mathf.Clamp(level.shadowCascadeCount, 1, 4);
            SoftShadowsField?.SetValue(asset, level.softShadows);
            if (ShadowResolutionField != null && level.shadowResolution > 0)
                ShadowResolutionField.SetValue(asset, Enum.ToObject(ShadowResolutionField.FieldType, level.shadowResolution));
            // Splits are authored in metres; URP stores them as fractions of the shadow distance.
            var splits = level.shadowCascadeSplitsMeters ?? new List<float>();
            float distance = Mathf.Max(0.01f, level.shadowDistance);
            float Fraction(int i) => i < splits.Count ? Mathf.Clamp01(splits[i] / distance) : 0f;
            if (asset.shadowCascadeCount == 2 && splits.Count >= 1)
                Cascade2Field?.SetValue(asset, Fraction(0));
            else if (asset.shadowCascadeCount == 3 && splits.Count >= 2)
                Cascade3Field?.SetValue(asset, new Vector2(Fraction(0), Fraction(1)));
            else if (asset.shadowCascadeCount == 4 && splits.Count >= 3)
                Cascade4Field?.SetValue(asset, new Vector3(Fraction(0), Fraction(1), Fraction(2)));
        }

        static bool SamePipeline(UniversalRenderPipelineAsset a, UniversalRenderPipelineAsset b)
        {
            return Mathf.Approximately(a.renderScale, b.renderScale)
                && a.upscalingFilter == b.upscalingFilter
                && a.msaaSampleCount == b.msaaSampleCount
                && Equals(MainLightShadowsField?.GetValue(a), MainLightShadowsField?.GetValue(b))
                && (!(bool)(MainLightShadowsField?.GetValue(a) ?? true)
                    || (Mathf.Approximately(a.shadowDistance, b.shadowDistance)
                        && a.shadowCascadeCount == b.shadowCascadeCount
                        && Equals(SoftShadowsField?.GetValue(a), SoftShadowsField?.GetValue(b))
                        && Equals(ShadowResolutionField?.GetValue(a), ShadowResolutionField?.GetValue(b))
                        && CascadesMatch(a, b)));
        }

        static bool CascadesMatch(UniversalRenderPipelineAsset a, UniversalRenderPipelineAsset b)
        {
            switch (a.shadowCascadeCount)
            {
                case 2: return Mathf.Abs((float)Cascade2Field.GetValue(a) - (float)Cascade2Field.GetValue(b)) < 1e-3f;
                case 3: return ((Vector2)Cascade3Field.GetValue(a) - (Vector2)Cascade3Field.GetValue(b)).sqrMagnitude < 1e-6f;
                case 4: return ((Vector3)Cascade4Field.GetValue(a) - (Vector3)Cascade4Field.GetValue(b)).sqrMagnitude < 1e-6f;
                default: return true;
            }
        }

        // ---------------------------------------------------------------- renderer data and features

        public static ScriptableRendererData[] RendererData(UniversalRenderPipelineAsset asset)
        {
            return RendererDataListField?.GetValue(asset) as ScriptableRendererData[] ?? Array.Empty<ScriptableRendererData>();
        }

        /// <summary>
        /// A runtime copy of <paramref name="original"/> with its own copies of the renderer data and their features, so
        /// pipeline and feature settings can change without touching assets. Every copy is marked DontSave; release it
        /// with <see cref="DestroyDetachedClone"/>.
        /// </summary>
        public static UniversalRenderPipelineAsset CreateDetachedClone(UniversalRenderPipelineAsset original)
        {
            var clone = UnityEngine.Object.Instantiate(original);
            clone.name = original.name + " (Settings)";
            clone.hideFlags = HideFlags.DontSave;
            var source = RendererData(clone);
            var copies = new ScriptableRendererData[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null)
                    continue;
                var data = UnityEngine.Object.Instantiate(source[i]);
                data.name = source[i].name + " (Settings)";
                data.hideFlags = HideFlags.DontSave;
                var features = data.rendererFeatures;
                for (int f = 0; f < features.Count; f++)
                {
                    if (features[f] == null)
                        continue;
                    var feature = UnityEngine.Object.Instantiate(features[f]);
                    feature.name = features[f].name;
                    feature.hideFlags = HideFlags.DontSave;
                    features[f] = feature;
                }
                copies[i] = data;
            }
            RendererDataListField?.SetValue(clone, copies);
            return clone;
        }

        /// <summary>
        /// Destroys a clone from <see cref="CreateDetachedClone"/> and the copies it owns; objects not marked DontSave (the
        /// assets) are left alone. Features are disposed first: copying one runs its Create(), which can allocate resources
        /// (the SSR runtime material) that only Dispose frees when no renderer was ever built from the clone.
        /// </summary>
        public static void DestroyDetachedClone(UniversalRenderPipelineAsset clone)
        {
            if (clone == null || !IsRuntimeCopy(clone))
                return;
            var data = RendererData(clone);
            UnityEngine.Object.DestroyImmediate(clone);
            foreach (var d in data)
            {
                if (d == null || !IsRuntimeCopy(d))
                    continue;
                foreach (var feature in d.rendererFeatures)
                {
                    if (feature == null || !IsRuntimeCopy(feature))
                        continue;
                    feature.Dispose();
                    UnityEngine.Object.DestroyImmediate(feature);
                }
                UnityEngine.Object.DestroyImmediate(d);
            }
        }

        static bool IsRuntimeCopy(UnityEngine.Object o) => (o.hideFlags & HideFlags.DontSave) == HideFlags.DontSave;

        public static bool HasFeature(UniversalRenderPipelineAsset asset, string optionId)
        {
            string typeName = optionId == Reflections ? SsrTypeName : optionId == AmbientOcclusion ? SsaoTypeName : null;
            if (typeName == null || asset == null)
                return true;
            foreach (var data in RendererData(asset))
                if (data != null)
                    foreach (var feature in data.rendererFeatures)
                        if (feature != null && feature.GetType().Name == typeName)
                            return true;
            return false;
        }

        public static void ApplyToFeatures(UniversalRenderPipelineAsset clone, IReadOnlyDictionary<string, GraphicsOptionLevelRecord> levels)
        {
            foreach (var data in RendererData(clone))
            {
                if (data == null)
                    continue;
                foreach (var feature in data.rendererFeatures)
                {
                    if (feature == null)
                        continue;
                    string type = feature.GetType().Name;
                    if (type == SsrTypeName && levels.TryGetValue(Reflections, out var ssr))
                        ApplySsr(feature, ssr);
                    else if (type == SsaoTypeName && levels.TryGetValue(AmbientOcclusion, out var ssao))
                        ApplySsao(feature, ssao);
                }
                data.SetDirty();
            }
        }

        static bool FeatureDiffers(ScriptableRendererFeature feature, IReadOnlyDictionary<string, GraphicsOptionLevelRecord> levels)
        {
            string type = feature.GetType().Name;
            if (type == SsrTypeName && levels.TryGetValue(Reflections, out var ssr))
            {
                if (feature.isActive != ssr.ssrEnabled)
                    return true;
                if (!ssr.ssrEnabled)
                    return false;
                return FieldName(feature, "resolution") != ssr.ssrResolution
                    || FieldName(feature, "mipmapsMode") != (ssr.ssrMipmaps ? "Trilinear" : "None");
            }
            if (type == SsaoTypeName && levels.TryGetValue(AmbientOcclusion, out var ssao))
            {
                if (feature.isActive != ssao.ssaoEnabled)
                    return true;
                if (!ssao.ssaoEnabled)
                    return false;
                var settings = feature.GetType().GetField("m_Settings", Instance)?.GetValue(feature);
                if (settings == null)
                    return false;
                return !Equals(SettingsField(settings, "Downsample"), ssao.ssaoDownsample)
                    || SettingsField(settings, "Source")?.ToString() != (ssao.ssaoNormals ? "DepthNormals" : "Depth")
                    || SettingsField(settings, "Samples")?.ToString() != ssao.ssaoSamples;
            }
            return false;
        }

        static void ApplySsr(ScriptableRendererFeature feature, GraphicsOptionLevelRecord level)
        {
            feature.SetActive(level.ssrEnabled);
            if (!level.ssrEnabled)
                return;
            SetEnumField(feature, "resolution", level.ssrResolution);
            SetEnumField(feature, "mipmapsMode", level.ssrMipmaps ? "Trilinear" : "None");
            feature.Create();
        }

        static void ApplySsao(ScriptableRendererFeature feature, GraphicsOptionLevelRecord level)
        {
            feature.SetActive(level.ssaoEnabled);
            if (!level.ssaoEnabled)
                return;
            var settings = feature.GetType().GetField("m_Settings", Instance)?.GetValue(feature);
            if (settings == null)
                return;
            SetSettingsField(settings, "Downsample", level.ssaoDownsample);
            SetSettingsEnum(settings, "Source", level.ssaoNormals ? "DepthNormals" : "Depth");
            SetSettingsEnum(settings, "Samples", level.ssaoSamples);
        }

        static string FieldName(object target, string field) => target.GetType().GetField(field, Instance)?.GetValue(target)?.ToString();

        static void SetEnumField(object target, string field, string value)
        {
            var info = target.GetType().GetField(field, Instance);
            if (info != null && info.FieldType.IsEnum && !string.IsNullOrEmpty(value) && Enum.IsDefined(info.FieldType, value))
                info.SetValue(target, Enum.Parse(info.FieldType, value));
        }

        static object SettingsField(object settings, string field) => settings.GetType().GetField(field, Instance)?.GetValue(settings);

        static void SetSettingsField(object settings, string field, object value) => settings.GetType().GetField(field, Instance)?.SetValue(settings, value);

        static void SetSettingsEnum(object settings, string field, string value) => SetEnumField(settings, field, value);
    }
}
