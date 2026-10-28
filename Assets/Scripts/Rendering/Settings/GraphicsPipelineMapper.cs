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
    // SSAO feature is internal; those are set through their serialized fields. Reflections are planar reflections
    // (PlanarReflection), which the applier sets directly; they need no pipeline change.
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
        public const string VSync = "vsync";
        public const string WindowMode = "window_mode";
        public const string Resolution = "resolution";

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

        // The pipeline-asset fields the options write. Comparisons read them straight from the asset: creating or
        // destroying any RenderPipelineAsset, even a throwaway copy, disposes the active render pipeline
        // (RenderPipelineAsset.OnDisable cleans it up), which a comparison must not do.
        struct PipelineValues
        {
            public float RenderScale;
            public UpscalingFilterSelection Upscaling;
            public int Msaa;
            public bool MainLightShadows;
            public float ShadowDistance;
            public int Cascades;
            public object Soft;
            public object ShadowResolution;
            public object Split2, Split3, Split4;
        }

        static PipelineValues Read(UniversalRenderPipelineAsset asset) => new PipelineValues
        {
            RenderScale = asset.renderScale,
            Upscaling = asset.upscalingFilter,
            Msaa = asset.msaaSampleCount,
            MainLightShadows = (bool)(MainLightShadowsField?.GetValue(asset) ?? true),
            ShadowDistance = asset.shadowDistance,
            Cascades = asset.shadowCascadeCount,
            Soft = SoftShadowsField?.GetValue(asset),
            ShadowResolution = ShadowResolutionField?.GetValue(asset),
            Split2 = Cascade2Field?.GetValue(asset),
            Split3 = Cascade3Field?.GetValue(asset),
            Split4 = Cascade4Field?.GetValue(asset),
        };

        // The asset's values with the levels laid over them, clamped as URP's setters would.
        static PipelineValues Resolve(UniversalRenderPipelineAsset asset, IReadOnlyDictionary<string, GraphicsOptionLevelRecord> levels)
        {
            var values = Read(asset);
            if (levels.TryGetValue(RenderScale, out var scale))
                values.RenderScale = Mathf.Clamp(scale.renderScale, 0.1f, 2f);
            if (levels.TryGetValue(Upscaler, out var upscaler) && Enum.TryParse(upscaler.upscalingFilter, true, out UpscalingFilterSelection filter))
                values.Upscaling = filter;
            if (levels.TryGetValue(AntiAliasing, out var aa) && aa.msaaSamples >= 1)
                values.Msaa = aa.msaaSamples;
            if (levels.TryGetValue(ShadowQuality, out var shadows))
                ResolveShadows(ref values, shadows);
            return values;
        }

        static void ResolveShadows(ref PipelineValues values, GraphicsOptionLevelRecord level)
        {
            values.MainLightShadows = level.shadowsEnabled;
            if (!level.shadowsEnabled)
                return;
            values.ShadowDistance = Mathf.Max(0f, level.shadowDistance);
            values.Cascades = Mathf.Clamp(level.shadowCascadeCount, 1, 4);
            values.Soft = level.softShadows;
            if (ShadowResolutionField != null && level.shadowResolution > 0)
                values.ShadowResolution = Enum.ToObject(ShadowResolutionField.FieldType, level.shadowResolution);
            // Splits are authored in metres; URP stores them as fractions of the shadow distance.
            var splits = level.shadowCascadeSplitsMeters ?? new List<float>();
            float distance = Mathf.Max(0.01f, level.shadowDistance);
            float Fraction(int i) => i < splits.Count ? Mathf.Clamp01(splits[i] / distance) : 0f;
            if (values.Cascades == 2 && splits.Count >= 1)
                values.Split2 = Fraction(0);
            else if (values.Cascades == 3 && splits.Count >= 2)
                values.Split3 = new Vector2(Fraction(0), Fraction(1));
            else if (values.Cascades == 4 && splits.Count >= 3)
                values.Split4 = new Vector3(Fraction(0), Fraction(1), Fraction(2));
        }

        static void Write(UniversalRenderPipelineAsset asset, PipelineValues values)
        {
            asset.renderScale = values.RenderScale;
            asset.upscalingFilter = values.Upscaling;
            asset.msaaSampleCount = values.Msaa;
            MainLightShadowsField?.SetValue(asset, values.MainLightShadows);
            if (!values.MainLightShadows)
                return;
            asset.shadowDistance = values.ShadowDistance;
            asset.shadowCascadeCount = values.Cascades;
            SetIfRead(SoftShadowsField, asset, values.Soft);
            SetIfRead(ShadowResolutionField, asset, values.ShadowResolution);
            SetIfRead(Cascade2Field, asset, values.Split2);
            SetIfRead(Cascade3Field, asset, values.Split3);
            SetIfRead(Cascade4Field, asset, values.Split4);
        }

        static void SetIfRead(FieldInfo field, object target, object value)
        {
            if (field != null && value != null)
                field.SetValue(target, value);
        }

        static bool Same(PipelineValues a, PipelineValues b)
        {
            if (!Mathf.Approximately(a.RenderScale, b.RenderScale) || a.Upscaling != b.Upscaling || a.Msaa != b.Msaa
                || a.MainLightShadows != b.MainLightShadows)
                return false;
            if (!a.MainLightShadows)
                return true;
            return Mathf.Approximately(a.ShadowDistance, b.ShadowDistance) && a.Cascades == b.Cascades
                && Equals(a.Soft, b.Soft) && Equals(a.ShadowResolution, b.ShadowResolution) && SplitsMatch(a, b);
        }

        static bool SplitsMatch(PipelineValues a, PipelineValues b)
        {
            switch (a.Cascades)
            {
                case 2: return a.Split2 == null || b.Split2 == null || Mathf.Abs((float)a.Split2 - (float)b.Split2) < 1e-3f;
                case 3: return a.Split3 == null || b.Split3 == null || ((Vector2)a.Split3 - (Vector2)b.Split3).sqrMagnitude < 1e-6f;
                case 4: return a.Split4 == null || b.Split4 == null || ((Vector3)a.Split4 - (Vector3)b.Split4).sqrMagnitude < 1e-6f;
                default: return true;
            }
        }

        /// <summary>
        /// True when applying <paramref name="levels"/> would change anything on <paramref name="asset"/> or its features.
        /// Reads only; it never creates or destroys an asset, so the active render pipeline survives the comparison.
        /// </summary>
        public static bool DiffersFrom(UniversalRenderPipelineAsset asset, IReadOnlyDictionary<string, GraphicsOptionLevelRecord> levels)
        {
            if (!Same(Read(asset), Resolve(asset, levels)))
                return true;
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
            Write(asset, Resolve(asset, levels));
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
            string typeName = optionId == AmbientOcclusion ? SsaoTypeName : null;
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
                    if (feature.GetType().Name == SsaoTypeName && levels.TryGetValue(AmbientOcclusion, out var ssao))
                        ApplySsao(feature, ssao);
                }
                data.SetDirty();
            }
        }

        static bool FeatureDiffers(ScriptableRendererFeature feature, IReadOnlyDictionary<string, GraphicsOptionLevelRecord> levels)
        {
            if (feature.GetType().Name == SsaoTypeName && levels.TryGetValue(AmbientOcclusion, out var ssao))
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
