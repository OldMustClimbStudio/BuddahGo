using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuddahGo.VFX.EditorTools
{
    // Review rig for the palm effect: an unsaved scene with a sand-coloured floor, an 8-unit caster capsule, the
    // RaceMap volume profile (ACES + bloom) and three cameras (caster, side, victim). Enter Play Mode afterwards;
    // SkillVfxPreviewDriver plays the cast and can freeze time for Capture.
    internal static class PalmVfxPreview
    {
        const string RaceMapProfile = "Assets/Scenes/RaceMap/RaceMap_Environment_Profile.asset";

        internal static string Setup(bool anti, float freezeAt, float timeScale)
        {
            return SetupWith(anti ? PalmVfxBuilder.AntiPrefabPath : PalmVfxBuilder.PalmPrefabPath, anti, freezeAt, timeScale);
        }

        internal static string SetupWith(string prefabPath, bool anti, float freezeAt, float timeScale)
        {
            Time.timeScale = 1f;
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Transform caster = BuildStage(null);

            var driverGo = new GameObject("PreviewDriver");
            var driver = driverGo.AddComponent<SkillVfxPreviewDriver>();
            driver.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            driver.buffPrefab = anti ? null : AssetDatabase.LoadAssetAtPath<GameObject>(PalmVfxBuilder.BufPrefabPath);
            driver.caster = caster;
            driver.freezeAt = freezeAt;
            driver.timeScale = timeScale;
            driver.fixedFrames = true;
            if (anti)
            {
                driver.buildUpSeconds = 0.3f;
                driver.lifetimeSeconds = 2f;
                driver.colliderSize = new Vector3(60f, 50f, 20f);
                driver.burstCount = 5;
                driver.burstSpacing = 30f;
            }
            return "preview scene ready (anti=" + anti + ", freezeAt=" + freezeAt + ")";
        }

        // Floor, caster, RaceMap volume and the three review cameras in the active scene; returns the caster. A null
        // floor material makes an unsaved one (fine for the throwaway preview scene).
        internal static Transform BuildStage(Material floorMaterial)
        {
            Camera main = Camera.main;
            if (main != null)
                Object.DestroyImmediate(main.gameObject);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(400f, 1f, 400f);
            floor.transform.position = new Vector3(0f, -4.25f, 0f);
            if (floorMaterial == null)
                floorMaterial = NewFloorMaterial();
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            // Unscaled root like the real character (pivot mid-body), with a capsule body 8.5 tall.
            var caster = new GameObject("Caster");
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(caster.transform, false);
            capsule.transform.localScale = new Vector3(4f, 4.25f, 4f);

            var volumeGo = new GameObject("Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RaceMapProfile);

            AddCamera("Cam_Caster", new Vector3(0f, 14f, -34f), new Vector3(0f, 18f, 60f), 60f + 30f);
            AddCamera("Cam_Side", new Vector3(420f, 70f, 260f), new Vector3(0f, 25f, 300f), 55f);
            AddCamera("Cam_Victim", new Vector3(0f, 8f, 1300f), new Vector3(0f, 25f, 0f), 60f);
            return caster.transform;
        }

        internal static Material NewFloorMaterial()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = new Color(0.78f, 0.70f, 0.58f);
            return material;
        }

        internal static Camera AddCamera(string name, Vector3 position, Vector3 lookAt, float fov)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.LookAt(lookAt);
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = fov;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 5000f;
            camera.allowHDR = true;
            var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            if (name == "Cam_Caster")
                go.tag = "MainCamera";
            return camera;
        }

        // Renders one preview camera to a PNG (works while time is frozen).
        internal static string Capture(string cameraName, string path, int width, int height)
        {
            GameObject go = GameObject.Find(cameraName);
            if (go == null)
                return "camera not found: " + cameraName;
            Camera camera = go.GetComponent<Camera>();
            // faceCamera layers turn toward Camera.main: make the captured camera the main one.
            foreach (Camera other in Object.FindObjectsOfType<Camera>())
                other.tag = other == camera ? "MainCamera" : "Untagged";
            foreach (SkillVfxPhaseController controller in Object.FindObjectsOfType<SkillVfxPhaseController>())
                controller.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);

            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.antiAliasing = 4;
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = previous;
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return "captured " + cameraName + " -> " + path;
        }
    }
}
