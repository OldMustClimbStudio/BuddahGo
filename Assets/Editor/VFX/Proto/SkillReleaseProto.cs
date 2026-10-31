using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.VFX.EditorTools
{
    // Design-session prototype (2026-10-28 深夜, user: "6 个技能全部按照这种释放占位符绑定到这个场景上，我一个一个做").
    // One release placeholder per skill, built like the 如来神掌 Frontal burst: root SkillVfxPhaseController → Scaled →
    // one Imp layer with the CasterRelative anchor (a fixed point of the caster's body, turned with the release
    // direction, scaled with the caster) → an empty "Content" child. Drop pack effects into Content in VFXtests, move the
    // layer, then run "Write Release Pose (selected placeholder)". 如来神掌 keeps its own builder (PalmFrontalProto).
    public static class SkillReleaseProto
    {
        public struct Skill
        {
            public string skillId;      // gameplay id (Skill table)
            public string shortName;    // README section 6
            public string folderName;   // Assets/VFX/<技能中文名>
            public string vfxId;
            public float windowSeconds; // effectDurationSeconds from the Skill table
            public SkillVfxFunction function;
            public Vector3 launchOffset; // where the effect root sits relative to the caster (palm: the projectile spawn)
            public Vector3 casterOffset;
            // "Release" (on the caster when the skill fires) or "Hit" (on each player the skill lands on; the
            // controller's caster is then that player). User 2026-10-29: 转向反转's effect is the reversed player's.
            public string slot;
            public bool anti;           // the red reaction variant: VFX_<Short>_<Slot>_Anti

            public string Slot { get { return string.IsNullOrEmpty(slot) ? "Release" : slot; } }
            // README section 6: Assets/VFX/<中文名>/<Slot>/VFX_<Short>_<Slot>[_Anti].prefab
            public string Folder { get { return "Assets/VFX/" + folderName + "/" + Slot; } }
            public string PrefabPath { get { return Folder + "/VFX_" + shortName + "_" + Slot + (anti ? "_Anti" : "") + ".prefab"; } }
            // The VFXtests instance carries the prefab's own name (README section 6).
            public string PlaceholderName { get { return Path.GetFileNameWithoutExtension(PrefabPath); } }
            public string LayerName { get { return "FX_" + shortName + "_Imp_C_" + function + "_01"; } }
        }

        // README section 10 order after 如来神掌. Window = the skill's effectDurationSeconds (Skill table, 2026-10-28);
        // offsets start at the caster's pivot and are tuned by hand in VFXtests. All are the normal (not anti) variants.
        public static readonly Skill[] Skills =
        {
            new Skill { skillId = "giant", shortName = "Giant", folderName = "巨大化", vfxId = "giant_release", windowSeconds = 6f, function = SkillVfxFunction.Flash },
            new Skill { skillId = "reverseturn", shortName = "Reverse", folderName = "转向反转", vfxId = "reverse_release", windowSeconds = 10f, function = SkillVfxFunction.Flash },
            new Skill { skillId = "reverseturn", shortName = "Reverse", folderName = "转向反转", vfxId = "reverse_hit", windowSeconds = 10f, function = SkillVfxFunction.Smoke, slot = "Hit" },
            new Skill { skillId = "slowtrap", shortName = "Slow", folderName = "禅定缓流", vfxId = "slow_release", windowSeconds = 6f, function = SkillVfxFunction.Flash },
            new Skill { skillId = "blackcurtain", shortName = "Curtain", folderName = "一念光明", vfxId = "curtain_release", windowSeconds = 10f, function = SkillVfxFunction.Flash },
            // Played on every player while the curtain is up (BlackCurtainViewController), carries the route trail.
            new Skill { skillId = "blackcurtain", shortName = "Curtain", folderName = "一念光明", vfxId = "curtain_hit", windowSeconds = 10f, function = SkillVfxFunction.Smoke, slot = "Hit" },
            new Skill { skillId = "acceleration", shortName = "Accel", folderName = "神足通", vfxId = "accel_release", windowSeconds = 3f, function = SkillVfxFunction.Flash },
            // Shared wind-ups (user 2026-10-29): played on every player for every skill during the cast confirmation
            // delay (SkillExecutor, 1 s); they replace the character's 技能Particle / 反噬Particle. Cast uses the
            // Yixuan palette, CastAnti the red of the reaction effects (README section 2).
            new Skill { skillId = "*", shortName = "Common", folderName = "通用前摇", vfxId = "cast", windowSeconds = 1f, function = SkillVfxFunction.Glow, slot = "Cast" },
            new Skill { skillId = "*_anti", shortName = "Common", folderName = "通用前摇", vfxId = "cast_anti", windowSeconds = 1f, function = SkillVfxFunction.Glow, slot = "Cast", anti = true },
        };

        [MenuItem("Tools/BuddahGo/VFX/Proto/Release Placeholders/Build All And Place In VFXtests")]
        public static void BuildAllMenu() { Debug.Log(BuildAll()); }

        public static string BuildAll()
        {
            var log = new System.Text.StringBuilder();
            foreach (Skill skill in Skills)
                log.AppendLine(Build(skill, false));
            log.AppendLine(PlaceAll());
            return log.ToString();
        }

        // Builds the prefab only when it is missing (overwrite = false), so hand work applied to it is never lost.
        public static string Build(Skill skill, bool overwrite)
        {
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(skill.PrefabPath) != null)
                return "kept " + skill.PrefabPath;
            Directory.CreateDirectory(skill.Folder);
            AssetDatabase.Refresh();

            var root = new GameObject(Path.GetFileNameWithoutExtension(skill.PrefabPath));
            var controller = root.AddComponent<SkillVfxPhaseController>();
            Transform scaled = Child(root.transform, "Scaled");
            Transform bodyFit = Child(scaled, "BodyFit");
            var so = new SerializedObject(controller);
            so.FindProperty("vfxId").stringValue = skill.vfxId;
            so.FindProperty("scaledRoot").objectReferenceValue = scaled;
            so.FindProperty("bodyFitRoot").objectReferenceValue = bodyFit;
            // Tuned at the skill's current length; the window is a share of the phase so a table change rescales it.
            SerializedProperty references = so.FindProperty("referenceSeconds");
            references.arraySize = 1;
            references.GetArrayElementAtIndex(0).FindPropertyRelative("phase").enumValueIndex = (int)SkillVfxPhase.Imp;
            references.GetArrayElementAtIndex(0).FindPropertyRelative("seconds").floatValue = skill.windowSeconds;
            so.ApplyModifiedPropertiesWithoutUndo();

            var layer = Child(scaled, skill.LayerName).gameObject.AddComponent<SkillVfxLayer>();
            layer.phase = SkillVfxPhase.Imp;
            layer.tier = SkillVfxTier.Core;
            layer.function = skill.function;
            layer.priority = 1;
            layer.gameplayInfo = true;
            // These skills last on the caster (buff, zone, trail): the layer stays on the caster for its window.
            layer.anchor = SkillVfxAnchor.CasterAttached;
            layer.casterOffset = skill.casterOffset;
            layer.casterEuler = Vector3.zero;
            layer.startUnit = SkillVfxWindowUnit.PhaseFraction;
            layer.start = 0f;
            layer.endUnit = SkillVfxWindowUnit.PhaseFraction;
            layer.end = -1f;
            // Edit-time preview only (the anchor replaces it at runtime).
            layer.transform.localPosition = skill.casterOffset - skill.launchOffset;
            Child(layer.transform, "Content");

            PrefabUtility.SaveAsPrefabAsset(root, skill.PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return "built " + skill.PrefabPath;
        }

        // Adds the missing placeholders to the open VFXtests scene (PalmFrontalProto.StagePath) and saves it.
        // Existing objects are left alone.
        public static string PlaceAll()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != PalmFrontalProto.StagePath)
                return "open " + PalmFrontalProto.StagePath + " first";
            Transform player = FindRoot("Player");
            if (player == null)
                return "VFXtests has no Player";

            var log = new System.Text.StringBuilder();
            foreach (Skill skill in Skills)
            {
                if (FindRoot(skill.PlaceholderName) != null)
                {
                    log.AppendLine("kept " + skill.PlaceholderName);
                    continue;
                }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(skill.PrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = skill.PlaceholderName;
                Quaternion yaw = Quaternion.Euler(0f, player.eulerAngles.y, 0f);
                instance.transform.SetPositionAndRotation(player.position + yaw * (skill.launchOffset * Mathf.Abs(player.lossyScale.y)), yaw);
                log.AppendLine("placed " + skill.PlaceholderName);
            }
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            return log.ToString();
        }

        // Hand tuning: select anything inside one placeholder (palm's VFX_Palm_Release included), move or turn
        // its CasterRelative layer around the Player, then run this. It turns the layer's world pose relative to the
        // Player into casterOffset / casterEuler, puts the root back on its launch point, readies the particle systems
        // under the layer (no play on awake, hierarchy scaling), applies the placeholder to its prefab and saves the scene.
        [MenuItem("Tools/BuddahGo/VFX/Proto/Release Placeholders/Write Release Pose (selected placeholder)")]
        public static void WriteSelectedMenu() { Debug.Log(WritePose(Selection.activeGameObject)); }

        public static string WritePose(GameObject selected)
        {
            if (selected == null)
                return "select a release placeholder in VFXtests first";
            GameObject placeholder = PrefabUtility.GetOutermostPrefabInstanceRoot(selected);
            if (placeholder == null || placeholder.GetComponent<SkillVfxPhaseController>() == null)
                return selected.name + " is not inside a release placeholder";
            Transform player = FindRoot("Player");
            if (player == null)
                return "VFXtests has no Player";

            SkillVfxLayer layer = null;
            foreach (SkillVfxLayer candidate in placeholder.GetComponentsInChildren<SkillVfxLayer>(true))
                if (candidate.anchor == SkillVfxAnchor.CasterRelative || candidate.anchor == SkillVfxAnchor.CasterAttached || candidate.anchor == SkillVfxAnchor.CasterFollow)
                    layer = candidate;
            if (layer == null)
                return placeholder.name + " has no CasterRelative / CasterAttached / CasterFollow layer";

            WriteCasterPose(layer, placeholder.transform, player, LaunchOffsetOf(placeholder));
            foreach (ParticleSystem ps in layer.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.playOnAwake = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            // The layer controls its content; scripts brought in with it (pack showcase / scale helpers, the player's
            // PlayerAccelerationTrail that switches trails off until gameplay calls it) would fight the layer.
            var removed = new System.Collections.Generic.List<string>();
            foreach (MonoBehaviour behaviour in layer.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour is SkillVfxLayer)
                    continue;
                removed.Add(behaviour.GetType().Name + "@" + behaviour.name);
                Object.DestroyImmediate(behaviour);
            }
            EditorUtility.SetDirty(layer);

            // Not through the undo stack: an editor Ctrl+Z must not silently roll the prefab back.
            PrefabUtility.ApplyPrefabInstance(placeholder, InteractionMode.AutomatedAction);
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(placeholder.scene);
            return placeholder.name + ": casterOffset=" + layer.casterOffset.ToString("F2") + " casterEuler=" + layer.casterEuler.ToString("F1")
                + (removed.Count > 0 ? " removed scripts: " + string.Join(", ", removed.ToArray()) : "")
                + " -> " + AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(placeholder));
        }

        // Play Mode preview: points the VFXtests PreviewDriver at one skill's release prefab (1 s lead-in, then the
        // release layer on the caster), looping, and runs the player round a circle with Cam_Caster chasing it
        // (SkillVfxPreviewCasterMotion on the PreviewDriver object; switch it off to preview a standing caster).
        [MenuItem("Tools/BuddahGo/VFX/Proto/Release Placeholders/Preview Selected In Play Mode")]
        public static void PreviewSelectedMenu()
        {
            GameObject selected = Selection.activeGameObject;
            GameObject placeholder = selected != null ? PrefabUtility.GetOutermostPrefabInstanceRoot(selected) : null;
            GameObject prefab = placeholder != null ? PrefabUtility.GetCorrespondingObjectFromSource(placeholder) : null;
            Debug.Log(prefab != null ? Preview(prefab, -1f) : "select a release placeholder in VFXtests first");
        }

        public static string Preview(GameObject prefab, float freezeAt)
        {
            var driver = Object.FindObjectOfType<SkillVfxPreviewDriver>();
            Transform player = FindRoot("Player");
            if (driver == null || player == null)
                return "VFXtests needs PreviewDriver and Player";
            PointDriver(driver, prefab, player, freezeAt);
            var motion = driver.GetComponent<SkillVfxPreviewCasterMotion>();
            if (motion == null)
                motion = driver.gameObject.AddComponent<SkillVfxPreviewCasterMotion>();
            motion.caster = player;
            GameObject chase = GameObject.Find("Cam_Caster");
            motion.chaseCamera = chase != null ? chase.transform : null;
            motion.enabled = true;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            return "PreviewDriver -> " + AssetDatabase.GetAssetPath(prefab) + " (press Play)";
        }

        static Vector3 LaunchOffsetOf(GameObject placeholder)
        {
            string path = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(placeholder));
            if (path == PalmFrontalProto.PrefabPath)
                return PalmFrontalProto.LaunchOffset;
            foreach (Skill skill in Skills)
                if (skill.PrefabPath == path)
                    return skill.launchOffset;
            return Vector3.zero;
        }

        // Root objects only: the Player is switched off by its own components in Play Mode, so GameObject.Find misses it.
        static Transform FindRoot(string name)
        {
            foreach (GameObject go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == name)
                    return go.transform;
            return null;
        }

        internal static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // The layer stays where it was placed by hand: its world pose relative to the player becomes the
        // CasterRelative offset (player frame, player scale 1), and the placeholder root goes back to its launch point.
        internal static void WriteCasterPose(SkillVfxLayer layer, Transform placeholder, Transform player, Vector3 launchOffset)
        {
            float scale = Mathf.Abs(player.lossyScale.y);
            Quaternion yaw = Quaternion.Euler(0f, player.eulerAngles.y, 0f);
            Vector3 worldPos = layer.transform.position;
            Quaternion worldRot = layer.transform.rotation;
            layer.casterOffset = Quaternion.Inverse(yaw) * (worldPos - player.position) / scale;
            layer.casterEuler = (Quaternion.Inverse(yaw) * worldRot).eulerAngles;
            placeholder.SetPositionAndRotation(player.position + yaw * (launchOffset * scale), yaw);
            layer.transform.SetPositionAndRotation(worldPos, worldRot);
        }

        // Points the VFXtests PreviewDriver at one effect prefab, played on `caster`; freezeAt < 0 loops.
        internal static void PointDriver(SkillVfxPreviewDriver driver, GameObject prefab, Transform caster, float freezeAt)
        {
            driver.projectilePrefab = prefab;
            driver.buffPrefab = null;
            driver.caster = caster;
            driver.freezeAt = freezeAt;
            driver.timeScale = 1f;
            driver.repeat = freezeAt < 0f;
        }
    }
}
