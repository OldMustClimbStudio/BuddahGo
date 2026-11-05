// The one scripted way to bake RaceMap and the menu environment the way the repository records it
// (Docs/rendering/ARCHITECTURE.md, section "Bake"). It holds the recorded shared environment (sky, ambient, fog,
// reflections, lighting-settings asset), checks every other bake input against what is recorded, bakes, re-renders the
// custom reflection probes, saves both scenes and writes Docs/rendering/bake-manifest.json (inputs hash, output files,
// machine, lightmapper messages).
//
//   BuddahGo/Rendering/Bake/Verify Recorded Lighting   -> RenderingBake.Verify()    (opens the scenes, changes nothing)
//   BuddahGo/Rendering/Bake/Bake RaceMap + Menu         -> RenderingBake.BakeAll()   (apply, check, bake, save, manifest)
//   ... + Regenerate Light Probes                       -> RenderingBake.BakeAll(true, false)
//   Batch mode: -executeMethod BuddahGo.Rendering.Editor.RenderingBake.BakeAllBatch (exit code 0 = baked)
//
// Re-tuning a value recorded here: change it here first, then bake; the scene copy is written from these constants, so
// an LDR colour-picker edit (which clamps the fog's red channel above 1) cannot slip into a bake.
// MenuEnvironmentImporter calls ApplyEnvironment(), so the menu importer and the bake share these numbers.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace BuddahGo.Rendering.Editor
{
    public static class RenderingBake
    {
        public const string RaceMapScenePath = "Assets/Scenes/RaceMap.unity";
        public const string MenuScenePath = "Assets/Scenes/MenuEnvironmentSetting.unity";
        public const string LightingSettingsPath = "Assets/Scenes/RaceMap/RaceMap_Lighting.lighting";
        public const string LightmapParametersPath = "Assets/Scenes/RaceMap/RaceMap_LightmapParameters.giparams";
        public const string SkyPath = "Assets/Scenes/RaceMap/RaceMap_Sky_Dusk.mat";
        public const string ManifestPath = "Docs/rendering/bake-manifest.json";

        // ---- Recorded shared environment (RaceMap and MenuEnvironmentSetting). Dusk C2 + bake "D", 2026-11-03. ----
        public static readonly Color AmbientSky = new Color(0.50f, 0.62f, 0.90f);
        public static readonly Color AmbientEquator = new Color(0.50f, 0.50f, 0.52f);
        public static readonly Color AmbientGround = new Color(0.18f, 0.16f, 0.16f);
        public const float AmbientIntensity = 1.2f;   // stored by Unity; Trilight ignores it
        public const int ReflectionResolution = 256;
        public const int ReflectionBounces = 2;
        public const float ReflectionIntensity = 1f;
        public const float FogDensity = 0.0009f;
        // Inspector (gamma) value, red above 1 on purpose: calibrated against the sky (dusk-color-study.md section 11).
        public static readonly Color FogColor = new Color(1.263f, 0.967f, 0.612f);
        public static readonly Color SubtractiveShadowColor = new Color(0.1792453f, 0.1792453f, 0.1792453f);

        // ---- Recorded RaceMap sun (checked, not written: it is a scene object). ----
        public const string RaceMapSunName = "MainLight";
        public static readonly Vector3 RaceMapSunEuler = new Vector3(19f, 66f, 0f);
        public static readonly Color RaceMapSunFilter = new Color(1f, 0.92f, 0.82f);
        public const float RaceMapSunTemperature = 2900f;
        public const float RaceMapSunIntensity = 3.6f;

        // ---- Custom reflection probes: GameObject name -> cubemap file it is rendered into after the GI bake. ----
        static readonly (string probe, string file)[] RaceMapProbes =
        {
            ("Reflection Probe Lower", "Assets/Scenes/RaceMap/ReflectionProbe-Lower.exr"),
            ("Reflection Probe EndField", "Assets/Scenes/RaceMap/ReflectionProbe-EndField.exr"),
        };
        static readonly (string probe, string file)[] MenuProbes =
        {
            ("Reflection Probe Shrine", "Assets/Scenes/MenuEnvironmentSetting/ReflectionProbe-Shrine.exr"),
        };

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---- Lighting-settings asset: (field, actual, recorded). The asset file itself is the source; this guards it. ----
        static IEnumerable<(string name, object actual, object expected)> LightingSettingsChecks(LightingSettings s)
        {
            yield return ("bakedGI", s.bakedGI, true);
            yield return ("realtimeGI", s.realtimeGI, false);
            yield return ("realtimeEnvironmentLighting", s.realtimeEnvironmentLighting, true);
            yield return ("autoGenerate", s.autoGenerate, false);
            yield return ("lightmapper", s.lightmapper, LightingSettings.Lightmapper.ProgressiveGPU);
            yield return ("mixedBakeMode", s.mixedBakeMode, MixedLightingMode.IndirectOnly);
            yield return ("directSampleCount", s.directSampleCount, 32);
            yield return ("indirectSampleCount", s.indirectSampleCount, 256);
            yield return ("environmentSampleCount", s.environmentSampleCount, 128);
            yield return ("maxBounces", s.maxBounces, 2);
            yield return ("lightProbeSampleCountMultiplier", s.lightProbeSampleCountMultiplier, 8f);
            yield return ("lightmapResolution", s.lightmapResolution, 8f);
            yield return ("lightmapMaxSize", s.lightmapMaxSize, 2048);
            yield return ("lightmapPadding", s.lightmapPadding, 2);
            yield return ("ao", s.ao, false);
            yield return ("albedoBoost", s.albedoBoost, 1f);
            yield return ("indirectScale", s.indirectScale, 1f);
            // No public property for the asset's lightmap parameters in 2022.3; read the serialized field.
            var parameters = new SerializedObject(s).FindProperty("m_LightmapParameters");
            yield return ("lightmapParameters", parameters == null ? "(none)" : AssetDatabase.GetAssetPath(parameters.objectReferenceValue), LightmapParametersPath);
        }

        // ------------------------------------------------------------------------------------------------ entry points

        [MenuItem("BuddahGo/Rendering/Bake/Verify Recorded Lighting")]
        static void VerifyMenu() => Report(Verify());

        [MenuItem("BuddahGo/Rendering/Bake/Bake RaceMap + Menu")]
        static void BakeMenu() => Report(BakeAll(false, false));

        [MenuItem("BuddahGo/Rendering/Bake/Bake RaceMap + Menu (Regenerate Light Probes)")]
        static void BakeRegenerateMenu() => Report(BakeAll(true, false));

        /// <summary>Opens both scenes, compares every recorded input with what is there, changes nothing.</summary>
        public static string Verify()
        {
            if (!ScenesClean(out string why)) return "NOT RUN: " + why;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var sb = new StringBuilder();
            try
            {
                EditorSceneManager.OpenScene(RaceMapScenePath, OpenSceneMode.Single);
                var raceProblems = EnvironmentProblems().Concat(LightingSettingsProblems()).Concat(RaceMapSunProblems())
                    .Concat(ProbeProblems(RaceMapProbes)).ToList();
                AppendSection(sb, "RaceMap", raceProblems);
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
                var menuProblems = EnvironmentProblems().Concat(ProbeProblems(MenuProbes)).ToList();
                AppendSection(sb, "MenuEnvironmentSetting", menuProblems);
                sb.Insert(0, raceProblems.Count + menuProblems.Count == 0 ? "OK: every recorded input matches.\n" : "MISMATCH:\n");
            }
            finally { RestoreScenes(setup); }
            return sb.ToString();
        }

        public static string BakeAll() => BakeAll(false, false);

        /// <summary>
        /// Writes the recorded environment into both scenes, refuses to bake when another input differs from the record
        /// (unless <paramref name="force"/>), optionally regenerates RaceMap's light probes, bakes RaceMap, re-renders the
        /// custom reflection probes of both scenes, saves them and writes the manifest. Blocks the editor for the bake.
        /// </summary>
        public static string BakeAll(bool regenerateLightProbes, bool force)
        {
            if (!ScenesClean(out string why)) return "NOT RUN: " + why;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var manifest = new Manifest
            {
                startedUtc = DateTime.UtcNow.ToString("o", Inv),
                unityVersion = Application.unityVersion,
                gitHead = Git("rev-parse HEAD"),
                machine = new Machine
                {
                    gpu = SystemInfo.graphicsDeviceName,
                    gpuApi = SystemInfo.graphicsDeviceVersion,
                    gpuMemoryMB = SystemInfo.graphicsMemorySize,
                    cpu = SystemInfo.processorType,
                    cpuThreads = SystemInfo.processorCount,
                    systemMemoryMB = SystemInfo.systemMemorySize,
                    os = SystemInfo.operatingSystem,
                },
                regeneratedLightProbes = regenerateLightProbes,
                forced = force,
            };
            var messages = new List<string>();
            Application.LogCallback capture = (text, stack, type) =>
            {
                string t = text ?? "";
                if (type != LogType.Log || t.IndexOf("lightmap", StringComparison.OrdinalIgnoreCase) >= 0
                    || t.IndexOf("out of memory", StringComparison.OrdinalIgnoreCase) >= 0
                    || t.IndexOf("OpenCL", StringComparison.OrdinalIgnoreCase) >= 0
                    || t.IndexOf("fall", StringComparison.OrdinalIgnoreCase) >= 0 && t.IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0)
                    if (messages.Count < 200) messages.Add(type +": " + t.Split('\n')[0]);
            };
            Application.logMessageReceived += capture;
            var bakeStart = DateTime.UtcNow;
            var sw = Stopwatch.StartNew();
            try
            {
                // RaceMap: environment from the record, everything else checked, then the GI bake and its probes.
                var race = EditorSceneManager.OpenScene(RaceMapScenePath, OpenSceneMode.Single);
                manifest.raceMap.applied = EnvironmentProblems();
                ApplyEnvironment();
                var problems = LightingSettingsProblems().Concat(RaceMapSunProblems()).Concat(ProbeProblems(RaceMapProbes)).ToList();
                manifest.raceMap.problems = problems;
                if (problems.Count > 0 && !force)
                    return Fail(manifest, messages, "RaceMap inputs differ from the record (record them in RenderingBake first, or pass force):\n  " + string.Join("\n  ", problems));
                if (regenerateLightProbes) manifest.raceMap.notes.Add("light probes: " + RaceMapLightProbeLayout.Generate());
                manifest.inputsHash = InputsHash();
                var settings = CurrentSettings();
                manifest.lightmapper = settings == null ? "(none)" : settings.lightmapper.ToString();
                if (settings != null) manifest.lightingSettings = DescribeLightingSettings(settings);

                var t0 = sw.Elapsed.TotalSeconds;
                if (!Lightmapping.Bake())
                    return Fail(manifest, messages, "Lightmapping.Bake() failed or was cancelled; the scene was not saved. If it was cancelled, reassign Lightmapping.lightingDataAsset before saving anything.");
                manifest.raceMap.bakeSeconds = Math.Round(sw.Elapsed.TotalSeconds - t0, 1);
                manifest.raceMap.lightProbes = LightmapSettings.lightProbes != null ? LightmapSettings.lightProbes.count : 0;
                manifest.raceMap.lightmaps = LightmapSettings.lightmaps.Length;
                RenderProbes(RaceMapProbes, manifest.raceMap.notes);
                EditorSceneManager.SaveScene(race);

                // Menu: no lightmaps (it has no LightingData by design); shared environment and its shrine probe only.
                var menu = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
                manifest.menu.applied = EnvironmentProblems();
                ApplyEnvironment();
                manifest.menu.problems = ProbeProblems(MenuProbes);
                if (manifest.menu.problems.Count > 0 && !force)
                    return Fail(manifest, messages, "Menu probes differ from the record:\n  " + string.Join("\n  ", manifest.menu.problems));
                if (Lightmapping.lightingDataAsset != null)
                    manifest.menu.notes.Add("menu has a LightingData asset (" + AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset) + "); not baked by this tool");
                RenderProbes(MenuProbes, manifest.menu.notes);
                EditorSceneManager.SaveScene(menu);

                AssetDatabase.SaveAssets();
                manifest.outputs = Outputs(bakeStart);
                manifest.totalSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1);
                manifest.result = "baked";
                manifest.lightmapperMessages = messages;
                WriteManifest(manifest);
                return "BAKED in " + manifest.totalSeconds + " s on " + manifest.machine.gpu + " (" + manifest.lightmapper + "), inputs " +
                       manifest.inputsHash.Substring(0, 12) + ", " + manifest.outputs.Count + " output files. Manifest: " + ManifestPath +
                       (messages.Count > 0 ? "\nLightmapper messages:\n  " + string.Join("\n  ", messages.Take(20)) : "");
            }
            finally
            {
                Application.logMessageReceived -= capture;
                RestoreScenes(setup);
            }
        }

        /// <summary>Batch-mode entry: bakes and exits with 0 when baked, 1 otherwise. Not used by the editor menus.</summary>
        public static void BakeAllBatch()
        {
            string result = BakeAll(Environment.GetCommandLineArgs().Contains("-regenerateLightProbes"), false);
            Debug.Log("[RenderingBake] " + result);
            EditorApplication.Exit(result.StartsWith("BAKED", StringComparison.Ordinal) ? 0 : 1);
        }

        /// <summary>Writes the recorded shared environment into the active scene's RenderSettings and lighting settings.</summary>
        public static void ApplyEnvironment()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (settings == null) Debug.LogError("[RenderingBake] Lighting settings not found at " + LightingSettingsPath);
            else Lightmapping.lightingSettings = settings;
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (sky == null) Debug.LogError("[RenderingBake] Sky material not found at " + SkyPath);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.ambientIntensity = AmbientIntensity;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = ReflectionResolution;
            RenderSettings.reflectionBounces = ReflectionBounces;
            RenderSettings.reflectionIntensity = ReflectionIntensity;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = FogDensity;
            RenderSettings.fogColor = FogColor;
            RenderSettings.subtractiveShadowColor = SubtractiveShadowColor;
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // ------------------------------------------------------------------------------------------------ checks

        static List<string> EnvironmentProblems()
        {
            var p = new List<string>();
            var settings = CurrentSettings();
            Expect(p, "lighting settings asset", settings == null ? "(none)" : AssetDatabase.GetAssetPath(settings), LightingSettingsPath);
            Expect(p, "skybox", AssetDatabase.GetAssetPath(RenderSettings.skybox), SkyPath);
            Expect(p, "ambientMode", RenderSettings.ambientMode, AmbientMode.Trilight);
            ExpectColor(p, "ambientSkyColor", RenderSettings.ambientSkyColor, AmbientSky);
            ExpectColor(p, "ambientEquatorColor", RenderSettings.ambientEquatorColor, AmbientEquator);
            ExpectColor(p, "ambientGroundColor", RenderSettings.ambientGroundColor, AmbientGround);
            ExpectFloat(p, "ambientIntensity", RenderSettings.ambientIntensity, AmbientIntensity);
            Expect(p, "defaultReflectionMode", RenderSettings.defaultReflectionMode, DefaultReflectionMode.Skybox);
            Expect(p, "defaultReflectionResolution", RenderSettings.defaultReflectionResolution, ReflectionResolution);
            Expect(p, "reflectionBounces", RenderSettings.reflectionBounces, ReflectionBounces);
            ExpectFloat(p, "reflectionIntensity", RenderSettings.reflectionIntensity, ReflectionIntensity);
            Expect(p, "fog", RenderSettings.fog, true);
            Expect(p, "fogMode", RenderSettings.fogMode, FogMode.ExponentialSquared);
            ExpectFloat(p, "fogDensity", RenderSettings.fogDensity, FogDensity);
            ExpectColor(p, "fogColor", RenderSettings.fogColor, FogColor);
            if (RenderSettings.fogColor.r <= 1f) p.Add("fogColor.r is clamped to " + F(RenderSettings.fogColor.r) + " (an LDR colour-picker edit?)");
            if (RenderSettings.sun == null) p.Add("RenderSettings.sun is not set");
            return p;
        }

        static List<string> LightingSettingsProblems()
        {
            var p = new List<string>();
            var s = CurrentSettings();
            if (s == null) { p.Add("no lighting settings asset"); return p; }
            foreach (var (name, actual, expected) in LightingSettingsChecks(s)) Expect(p, "lighting." + name, actual, expected);
            return p;
        }

        static List<string> RaceMapSunProblems()
        {
            var p = new List<string>();
            var sun = RenderSettings.sun;
            if (sun == null) { p.Add("RenderSettings.sun is not set"); return p; }
            Expect(p, "sun object", sun.name, RaceMapSunName);
            Vector3 e = sun.transform.rotation.eulerAngles;
            if (Quaternion.Angle(sun.transform.rotation, Quaternion.Euler(RaceMapSunEuler)) > 0.05f)
                p.Add("sun rotation " + V(e) + ", recorded " + V(RaceMapSunEuler));
            ExpectColor(p, "sun colour filter", sun.color, RaceMapSunFilter);
            Expect(p, "sun useColorTemperature", sun.useColorTemperature, true);
            ExpectFloat(p, "sun colorTemperature", sun.colorTemperature, RaceMapSunTemperature);
            ExpectFloat(p, "sun intensity", sun.intensity, RaceMapSunIntensity);
            Expect(p, "sun lightmapBakeType", sun.lightmapBakeType, LightmapBakeType.Mixed);
            return p;
        }

        static List<string> ProbeProblems((string probe, string file)[] probes)
        {
            var p = new List<string>();
            foreach (var (name, file) in probes)
            {
                var probe = FindProbe(name);
                if (probe == null) { p.Add("reflection probe '" + name + "' not found"); continue; }
                Expect(p, name + " mode", probe.mode, ReflectionProbeMode.Custom);
                Expect(p, name + " cubemap", AssetDatabase.GetAssetPath(probe.customBakedTexture), file);
            }
            return p;
        }

        // ------------------------------------------------------------------------------------------------ bake helpers

        static void RenderProbes((string probe, string file)[] probes, List<string> notes)
        {
            foreach (var (name, file) in probes)
            {
                var probe = FindProbe(name);
                if (probe == null) { notes.Add(name + ": not found, not rendered"); continue; }
                bool ok = Lightmapping.BakeReflectionProbe(probe, file);
                AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceUpdate);
                var cube = AssetDatabase.LoadAssetAtPath<Texture>(file);
                probe.mode = ReflectionProbeMode.Custom;
                probe.customBakedTexture = cube;
                notes.Add(name + (ok ? " rendered into " : " FAILED to render into ") + file);
            }
        }

        // Lightmapping.lightingSettings throws when a scene has no lighting settings asset.
        static LightingSettings CurrentSettings()
        {
            try { return Lightmapping.lightingSettings; }
            catch (Exception) { return null; }
        }

        static ReflectionProbe FindProbe(string name) =>
            Object.FindObjectsOfType<ReflectionProbe>(true).FirstOrDefault(r => r.gameObject.name == name);

        static string InputsHash()
        {
            var sb = new StringBuilder();
            sb.Append("env|").Append(string.Join(";", new[] { C(AmbientSky), C(AmbientEquator), C(AmbientGround), F(AmbientIntensity),
                ReflectionResolution.ToString(Inv), ReflectionBounces.ToString(Inv), F(ReflectionIntensity), F(FogDensity), C(FogColor) }));
            var sun = RenderSettings.sun;
            if (sun != null)
                sb.Append("|sun|").Append(V(sun.transform.rotation.eulerAngles)).Append(C(sun.color))
                    .Append(F(sun.colorTemperature)).Append(F(sun.intensity));
            foreach (var path in new[] { LightingSettingsPath, LightmapParametersPath, SkyPath })
                sb.Append('|').Append(path).Append('|').Append(FileHash(path));
            foreach (var group in Object.FindObjectsOfType<LightProbeGroup>(true).OrderBy(g => g.name))
                foreach (var pos in group.probePositions)
                    sb.Append(V(group.transform.TransformPoint(pos)));
            using (var sha = SHA256.Create())
                return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
        }

        static List<string> DescribeLightingSettings(LightingSettings s)
        {
            var list = LightingSettingsChecks(s).Select(c => c.name + " = " + Str(c.actual)).ToList();
            list.Add("directionalityMode = " + s.directionalityMode);
            list.Add("lightmapCompression = " + s.lightmapCompression);
            list.Add("filteringMode = " + s.filteringMode);
            list.Add("denoiserTypeDirect / Indirect / AO = " + s.denoiserTypeDirect + " / " + s.denoiserTypeIndirect + " / " + s.denoiserTypeAO);
            list.Add("environmentImportanceSampling = " + s.environmentImportanceSampling);
            return list;
        }

        static List<OutputFile> Outputs(DateTime since)
        {
            var files = new List<OutputFile>();
            foreach (var dir in new[] { "Assets/Scenes/RaceMap", "Assets/Scenes/MenuEnvironmentSetting" })
                foreach (var path in Directory.GetFiles(dir).Where(f => !f.EndsWith(".meta", StringComparison.Ordinal)).OrderBy(f => f, StringComparer.Ordinal))
                {
                    if (File.GetLastWriteTimeUtc(path) < since.AddSeconds(-2)) continue;
                    var p = path.Replace('\\', '/');
                    files.Add(new OutputFile { path = p, bytes = new FileInfo(p).Length, sha256 = FileHash(p) });
                }
            return files;
        }

        static string Fail(Manifest manifest, List<string> messages, string reason)
        {
            manifest.result = "not baked: " + reason;
            manifest.lightmapperMessages = messages;
            Debug.LogError("[RenderingBake] " + reason);
            return "NOT BAKED: " + reason;
        }

        static void WriteManifest(Manifest manifest)
        {
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ManifestPath));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, JsonUtility.ToJson(manifest, true) + "\n", new UTF8Encoding(false));
        }

        // ------------------------------------------------------------------------------------------------ scene handling

        static bool ScenesClean(out string why)
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var s = EditorSceneManager.GetSceneAt(i);
                if (s.isDirty) { why = "scene '" + s.path + "' has unsaved changes; save or discard them first (no dialog is shown)"; return false; }
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) { why = "leave play mode first"; return false; }
            why = null;
            return true;
        }

        static void RestoreScenes(SceneSetup[] setup)
        {
            if (setup == null || setup.Length == 0 || setup.Any(s => string.IsNullOrEmpty(s.path))) return;
            try { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            catch (Exception e) { Debug.LogWarning("[RenderingBake] could not restore the open scenes: " + e.Message); }
        }

        static void AppendSection(StringBuilder sb, string title, List<string> problems)
        {
            sb.Append(title).Append(problems.Count == 0 ? ": OK\n" : ":\n");
            foreach (var p in problems) sb.Append("  ").Append(p).Append('\n');
        }

        static void Report(string result)
        {
            if (result.StartsWith("OK", StringComparison.Ordinal) || result.StartsWith("BAKED", StringComparison.Ordinal)) Debug.Log("[RenderingBake] " + result);
            else Debug.LogWarning("[RenderingBake] " + result);
        }

        // ------------------------------------------------------------------------------------------------ small helpers

        static void Expect(List<string> p, string what, object actual, object expected)
        {
            if (!Equals(actual, expected)) p.Add(what + " = " + Str(actual) + ", recorded " + Str(expected));
        }

        static void ExpectFloat(List<string> p, string what, float actual, float expected)
        {
            if (Mathf.Abs(actual - expected) > 1e-4f * Mathf.Max(1f, Mathf.Abs(expected))) p.Add(what + " = " + F(actual) + ", recorded " + F(expected));
        }

        static void ExpectColor(List<string> p, string what, Color actual, Color expected)
        {
            if (Mathf.Abs(actual.r - expected.r) > 1e-3f || Mathf.Abs(actual.g - expected.g) > 1e-3f || Mathf.Abs(actual.b - expected.b) > 1e-3f)
                p.Add(what + " = " + C(actual) + ", recorded " + C(expected));
        }

        static string Str(object o) => o is float f ? F(f) : o is bool b ? (b ? "true" : "false") : (o == null || o as string == "" ? "(none)" : o.ToString());
        static string F(float f) => f.ToString("0.####", Inv);
        static string C(Color c) => "(" + F(c.r) + ", " + F(c.g) + ", " + F(c.b) + ")";
        static string V(Vector3 v) => "(" + v.x.ToString("0.###", Inv) + ", " + v.y.ToString("0.###", Inv) + ", " + v.z.ToString("0.###", Inv) + ")";

        static string FileHash(string path)
        {
            if (!File.Exists(path)) return "missing";
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return Hex(sha.ComputeHash(stream));
        }

        static string Hex(byte[] bytes) => string.Concat(bytes.Select(b => b.ToString("x2")));

        static string Git(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("git", args)
                {
                    RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
                    WorkingDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                };
                using (var proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit(5000);
                    return output;
                }
            }
            catch (Exception e) { return "unknown (" + e.Message + ")"; }
        }

        // ------------------------------------------------------------------------------------------------ manifest

        [Serializable] sealed class Manifest
        {
            public string result = "running";
            public string startedUtc;
            public double totalSeconds;
            public string unityVersion;
            public string gitHead;
            public Machine machine;
            public string lightmapper;
            public bool regeneratedLightProbes;
            public bool forced;
            public string inputsHash;
            public List<string> lightingSettings = new List<string>();
            public SceneRecord raceMap = new SceneRecord { scene = RaceMapScenePath };
            public SceneRecord menu = new SceneRecord { scene = MenuScenePath };
            public List<OutputFile> outputs = new List<OutputFile>();
            public List<string> lightmapperMessages = new List<string>();
        }

        [Serializable] sealed class Machine
        {
            public string gpu, gpuApi, cpu, os;
            public int gpuMemoryMB, cpuThreads, systemMemoryMB;
        }

        [Serializable] sealed class SceneRecord
        {
            public string scene;
            public double bakeSeconds;
            public int lightProbes, lightmaps;
            public List<string> applied = new List<string>();   // recorded values the scene did not hold before this bake
            public List<string> problems = new List<string>();
            public List<string> notes = new List<string>();
        }

        [Serializable] sealed class OutputFile
        {
            public string path, sha256;
            public long bytes;
        }
    }
}
