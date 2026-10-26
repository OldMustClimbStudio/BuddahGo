// Renders RaceMap from fixed gameplay viewpoints so every rendering phase can be compared before/after.
// Menu: BuddahGo/Rendering/Capture RaceMap Viewpoints. Output: Docs/rendering/racemap/<label>/NN_<view>.png.
//
// RaceMap needs FishNet to spawn players, so the capture places a temporary proxy of the player model (baked from
// the Buddah prefab in an isolated prefab context, never instantiated with its network components) on the track
// and renders a temporary camera that copies the gameplay camera's framing. Nothing is saved to the scene.
//
// Trap: right after a script reopens the scene (EditorSceneManager.OpenScene) the editor renders the sky's environment
// reflection wrongly (the polished lane comes out about 20% darker) until a bake runs or play mode is entered and left
// once. Play mode, which reloads like a build, shows the correct state (a RaceMap player build was not checked). Check:
// in 01_start_grid the lane patch x 1000-1200, y 760-860 averages about (93, 68, 78) when correct, (76, 55, 66) when not.
// Capture after a bake or after a play-mode round trip.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BuddahGo.Rendering.Editor
{
    public static class RaceMapViewpointCapture
    {
        public const string ScenePath = "Assets/Scenes/RaceMap.unity";
        public const string OutputRoot = "Docs/rendering/racemap";
        const string PlayerPrefab = "Assets/Character/Prefab/Buddah.prefab";
        const int Width = 1920;
        const int Height = 1080;

        // Gameplay camera (CM_Gameplay): CinemachineTransposer offset from the player, fixed rotation, FOV 40.
        static readonly Vector3 GameplayOffset = new Vector3(77.34f, 60f, 6.6f);
        static readonly Vector3 GameplayEuler = new Vector3(35f, 258.4f, 0f);
        const float GameplayFov = 40f;
        const float TrackHeight = 2.62f;

        public struct Viewpoint
        {
            public string Id;
            public string Description;
            public bool HasPlayer;
            public Vector3 PlayerPosition;   // on the track surface
            public float PlayerYaw;          // track heading in degrees
            public bool GameplayCamera;      // true: camera = player + gameplay offset
            public Vector3 CameraPosition;   // used when GameplayCamera is false
            public Vector3 CameraEuler;
            public float Fov;
        }

        /// <summary>
        /// Fixed for every phase. Player positions are samples of the MapPathLine spline (track y = 2.62):
        /// start grid = centre of SpawnP1..6; marble stretch = spline t 0.325 (straight); corner = spline t 0.275
        /// (sharpest turn); intro = IntroSequenceRoot/CM_Shot1, the only in-game view that shows the sky.
        /// </summary>
        public static readonly Viewpoint[] Viewpoints =
        {
            new Viewpoint { Id = "01_start_grid", Description = "Start grid, gameplay camera", HasPlayer = true,
                PlayerPosition = new Vector3(-324f, TrackHeight, -65f), PlayerYaw = -110f, GameplayCamera = true },
            new Viewpoint { Id = "02_marble_stretch", Description = "Straight marble stretch, gameplay camera", HasPlayer = true,
                PlayerPosition = new Vector3(-155f, TrackHeight, -817f), PlayerYaw = -133f, GameplayCamera = true },
            new Viewpoint { Id = "03_corner", Description = "Sharpest corner, gameplay camera", HasPlayer = true,
                PlayerPosition = new Vector3(-4f, TrackHeight, -603f), PlayerYaw = 137f, GameplayCamera = true },
            new Viewpoint { Id = "04_intro_shot1", Description = "Intro shot 1 (CM_Shot1), shows the horizon", HasPlayer = true,
                PlayerPosition = new Vector3(-490f, TrackHeight, 128f), PlayerYaw = 150f, GameplayCamera = false,
                CameraPosition = new Vector3(-516f, 20f, 172f), CameraEuler = new Vector3(15f, 149.6f, 0f), Fov = 60f },
            new Viewpoint { Id = "05_end_field", Description = "Results ceremony (EndField/CM_End), player on ResultAnchors/SpawnPoint1", HasPlayer = true,
                PlayerPosition = new Vector3(1493.2f, 1.8f, -683.8f), PlayerYaw = 180f, GameplayCamera = false,
                CameraPosition = new Vector3(1492.1f, 81f, -650.3f), CameraEuler = new Vector3(46f, 180f, 0f), Fov = 60f },
        };

        [MenuItem("BuddahGo/Rendering/Capture RaceMap Viewpoints")]
        static void CaptureFromMenu()
        {
            Debug.Log(Capture("manual_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")));
        }

        /// <summary>Renders every viewpoint into Docs/rendering/racemap/&lt;label&gt;/. Returns one report line per image.</summary>
        public static string Capture(string label)
        {
            return Capture(label, 1);
        }

        /// <summary>
        /// Same as <see cref="Capture(string)"/> with an explicit MSAA sample count for the capture target. 1 keeps the
        /// images comparable with the phase-0 captures; 8 matches the High Fidelity pipeline (some passes, e.g. SSR's
        /// forward GBuffer, take a different path with MSAA).
        /// </summary>
        public static string Capture(string label, int msaaSamples)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                return "FAILED: open " + ScenePath + " first (the capture never opens or saves scenes itself)";

            var mainCamera = Camera.main;
            if (mainCamera == null)
                return "FAILED: RaceMap has no MainCamera";

            var dir = Path.Combine(OutputRoot, label);
            Directory.CreateDirectory(dir);
            var report = new System.Text.StringBuilder();
            var temporaries = new List<UnityEngine.Object>();
            try
            {
                var proxyMesh = BakePlayerMesh(temporaries);
                var camera = CreateCaptureCamera(mainCamera, temporaries);
                foreach (var view in Viewpoints)
                {
                    GameObject proxy = null;
                    if (view.HasPlayer && proxyMesh.Mesh != null)
                    {
                        proxy = CreatePlayerProxy(proxyMesh, view.PlayerPosition, view.PlayerYaw);
                        temporaries.Add(proxy);
                    }

                    PlaceCamera(camera, view);

                    var path = Path.Combine(dir, view.Id + ".png");
                    RenderToPng(camera, path, Mathf.Max(1, msaaSamples));
                    report.AppendLine(view.Id + ": " + path.Replace('\\', '/'));

                    if (proxy != null)
                    {
                        temporaries.Remove(proxy);
                        UnityEngine.Object.DestroyImmediate(proxy);
                    }
                }
            }
            finally
            {
                for (int i = temporaries.Count - 1; i >= 0; i--)
                    if (temporaries[i] != null)
                        UnityEngine.Object.DestroyImmediate(temporaries[i]);
            }
            return report.ToString();
        }

        /// <summary>Puts a camera at a viewpoint: the gameplay framing (player + CM_Gameplay offset) or a fixed shot.</summary>
        public static void PlaceCamera(Camera camera, Viewpoint view)
        {
            if (view.GameplayCamera)
            {
                camera.transform.position = view.PlayerPosition + GameplayOffset;
                camera.transform.rotation = Quaternion.Euler(GameplayEuler);
                camera.fieldOfView = GameplayFov;
            }
            else
            {
                camera.transform.position = view.CameraPosition;
                camera.transform.rotation = Quaternion.Euler(view.CameraEuler);
                camera.fieldOfView = view.Fov;
            }
        }

        struct PlayerProxy
        {
            public Mesh Mesh;
            public Material Material;
            public ShadowCastingMode ShadowCasting;
        }

        /// <summary>Bakes the player's skinned mesh in an isolated prefab context and keeps its renderer settings.</summary>
        static PlayerProxy BakePlayerMesh(List<UnityEngine.Object> temporaries)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var smr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr == null)
                    return new PlayerProxy();
                // Baked without scale the vertices are already in world units relative to the renderer.
                var mesh = new Mesh { name = "RaceMapCaptureProxy", hideFlags = HideFlags.DontSave };
                smr.BakeMesh(mesh, false);
                mesh.RecalculateBounds();
                temporaries.Add(mesh);
                return new PlayerProxy { Mesh = mesh, Material = smr.sharedMaterial, ShadowCasting = smr.shadowCastingMode };
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static GameObject CreatePlayerProxy(PlayerProxy proxy, Vector3 position, float yaw)
        {
            var go = new GameObject("RaceMapCaptureProxy") { hideFlags = HideFlags.DontSave };
            go.layer = LayerMask.NameToLayer("Buddah");
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<MeshFilter>().sharedMesh = proxy.Mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = proxy.Material;
            renderer.shadowCastingMode = proxy.ShadowCasting;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            return go;
        }

        static Camera CreateCaptureCamera(Camera gameplay, List<UnityEngine.Object> temporaries)
        {
            var go = new GameObject("RaceMapCaptureCamera") { hideFlags = HideFlags.DontSave };
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

        static void RenderToPng(Camera camera, string path, int msaaSamples)
        {
            // MSAA 1 by default on purpose: every phase is compared with the phase-0 captures, which were taken without
            // it. The game itself renders with the pipeline asset's MSAA (8x on High Fidelity).
            var desc = new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = msaaSamples };
            var rt = RenderTexture.GetTemporary(desc);
            var previous = RenderTexture.active;
            Texture2D tex = null;
            try
            {
                camera.targetTexture = rt;
                // A few frames so SSAO and temporal state settle.
                for (int i = 0; i < 3; i++)
                    camera.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                if (tex != null)
                    UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
