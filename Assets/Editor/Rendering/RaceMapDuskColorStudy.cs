// Research probe for the dusk colour study (Docs/rendering/racemap/dusk-color-study.md): renders RaceMap's fixed
// viewpoints (RaceMapViewpointCapture.Viewpoints) with temporary runtime overrides of the warm layers (sun, fog, sky,
// reflections, post profile) so each layer's share of the "everything turns yellow" look can be measured, plus a
// material-id mask render for segmenting lane / curb / sand / player / sky in Python, and optional skill VFX placed on
// the player proxy. Every override is undone when the capture ends; nothing is saved to the scene or to any asset.
//
// Differences from RaceMapViewpointCapture: renders into an HDR buffer like the game (token "ldr" for the old 8-bit
// path), places the proxy on the raycast ground plus the capsule clearance (the old tool still assumes track y 2.62),
// and writes <label>/rects.json with the proxy's screen rectangle per view.
//
// Call from the MCP client: RaceMapDuskColorStudy.Capture("label", "fog:off;post:neutral", "01,04").
// Tokens (';' separated, key:args):
//   sun:neutral | sun:temp:K | sun:filter:r,g,b | sun:intensity:x | sun:rot:pitch,yaw
//   fog:off | fog:density:x | fog:color:r,g,b (inspector sRGB) | fog:neutral (grey, same luminance)
//   sky:neutral (grey sky, same luminance per colour) | sky:exposure:x | sky:set:_Prop:r,g,b | sky:guid:<material guid>
//   ambient:x (intensity) | ambient:neutral (trilight colours to grey)
//   probe:grey (every reflection probe shows a neutral gradient cubemap) | probe:refresh (re-render realtime probes)
//   post:off | post:neutral (filter white, white balance 0/0, bloom tint white) | post:filter:r,g,b | post:wb:t,tint
//   post:bloomtint:r,g,b | post:bloom:x | post:sat:x | post:contrast:x | post:exposure:x | post:tonemap:none|neutral|aces
//   post:smh:sr,sg,sb,sw/mr,mg,mb,mw/hr,hg,hb,hw | post:lgg:lr,lg,lb,lw/gr,gg,gb,gw/hr,hg,hb,hw
//   post:split:sr,sg,sb/hr,hg,hb/balance | post:hue:x | post:lumsat:x0,y0/x1,y1/... (LumVsSat curve)
//   grading:hdr | grading:ldr (pipeline asset colour grading mode, restored afterwards)
//   vfx:palm:t | vfx:slow:t | vfx:accel:t (slot prefab on the proxy, simulated t seconds) | ldr | mask | msaa:n
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace BuddahGo.Rendering.Editor
{
    public static class RaceMapDuskColorStudy
    {
        const int Width = 1920;
        const int Height = 1080;
        const string PlayerPrefabGuid = "c2b8c569b26585545904610506c355cd";   // Assets/Character/Prefab/Buddah.prefab
        const string PalmReleaseGuid = "3a01fd3e2417058498c8dd2cf0b5bd92";    // VFX_Palm_Release (additive burst)
        const string SlowReleaseGuid = "5b787d04ec2b732418d1a29626e9ddcd";    // VFX_Slow_Release (ink pool, alpha)
        const string AccelReleaseGuid = "aade18b8924700b458278886721b0a0a";   // VFX_Accel_Release
        const string CastGuid = "b34a38874e91c334582b08c58792bead";   // VFX_Common_Cast (Ying Yang Rune Fall, gold)

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        sealed class Restore
        {
            readonly List<Action> m_Actions = new List<Action>();
            public void Add(Action action) => m_Actions.Add(action);
            public void Undo()
            {
                for (int i = m_Actions.Count - 1; i >= 0; i--)
                {
                    try { m_Actions[i](); }
                    catch (Exception e) { Debug.LogException(e); }
                }
                m_Actions.Clear();
            }
        }

        sealed class Options
        {
            public bool Hdr = true;
            public bool Mask;
            public int Msaa = 1;
            public string Vfx;
            public float VfxSeconds;
            public bool RefreshProbes;
        }

        public static string Capture(string label, string tokens) => Capture(label, tokens, "");

        /// <summary>Renders the viewpoints whose id starts with one of <paramref name="views"/> (comma list, empty = all).</summary>
        public static string Capture(string label, string tokens, string views)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != RaceMapViewpointCapture.ScenePath)
                return "FAILED: open " + RaceMapViewpointCapture.ScenePath + " first";
            var mainCamera = Camera.main;
            if (mainCamera == null)
                return "FAILED: no MainCamera";

            var dir = Path.Combine(RaceMapViewpointCapture.OutputRoot, label);
            Directory.CreateDirectory(dir);
            var wanted = (views ?? "").Split(',').Where(v => v.Length > 0).ToArray();
            var report = new StringBuilder();
            var restore = new Restore();
            var temporaries = new List<Object>();
            var rects = new StringBuilder("[\n");
            try
            {
                var options = new Options();
                var camera = CreateCaptureCamera(mainCamera, temporaries);
                Apply(tokens ?? "", restore, options, camera, dir);
                if (options.RefreshProbes)
                    RefreshEnvironment(camera);
                var proxy = CreatePlayerProxy(temporaries);
                if (options.Mask)
                    MaskProxy(proxy, temporaries);
                bool first = true;
                foreach (var view in RaceMapViewpointCapture.Viewpoints)
                {
                    if (wanted.Length > 0 && !wanted.Any(w => view.Id.StartsWith(w)))
                        continue;
                    PlaceProxy(proxy, view);
                    RaceMapViewpointCapture.PlaceCamera(camera, view);
                    GameObject vfx = null;
                    if (options.Vfx != null && view.HasPlayer)
                        vfx = SpawnVfx(options.Vfx, options.VfxSeconds, proxy.transform, temporaries);
                    var path = Path.Combine(dir, view.Id + ".png");
                    Render(camera, path, options);
                    if (vfx != null)
                    {
                        temporaries.Remove(vfx);
                        Object.DestroyImmediate(vfx);
                    }
                    var rect = ScreenRect(camera, proxy);
                    rects.Append(first ? "" : ",\n").Append("  {\"view\": \"" + view.Id + "\", \"proxy\": [" +
                        rect.xMin.ToString("F0", Inv) + ", " + rect.yMin.ToString("F0", Inv) + ", " + rect.xMax.ToString("F0", Inv) + ", " + rect.yMax.ToString("F0", Inv) + "]}");
                    first = false;
                    report.AppendLine(view.Id + ": " + path.Replace('\\', '/'));
                }
                rects.Append("\n]\n");
                report.AppendLine(s_LastStack);
                File.WriteAllText(Path.Combine(dir, "rects.json"), rects.ToString());
            }
            finally
            {
                restore.Undo();
                // The ambient probe and the realtime reflection probes still hold the overridden sky: re-render them.
                RefreshEnvironment(temporaries.OfType<GameObject>().Select(g => g.GetComponent<Camera>()).FirstOrDefault(c => c != null));
                for (int i = temporaries.Count - 1; i >= 0; i--)
                    if (temporaries[i] != null)
                        Object.DestroyImmediate(temporaries[i]);
            }
            return report.ToString();
        }

        /// <summary>Re-renders the ambient probe and the realtime reflection probes from the current scene state.</summary>
        public static string RefreshNow()
        {
            var go = new GameObject("DuskStudyRefreshCamera") { hideFlags = HideFlags.DontSave };
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.enabled = false;
                camera.CopyFrom(Camera.main);
                RefreshEnvironment(camera);
                var sh = RenderSettings.ambientProbe;
                return "ambientProbe L0=(" + sh[0, 0].ToString("F3") + ", " + sh[1, 0].ToString("F3") + ", " + sh[2, 0].ToString("F3") + ")";
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Applies post tokens, renders once and reports what the volume stack resolved to.</summary>
        public static string DebugPost(string tokens)
        {
            var restore = new Restore();
            var temporaries = new List<Object>();
            try
            {
                var camera = CreateCaptureCamera(Camera.main, temporaries);
                var options = new Options();
                Apply(tokens, restore, options, camera, Path.Combine(RaceMapViewpointCapture.OutputRoot, "_debug"));
                var volume = DuskVolume();
                var sb = new StringBuilder();
                sb.Append("volume=" + volume.name + " instantiated=" + volume.HasInstantiatedProfile() + " sharedProfile=" + volume.sharedProfile.name + " updateMode=" + camera.GetComponent<UniversalAdditionalCameraData>().requiresVolumeFrameworkUpdate + "\n");
                var profileRef = volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile;
                sb.Append("profileRef=" + profileRef.name + " components=" + profileRef.components.Count + " shared=" + string.Join(",", profileRef.components.Select((c, i) => (i < volume.sharedProfile.components.Count && ReferenceEquals(c, volume.sharedProfile.components[i])).ToString())) + "\n");
                if (profileRef.TryGet<ColorAdjustments>(out var pca))
                    sb.Append("profile ColorAdjustments filter=" + pca.colorFilter.value.ToString("F3") + " override=" + pca.colorFilter.overrideState + "\n");
                RaceMapViewpointCapture.PlaceCamera(camera, RaceMapViewpointCapture.Viewpoints[0]);
                var rt = RenderTexture.GetTemporary(256, 144, 24, RenderTextureFormat.DefaultHDR);
                camera.targetTexture = rt;
                camera.UpdateVolumeStack();
                camera.Render();
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                var stack = VolumeManager.instance.stack;
                var ca = stack.GetComponent<ColorAdjustments>();
                var wb = stack.GetComponent<WhiteBalance>();
                sb.Append("stack filter=" + ca.colorFilter.value.ToString("F3") + " sat=" + ca.saturation.value + " wb=" + wb.temperature.value + "/" + wb.tint.value + "\n");
                var camStack = camera.GetComponent<UniversalAdditionalCameraData>().volumeStack;
                if (camStack != null)
                    sb.Append("camera stack filter=" + camStack.GetComponent<ColorAdjustments>().colorFilter.value.ToString("F3") + "\n");
                else
                    sb.Append("camera stack null\n");
                return sb.ToString();
            }
            finally
            {
                restore.Undo();
                for (int i = temporaries.Count - 1; i >= 0; i--)
                    if (temporaries[i] != null)
                        Object.DestroyImmediate(temporaries[i]);
            }
        }

        /// <summary>Spawns a slot VFX at viewpoint 01's player position and reports what its particle systems hold.</summary>
        public static string DebugVfx(string kind, float seconds)
        {
            var temporaries = new List<Object>();
            try
            {
                var proxy = CreatePlayerProxy(temporaries);
                PlaceProxy(proxy, RaceMapViewpointCapture.Viewpoints[0]);
                var go = SpawnVfx(kind, seconds, proxy.transform, temporaries);
                var sb = new StringBuilder();
                sb.Append("root " + go.transform.position.ToString("F1") + " active=" + go.activeInHierarchy + "\n");
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var r = ps.GetComponent<ParticleSystemRenderer>();
                    sb.Append(ps.name + " active=" + ps.gameObject.activeInHierarchy + " playing=" + ps.isPlaying + " emitting=" + ps.isEmitting + " n=" + ps.particleCount + " time=" + ps.time.ToString("F2") + " dur=" + ps.main.duration + " loop=" + ps.main.loop + " emission=" + ps.emission.enabled + " renderer=" + (r != null ? (r.enabled + " " + (r.sharedMaterial != null ? r.sharedMaterial.name + "/" + (r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "?") : "nomat") + " bounds=" + r.bounds.size.ToString("F1")) : "none") + " scale=" + ps.transform.lossyScale.ToString("F2") + "\n");
                }
                return sb.ToString();
            }
            finally
            {
                for (int i = temporaries.Count - 1; i >= 0; i--)
                    if (temporaries[i] != null)
                        Object.DestroyImmediate(temporaries[i]);
            }
        }

        /// <summary>Current values of the warm layers, for the study's tables.</summary>
        public static string Snapshot()
        {
            var sb = new StringBuilder();
            var sun = RenderSettings.sun;
            if (sun != null)
                sb.Append("sun " + sun.name + " rot=" + sun.transform.eulerAngles.ToString("F1") + " color=" + sun.color.ToString("F3") + " temp=" + sun.colorTemperature + " useTemp=" + sun.useColorTemperature + " intensity=" + sun.intensity + " final(linear)=" + FinalLightColor(sun).ToString("F3") + "\n");
            sb.Append("fog=" + RenderSettings.fog + " mode=" + RenderSettings.fogMode + " density=" + RenderSettings.fogDensity + " color(sRGB)=" + RenderSettings.fogColor.ToString("F3") + " linear=" + RenderSettings.fogColor.linear.ToString("F3") + "\n");
            sb.Append("ambientMode=" + RenderSettings.ambientMode + " intensity=" + RenderSettings.ambientIntensity + " sky=" + RenderSettings.ambientSkyColor.ToString("F3") + " equator=" + RenderSettings.ambientEquatorColor.ToString("F3") + " ground=" + RenderSettings.ambientGroundColor.ToString("F3") + "\n");
            var sh = RenderSettings.ambientProbe;
            sb.Append("ambientProbe L0=(" + sh[0, 0].ToString("F3") + ", " + sh[1, 0].ToString("F3") + ", " + sh[2, 0].ToString("F3") + ")\n");
            var sky = RenderSettings.skybox;
            if (sky != null)
            {
                sb.Append("skybox=" + sky.name + " shader=" + sky.shader.name + "\n");
                var shader = sky.shader;
                for (int i = 0; i < shader.GetPropertyCount(); i++)
                {
                    var name = shader.GetPropertyName(i);
                    var type = shader.GetPropertyType(i);
                    if (type == UnityEngine.Rendering.ShaderPropertyType.Color)
                        sb.Append("  " + name + "=" + sky.GetColor(name).ToString("F3") + "\n");
                    else if (type == UnityEngine.Rendering.ShaderPropertyType.Float || type == UnityEngine.Rendering.ShaderPropertyType.Range)
                        sb.Append("  " + name + "=" + sky.GetFloat(name).ToString("F3") + "\n");
                }
            }
            var def = ReflectionProbe.defaultTexture;
            sb.Append("defaultReflection=" + (def != null ? def.width + "px " + def.GetType().Name + " avg=" + CubemapAverage(def as Cubemap).ToString("F3") : "null") + "\n");
            foreach (var p in Object.FindObjectsOfType<ReflectionProbe>())
                sb.Append("probe " + p.name + " mode=" + p.mode + " refresh=" + p.refreshMode + " res=" + p.resolution + " hdr=" + p.hdr + " intensity=" + p.intensity + " tex=" + (p.texture != null ? p.texture.width + "px avg=" + CubemapAverage(p.texture as Cubemap).ToString("F3") : "null") + " center=" + p.transform.position.ToString("F0") + " size=" + p.size.ToString("F0") + "\n");
            foreach (var v in Object.FindObjectsOfType<Volume>())
            {
                sb.Append("volume " + v.name + " global=" + v.isGlobal + " prio=" + v.priority + " weight=" + v.weight + " on=" + v.enabled + " profile=" + (v.sharedProfile != null ? v.sharedProfile.name : "null") + "\n");
                if (v.sharedProfile != null)
                    foreach (var c in v.sharedProfile.components)
                        sb.Append("  " + c.GetType().Name + " active=" + c.active + " " + string.Join(" ", c.parameters.Where(p => p.overrideState).Select(p => ParamToString(c, p))) + "\n");
            }
            var asset = UniversalRenderPipeline.asset;
            sb.Append("pipeline=" + asset.name + " grading=" + asset.colorGradingMode + " lut=" + asset.colorGradingLutSize + " hdr=" + asset.supportsHDR + " msaa=" + asset.msaaSampleCount + "\n");
            var data = Camera.main != null ? Camera.main.GetComponent<UniversalAdditionalCameraData>() : null;
            if (data != null)
                sb.Append("mainCamera post=" + data.renderPostProcessing + " aa=" + data.antialiasing + " volumeMask=" + data.volumeLayerMask.value + "\n");
            return sb.ToString();
        }

        static string ParamToString(VolumeComponent c, VolumeParameter p)
        {
            var field = c.GetType().GetFields().FirstOrDefault(f => ReferenceEquals(f.GetValue(c), p));
            var value = p.GetType().GetProperty("value")?.GetValue(p);
            string text = value is Color col ? col.ToString("F3") : value is Vector4 v4 ? v4.ToString("F3") : value is float f ? f.ToString("F3", Inv) : value?.ToString();
            return (field != null ? field.Name : "?") + "=" + text;
        }

        static Color CubemapAverage(Cubemap cube)
        {
            if (cube == null)
                return Color.black;
            var sum = Vector4.zero;
            int count = 0;
            var rt = RenderTexture.GetTemporary(cube.width, cube.width, 0, RenderTextureFormat.ARGBFloat);
            var tex = new Texture2D(cube.width, cube.width, TextureFormat.RGBAFloat, false);
            try
            {
                for (int face = 0; face < 6; face++)
                {
                    Graphics.CopyTexture(cube, face, 0, rt, 0, 0);
                    var previous = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, cube.width, cube.width), 0, 0);
                    RenderTexture.active = previous;
                    foreach (var p in tex.GetPixels())
                    {
                        sum += new Vector4(p.r, p.g, p.b, 0f);
                        count++;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("CubemapAverage: " + e.Message);
                return Color.black;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(tex);
            }
            return count > 0 ? new Color(sum.x / count, sum.y / count, sum.z / count, 1f) : Color.black;
        }

        static Color FinalLightColor(Light light)
        {
            var c = light.color.linear;
            if (light.useColorTemperature)
                c *= Mathf.CorrelatedColorTemperatureToRGB(light.colorTemperature);
            return c * light.intensity;
        }

        // ---------------------------------------------------------------- overrides

        static float[] Floats(string s) => s.Split(',').Select(x => float.Parse(x, Inv)).ToArray();
        static Color Rgb(string s) { var f = Floats(s); return new Color(f[0], f[1], f[2], f.Length > 3 ? f[3] : 1f); }
        static Vector4 Vec4(string s) { var f = Floats(s); return new Vector4(f[0], f[1], f[2], f.Length > 3 ? f[3] : 0f); }
        static Color Grey(Color c) { float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b; return new Color(l, l, l, c.a); }

        static void Apply(string tokens, Restore restore, Options options, Camera camera, string dir)
        {
            foreach (var token in tokens.Split(';').Where(t => t.Length > 0))
            {
                var parts = token.Split(new[] { ':' }, 3);
                string key = parts[0], op = parts.Length > 1 ? parts[1] : "", arg = parts.Length > 2 ? parts[2] : "";
                switch (key)
                {
                    case "ldr": options.Hdr = false; break;
                    case "mask": options.Mask = true; ApplyMask(restore, camera, dir); break;
                    case "msaa": options.Msaa = int.Parse(op); break;
                    case "vfx": options.Vfx = op; options.VfxSeconds = arg.Length > 0 ? float.Parse(arg, Inv) : 0.3f; break;
                    case "sun": ApplySun(op, arg, restore); break;
                    case "fog": ApplyFog(op, arg, restore); break;
                    case "sky": ApplySky(op, arg, restore); options.RefreshProbes = true; break;
                    case "ambient": ApplyAmbient(op, arg, restore); options.RefreshProbes = true; break;
                    case "probe": ApplyProbe(op, restore, options); break;
                    case "post": ApplyPost(op, arg, restore, camera); break;
                    case "grading":
                    {
                        var asset = UniversalRenderPipeline.asset;
                        var previous = asset.colorGradingMode;
                        asset.colorGradingMode = op == "hdr" ? ColorGradingMode.HighDynamicRange : ColorGradingMode.LowDynamicRange;
                        restore.Add(() => { asset.colorGradingMode = previous; EditorUtility.ClearDirty(asset); });
                        break;
                    }
                    default: throw new ArgumentException("unknown token " + token);
                }
            }
        }

        static void ApplySun(string op, string arg, Restore restore)
        {
            var sun = RenderSettings.sun;
            if (sun == null) throw new InvalidOperationException("no sun");
            var color = sun.color; var temp = sun.colorTemperature; var useTemp = sun.useColorTemperature; var intensity = sun.intensity; var rot = sun.transform.rotation;
            restore.Add(() => { sun.color = color; sun.colorTemperature = temp; sun.useColorTemperature = useTemp; sun.intensity = intensity; sun.transform.rotation = rot; });
            switch (op)
            {
                case "neutral": sun.useColorTemperature = false; sun.color = Color.white; break;
                case "temp": sun.useColorTemperature = true; sun.colorTemperature = float.Parse(arg, Inv); break;
                case "filter": sun.color = Rgb(arg); break;
                case "intensity": sun.intensity = float.Parse(arg, Inv); break;
                case "rot": { var f = Floats(arg); sun.transform.rotation = Quaternion.Euler(f[0], f[1], 0f); break; }
                default: throw new ArgumentException("sun:" + op);
            }
        }

        static void ApplyFog(string op, string arg, Restore restore)
        {
            bool fog = RenderSettings.fog; var color = RenderSettings.fogColor; var density = RenderSettings.fogDensity;
            restore.Add(() => { RenderSettings.fog = fog; RenderSettings.fogColor = color; RenderSettings.fogDensity = density; });
            switch (op)
            {
                case "off": RenderSettings.fog = false; break;
                case "density": RenderSettings.fogDensity = float.Parse(arg, Inv); break;
                case "color": RenderSettings.fogColor = Rgb(arg); break;
                case "neutral": RenderSettings.fogColor = Grey(color.linear).gamma; break;
                default: throw new ArgumentException("fog:" + op);
            }
        }

        static void ApplySky(string op, string arg, Restore restore)
        {
            var original = RenderSettings.skybox;
            if (original == null) throw new InvalidOperationException("no skybox");
            Material mat;
            if (op == "guid")
            {
                mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(arg));
                if (mat == null) throw new ArgumentException("sky material " + arg);
                RenderSettings.skybox = mat;
                restore.Add(() => RenderSettings.skybox = original);
                return;
            }
            // One temporary copy per capture, shared by every sky token.
            mat = RenderSettings.skybox.name.EndsWith("(DuskStudy)") ? RenderSettings.skybox : null;
            if (mat == null)
            {
                mat = new Material(original) { name = original.name + " (DuskStudy)", hideFlags = HideFlags.DontSave };
                RenderSettings.skybox = mat;
                restore.Add(() => { RenderSettings.skybox = original; Object.DestroyImmediate(mat); });
            }
            switch (op)
            {
                case "neutral":
                {
                    var shader = mat.shader;
                    for (int i = 0; i < shader.GetPropertyCount(); i++)
                        if (shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Color)
                            mat.SetColor(shader.GetPropertyName(i), Grey(mat.GetColor(shader.GetPropertyName(i))));
                    break;
                }
                case "exposure": mat.SetFloat("_Exposure", float.Parse(arg, Inv)); break;
                case "set":
                {
                    var kv = arg.Split(new[] { ':' }, 2);
                    if (kv[1].Contains(",")) mat.SetColor(kv[0], Rgb(kv[1])); else mat.SetFloat(kv[0], float.Parse(kv[1], Inv));
                    break;
                }
                default: throw new ArgumentException("sky:" + op);
            }
        }

        static void ApplyAmbient(string op, string arg, Restore restore)
        {
            var intensity = RenderSettings.ambientIntensity; var sky = RenderSettings.ambientSkyColor; var eq = RenderSettings.ambientEquatorColor; var ground = RenderSettings.ambientGroundColor;
            restore.Add(() => { RenderSettings.ambientIntensity = intensity; RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = ground; });
            if (op == "neutral")
            {
                RenderSettings.ambientSkyColor = Grey(sky); RenderSettings.ambientEquatorColor = Grey(eq); RenderSettings.ambientGroundColor = Grey(ground);
            }
            else
                RenderSettings.ambientIntensity = float.Parse(op, Inv);
        }

        static void ApplyProbe(string op, Restore restore, Options options)
        {
            if (op == "refresh") { options.RefreshProbes = true; return; }
            if (op != "grey") throw new ArgumentException("probe:" + op);
            var cube = NeutralCubemap();
            restore.Add(() => Object.DestroyImmediate(cube));
            foreach (var probe in Object.FindObjectsOfType<ReflectionProbe>())
            {
                var p = probe; var mode = p.mode; var custom = p.customBakedTexture;
                p.mode = ReflectionProbeMode.Custom;
                p.customBakedTexture = cube;
                restore.Add(() => { p.mode = mode; p.customBakedTexture = custom; });
            }
            // Objects outside every probe fall back to the sky reflection; swap that too.
            var defMode = RenderSettings.defaultReflectionMode; var defTex = RenderSettings.customReflectionTexture;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = cube;
            restore.Add(() => { RenderSettings.defaultReflectionMode = defMode; RenderSettings.customReflectionTexture = defTex; });
        }

        /// <summary>A 64 px cubemap: light neutral grey above the horizon, darker grey below, soft band between.</summary>
        static Cubemap NeutralCubemap()
        {
            const int size = 64;
            var cube = new Cubemap(size, TextureFormat.RGBAHalf, false) { name = "DuskStudyNeutral", hideFlags = HideFlags.DontSave };
            var pixels = new Color[size * size];
            for (int face = 0; face < 6; face++)
            {
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                        Vector3 d;
                        switch (face)
                        {
                            case 0: d = new Vector3(1f, -v, -u); break;
                            case 1: d = new Vector3(-1f, -v, u); break;
                            case 2: d = new Vector3(u, 1f, v); break;
                            case 3: d = new Vector3(u, -1f, -v); break;
                            case 4: d = new Vector3(u, -v, 1f); break;
                            default: d = new Vector3(-u, -v, -1f); break;
                        }
                        float up = d.normalized.y;
                        float l = Mathf.Lerp(0.12f, 0.55f, Mathf.SmoothStep(0f, 1f, up * 0.5f + 0.5f));
                        pixels[y * size + x] = new Color(l, l, l, 1f);
                    }
                cube.SetPixels(pixels, (CubemapFace)face);
            }
            cube.Apply(true);
            return cube;
        }

        static void RefreshEnvironment(Camera camera)
        {
            if (camera == null)
                return;
            DynamicGI.UpdateEnvironment();
            foreach (var probe in Object.FindObjectsOfType<ReflectionProbe>())
                if (probe.mode == ReflectionProbeMode.Realtime)
                    probe.RenderProbe();
            // Give the probe renders a few frames.
            for (int i = 0; i < 3; i++)
                camera.Render();
        }

        static Volume DuskVolume()
        {
            var volumes = Object.FindObjectsOfType<Volume>().Where(v => v.enabled && v.isGlobal && v.sharedProfile != null).ToArray();
            var dusk = volumes.FirstOrDefault(v => v.sharedProfile.name.Contains("Dusk"));
            return dusk != null ? dusk : volumes.OrderByDescending(v => v.priority).FirstOrDefault();
        }

        static VolumeProfile s_ProfileClone;

        static VolumeProfile StudyProfile(Restore restore)
        {
            if (s_ProfileClone != null)
                return s_ProfileClone;
            var volume = DuskVolume();
            if (volume == null) throw new InvalidOperationException("no global volume");
            // Volume.profile deep-copies the shared profile's components (Object.Instantiate of the profile alone would
            // share them with the asset, so an override would edit the asset in memory).
            volume.profile = null;
            var clone = volume.profile;
            clone.name = volume.sharedProfile.name + " (DuskStudy)";
            clone.hideFlags = HideFlags.DontSave;
            s_ProfileClone = clone;
            restore.Add(() =>
            {
                volume.profile = null;
                s_ProfileClone = null;
                foreach (var c in clone.components)
                    if (c != null)
                        Object.DestroyImmediate(c);
                Object.DestroyImmediate(clone);
            });
            return clone;
        }

        static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var c)) return c;
            c = profile.Add<T>(true);
            return c;
        }

        static void ApplyPost(string op, string arg, Restore restore, Camera camera)
        {
            if (op == "off")
            {
                var data = camera.GetComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = false;
                return;
            }
            var profile = StudyProfile(restore);
            switch (op)
            {
                case "neutral":
                {
                    var ca = Component<ColorAdjustments>(profile);
                    ca.colorFilter.Override(Color.white);
                    var wb = Component<WhiteBalance>(profile);
                    wb.temperature.Override(0f); wb.tint.Override(0f);
                    var bloom = Component<Bloom>(profile);
                    bloom.tint.Override(Color.white);
                    break;
                }
                case "filter": Component<ColorAdjustments>(profile).colorFilter.Override(Rgb(arg)); break;
                case "sat": Component<ColorAdjustments>(profile).saturation.Override(float.Parse(arg, Inv)); break;
                case "contrast": Component<ColorAdjustments>(profile).contrast.Override(float.Parse(arg, Inv)); break;
                case "exposure": Component<ColorAdjustments>(profile).postExposure.Override(float.Parse(arg, Inv)); break;
                case "hue": Component<ColorAdjustments>(profile).hueShift.Override(float.Parse(arg, Inv)); break;
                case "wb": { var f = Floats(arg); var wb = Component<WhiteBalance>(profile); wb.temperature.Override(f[0]); wb.tint.Override(f[1]); break; }
                case "bloomtint": Component<Bloom>(profile).tint.Override(Rgb(arg)); break;
                case "bloom": Component<Bloom>(profile).intensity.Override(float.Parse(arg, Inv)); break;
                case "bloomthreshold": Component<Bloom>(profile).threshold.Override(float.Parse(arg, Inv)); break;
                case "tonemap":
                {
                    var tm = Component<Tonemapping>(profile);
                    tm.mode.Override(arg == "none" ? TonemappingMode.None : arg == "neutral" ? TonemappingMode.Neutral : TonemappingMode.ACES);
                    break;
                }
                case "smh":
                {
                    var v = arg.Split('/');
                    var smh = Component<ShadowsMidtonesHighlights>(profile);
                    smh.shadows.Override(Vec4(v[0])); smh.midtones.Override(Vec4(v[1])); smh.highlights.Override(Vec4(v[2]));
                    if (v.Length > 3) { var f = Floats(v[3]); smh.shadowsStart.Override(f[0]); smh.shadowsEnd.Override(f[1]); smh.highlightsStart.Override(f[2]); smh.highlightsEnd.Override(f[3]); }
                    break;
                }
                case "lgg":
                {
                    var v = arg.Split('/');
                    var lgg = Component<LiftGammaGain>(profile);
                    lgg.lift.Override(Vec4(v[0])); lgg.gamma.Override(Vec4(v[1])); lgg.gain.Override(Vec4(v[2]));
                    break;
                }
                case "split":
                {
                    var v = arg.Split('/');
                    var st = Component<SplitToning>(profile);
                    st.shadows.Override(Rgb(v[0])); st.highlights.Override(Rgb(v[1]));
                    if (v.Length > 2) st.balance.Override(float.Parse(v[2], Inv));
                    break;
                }
                case "lumsat":
                {
                    var curves = Component<ColorCurves>(profile);
                    var keys = arg.Split('/').Select(p => { var f = Floats(p); return new Keyframe(f[0], f[1]); }).ToArray();
                    curves.lumVsSat.Override(new TextureCurve(keys, 0.5f, false, new Vector2(0f, 1f)));
                    break;
                }
                default: throw new ArgumentException("post:" + op);
            }
        }

        // ---------------------------------------------------------------- mask

        static void ApplyMask(Restore restore, Camera camera, string dir)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var byName = new Dictionary<string, int>();
            var materials = new List<Material>();
            var names = new List<string>();
            Material MaskMaterial(Material source)
            {
                string name = source != null ? source.name : "(null)";
                if (!byName.TryGetValue(name, out int id))
                {
                    id = materials.Count + 1;
                    byName[name] = id;
                    var m = new Material(shader) { name = "Mask " + id, hideFlags = HideFlags.DontSave };
                    m.SetColor("_BaseColor", new Color((id % 16) * 17 / 255f, ((id / 16) % 16) * 17 / 255f, ((id / 256) % 16) * 17 / 255f, 1f));
                    materials.Add(m);
                    names.Add(name + " | " + (source != null && source.shader != null ? source.shader.name : "?"));
                }
                return materials[id - 1];
            }
            foreach (var r in Object.FindObjectsOfType<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy)
                    continue;
                var renderer = r;
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                {
                    renderer.enabled = false;
                    restore.Add(() => renderer.enabled = true);
                    continue;
                }
                var shared = renderer.sharedMaterials;
                renderer.sharedMaterials = shared.Select(MaskMaterial).ToArray();
                restore.Add(() => renderer.sharedMaterials = shared);
            }
            restore.Add(() => { foreach (var m in materials) Object.DestroyImmediate(m); });
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            restore.Add(() => RenderSettings.fog = fog);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            var sb = new StringBuilder("{\n  \"0\": \"sky | background\"");
            for (int i = 0; i < names.Count; i++)
                sb.Append(",\n  \"" + (i + 1) + "\": \"" + names[i].Replace("\\", "/").Replace("\"", "'") + "\"");
            sb.Append(",\n  \"4095\": \"player proxy | SilverSkin\"\n}\n");
            File.WriteAllText(Path.Combine(dir, "materials.json"), sb.ToString());
        }

        /// <summary>The proxy is created after ApplyMask, so it gets its own id (4095, white).</summary>
        static void MaskProxy(GameObject proxy, List<Object> temporaries)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader) { name = "Mask proxy", hideFlags = HideFlags.DontSave };
            m.SetColor("_BaseColor", Color.white);
            temporaries.Add(m);
            proxy.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        // ---------------------------------------------------------------- proxy, vfx, camera, render

        static float s_ProxyClearance;
        static string s_LastStack = "";

        static GameObject CreatePlayerProxy(List<Object> temporaries)
        {
            var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GUIDToAssetPath(PlayerPrefabGuid));
            try
            {
                var smr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var capsule = root.GetComponents<CapsuleCollider>().First(c => !c.isTrigger);
                var mesh = new Mesh { name = "DuskStudyProxy", hideFlags = HideFlags.DontSave };
                smr.BakeMesh(mesh, true);
                var toRoot = root.transform.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = toRoot.MultiplyPoint3x4(vertices[i]);
                mesh.vertices = vertices;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                temporaries.Add(mesh);
                var go = new GameObject("DuskStudyProxy") { hideFlags = HideFlags.DontSave };
                go.layer = LayerMask.NameToLayer("Buddah");
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = smr.sharedMaterial;
                renderer.shadowCastingMode = smr.shadowCastingMode;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                s_ProxyClearance = capsule.height * 0.5f - capsule.center.y;
                temporaries.Add(go);
                return go;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void PlaceProxy(GameObject proxy, RaceMapViewpointCapture.Viewpoint view)
        {
            proxy.SetActive(view.HasPlayer);
            if (!view.HasPlayer)
                return;
            var p = view.PlayerPosition;
            float ground = p.y;
            int mask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Buddah", "TrackEdge", "RollbackBoundingBox");
            if (Physics.Raycast(new Vector3(p.x, p.y + 30f, p.z), Vector3.down, out var hit, 80f, mask, QueryTriggerInteraction.Ignore))
                ground = hit.point.y;
            proxy.transform.SetPositionAndRotation(new Vector3(p.x, ground + s_ProxyClearance, p.z), Quaternion.Euler(0f, view.PlayerYaw, 0f));
        }

        static GameObject SpawnVfx(string kind, float seconds, Transform proxy, List<Object> temporaries)
        {
            string guid = kind == "palm" ? PalmReleaseGuid : kind == "slow" ? SlowReleaseGuid : kind == "accel" ? AccelReleaseGuid : kind == "cast" ? CastGuid : throw new ArgumentException("vfx:" + kind);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var go = (GameObject)Object.Instantiate(prefab);
            go.name = "DuskStudyVfx";
            go.hideFlags = HideFlags.DontSave;
            temporaries.Add(go);
            foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true))
                b.enabled = false;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.SetActive(true);
            // Slot layers follow the caster: CasterFollow with casterOffset.y 0.62 for the ink pool, the body for the burst.
            go.transform.position = proxy.position + new Vector3(0f, kind == "slow" ? 0.62f : 0f, 0f);
            go.transform.rotation = proxy.rotation;
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.useAutoRandomSeed = false;
                ps.randomSeed = 1234;
                var main = ps.main;
                main.playOnAwake = false;
            }
            foreach (var ps in systems)
                ps.Play(false);
            const float dt = 1f / 60f;
            for (float t = 0f; t < seconds; t += dt)
                foreach (var ps in systems)
                    ps.Simulate(dt, false, false, false);
            return go;
        }

        static Camera CreateCaptureCamera(Camera gameplay, List<Object> temporaries)
        {
            var go = new GameObject("DuskStudyCamera") { hideFlags = HideFlags.DontSave };
            temporaries.Add(go);
            var camera = go.AddComponent<Camera>();
            camera.CopyFrom(gameplay);
            camera.enabled = false;
            var source = gameplay.GetComponent<UniversalAdditionalCameraData>();
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            if (source != null)
            {
                data.renderPostProcessing = source.renderPostProcessing;
                data.antialiasing = source.antialiasing;
                data.antialiasingQuality = source.antialiasingQuality;
                data.stopNaN = source.stopNaN;
                data.dithering = source.dithering;
                data.renderShadows = source.renderShadows;
                data.volumeLayerMask = source.volumeLayerMask;
                data.volumeTrigger = null;
                data.requiresColorOption = source.requiresColorOption;
                data.requiresDepthOption = source.requiresDepthOption;
            }
            return camera;
        }

        static Rect ScreenRect(Camera camera, GameObject proxy)
        {
            if (proxy == null || !proxy.activeSelf)
                return Rect.zero;
            var b = proxy.GetComponent<MeshRenderer>().bounds;
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                var s = camera.WorldToScreenPoint(corner);
                // Screen y is bottom-up; images are top-down.
                float x = s.x / camera.pixelWidth * Width, y = (1f - s.y / camera.pixelHeight) * Height;
                xMin = Mathf.Min(xMin, x); xMax = Mathf.Max(xMax, x); yMin = Mathf.Min(yMin, y); yMax = Mathf.Max(yMax, y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        static void Render(Camera camera, string path, Options options)
        {
            RenderTexture target, readback = null;
            if (options.Hdr)
            {
                target = RenderTexture.GetTemporary(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.DefaultHDR, 24) { msaaSamples = Mathf.Max(1, options.Msaa) });
                readback = RenderTexture.GetTemporary(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 0) { sRGB = true });
            }
            else
                target = RenderTexture.GetTemporary(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = Mathf.Max(1, options.Msaa) });
            var previous = RenderTexture.active;
            Texture2D tex = null;
            try
            {
                camera.targetTexture = target;
                // The project updates volumes via scripting, so a profile override needs an explicit stack update.
                camera.UpdateVolumeStack();
                for (int i = 0; i < 3; i++)
                    camera.Render();
                var stackCa = VolumeManager.instance.stack.GetComponent<ColorAdjustments>();
                s_LastStack = "stack filter=" + stackCa.colorFilter.value.ToString("F2") + " sat=" + stackCa.saturation.value + " wb=" + VolumeManager.instance.stack.GetComponent<WhiteBalance>().temperature.value;
                var source = target;
                if (readback != null)
                {
                    Graphics.Blit(target, readback);
                    source = readback;
                }
                RenderTexture.active = source;
                tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                if (readback != null)
                    RenderTexture.ReleaseTemporary(readback);
                if (tex != null)
                    Object.DestroyImmediate(tex);
            }
        }
    }
}
