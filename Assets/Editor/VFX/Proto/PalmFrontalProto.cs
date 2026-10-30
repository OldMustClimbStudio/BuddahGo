using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.VFX.EditorTools
{
    // Design-session prototype, round 2 (2026-10-28 晚, user: "重新从头开始设计，抛弃所有和手部 mesh 相关的东西；
    // 时间轴按 10 秒；先把 Ying Yang Frontal 绑定成发射的 burst"). Step 1: the pack's "Ying Yang Frontal" prefab, as
    // is, scaled to our units and fired once at the launch point along the flight direction. Nothing else yet.
    // Rebuild: Tools → BuddahGo → VFX → Proto → Build Frontal Burst Proto.
    public static class PalmFrontalProto
    {
        const string Pack = "Assets/Piloto Studio/Super Realistic FX Bundle 02/Realistic Ying Yang/";
        const string Folder = "Assets/VFX/如来神掌/Release";
        public const string PrefabPath = Folder + "/VFX_Palm_Release.prefab";
        // The VFXtests instance carries the prefab's own name (README section 6).
        public const string PlaceholderName = "VFX_Palm_Release";

        // The pack is authored at roughly 1 unit = 1 m; our character is 8 units tall.
        public static float PackScale = 8f;
        // Placement of the burst, user hand-tuned in VFXtests 2026-10-28 (second pass): a fixed point of the caster's
        // body, 2.93 up and 7 ahead along the launch direction at caster scale 1. The layer uses the CasterRelative
        // anchor, so it follows the caster's scale (PlayerScaleEffect) and any release angle, never the collider.
        // The Frontal instance itself is unrotated; the pack's two inner pivots are turned (holder_1 +90, Center -90).
        public static Vector3 CasterOffset = new Vector3(0f, 2.93f, 7f);
        public static Vector3 CasterEuler = Vector3.zero;
        public static Vector3 FrontalOffset = Vector3.zero;
        public static Vector3 FrontalEuler = Vector3.zero;
        public static Vector3 HolderEuler = new Vector3(0f, 90f, 0f);
        public static Vector3 CenterEuler = new Vector3(0f, -90f, 0f);
        // The new timeline: the whole effect is laid out over 10 s; the burst layer may run that long.
        public static float TimelineSeconds = 10f;

        [MenuItem("Tools/BuddahGo/VFX/Proto/Build Frontal Burst Proto")]
        public static void BuildMenu() { Debug.Log(Build()); }

        public static string Build()
        {
            Directory.CreateDirectory(Folder);
            var frontal = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Ying Yang Frontal.prefab");
            if (frontal == null)
                return "Ying Yang Frontal.prefab not found under " + Pack;

            var root = new GameObject("VFX_Palm_Release");
            var controller = root.AddComponent<SkillVfxPhaseController>();
            Transform scaled = Child(root.transform, "Scaled");
            Transform bodyFit = Child(scaled, "BodyFit");
            SetController(controller, "palm_release", scaled, bodyFit);

            // Imp: the Frontal burst, left at the launch point (the root flies on without it).
            var layer = Child(scaled, "FX_Palm_Imp_C_Smoke_01").gameObject.AddComponent<SkillVfxLayer>();
            layer.phase = SkillVfxPhase.Imp;
            layer.tier = SkillVfxTier.Core;
            layer.function = SkillVfxFunction.Smoke;
            layer.priority = 1;
            layer.gameplayInfo = true;
            layer.anchor = SkillVfxAnchor.CasterRelative;
            layer.casterOffset = CasterOffset;
            layer.casterEuler = CasterEuler;
            // Edit-time preview only (the anchor replaces it at runtime): where the burst lands for the reference
            // caster when the root sits at the launch point.
            layer.transform.localPosition = CasterOffset - LaunchOffset;
            layer.transform.localRotation = Quaternion.Euler(CasterEuler);
            layer.start = 0f;
            layer.endUnit = SkillVfxWindowUnit.Seconds;
            layer.end = TimelineSeconds;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(frontal);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "YingYangFrontal";
            instance.transform.SetParent(layer.transform, false);
            instance.transform.localPosition = FrontalOffset;
            instance.transform.localRotation = Quaternion.Euler(FrontalEuler);
            instance.transform.localScale = Vector3.one * PackScale;
            Transform holder = instance.transform.Find("holder_1");
            if (holder != null)
                holder.localRotation = Quaternion.Euler(HolderEuler);
            Transform center = instance.transform.Find("Center");
            if (center != null)
                center.localRotation = Quaternion.Euler(CenterEuler);
            foreach (MonoBehaviour mb in instance.GetComponentsInChildren<MonoBehaviour>(true))
                Object.DestroyImmediate(mb);   // pack helper scripts (showcase / scale updater) are not wanted
            foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.playOnAwake = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return "built " + PrefabPath + " (" + frontal.GetComponentsInChildren<ParticleSystem>(true).Length + " particle systems)";
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void SetController(SkillVfxPhaseController controller, string vfxId, Transform scaled, Transform bodyFit)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("vfxId").stringValue = vfxId;
            so.FindProperty("referenceColliderSize").vector3Value = new Vector3(60f, 90f, 20f);
            so.FindProperty("scaledRoot").objectReferenceValue = scaled;
            so.FindProperty("bodyFitRoot").objectReferenceValue = bodyFit;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- review

        // Hand tuning in VFXtests: move or turn VFX_Palm_Release/Scaled/FX_Palm_Imp_C_Smoke_01 (or its
        // YingYangFrontal child) around the Player, then run this. It turns the layer's world pose relative to the
        // Player into the CasterRelative offset, applies the placeholder to the prefab and saves the scene.
        [MenuItem("Tools/BuddahGo/VFX/Proto/Write Frontal Pose From VFXtests")]
        public static void WritePoseMenu() { Debug.Log(WritePoseFromStage()); }

        public static string WritePoseFromStage()
        {
            GameObject player = GameObject.Find("Player");
            GameObject placeholder = GameObject.Find(PlaceholderName);
            if (player == null || placeholder == null)
                return "VFXtests needs Player and " + PlaceholderName;
            SkillVfxLayer layer = placeholder.GetComponentInChildren<SkillVfxLayer>(true);
            Transform p = player.transform;
            float scale = Mathf.Abs(p.lossyScale.y);
            Quaternion yaw = Quaternion.Euler(0f, p.eulerAngles.y, 0f);

            Undo.RecordObject(placeholder.transform, "Write Frontal pose");
            Undo.RecordObject(layer, "Write Frontal pose");
            Undo.RecordObject(layer.transform, "Write Frontal pose");
            Vector3 worldPos = layer.transform.position;
            Quaternion worldRot = layer.transform.rotation;
            layer.casterOffset = Quaternion.Inverse(yaw) * (worldPos - p.position) / scale;
            layer.casterEuler = (Quaternion.Inverse(yaw) * worldRot).eulerAngles;
            placeholder.transform.SetPositionAndRotation(p.position + yaw * (LaunchOffset * scale), yaw);
            layer.transform.SetPositionAndRotation(worldPos, worldRot);
            PrefabUtility.ApplyPrefabInstance(placeholder, InteractionMode.UserAction);
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            return "casterOffset=" + layer.casterOffset.ToString("F2") + " casterEuler=" + layer.casterEuler.ToString("F1")
                + " (copy into PalmFrontalProto.CasterOffset / CasterEuler so a rebuild keeps them)";
        }

        [MenuItem("Tools/BuddahGo/VFX/Proto/Preview Frontal Burst Proto (loop, press Play)")]
        public static void PreviewMenu() { Debug.Log(Setup(-1f)); }

        public static string Setup(float freezeAt)
        {
            return PrepareVfxTests(freezeAt, false);
        }

        // A saved stage with the real player prefab (networking stripped) and one idle instance of the burst at the launch
        // point, so the spatial relation can be adjusted by hand in the editor. Play Mode still runs the timed driver.
        // The user keeps every VFX test, hand tuning included, in this scene.
        public const string StagePath = "Assets/Scenes/VFXtests.unity";
        public static readonly Vector3 LaunchOffset = new Vector3(0f, 30f, 7f);   // Push table: height 30, forward 7

        [MenuItem("Tools/BuddahGo/VFX/Proto/Open Frontal Stage (player + burst)")]
        public static void StageMenu() { Debug.Log(BuildStageScene()); }

        public static string BuildStageScene()
        {
            return PrepareVfxTests(-1f, true);
        }

        // Opens Assets/Scenes/VFXtests.unity (creates the rig if the scene is missing) and makes sure it holds: the real
        // player as the caster, the timed PreviewDriver pointed at this prototype, and one idle burst instance at the
        // launch point for hand tuning. Existing objects are kept; only what is missing is added. freezeAt < 0 loops.
        public static string PrepareVfxTests(float freezeAt, bool save)
        {
            Time.timeScale = 1f;
            bool alreadyOpen = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == StagePath;
            if (alreadyOpen)
            {
                // keep the user's unsaved hand tuning
            }
            else if (System.IO.File.Exists(StagePath))
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(StagePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            else
                return StagePath + " is missing: it is the user's local test scene (not in the repository)";

            // Caster: the real player model replaces the capsule stand-in.
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                GameObject capsule = GameObject.Find("Caster");
                var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Prefab/Buddah.prefab");
                if (playerPrefab == null)
                    return "Buddah.prefab not found";
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                PrefabUtility.UnpackPrefabInstance(player, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                player.name = "Player";
                StripNetworking(player);
                player.transform.position = capsule != null ? capsule.transform.position : Vector3.zero;
                player.transform.rotation = capsule != null ? capsule.transform.rotation : Quaternion.identity;
                if (capsule != null)
                    Object.DestroyImmediate(capsule);
            }

            // Hand-tuning placeholder at the launch point.
            if (GameObject.Find(PlaceholderName) == null)
            {
                var burst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                burst.name = PlaceholderName;
                burst.transform.position = player.transform.position + player.transform.rotation * LaunchOffset;
                burst.transform.rotation = player.transform.rotation;
            }

            // Timed driver.
            var driver = Object.FindObjectOfType<SkillVfxPreviewDriver>();
            if (driver == null)
                driver = new GameObject("PreviewDriver").AddComponent<SkillVfxPreviewDriver>();
            driver.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            driver.buffPrefab = null;
            driver.caster = player.transform;
            driver.freezeAt = freezeAt;
            driver.timeScale = 1f;
            driver.repeat = freezeAt < 0f;
            // The palm is reviewed from a standing caster (the release placeholders' preview may have left the player running).
            var motion = driver.GetComponent<SkillVfxPreviewCasterMotion>();
            if (motion != null)
                motion.enabled = false;

            if (save)
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), StagePath);
            else
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            return (save ? "saved " : "prepared ") + StagePath + " (freezeAt=" + freezeAt + ")";
        }

        static void StripNetworking(GameObject go)
        {
            var networked = new System.Collections.Generic.List<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && IsNetworked(behaviour.GetType()))
                    networked.Add(behaviour);
            networked.Sort((a, b) => (a.GetType().Name == "NetworkObject").CompareTo(b.GetType().Name == "NetworkObject"));
            foreach (MonoBehaviour behaviour in networked)
                Object.DestroyImmediate(behaviour);
        }

        static bool IsNetworked(System.Type type)
        {
            for (; type != null; type = type.BaseType)
                if (type.Namespace != null && type.Namespace.StartsWith("FishNet"))
                    return true;
            return false;
        }

        // Renders one scene camera to a PNG (edit mode or a frozen Play Mode). The captured camera becomes the main
        // camera first, so camera-facing layers turn toward it.
        public static string Capture(string cameraName, string path)
        {
            const int width = 1600, height = 900;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            GameObject go = GameObject.Find(cameraName);
            Camera camera = go != null ? go.GetComponent<Camera>() : null;
            if (camera == null)
                return "camera not found: " + cameraName;
            foreach (Camera other in Object.FindObjectsOfType<Camera>())
                other.tag = other == camera ? "MainCamera" : "Untagged";

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
