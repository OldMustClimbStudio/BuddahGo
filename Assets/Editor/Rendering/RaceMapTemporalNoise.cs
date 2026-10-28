// Measures how much RaceMap's gameplay views shimmer, one rendering factor at a time.
//
// Sweep (play mode, RaceMap opened directly): a camera copied from the gameplay camera renders the fixed viewpoints of
// RaceMapViewpointCapture into an HDR target every frame while it slides sideways by a fraction of a pixel per frame.
// A well anti-aliased image barely changes under sub-pixel motion; aliased specular, geometry edges and noisy effects
// flicker. Real frames are used so TAA's jitter and history behave as in the game. Each captured frame is written to
// Docs/rendering/racemap/<label>/<config>__<view>__fNN.png (git-ignored) for Tools/rendering/temporal_noise.py.
//
// Cost (edit mode): renders a viewpoint synchronously into a 1920x1080 HDR target with a configuration and waits for
// the GPU each frame, like RaceMapRenderCost but with the game's HDR buffer and per-camera AA settings.
//
// Configurations are "name=token,token;name2=..." with tokens: quality:<index>, msaa:on|off, aa:none|fxaa|smaa|taa,
// smaaq:low|medium|high, taaq:<0-4>, taablend:<0-1>, taacas:<0-1>, taamip:<bias>, ssr:off, ssao:off, lit:urp, nrm:off, dither:off,
// shadows:off, fog:off. Everything is applied to the probe camera, runtime objects or scene objects and restored
// afterwards; no asset is written (the SSAO toggle flips a renderer feature in memory and restores it).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace BuddahGo.Rendering.Editor
{
    public static class RaceMapTemporalNoise
    {
        const int Width = 1920;
        const int Height = 1080;
        const string PlayerPrefab = "Assets/Character/Prefab/Buddah.prefab";

        static readonly Dictionary<string, string> OriginalMaterials = new Dictionary<string, string>
        {
            { "RaceMap_Lane", "Map_subsMarble_graph_0_material" },
            { "RaceMap_Curb", "Map_subsMarble 1_graph_0_material" },
            { "RaceMap_Sand", "Desert_Sand 1_graph_0_material" },
        };

        sealed class Job
        {
            public string Config;
            public string[] Tokens;
            public RaceMapViewpointCapture.Viewpoint View;
        }

        sealed class Sweep
        {
            public string OutputDir;
            public Queue<Job> Jobs;
            public int Warmup, Frames;
            public float PixelStep;
            public Job Current;
            public Restore Restore;
            public Camera Camera;
            public RenderTexture Hdr, Ldr;
            public GameObject Proxy;
            public Vector3 BasePosition, Right;
            public float MetresPerPixel;
            public int Frame, Pending;
            public Camera DisabledGameplay;
            public readonly List<string> Log = new List<string>();
        }

        static Sweep s_Sweep;

        /// <summary>Starts a sweep in play mode. Returns immediately; poll <see cref="Status"/>.</summary>
        public static string Begin(string label, string configs, string viewIds, int warmup = 12, int frames = 24, float pixelStep = 1f / 16f)
        {
            if (!Application.isPlaying)
                return "FAILED: enter play mode in RaceMap first";
            if (s_Sweep != null)
                return "FAILED: a sweep is running";
            var gameplay = Camera.main;
            if (gameplay == null)
                return "FAILED: no MainCamera";

            var views = RaceMapViewpointCapture.Viewpoints.Where(v => viewIds.Split(',').Contains(v.Id)).ToList();
            var jobs = new Queue<Job>();
            foreach (var entry in configs.Split(';').Where(e => e.Length > 0))
            {
                var parts = entry.Split('=');
                var tokens = parts.Length > 1 ? parts[1].Split(',').Where(t => t.Length > 0).ToArray() : Array.Empty<string>();
                foreach (var view in views)
                    jobs.Enqueue(new Job { Config = parts[0], Tokens = tokens, View = view });
            }

            var dir = Path.Combine(RaceMapViewpointCapture.OutputRoot, label);
            Directory.CreateDirectory(dir);
            s_Sweep = new Sweep { OutputDir = dir, Jobs = jobs, Warmup = warmup, Frames = frames, PixelStep = pixelStep };
            s_Sweep.Camera = CreateProbeCamera(gameplay);
            s_Sweep.Hdr = new RenderTexture(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.DefaultHDR, 24)) { name = "TemporalNoiseHdr" };
            s_Sweep.Ldr = new RenderTexture(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 0) { sRGB = true }) { name = "TemporalNoiseLdr" };
            s_Sweep.Camera.targetTexture = s_Sweep.Hdr;
            s_Sweep.Proxy = CreatePlayerProxy();
            // The gameplay camera would render the same scene again every frame; it is re-enabled when the sweep ends.
            gameplay.enabled = false;
            s_Sweep.DisabledGameplay = gameplay;

            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            return "started " + jobs.Count + " jobs into " + dir;
        }

        /// <summary>"running: ..." while a sweep runs, then the job log.</summary>
        public static string Status()
        {
            if (s_Sweep == null)
                return s_LastLog ?? "idle";
            return "running: " + (s_Sweep.Current != null ? s_Sweep.Current.Config + " " + s_Sweep.Current.View.Id + " frame " + s_Sweep.Frame : "between jobs")
                + ", " + s_Sweep.Jobs.Count + " jobs left";
        }

        static string s_LastLog;

        static void Tick()
        {
            var sweep = s_Sweep;
            if (sweep == null)
                return;
            try
            {
                if (sweep.Current != null)
                {
                    if (sweep.Frame >= sweep.Warmup + sweep.Frames && sweep.Pending == 0)
                    {
                        sweep.Restore?.Undo();
                        sweep.Restore = null;
                        sweep.Log.Add(sweep.Current.Config + " " + sweep.Current.View.Id + " done");
                        sweep.Current = null;
                    }
                    return;
                }
                if (sweep.Jobs.Count == 0)
                {
                    End("done");
                    return;
                }
                var job = sweep.Jobs.Dequeue();
                sweep.Current = job;
                sweep.Frame = 0;
                sweep.Restore = Apply(job.Tokens, sweep.Camera);
                MatchPipelineMsaa(sweep.Camera, sweep.Hdr);
                RaceMapViewpointCapture.PlaceCamera(sweep.Camera, job.View);
                PlaceProxy(sweep.Proxy, job.View);
                sweep.BasePosition = sweep.Camera.transform.position;
                sweep.Right = sweep.Camera.transform.right;
                // Size of one pixel at the player's distance, so the step is the same fraction of a pixel in every view.
                float distance = job.View.HasPlayer ? Vector3.Distance(sweep.BasePosition, job.View.PlayerPosition) : 100f;
                sweep.MetresPerPixel = 2f * distance * Mathf.Tan(0.5f * sweep.Camera.fieldOfView * Mathf.Deg2Rad) / Height;
            }
            catch (Exception e)
            {
                End("FAILED: " + e);
            }
        }

        static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            var sweep = s_Sweep;
            if (sweep == null || camera != sweep.Camera || sweep.Current == null)
                return;
            camera.transform.position = sweep.BasePosition + sweep.Right * (sweep.Frame * sweep.PixelStep * sweep.MetresPerPixel);
        }

        static void OnEndCamera(ScriptableRenderContext context, Camera camera)
        {
            var sweep = s_Sweep;
            if (sweep == null || camera != sweep.Camera || sweep.Current == null)
                return;
            int frame = sweep.Frame++;
            if (frame < sweep.Warmup || frame >= sweep.Warmup + sweep.Frames)
                return;
            string path = Path.Combine(sweep.OutputDir, string.Format("{0}__{1}__f{2:00}.png", sweep.Current.Config, sweep.Current.View.Id, frame - sweep.Warmup));
            var cmd = CommandBufferPool.Get("TemporalNoiseReadback");
            cmd.Blit(sweep.Hdr, sweep.Ldr);
            sweep.Pending++;
            cmd.RequestAsyncReadback(sweep.Ldr, 0, TextureFormat.RGBA32, request =>
            {
                if (!request.hasError)
                {
                    var bytes = ImageConversion.EncodeNativeArrayToPNG(request.GetData<byte>(), GraphicsFormat.R8G8B8A8_UNorm, (uint)Width, (uint)Height);
                    File.WriteAllBytes(path, bytes.ToArray());
                    bytes.Dispose();
                }
                if (s_Sweep != null)
                    s_Sweep.Pending--;
            });
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
                End("aborted: play mode ended");
        }

        static void End(string reason)
        {
            var sweep = s_Sweep;
            s_Sweep = null;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            if (sweep == null)
                return;
            sweep.Restore?.Undo();
            if (sweep.DisabledGameplay != null)
                sweep.DisabledGameplay.enabled = true;
            if (sweep.Camera != null)
            {
                sweep.Camera.targetTexture = null;
                Object.DestroyImmediate(sweep.Camera.gameObject);
            }
            if (sweep.Proxy != null)
                Object.DestroyImmediate(sweep.Proxy);
            if (sweep.Hdr != null) sweep.Hdr.Release();
            if (sweep.Ldr != null) sweep.Ldr.Release();
            sweep.Log.Add(reason);
            s_LastLog = string.Join("\n", sweep.Log);
        }

        sealed class Clip
        {
            public Camera Camera;
            public RenderTexture Hdr, Ldr;
            public RectInt Crop;
            public int Frames, Requested, Pending, Height;
            public string Dir;
            public readonly List<byte[]> Data = new List<byte[]>();
        }

        static Clip s_Clip;

        /// <summary>
        /// Play mode, during a race: records <paramref name="frames"/> consecutive frames of the gameplay camera as rendered
        /// (HDR buffer, the camera's own AA), reading back only <paramref name="crop"/> (x, y from the top-left) so the game keeps
        /// its frame rate. Frames are written to Docs/rendering/racemap/&lt;label&gt;/fNNN.png when the clip ends. Poll
        /// <see cref="ClipStatus"/>.
        /// </summary>
        public static string BeginClip(string label, int frames, int x, int y, int w, int h, int width = Width, int height = Height)
        {
            if (!Application.isPlaying || Camera.main == null)
                return "FAILED: needs play mode and a MainCamera";
            if (s_Clip != null)
                return "FAILED: a clip is recording";
            var dir = Path.Combine(RaceMapViewpointCapture.OutputRoot, label);
            Directory.CreateDirectory(dir);
            s_Clip = new Clip
            {
                Camera = Camera.main,
                Frames = frames,
                Dir = dir,
                Crop = new RectInt(x, y, w, h),
                Height = height,
                Hdr = new RenderTexture(new RenderTextureDescriptor(width, height, RenderTextureFormat.DefaultHDR, 24)) { name = "ClipHdr" },
                Ldr = new RenderTexture(new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 0) { sRGB = true }) { name = "ClipLdr" },
            };
            s_Clip.Camera.targetTexture = s_Clip.Hdr;
            MatchPipelineMsaa(s_Clip.Camera, s_Clip.Hdr);
            RenderPipelineManager.endCameraRendering += OnEndClipCamera;
            EditorApplication.update += TickClip;
            return "recording " + frames + " frames (" + width + "x" + height + ", MSAA " + s_Clip.Hdr.antiAliasing + ") into " + dir;
        }

        public static string ClipStatus() => s_Clip == null ? (s_LastClip ?? "idle") : "recording " + s_Clip.Data.Count + "/" + s_Clip.Frames;

        static string s_LastClip;

        static void OnEndClipCamera(ScriptableRenderContext context, Camera camera)
        {
            var clip = s_Clip;
            if (clip == null || camera != clip.Camera || clip.Requested >= clip.Frames)
                return;
            int index = clip.Requested++;
            var cmd = CommandBufferPool.Get("ClipReadback");
            cmd.Blit(clip.Hdr, clip.Ldr);
            clip.Pending++;
            // GPU rows run bottom-up here, so the top-left crop is converted.
            int gpuY = clip.Height - clip.Crop.y - clip.Crop.height;
            cmd.RequestAsyncReadback(clip.Ldr, 0, clip.Crop.x, clip.Crop.width, gpuY, clip.Crop.height, 0, 1, TextureFormat.RGBA32, request =>
            {
                if (s_Clip != clip)
                    return;
                clip.Pending--;
                if (request.hasError)
                    return;
                var bytes = request.GetData<byte>().ToArray();
                while (clip.Data.Count <= index) clip.Data.Add(null);
                clip.Data[index] = bytes;
            });
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        static void TickClip()
        {
            var clip = s_Clip;
            if (clip == null)
                return;
            if (!Application.isPlaying || (clip.Requested >= clip.Frames && clip.Pending == 0))
            {
                s_Clip = null;
                RenderPipelineManager.endCameraRendering -= OnEndClipCamera;
                EditorApplication.update -= TickClip;
                if (clip.Camera != null)
                    clip.Camera.targetTexture = null;
                int written = 0;
                for (int i = 0; i < clip.Data.Count; i++)
                {
                    if (clip.Data[i] == null)
                        continue;
                    using (var native = new NativeArray<byte>(clip.Data[i], Allocator.Temp))
                    {
                        // Readback rows are bottom-up; PNG wants top-down.
                        int stride = clip.Crop.width * 4;
                        var flipped = new byte[native.Length];
                        for (int row = 0; row < clip.Crop.height; row++)
                            NativeArray<byte>.Copy(native, row * stride, flipped, (clip.Crop.height - 1 - row) * stride, stride);
                        using (var src = new NativeArray<byte>(flipped, Allocator.Temp))
                        using (var png = ImageConversion.EncodeNativeArrayToPNG(src, GraphicsFormat.R8G8B8A8_UNorm, (uint)clip.Crop.width, (uint)clip.Crop.height))
                            File.WriteAllBytes(Path.Combine(clip.Dir, string.Format("f{0:000}.png", i)), png.ToArray());
                    }
                    written++;
                }
                clip.Hdr.Release();
                clip.Ldr.Release();
                s_LastClip = "wrote " + written + " frames to " + clip.Dir;
            }
        }

        /// <summary>
        /// Edit mode: median and p90 milliseconds over <paramref name="frames"/> synchronous renders of one viewpoint into a
        /// 1080p HDR target with each configuration, rounds interleaved so drift hits every configuration equally.
        /// </summary>
        public static string MeasureCost(string configs, int viewIndex, int frames, int rounds)
        {
            var gameplay = Camera.main;
            if (gameplay == null)
                return "FAILED: no MainCamera";
            var view = RaceMapViewpointCapture.Viewpoints[viewIndex];
            var entries = configs.Split(';').Where(e => e.Length > 0).Select(e => e.Split('=')).ToList();
            var samples = entries.ToDictionary(e => e[0], e => new List<double>());
            var camera = CreateProbeCamera(gameplay);
            camera.enabled = false;
            var proxy = CreatePlayerProxy();
            var rt = new RenderTexture(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.DefaultHDR, 24));
            camera.targetTexture = rt;
            try
            {
                for (int round = 0; round < rounds; round++)
                {
                    foreach (var entry in entries)
                    {
                        var restore = Apply(entry.Length > 1 ? entry[1].Split(',').Where(t => t.Length > 0).ToArray() : Array.Empty<string>(), camera);
                        try
                        {
                            MatchPipelineMsaa(camera, rt);
                            RaceMapViewpointCapture.PlaceCamera(camera, view);
                            PlaceProxy(proxy, view);
                            for (int i = 0; i < 5; i++) camera.Render();
                            AsyncGPUReadback.Request(rt).WaitForCompletion();
                            for (int i = 0; i < frames; i++)
                            {
                                var watch = Stopwatch.StartNew();
                                camera.Render();
                                AsyncGPUReadback.Request(rt, 0, 0, 1, 0, 1, 0, 1).WaitForCompletion();
                                samples[entry[0]].Add(watch.Elapsed.TotalMilliseconds);
                            }
                        }
                        finally { restore.Undo(); }
                    }
                }
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(proxy);
                rt.Release();
            }
            return string.Join("\n", samples.Select(kv =>
            {
                var sorted = kv.Value.OrderBy(v => v).ToList();
                return kv.Key + ": median " + sorted[sorted.Count / 2].ToString("F2") + " ms, p90 " + sorted[(int)(sorted.Count * 0.9)].ToString("F2") + " ms";
            }));
        }

        /// <summary>
        /// Play mode, during a race: pauses the game and renders the gameplay camera's current frame once per configuration
        /// (same tokens as <see cref="Begin"/>, entries "name=token,token;...") at <paramref name="width"/> x
        /// <paramref name="height"/> with the pipeline's MSAA, writing Docs/rendering/racemap/&lt;label&gt;/&lt;name&gt;.png.
        /// The game stays paused; unpause or stop play mode afterwards.
        /// </summary>
        public static string FreezeCompare(string label, string configs, int width, int height)
        {
            var camera = Camera.main;
            if (!Application.isPlaying || camera == null)
                return "FAILED: needs play mode and a MainCamera";
            EditorApplication.isPaused = true;
            var dir = Path.Combine(RaceMapViewpointCapture.OutputRoot, label);
            Directory.CreateDirectory(dir);
            var hdr = new RenderTexture(new RenderTextureDescriptor(width, height, RenderTextureFormat.DefaultHDR, 24)) { name = "FreezeHdr" };
            var ldr = new RenderTexture(new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 0) { sRGB = true }) { name = "FreezeLdr" };
            var previousTarget = camera.targetTexture;
            var written = new List<string>();
            try
            {
                foreach (var entry in configs.Split(';').Where(e => e.Length > 0))
                {
                    var parts = entry.Split('=');
                    var restore = Apply(parts.Length > 1 ? parts[1].Split(',').Where(t => t.Length > 0).ToArray() : Array.Empty<string>(), camera);
                    try
                    {
                        camera.targetTexture = hdr;
                        MatchPipelineMsaa(camera, hdr);
                        for (int i = 0; i < 3; i++)
                            camera.Render();
                        Graphics.Blit(hdr, ldr);
                        var request = AsyncGPUReadback.Request(ldr, 0, TextureFormat.RGBA32);
                        request.WaitForCompletion();
                        var rows = request.GetData<byte>().ToArray();
                        int stride = width * 4;
                        var flipped = new byte[rows.Length];
                        for (int row = 0; row < height; row++)
                            Buffer.BlockCopy(rows, row * stride, flipped, (height - 1 - row) * stride, stride);
                        using (var src = new NativeArray<byte>(flipped, Allocator.Temp))
                        using (var png = ImageConversion.EncodeNativeArrayToPNG(src, GraphicsFormat.R8G8B8A8_UNorm, (uint)width, (uint)height))
                            File.WriteAllBytes(Path.Combine(dir, parts[0] + ".png"), png.ToArray());
                        written.Add(parts[0]);
                    }
                    finally
                    {
                        restore.Undo();
                    }
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                hdr.Release();
                ldr.Release();
            }
            return "wrote " + string.Join(", ", written) + " to " + dir;
        }

        /// <summary>
        /// URP takes a camera's MSAA sample count from its target texture, not from the pipeline asset, so a probe target
        /// must carry the asset's count or it measures a frame without MSAA (as the game's screen target has it).
        /// </summary>
        static void MatchPipelineMsaa(Camera camera, RenderTexture target)
        {
            var asset = UniversalRenderPipeline.asset;
            int samples = camera.allowMSAA && asset != null ? Mathf.Max(1, asset.msaaSampleCount) : 1;
            if (target.antiAliasing == samples)
                return;
            camera.targetTexture = null;
            target.Release();
            target.antiAliasing = samples;
            target.Create();
            camera.targetTexture = target;
        }

        static Camera CreateProbeCamera(Camera gameplay)
        {
            var go = new GameObject("TemporalNoiseCamera") { hideFlags = HideFlags.DontSave };
            var camera = go.AddComponent<Camera>();
            camera.CopyFrom(gameplay);
            camera.enabled = true;
            var src = gameplay.GetComponent<UniversalAdditionalCameraData>();
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            if (src != null)
            {
                data.renderPostProcessing = src.renderPostProcessing;
                data.antialiasing = src.antialiasing;
                data.antialiasingQuality = src.antialiasingQuality;
                data.taaSettings = src.taaSettings;
                data.stopNaN = src.stopNaN;
                data.dithering = src.dithering;
                data.renderShadows = src.renderShadows;
                data.volumeLayerMask = src.volumeLayerMask;
                data.volumeTrigger = null;
                data.requiresColorOption = src.requiresColorOption;
                data.requiresDepthOption = src.requiresDepthOption;
            }
            return camera;
        }

        static GameObject CreatePlayerProxy()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var smr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var capsule = root.GetComponents<CapsuleCollider>().First(c => !c.isTrigger);
                var mesh = new Mesh { name = "TemporalNoiseProxy", hideFlags = HideFlags.DontSave };
                smr.BakeMesh(mesh, true);
                // Express the baked vertices relative to the body root so the proxy can be placed by its root.
                var toRoot = root.transform.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = toRoot.MultiplyPoint3x4(vertices[i]);
                mesh.vertices = vertices;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject("TemporalNoiseProxy") { hideFlags = HideFlags.DontSave };
                go.layer = LayerMask.NameToLayer("Buddah");
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = smr.sharedMaterial;
                renderer.shadowCastingMode = smr.shadowCastingMode;
                // Clearance from the body root to the bottom of the solid capsule: the root rests this far above the ground.
                s_ProxyClearance = capsule.height * 0.5f - capsule.center.y;
                return go;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static float s_ProxyClearance;

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

        sealed class Restore
        {
            readonly List<Action> m_Actions = new List<Action>();
            public void Add(Action action) => m_Actions.Add(action);
            public void Undo()
            {
                for (int i = m_Actions.Count - 1; i >= 0; i--)
                    m_Actions[i]();
                m_Actions.Clear();
            }
        }

        static Restore Apply(string[] tokens, Camera camera)
        {
            var restore = new Restore();
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            foreach (var token in tokens)
            {
                var kv = token.Split(':');
                string key = kv[0], value = kv.Length > 1 ? kv[1] : "";
                switch (key)
                {
                    case "quality":
                    {
                        int previous = QualitySettings.GetQualityLevel();
                        QualitySettings.SetQualityLevel(int.Parse(value), true);
                        restore.Add(() => QualitySettings.SetQualityLevel(previous, true));
                        break;
                    }
                    case "msaa":
                    {
                        bool previous = camera.allowMSAA;
                        camera.allowMSAA = value == "on";
                        restore.Add(() => camera.allowMSAA = previous);
                        break;
                    }
                    case "aa":
                    {
                        var previous = data.antialiasing;
                        data.antialiasing = value == "fxaa" ? AntialiasingMode.FastApproximateAntialiasing
                            : value == "smaa" ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                            : value == "taa" ? AntialiasingMode.TemporalAntiAliasing : AntialiasingMode.None;
                        restore.Add(() => data.antialiasing = previous);
                        break;
                    }
                    case "smaaq":
                    {
                        var previous = data.antialiasingQuality;
                        data.antialiasingQuality = value == "low" ? AntialiasingQuality.Low : value == "medium" ? AntialiasingQuality.Medium : AntialiasingQuality.High;
                        restore.Add(() => data.antialiasingQuality = previous);
                        break;
                    }
                    case "taaq":
                    case "taablend":
                    case "taacas":
                    case "taamip":
                    {
                        var previous = data.taaSettings;
                        ref var taa = ref data.taaSettings;
                        if (key == "taaq") taa.quality = (TemporalAAQuality)int.Parse(value);
                        if (key == "taablend") taa.baseBlendFactor = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "taacas") taa.contrastAdaptiveSharpening = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "taamip") taa.mipBias = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                        restore.Add(() => data.taaSettings = previous);
                        break;
                    }
                    case "dither":
                    {
                        bool previous = data.dithering;
                        data.dithering = value != "off";
                        restore.Add(() => data.dithering = previous);
                        break;
                    }
                    case "shadows":
                    {
                        bool previous = data.renderShadows;
                        data.renderShadows = value != "off";
                        restore.Add(() => data.renderShadows = previous);
                        break;
                    }
                    case "fog":
                    {
                        bool previous = RenderSettings.fog;
                        RenderSettings.fog = value != "off";
                        restore.Add(() => RenderSettings.fog = previous);
                        break;
                    }
                    case "ssr":
                    {
                        var go = new GameObject("TemporalNoiseVolume") { hideFlags = HideFlags.DontSave };
                        var volume = go.AddComponent<Volume>();
                        volume.isGlobal = true;
                        volume.priority = 1000f;
                        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                        profile.hideFlags = HideFlags.DontSave;
                        var ssr = profile.Add<ScreenSpaceReflection>(true);
                        ssr.state.Override(value == "on" ? ScreenSpaceReflection.State.Enabled : ScreenSpaceReflection.State.Disabled);
                        volume.sharedProfile = profile;
                        restore.Add(() => { Object.DestroyImmediate(go); Object.DestroyImmediate(profile); });
                        break;
                    }
                    case "planar":
                    {
                        var previous = BuddahGo.Rendering.PlanarReflection.SettingsScale;
                        BuddahGo.Rendering.PlanarReflection.SettingsScale = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                        restore.Add(() => BuddahGo.Rendering.PlanarReflection.SettingsScale = previous);
                        break;
                    }
                    case "ssrres":
                    case "ssrmip":
                    {
                        // The renderer feature belongs to the pipeline asset; every change is undone after the render.
                        foreach (var feature in ActiveRendererFeatures().OfType<ScreenSpaceReflectionURP>())
                        {
                            var resolution = feature.resolution;
                            var mips = feature.mipmapsMode;
                            if (key == "ssrres")
                                feature.resolution = (ScreenSpaceReflectionURP.Resolution)Enum.Parse(typeof(ScreenSpaceReflectionURP.Resolution), value);
                            else
                                feature.mipmapsMode = value == "on" ? ScreenSpaceReflectionURP.MipmapsMode.Trilinear : ScreenSpaceReflectionURP.MipmapsMode.None;
                            feature.Create();
                            restore.Add(() => { feature.resolution = resolution; feature.mipmapsMode = mips; feature.Create(); });
                        }
                        break;
                    }
                    case "ssrthick":
                    case "ssrsteps":
                    case "ssrsmooth":
                    {
                        // A high-priority volume overriding one SSR parameter on top of the scene's profile.
                        var go = new GameObject("TemporalNoiseVolume") { hideFlags = HideFlags.DontSave };
                        var volume = go.AddComponent<Volume>();
                        volume.isGlobal = true;
                        volume.priority = 1000f;
                        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                        profile.hideFlags = HideFlags.DontSave;
                        var ssr = profile.Add<ScreenSpaceReflection>(false);
                        float number = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                        if (key == "ssrthick")
                            ssr.thickness.Override(number);
                        else if (key == "ssrsmooth")
                            ssr.minSmoothness.Override(number);
                        else
                        {
                            ssr.quality.Override(ScreenSpaceReflection.Quality.Custom);
                            ssr.maxStep.Override((int)number);
                        }
                        volume.sharedProfile = profile;
                        restore.Add(() => { Object.DestroyImmediate(go); Object.DestroyImmediate(profile); });
                        break;
                    }
                    case "ssao":
                    {
                        foreach (var feature in ActiveRendererFeatures().Where(f => f.name == "SSAO" && f.isActive))
                        {
                            feature.SetActive(false);
                            restore.Add(() => feature.SetActive(true));
                        }
                        break;
                    }
                    case "lit":
                    case "nrm":
                    {
                        foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                        {
                            var shared = renderer.sharedMaterials;
                            bool changed = false;
                            var replaced = new Material[shared.Length];
                            for (int i = 0; i < shared.Length; i++)
                            {
                                replaced[i] = shared[i];
                                if (shared[i] == null || !OriginalMaterials.ContainsKey(shared[i].name))
                                    continue;
                                if (key == "lit")
                                    replaced[i] = FindMaterial(OriginalMaterials[shared[i].name]);
                                else
                                {
                                    var clone = new Material(shared[i]) { hideFlags = HideFlags.DontSave };
                                    clone.SetFloat("_NormalMapOn", 0f);
                                    clone.DisableKeyword("_NORMALMAP");
                                    replaced[i] = clone;
                                }
                                changed = true;
                            }
                            if (!changed)
                                continue;
                            renderer.sharedMaterials = replaced;
                            restore.Add(() => renderer.sharedMaterials = shared);
                        }
                        break;
                    }
                    default:
                        throw new ArgumentException("unknown token " + token);
                }
            }
            return restore;
        }

        static Material FindMaterial(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material " + name))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material != null && material.name == name)
                    return material;
            }
            throw new InvalidOperationException("material not found: " + name);
        }

        static IEnumerable<ScriptableRendererFeature> ActiveRendererFeatures()
        {
            var asset = UniversalRenderPipeline.asset;
            var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var list = field?.GetValue(asset) as ScriptableRendererData[];
            return list != null && list.Length > 0 && list[0] != null ? list[0].rendererFeatures : Enumerable.Empty<ScriptableRendererFeature>();
        }
    }
}
