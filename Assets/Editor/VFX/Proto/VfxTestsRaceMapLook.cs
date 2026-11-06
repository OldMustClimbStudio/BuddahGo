using BuddahGo.Rendering.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BuddahGo.VFX.EditorTools
{
    // VFXtests renders roughly like RaceMap (user 2026-11-06: "VFXtests 的渲染要还原 Racemap ... 大体的渲染。这样才能测试
    // 到真正效果"), so an effect tuned there looks the way it will in the race. Applies to the open VFXtests scene:
    //  - RaceMap's environment through RenderingBake.ApplyEnvironment (dusk sky, Trilight ambient, exp2 fog, sky reflection),
    //  - a sun with RaceMap's recorded MainLight values (other directional lights in the scene are switched off),
    //  - a global volume on RaceMap_Dusk_Profile (the asset itself, never a copy, never edited here),
    //  - post-processing and FXAA on every camera in the scene (RaceMap's High Fidelity mode),
    //  - the zen track material on every object named "Floor".
    // Not covered: baked light probes and lightmaps, the zen sand and its rake, planar reflection, the curtain volume.
    // Re-run safe; the scene is saved (it is local and not in the repository).
    public static class VfxTestsRaceMapLook
    {
        const string ProfilePath = "Assets/Scenes/RaceMap/RaceMap_Dusk_Profile.asset";
        const string LanePath = "Assets/Sources/RaceMapZen/Materials/RaceMapZen_Lane.mat";
        const string SunName = "RaceMap Sun (look)";
        const string VolumeName = "RaceMap Volume (look)";

        [MenuItem("Tools/BuddahGo/VFX/Proto/Apply RaceMap Look To VFXtests")]
        public static void ApplyMenu() { Debug.Log(Apply()); }

        public static string Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != PalmFrontalProto.StagePath)
                return "open " + PalmFrontalProto.StagePath + " first";
            var log = new System.Text.StringBuilder();

            RenderingBake.ApplyEnvironment();
            log.Append("environment (sky, ambient, fog, reflection); ");

            Light sun = null;
            foreach (Light light in Object.FindObjectsOfType<Light>(true))
            {
                if (light.gameObject.scene != scene || light.type != LightType.Directional)
                    continue;
                if (light.name == SunName)
                    sun = light;
                else if (light.enabled)
                {
                    light.enabled = false;
                    log.Append("switched off ").Append(light.name).Append("; ");
                }
            }
            if (sun == null)
                sun = new GameObject(SunName).AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(RenderingBake.RaceMapSunEuler);
            sun.color = RenderingBake.RaceMapSunFilter;
            sun.useColorTemperature = true;
            sun.colorTemperature = RenderingBake.RaceMapSunTemperature;
            sun.intensity = RenderingBake.RaceMapSunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.6f;
            sun.shadowNormalBias = 0.4f;
            sun.enabled = true;
            RenderSettings.sun = sun;
            log.Append("sun; ");

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            GameObject volumeObject = GameObject.Find(VolumeName);
            if (volumeObject == null)
                volumeObject = new GameObject(VolumeName);
            Volume volume = volumeObject.GetComponent<Volume>();
            if (volume == null)
                volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;   // over whatever local volumes VFXtests already has
            volume.weight = 1f;
            volume.sharedProfile = profile;
            log.Append("volume on ").Append(profile != null ? profile.name : "MISSING " + ProfilePath).Append("; ");

            int cameras = 0;
            foreach (Camera camera in Object.FindObjectsOfType<Camera>(true))
            {
                if (camera.gameObject.scene != scene || camera.targetTexture != null)
                    continue;
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                data.volumeLayerMask = ~0;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.allowHDR = true;
                camera.allowMSAA = true;
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, 3000f);
                EditorUtility.SetDirty(camera);
                EditorUtility.SetDirty(data);
                cameras++;
            }
            log.Append(cameras).Append(" camera(s) with post + FXAA; ");

            var lane = AssetDatabase.LoadAssetAtPath<Material>(LanePath);
            int floors = 0;
            foreach (Renderer renderer in Object.FindObjectsOfType<Renderer>(true))
            {
                if (renderer.gameObject.scene != scene || renderer.name != "Floor" || lane == null)
                    continue;
                renderer.sharedMaterial = lane;
                floors++;
            }
            log.Append(floors).Append(" floor(s) on ").Append(lane != null ? lane.name : "MISSING " + LanePath).Append("; ");

            EditorSceneManager.SaveScene(scene);
            return log.Append("saved ").Append(scene.path).ToString();
        }
    }
}
