// Measures the editor-side render cost of RaceMap from one of the fixed capture viewpoints, so a rendering change can
// be compared before/after. Each frame renders the capture camera into a 1920x1080 target and waits for the GPU
// (AsyncGPUReadback.WaitForCompletion), so the number is CPU submit + GPU time serialised. It is a relative
// measurement on the development machine, not a shipped-build frame time: compare two runs, do not quote one alone.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BuddahGo.Rendering.Editor
{
    public static class RaceMapRenderCost
    {
        const int Width = 1920;
        const int Height = 1080;

        /// <summary>Returns "median ms / p90 ms" for <paramref name="frames"/> frames from viewpoint index <paramref name="viewIndex"/>.</summary>
        public static string Measure(int viewIndex, int frames)
        {
            if (EditorSceneManager.GetActiveScene().path != RaceMapViewpointCapture.ScenePath)
                return "FAILED: open RaceMap first";
            var gameplay = Camera.main;
            if (gameplay == null)
                return "FAILED: no MainCamera";
            var view = RaceMapViewpointCapture.Viewpoints[Mathf.Clamp(viewIndex, 0, RaceMapViewpointCapture.Viewpoints.Length - 1)];

            var go = new GameObject("RaceMapCostCamera") { hideFlags = HideFlags.DontSave };
            // URP takes MSAA from the target texture, so match the active pipeline asset (8x on High Fidelity).
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            int msaa = pipeline != null ? Mathf.Max(1, pipeline.msaaSampleCount) : 1;
            var rt = new RenderTexture(new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = msaa });
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.CopyFrom(gameplay);
                camera.enabled = false;
                var src = gameplay.GetComponent<UniversalAdditionalCameraData>();
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                if (src != null)
                {
                    data.renderPostProcessing = src.renderPostProcessing;
                    data.antialiasing = src.antialiasing;
                    data.renderShadows = src.renderShadows;
                    data.volumeLayerMask = src.volumeLayerMask;
                }
                RaceMapViewpointCapture.PlaceCamera(camera, view);
                camera.targetTexture = rt;

                // Warm up (shader variants, render targets, SSAO history).
                for (int i = 0; i < 10; i++)
                    RenderAndWait(camera, rt);

                var times = new List<double>(frames);
                var sw = new Stopwatch();
                for (int i = 0; i < frames; i++)
                {
                    sw.Restart();
                    RenderAndWait(camera, rt);
                    times.Add(sw.Elapsed.TotalMilliseconds);
                }
                times.Sort();
                double median = times[times.Count / 2];
                double p90 = times[Mathf.Min(times.Count - 1, (int)(times.Count * 0.9))];
                return view.Id + ": median " + median.ToString("F2") + " ms, p90 " + p90.ToString("F2") + " ms over " + frames + " frames, MSAA " + msaa + "x";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        static void RenderAndWait(Camera camera, RenderTexture rt)
        {
            camera.Render();
            var request = AsyncGPUReadback.Request(rt, 0, 0, 1, 0, 1, 0, 1);
            request.WaitForCompletion();
        }
    }
}
