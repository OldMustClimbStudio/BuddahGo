using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BuddahGo.Rendering.Settings
{
    // Applies resolved graphics levels at runtime without writing any asset:
    // - the preset's quality level (QualitySettings.SetQualityLevel);
    // - pipeline and renderer-feature levels on runtime clones of that level's URP asset, assigned through
    //   QualitySettings.renderPipeline only when they differ from the asset (the default preset uses the real assets);
    // - post AA and TAA sharpening through CameraAntialiasingPolicy, texture mip limit and anisotropic filtering through
    //   QualitySettings, the frame-rate cap (VSync stays off, see FrameRateLock) and the VFX tier.
    // Pipeline, texture limit, anisotropic filtering and VSync belong to the current quality level, so what each level held
    // is recorded before the first write to it. In the editor those writes would outlive play mode; they are put back
    // when the application quits (which includes leaving play mode), or after play mode if a recompile dropped that hook.
    public sealed class GraphicsSettingsApplier
    {
        sealed class LevelOriginal
        {
            public RenderPipelineAsset Pipeline;
            public int TextureLimit;
            public AnisotropicFiltering Anisotropic;
            public int VSync;

            public static LevelOriginal Capture() => new LevelOriginal
            {
                Pipeline = QualitySettings.renderPipeline,
                TextureLimit = QualitySettings.globalTextureMipmapLimit,
                Anisotropic = QualitySettings.anisotropicFiltering,
                VSync = QualitySettings.vSyncCount,
            };

            // Writes to the current quality level.
            public void WriteBack()
            {
                if (QualitySettings.renderPipeline != Pipeline)
                    QualitySettings.renderPipeline = Pipeline;
                QualitySettings.globalTextureMipmapLimit = TextureLimit;
                QualitySettings.anisotropicFiltering = Anisotropic;
                QualitySettings.vSyncCount = VSync;
            }
        }

        readonly Dictionary<int, LevelOriginal> m_Originals = new Dictionary<int, LevelOriginal>();
        readonly HashSet<UniversalRenderPipelineAsset> m_Clones = new HashSet<UniversalRenderPipelineAsset>();
        int m_OriginalQualityLevel = -1;
        bool m_RestoreHooked;

        /// <summary>Whether a level can take effect with the given preset (SSR and SSAO need the feature in that level's renderer).</summary>
        public static bool IsLevelAvailable(GraphicsSettingsCatalog.Preset preset, string optionId, GraphicsOptionLevelRecord level)
        {
            if (level == null)
                return false;
            bool needsFeature = (optionId == GraphicsPipelineMapper.Reflections && level.ssrEnabled)
                || (optionId == GraphicsPipelineMapper.AmbientOcclusion && level.ssaoEnabled);
            if (!needsFeature)
                return true;
            int quality = QualityIndex(preset);
            if (quality < 0)
                return false;
            var pipeline = (QualitySettings.GetRenderPipelineAssetAt(quality) ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            return GraphicsPipelineMapper.HasFeature(pipeline, optionId);
        }

        public void Apply(GraphicsSettingsModel model)
        {
            var preset = model.Preset;
            if (preset == null)
                return;
            HookRestore();
            var levels = model.Resolve();

            int quality = QualityIndex(preset);
            if (quality >= 0 && QualitySettings.GetQualityLevel() != quality)
            {
                if (m_OriginalQualityLevel < 0)
                    m_OriginalQualityLevel = QualitySettings.GetQualityLevel();
                QualitySettings.SetQualityLevel(quality, true);
            }
            var original = RecordLevel(QualitySettings.GetQualityLevel());
            ApplyPipeline(original, levels);

            if (levels.TryGetValue(GraphicsPipelineMapper.AntiAliasing, out var aa))
                CameraAntialiasingPolicy.SettingsMode = ParsePostAntialiasing(aa.postAntialiasing);
            CameraAntialiasingPolicy.SettingsSharpening = levels.TryGetValue(GraphicsPipelineMapper.Sharpening, out var sharpen) ? sharpen.taaSharpening : (float?)null;

            if (levels.TryGetValue(GraphicsPipelineMapper.TextureQuality, out var textures))
                QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(textures.textureMipmapLimit, 0, 3);
            if (levels.TryGetValue(GraphicsPipelineMapper.AnisotropicFiltering, out var aniso) && Enum.TryParse(aniso.anisotropicFiltering, true, out AnisotropicFiltering filtering))
                QualitySettings.anisotropicFiltering = filtering;

            // SetQualityLevel re-applies the level's VSync; prediction smoothing needs VSync off and a tick-multiple cap.
            QualitySettings.vSyncCount = 0;
            if (levels.TryGetValue(GraphicsPipelineMapper.FrameRateCap, out var frameRate) && frameRate.frameRate > 0)
                Application.targetFrameRate = frameRate.frameRate;

            if (levels.TryGetValue(GraphicsPipelineMapper.VfxQuality, out var vfx) && Enum.TryParse(vfx.vfxTier, true, out VfxQualityTier tier))
                VfxQuality.Set(tier);
        }

        LevelOriginal RecordLevel(int quality)
        {
            if (!m_Originals.TryGetValue(quality, out var original))
            {
                original = LevelOriginal.Capture();
                m_Originals[quality] = original;
                SaveForEditor();
            }
            return original;
        }

        void ApplyPipeline(LevelOriginal original, IReadOnlyDictionary<string, GraphicsOptionLevelRecord> levels)
        {
            var asset = (original.Pipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (asset == null)
                return;
            RenderPipelineAsset target = original.Pipeline;
            if (GraphicsPipelineMapper.DiffersFrom(asset, levels))
            {
                var clone = GraphicsPipelineMapper.CreateDetachedClone(asset);
                m_Clones.Add(clone);
                GraphicsPipelineMapper.ApplyToPipeline(clone, levels);
                GraphicsPipelineMapper.ApplyToFeatures(clone, levels);
                target = clone;
            }
            if (QualitySettings.renderPipeline != target)
            {
                var previous = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
                QualitySettings.renderPipeline = target;
                if (previous != null && m_Clones.Remove(previous))
                    GraphicsPipelineMapper.DestroyDetachedClone(previous);
            }
        }

        /// <summary>Puts back every quality level and QualitySettings value this applier changed, and destroys its clones.</summary>
        public void RestoreOriginals()
        {
            Application.quitting -= RestoreOriginals;
            m_RestoreHooked = false;
            int back = m_OriginalQualityLevel >= 0 ? m_OriginalQualityLevel : QualitySettings.GetQualityLevel();
            foreach (var kv in m_Originals)
            {
                QualitySettings.SetQualityLevel(kv.Key, false);
                kv.Value.WriteBack();
            }
            if (m_Originals.Count > 0 || m_OriginalQualityLevel >= 0)
                QualitySettings.SetQualityLevel(back, false);
            m_Originals.Clear();
            m_OriginalQualityLevel = -1;
            CameraAntialiasingPolicy.SettingsMode = null;
            CameraAntialiasingPolicy.SettingsSharpening = null;
            foreach (var clone in m_Clones)
                GraphicsPipelineMapper.DestroyDetachedClone(clone);
            m_Clones.Clear();
            EraseSaved();
        }

        void HookRestore()
        {
            if (m_RestoreHooked)
                return;
            m_RestoreHooked = true;
            Application.quitting += RestoreOriginals;
        }

        static int QualityIndex(GraphicsSettingsCatalog.Preset preset)
        {
            if (preset == null || string.IsNullOrEmpty(preset.QualityLevel))
                return -1;
            return Array.IndexOf(QualitySettings.names, preset.QualityLevel);
        }

        public static AntialiasingMode ParsePostAntialiasing(string value)
        {
            switch ((value ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "FXAA": return AntialiasingMode.FastApproximateAntialiasing;
                case "SMAA": return AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                case "TAA": return AntialiasingMode.TemporalAntiAliasing;
                default: return AntialiasingMode.None;
            }
        }

#if UNITY_EDITOR
        // A recompile during play mode drops the Application.quitting subscription with the old domain. This record
        // survives it, and RestoreAfterPlayMode puts the levels back once the editor is in edit mode again.
        const string SessionKey = "BuddahGo.GraphicsSettings.Originals";

        [Serializable]
        sealed class SavedLevel
        {
            public int level;
            public string pipelineGuid;
            public int textureLimit;
            public int anisotropic;
            public int vSync;
        }

        [Serializable]
        sealed class SavedOriginals
        {
            public int qualityLevel = -1;
            public List<SavedLevel> levels = new List<SavedLevel>();
        }

        void SaveForEditor()
        {
            var saved = new SavedOriginals { qualityLevel = m_OriginalQualityLevel };
            foreach (var kv in m_Originals)
                saved.levels.Add(new SavedLevel
                {
                    level = kv.Key,
                    pipelineGuid = kv.Value.Pipeline == null ? string.Empty : UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(kv.Value.Pipeline)),
                    textureLimit = kv.Value.TextureLimit,
                    anisotropic = (int)kv.Value.Anisotropic,
                    vSync = kv.Value.VSync,
                });
            UnityEditor.SessionState.SetString(SessionKey, JsonUtility.ToJson(saved));
        }

        static void EraseSaved() => UnityEditor.SessionState.EraseString(SessionKey);

        [UnityEditor.InitializeOnLoadMethod]
        static void HookPlayModeExit()
        {
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.EnteredEditMode)
                    RestoreAfterPlayMode();
            };
        }

        static void RestoreAfterPlayMode()
        {
            string json = UnityEditor.SessionState.GetString(SessionKey, string.Empty);
            EraseSaved();
            var saved = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SavedOriginals>(json);
            if (saved == null || saved.levels.Count == 0)
                return;
            int back = saved.qualityLevel >= 0 ? saved.qualityLevel : QualitySettings.GetQualityLevel();
            foreach (var level in saved.levels)
            {
                QualitySettings.SetQualityLevel(level.level, false);
                var left = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
                new LevelOriginal
                {
                    Pipeline = string.IsNullOrEmpty(level.pipelineGuid) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(level.pipelineGuid)),
                    TextureLimit = level.textureLimit,
                    Anisotropic = (AnisotropicFiltering)level.anisotropic,
                    VSync = level.vSync,
                }.WriteBack();
                if (left != null && left != QualitySettings.renderPipeline)
                    GraphicsPipelineMapper.DestroyDetachedClone(left);
            }
            QualitySettings.SetQualityLevel(back, false);
            Debug.Log("[GraphicsSettings] Restored the quality levels changed during play mode (the play session ended without its quit callback).");
        }
#else
        void SaveForEditor() { }

        static void EraseSaved() { }
#endif
    }
}
