using System.Collections.Generic;
using System.Linq;
using BuddahGo.Rendering;
using BuddahGo.Rendering.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BuddahGo.Tests
{
    public class GraphicsSettingsTests
    {
        const string DatabasePath = "Assets/Data/Config/ProjectConfigDatabase_Main.asset";

        static ProjectConfigDatabase Database() => AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(DatabasePath);
        static GraphicsSettingsCatalog TableCatalog() => GraphicsSettingsCatalog.FromDatabase(Database());

        static UniversalRenderPipelineAsset QualityPipeline(string level)
        {
            int index = System.Array.IndexOf(QualitySettings.names, level);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "quality level " + level);
            return (QualitySettings.GetRenderPipelineAssetAt(index) ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
        }

        // A two-option catalog independent of the tables, for the model rules.
        static GraphicsSettingsCatalog SmallCatalog()
        {
            var presets = new List<GraphicsPresetRecord>
            {
                new GraphicsPresetRecord { presetId = "low", sortOrder = 0, labelKey = "p.low", qualityLevel = "Performant" },
                new GraphicsPresetRecord { presetId = "high", sortOrder = 1, labelKey = "p.high", qualityLevel = "High Fidelity", isDefault = true },
            };
            var options = new List<GraphicsOptionRecord>
            {
                new GraphicsOptionRecord { optionId = "anti_aliasing", sortOrder = 0, groupId = "quality", labelKey = "o.aa" },
                new GraphicsOptionRecord { optionId = "frame_rate_cap", sortOrder = 1, groupId = "performance", labelKey = "o.fps" },
            };
            var levels = new List<GraphicsOptionLevelRecord>
            {
                new GraphicsOptionLevelRecord { optionId = "anti_aliasing", levelId = "fxaa", sortOrder = 0, defaultFor = new List<string> { "low" }, msaaSamples = 1, postAntialiasing = "FXAA" },
                new GraphicsOptionLevelRecord { optionId = "anti_aliasing", levelId = "taa", sortOrder = 1, msaaSamples = 1, postAntialiasing = "TAA" },
                new GraphicsOptionLevelRecord { optionId = "anti_aliasing", levelId = "msaa8", sortOrder = 2, defaultFor = new List<string> { "high" }, msaaSamples = 8, postAntialiasing = "None" },
                new GraphicsOptionLevelRecord { optionId = "frame_rate_cap", levelId = "fps60", sortOrder = 0, frameRate = 60 },
                new GraphicsOptionLevelRecord { optionId = "frame_rate_cap", levelId = "fps120", sortOrder = 1, defaultFor = new List<string> { "low", "high" }, frameRate = 120 },
            };
            return GraphicsSettingsCatalog.FromRecords(presets, options, levels);
        }

        sealed class MemoryStore : IGraphicsSettingsStore
        {
            public string Json = string.Empty;
            public string Read() => Json;
            public void Write(string json) => Json = json;
            public void Clear() => Json = string.Empty;
        }

        [Test]
        public void TablesDefineACompleteCatalog()
        {
            var catalog = TableCatalog();
            Assert.That(catalog.Errors, Is.Empty, string.Join("\n", catalog.Errors));
            Assert.That(catalog.Presets.Select(p => p.Id), Is.EqualTo(new[] { "low", "medium", "high" }));
            Assert.That(catalog.DefaultPreset.Id, Is.EqualTo("high"));
            Assert.That(catalog.Options.Select(o => o.Id), Is.EquivalentTo(new[]
            {
                "render_scale", "upscaler", "anti_aliasing", "sharpening", "shadow_quality", "texture_quality",
                "anisotropic_filtering", "reflections", "ambient_occlusion", "vfx_quality", "frame_rate_cap",
                "vsync", "window_mode", "resolution",
            }));
            var texts = new HashSet<string>(Database().localizedTexts.Select(t => t.key));
            foreach (var option in catalog.Options)
            {
                Assert.That(texts, Does.Contain(option.LabelKey), option.Id);
                foreach (var level in option.Levels)
                    Assert.That(texts, Does.Contain(level.LabelKey), option.Id + "/" + level.Id);
                foreach (var preset in catalog.Presets)
                    Assert.That(option.Find(preset.DefaultLevel(option.Id)), Is.Not.Null, preset.Id + " default for " + option.Id);
            }
            foreach (var preset in catalog.Presets)
            {
                Assert.That(texts, Does.Contain(preset.LabelKey));
                Assert.That(QualitySettings.names, Does.Contain(preset.QualityLevel));
            }
            Assert.That(texts, Does.Contain(GraphicsSettingsCatalog.CustomLabelKey));
        }

        [Test]
        public void EachPresetDefaultReproducesItsQualityLevel()
        {
            // The defaults are today's values, so the default preset of each level renders with the real asset (no clone).
            var catalog = TableCatalog();
            foreach (var preset in catalog.Presets)
            {
                var model = new GraphicsSettingsModel(catalog);
                model.SetPreset(preset.Id);
                var asset = QualityPipeline(preset.QualityLevel);
                Assert.That(GraphicsPipelineMapper.DiffersFrom(asset, model.Resolve()), Is.False, preset.Id + " vs " + asset.name);
            }
        }

        [Test]
        public void OverridesMakeTheChoiceCustomUntilTheyMatchThePreset()
        {
            var model = new GraphicsSettingsModel(SmallCatalog());
            Assert.That(model.PresetId, Is.EqualTo("high"));
            Assert.That(model.Get("anti_aliasing"), Is.EqualTo("msaa8"));
            Assert.That(model.IsCustom, Is.False);
            Assert.That(model.PresetLabelKey, Is.EqualTo("p.high"));

            int changes = 0;
            model.Changed += () => changes++;
            Assert.That(model.Set("anti_aliasing", "taa"), Is.True);
            Assert.That(model.IsCustom, Is.True);
            Assert.That(model.IsOverridden("anti_aliasing"), Is.True);
            Assert.That(model.PresetLabelKey, Is.EqualTo(GraphicsSettingsCatalog.CustomLabelKey));
            Assert.That(model.Set("anti_aliasing", "msaa8"), Is.True, "back to the preset default");
            Assert.That(model.IsCustom, Is.False);
            Assert.That(changes, Is.EqualTo(2));

            Assert.That(model.Set("anti_aliasing", "smaa"), Is.False, "unknown level");
            Assert.That(model.Set("bloom", "high"), Is.False, "unknown option");
            Assert.That(model.SetPreset("ultra"), Is.False, "unknown preset");

            model.Set("frame_rate_cap", "fps60");
            Assert.That(model.SetPreset("low"), Is.True);
            Assert.That(model.IsCustom, Is.False, "a preset drops overrides");
            Assert.That(model.Get("anti_aliasing"), Is.EqualTo("fxaa"));
            model.Set("frame_rate_cap", "fps60");
            model.ResetToPreset();
            Assert.That(model.Get("frame_rate_cap"), Is.EqualTo("fps120"));
        }

        [Test]
        public void StateRoundTripsAndDropsStaleEntries()
        {
            var catalog = SmallCatalog();
            var model = new GraphicsSettingsModel(catalog);
            model.SetPreset("low");
            model.Set("anti_aliasing", "taa");
            var store = new MemoryStore();
            store.Write(model.ToJson());

            var restored = new GraphicsSettingsModel(catalog);
            Assert.That(restored.TryLoadJson(store.Read()), Is.True);
            Assert.That(restored.PresetId, Is.EqualTo("low"));
            Assert.That(restored.Get("anti_aliasing"), Is.EqualTo("taa"));
            Assert.That(restored.IsCustom, Is.True);

            var stale = new GraphicsSettingsState { presetId = "ultra" };
            stale.overrides.Add(new GraphicsSettingsState.Override { optionId = "bloom", levelId = "high" });
            stale.overrides.Add(new GraphicsSettingsState.Override { optionId = "anti_aliasing", levelId = "smaa" });
            stale.overrides.Add(new GraphicsSettingsState.Override { optionId = "frame_rate_cap", levelId = "fps60" });
            restored.Load(stale);
            Assert.That(restored.PresetId, Is.EqualTo("high"), "unknown preset falls back to the default");
            Assert.That(restored.ToState().overrides.Select(o => o.optionId), Is.EqualTo(new[] { "frame_rate_cap" }));
            Assert.That(restored.TryLoadJson("not json"), Is.False);
        }

        [Test]
        public void PlayerPrefsStoreKeepsTheChoiceUnderItsKey()
        {
            var store = new PlayerPrefsGraphicsSettingsStore("BuddahGo.GraphicsSettings.Test");
            try
            {
                store.Write("{\"presetId\":\"low\"}");
                Assert.That(new PlayerPrefsGraphicsSettingsStore("BuddahGo.GraphicsSettings.Test").Read(), Is.EqualTo("{\"presetId\":\"low\"}"));
                store.Clear();
                Assert.That(store.Read(), Is.Empty);
            }
            finally
            {
                store.Clear();
            }
        }

        [Test]
        public void MappingWritesOnlyTheClone()
        {
            var original = QualityPipeline("High Fidelity");
            var catalog = TableCatalog();
            var model = new GraphicsSettingsModel(catalog);
            model.SetPreset("high");
            model.Set("render_scale", "p67");
            model.Set("anti_aliasing", "taa");
            model.Set("shadow_quality", "medium");
            model.Set("ambient_occlusion", "off");
            var levels = model.Resolve();

            float scale = original.renderScale;
            int msaa = original.msaaSampleCount;
            float distance = original.shadowDistance;
            var originalFeatures = GraphicsPipelineMapper.RendererData(original).Where(d => d != null).SelectMany(d => d.rendererFeatures).Where(f => f != null).ToList();
            var activeBefore = originalFeatures.Select(f => f.isActive).ToList();
            Assert.That(GraphicsPipelineMapper.DiffersFrom(original, levels), Is.True);

            var clone = GraphicsPipelineMapper.CreateDetachedClone(original);
            try
            {
                GraphicsPipelineMapper.ApplyToPipeline(clone, levels);
                GraphicsPipelineMapper.ApplyToFeatures(clone, levels);

                Assert.That(clone.renderScale, Is.EqualTo(0.67f).Within(1e-4f));
                Assert.That(clone.msaaSampleCount, Is.EqualTo(1));
                Assert.That(clone.shadowDistance, Is.EqualTo(150f));
                Assert.That(clone.shadowCascadeCount, Is.EqualTo(2));
                Assert.That(clone.cascade2Split, Is.EqualTo(0.2f).Within(1e-4f));
                Assert.That(clone.mainLightShadowmapResolution, Is.EqualTo(1024));
                var cloneFeatures = GraphicsPipelineMapper.RendererData(clone).Where(d => d != null).SelectMany(d => d.rendererFeatures).Where(f => f != null).ToList();
                Assert.That(cloneFeatures.Where(f => f.GetType().Name == "ScreenSpaceAmbientOcclusion").All(f => !f.isActive), Is.True);
                Assert.That(cloneFeatures.Intersect(originalFeatures), Is.Empty, "features are copies");

                Assert.That(original.renderScale, Is.EqualTo(scale));
                Assert.That(original.msaaSampleCount, Is.EqualTo(msaa));
                Assert.That(original.shadowDistance, Is.EqualTo(distance));
                Assert.That(originalFeatures.Select(f => f.isActive), Is.EqualTo(activeBefore));
                Assert.That(EditorUtility.IsDirty(original), Is.False, "the shared asset must not be touched");
            }
            finally
            {
                GraphicsPipelineMapper.DestroyDetachedClone(clone);
            }
            Assert.That(clone == null, Is.True, "the clone is destroyed");
            Assert.That(originalFeatures.All(f => f != null), Is.True, "releasing a clone leaves the assets alone");
        }

        [Test]
        public void ComparingAPresetKeepsTheActiveRenderPipeline()
        {
            // Creating or destroying any pipeline asset disposes the active render pipeline, so the comparison must only
            // read. A camera render makes sure a pipeline exists (there is none without a graphics device).
            var rt = new RenderTexture(16, 16, 24);
            var go = new GameObject("PipelineProbeCamera");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = null;
                var active = RenderPipelineManager.currentPipeline;
                if (active == null)
                    Assert.Pass("no render pipeline without a graphics device");
                var catalog = TableCatalog();
                string level = QualitySettings.names[QualitySettings.GetQualityLevel()];
                var model = new GraphicsSettingsModel(catalog);
                model.SetPreset(catalog.Presets.First(p => p.QualityLevel == level).Id);
                Assert.That(GraphicsPipelineMapper.DiffersFrom(UniversalRenderPipeline.asset, model.Resolve()), Is.False);
                model.Set("render_scale", "p77");
                Assert.That(GraphicsPipelineMapper.DiffersFrom(UniversalRenderPipeline.asset, model.Resolve()), Is.True);
                Assert.That(active.disposed, Is.False, "the comparison disposed the active pipeline");
            }
            finally
            {
                Object.DestroyImmediate(go);
                rt.Release();
            }
        }

        [Test]
        public void ApplyingAgainKeepsAMatchingClone()
        {
            var catalog = TableCatalog();
            int startLevel = QualitySettings.GetQualityLevel();
            var before = QualityLevelValues();
            int frameRate = Application.targetFrameRate;
            var vfx = VfxQuality.Current;
            var applier = new GraphicsSettingsApplier();
            try
            {
                var model = new GraphicsSettingsModel(catalog);
                model.SetPreset("high");
                model.Set("render_scale", "p77");
                applier.Apply(model);
                var clone = QualitySettings.renderPipeline;
                model.Set("texture_quality", "half");
                applier.Apply(model);
                Assert.That(QualitySettings.renderPipeline, Is.SameAs(clone), "an option outside the pipeline must not rebuild it");
                model.Set("render_scale", "p85");
                applier.Apply(model);
                Assert.That(QualitySettings.renderPipeline, Is.Not.SameAs(clone));
                Assert.That(((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).renderScale, Is.EqualTo(0.85f).Within(1e-4f));
            }
            finally
            {
                applier.RestoreOriginals();
                Application.targetFrameRate = frameRate;
                VfxQuality.Set(vfx);
            }
            Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(startLevel));
            Assert.That(QualityLevelValues(), Is.EqualTo(before));
        }

        // Per quality level: pipeline, texture limit, anisotropic filtering and VSync.
        static List<string> QualityLevelValues()
        {
            int current = QualitySettings.GetQualityLevel();
            var values = new List<string>();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                var pipeline = QualitySettings.renderPipeline;
                values.Add(QualitySettings.names[i] + ": " + (pipeline != null ? pipeline.name + "#" + pipeline.GetInstanceID() : "default")
                    + ", mip " + QualitySettings.globalTextureMipmapLimit + ", " + QualitySettings.anisotropicFiltering + ", vSync " + QualitySettings.vSyncCount);
            }
            QualitySettings.SetQualityLevel(current, false);
            return values;
        }

        [Test]
        public void RestorePutsBackEveryQualityLevelItChanged()
        {
            // QualitySettings changes made in play mode outlive it in the editor, so each level must get its own values back.
            var catalog = TableCatalog();
            int startLevel = QualitySettings.GetQualityLevel();
            var before = QualityLevelValues();
            var performant = QualityPipeline("Performant");
            var balanced = QualityPipeline("Balanced");
            var highFidelity = QualityPipeline("High Fidelity");
            int frameRate = Application.targetFrameRate;
            var vfx = VfxQuality.Current;
            var applier = new GraphicsSettingsApplier();
            UniversalRenderPipelineAsset custom = null;
            try
            {
                var model = new GraphicsSettingsModel(catalog);
                model.SetPreset("low");
                model.Set("anisotropic_filtering", "forced");
                model.Set("texture_quality", "half");
                model.Set("render_scale", "p77");
                applier.Apply(model);
                Assert.That(QualitySettings.names[QualitySettings.GetQualityLevel()], Is.EqualTo("Performant"));
                Assert.That(QualitySettings.anisotropicFiltering, Is.EqualTo(AnisotropicFiltering.ForceEnable));
                Assert.That(QualitySettings.globalTextureMipmapLimit, Is.EqualTo(1));
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1), "VSync is on by default");
                custom = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
                Assert.That(custom, Is.Not.Null.And.Not.EqualTo(performant), "a custom choice renders with a clone");
                Assert.That(custom.renderScale, Is.EqualTo(0.77f).Within(1e-4f));

                model.SetPreset("medium");
                model.Set("texture_quality", "quarter");
                applier.Apply(model);
                Assert.That(QualitySettings.renderPipeline, Is.EqualTo(balanced), "texture quality needs no clone");
                model.SetPreset("high");
                applier.Apply(model);
                Assert.That(QualitySettings.renderPipeline, Is.EqualTo(highFidelity), "a preset without overrides uses the real asset");
            }
            finally
            {
                applier.RestoreOriginals();
                Application.targetFrameRate = frameRate;
                VfxQuality.Set(vfx);
            }
            Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(startLevel));
            Assert.That(QualityLevelValues(), Is.EqualTo(before));
            Assert.That(custom == null, Is.True, "restore destroys the clones");
        }

        [Test]
        public void VSyncLevelSetsVSyncAndTheCapOnlyAppliesWithoutIt()
        {
            var catalog = TableCatalog();
            int startLevel = QualitySettings.GetQualityLevel();
            var before = QualityLevelValues();
            int frameRate = Application.targetFrameRate;
            var vfx = VfxQuality.Current;
            var applier = new GraphicsSettingsApplier();
            try
            {
                var model = new GraphicsSettingsModel(catalog);
                model.SetPreset("high");
                Application.targetFrameRate = 30;
                applier.Apply(model);
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1));
                Assert.That(Application.targetFrameRate, Is.EqualTo(30), "with VSync the cap is left alone");

                model.Set("vsync", "off");
                model.Set("frame_rate_cap", "fps60");
                applier.Apply(model);
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0));
                Assert.That(Application.targetFrameRate, Is.EqualTo(60));
            }
            finally
            {
                applier.RestoreOriginals();
                Application.targetFrameRate = frameRate;
                VfxQuality.Set(vfx);
            }
            Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(startLevel));
            Assert.That(QualityLevelValues(), Is.EqualTo(before));
        }

        [Test]
        public void ACommandLineWindowKeepsTheSettingsOffTheWindow()
        {
            Assert.That(GraphicsSettingsApplier.SetsWindowFromCommandLine(new[] { "BuddahGo.exe", "-screen-width", "1280", "-screen-height", "720" }), Is.True);
            Assert.That(GraphicsSettingsApplier.SetsWindowFromCommandLine(new[] { "BuddahGo.exe", "-Window-Mode", "borderless" }), Is.True);
            Assert.That(GraphicsSettingsApplier.SetsWindowFromCommandLine(new[] { "BuddahGo.exe", "-logFile", "x.log" }), Is.False);
            Assert.That(GraphicsSettingsApplier.SetsWindowFromCommandLine(null), Is.False);
        }

        [Test]
        public void RevertWithoutASavedChoiceReturnsToTheStartupChoice()
        {
            var store = new MemoryStore();
            try
            {
                GraphicsSettingsService.Initialize(SmallCatalog(), store, null, apply: false);
                string startup = GraphicsSettingsService.PresetId;
                GraphicsSettingsService.SetPreset(startup == "low" ? "high" : "low");
                GraphicsSettingsService.Set("frame_rate_cap", "fps60");
                GraphicsSettingsService.Revert();
                Assert.That(GraphicsSettingsService.PresetId, Is.EqualTo(startup));
                Assert.That(GraphicsSettingsService.IsCustom, Is.False);
                Assert.That(store.Json, Is.Empty, "Revert does not save");
            }
            finally
            {
                GraphicsSettingsService.Initialize(null, null, null, apply: false);
            }
        }

        [Test]
        public void OcclusionNeedsItsFeatureAndReflectionsAreAlwaysAvailable()
        {
            var catalog = TableCatalog();
            var low = catalog.FindPreset("low");
            var high = catalog.FindPreset("high");
            var reflectionsHigh = catalog.FindOption("reflections").Find("high").Values;
            var aoHigh = catalog.FindOption("ambient_occlusion").Find("high").Values;
            var aoOff = catalog.FindOption("ambient_occlusion").Find("off").Values;
            Assert.That(GraphicsSettingsApplier.IsLevelAvailable(low, "ambient_occlusion", aoHigh), Is.False, "Performant's renderer has no SSAO");
            Assert.That(GraphicsSettingsApplier.IsLevelAvailable(low, "ambient_occlusion", aoOff), Is.True);
            Assert.That(GraphicsSettingsApplier.IsLevelAvailable(high, "ambient_occlusion", aoHigh), Is.True);
            Assert.That(GraphicsSettingsApplier.IsLevelAvailable(low, "reflections", reflectionsHigh), Is.True, "planar reflections need no renderer feature");
        }

        [Test]
        public void ReflectionLevelsSetThePlanarReflectionScale()
        {
            var catalog = TableCatalog();
            Assert.That(catalog.FindOption("reflections").Levels.Select(l => l.Values.planarScale), Is.EqualTo(new[] { 0f, 0.5f, 1f }));
            int startLevel = QualitySettings.GetQualityLevel();
            var before = QualityLevelValues();
            int frameRate = Application.targetFrameRate;
            var vfx = VfxQuality.Current;
            var applier = new GraphicsSettingsApplier();
            try
            {
                var model = new GraphicsSettingsModel(catalog);
                model.SetPreset("medium");
                applier.Apply(model);
                Assert.That(PlanarReflection.SettingsScale, Is.EqualTo(0.5f));
                model.Set("reflections", "off");
                applier.Apply(model);
                Assert.That(PlanarReflection.SettingsScale, Is.EqualTo(0f));
            }
            finally
            {
                applier.RestoreOriginals();
                Application.targetFrameRate = frameRate;
                VfxQuality.Set(vfx);
            }
            Assert.That(PlanarReflection.SettingsScale, Is.Null, "restore hands the scale back to the components");
            Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(startLevel));
            Assert.That(QualityLevelValues(), Is.EqualTo(before));
        }

        [Test]
        public void PolicyNeverPutsTaaOnTopOfMsaaButAllowsFxaa()
        {
            var go = new GameObject("PolicyTestCamera");
            try
            {
                go.AddComponent<Camera>();
                go.AddComponent<UniversalAdditionalCameraData>();
                var policy = go.AddComponent<CameraAntialiasingPolicy>();
                CameraAntialiasingPolicy.SettingsMode = AntialiasingMode.TemporalAntiAliasing;
                bool msaa = UniversalRenderPipeline.asset != null && UniversalRenderPipeline.asset.msaaSampleCount > 1;
                Assert.That(policy.Resolve(), Is.EqualTo(msaa ? AntialiasingMode.None : AntialiasingMode.TemporalAntiAliasing));
                CameraAntialiasingPolicy.SettingsMode = AntialiasingMode.FastApproximateAntialiasing;
                Assert.That(policy.Resolve(), Is.EqualTo(AntialiasingMode.FastApproximateAntialiasing), "FXAA may run on top of MSAA");
                CameraAntialiasingPolicy.SettingsMode = AntialiasingMode.TemporalAntiAliasing;
                go.GetComponent<Camera>().allowMSAA = false;
                Assert.That(policy.Resolve(), Is.EqualTo(AntialiasingMode.TemporalAntiAliasing), "a camera without MSAA takes the settings mode");
                Assert.That(GraphicsSettingsApplier.ParsePostAntialiasing("FXAA"), Is.EqualTo(AntialiasingMode.FastApproximateAntialiasing));
                Assert.That(GraphicsSettingsApplier.ParsePostAntialiasing("None"), Is.EqualTo(AntialiasingMode.None));
            }
            finally
            {
                CameraAntialiasingPolicy.SettingsMode = null;
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void VfxQualityBroadcastsChanges()
        {
            var before = VfxQuality.Current;
            var seen = new List<VfxQualityTier>();
            System.Action<VfxQualityTier> handler = seen.Add;
            VfxQuality.Changed += handler;
            try
            {
                VfxQuality.Set(VfxQualityTier.Low);
                VfxQuality.Set(VfxQualityTier.Low);
                VfxQuality.Set(VfxQualityTier.Medium);
                Assert.That(seen, Is.EqualTo(before == VfxQualityTier.Low ? new[] { VfxQualityTier.Medium } : new[] { VfxQualityTier.Low, VfxQualityTier.Medium }));
            }
            finally
            {
                VfxQuality.Changed -= handler;
                VfxQuality.Set(before);
            }
        }
    }
}
