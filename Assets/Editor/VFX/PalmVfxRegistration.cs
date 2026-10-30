using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuddahGo.VFX.EditorTools
{
    // Registers the 如来神掌 effect with the game (palm doc section 8 item 5): SkillVfxDatabase entries, the skill assets'
    // vfx ids, the FX shaders in the preload collection, the RaceMap warmup, and the caster-only post pulse as Feel
    // events palm_launch_local / palm_anti_launch_local on Buddah.prefab. The palm gets its own small impulse source:
    // the shared ImpluseSource.prefab also drives the reflection and acceleration feedback and stays untouched.
    internal static class PalmVfxRegistration
    {
        const string DatabasePath = "Assets/Skills_Assets/SkillVfxDatabase.asset";
        const string SkillPath = "Assets/Skills_Assets/Skills/Skill_PushProjectileHands.asset";
        const string AntiSkillPath = "Assets/Skills_Assets/Skills/Skill_PushProjectileHands_Anti.asset";
        const string ShaderCollectionPath = "Assets/VFX/PreloadShader.shadervariants";
        const string RaceMapPath = "Assets/Scenes/RaceMap.unity";
        const string BuddahPath = "Assets/Character/Prefab/Buddah.prefab";
        const string FeelFolder = "Assets/VFX/FeelPrefab";
        const string SkillLocalFeel = FeelFolder + "/技能Reflection_Local.prefab";
        const string AntiLocalFeel = FeelFolder + "/反噬Reflection_Local.prefab";
        internal const string PalmFeelPath = FeelFolder + "/Palm_Launch_Local.prefab";
        internal const string PalmAntiFeelPath = FeelFolder + "/PalmAnti_Launch_Local.prefab";
        const string SharedImpulsePath = "Assets/Character/Prefab/ImpluseSource.prefab";
        internal const string PalmImpulsePath = FeelFolder + "/ImpulseSource_Palm.prefab";
        internal const float PostPulseSeconds = 0.15f;

        internal static readonly string[] FxShaders = { "FX/Additive_Glow", "FX/Alpha_Smoke", "FX/Decal", "FX/Mesh_Energy" };

        [MenuItem("Tools/BuddahGo/VFX/Register Palm VFX")]
        internal static void RegisterAll()
        {
            RegisterDatabase();
            RegisterSkills();
            RegisterShaders();
            BuildFeel();
            RegisterFeelOnBuddah();
            RegisterWarmup();
            AssetDatabase.SaveAssets();
            Debug.Log("[PalmVfxRegistration] done");
        }

        static void RegisterDatabase()
        {
            var database = AssetDatabase.LoadAssetAtPath<SkillVfxDatabase>(DatabasePath);
            Set(database, "palm", PalmVfxBuilder.PalmPrefabPath);
            Set(database, "palm_anti", PalmVfxBuilder.AntiPrefabPath);
            Set(database, "palm_buf", PalmVfxBuilder.BufPrefabPath);
            EditorUtility.SetDirty(database);
        }

        static void Set(SkillVfxDatabase database, string id, string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            SkillVfxDatabase.Entry entry = database.entries.FirstOrDefault(e => e != null && e.vfxId == id);
            if (entry == null)
            {
                entry = new SkillVfxDatabase.Entry { vfxId = id };
                database.entries.Add(entry);
            }
            entry.prefab = prefab;
        }

        // Writing the assets once stores the new vfx id fields and drops the removed prefab slots from the YAML.
        static void RegisterSkills()
        {
            SetString(SkillPath, "vfxId", "palm");
            SetString(SkillPath, "buffVfxId", "palm_buf");
            SetString(AntiSkillPath, "vfxId", "palm_anti");
        }

        static void SetString(string path, string property, string value)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            var so = new SerializedObject(asset);
            so.FindProperty(property).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void RegisterShaders()
        {
            var collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(ShaderCollectionPath);
            foreach (string name in FxShaders)
            {
                Shader shader = Shader.Find(name);
                TryAdd(collection, shader);
                if (name == "FX/Additive_Glow")
                    TryAdd(collection, shader, "_FX_POLAR");
            }
            EditorUtility.SetDirty(collection);
        }

        static void TryAdd(ShaderVariantCollection collection, Shader shader, params string[] keywords)
        {
            try
            {
                var variant = new ShaderVariantCollection.ShaderVariant(shader, PassType.ScriptableRenderPipeline, keywords);
                if (!collection.Contains(variant))
                    collection.Add(variant);
            }
            catch (System.ArgumentException e)
            {
                Debug.LogWarning("[PalmVfxRegistration] " + shader.name + " " + string.Join(" ", keywords) + ": " + e.Message);
            }
        }

        // ---- Feel: copies of the reflection feedback without the 1 s delay, shortened to the post pulse, with the
        // palm's own small impulse.
        static void BuildFeel()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PalmImpulsePath) == null)
                AssetDatabase.CopyAsset(SharedImpulsePath, PalmImpulsePath);
            GameObject impulse = PrefabUtility.LoadPrefabContents(PalmImpulsePath);
            impulse.name = "ImpulseSource_Palm";
            Component source = impulse.GetComponents<Component>().First(c => c != null && c.GetType().Name == "CinemachineImpulseSource");
            var so = new SerializedObject(source);
            so.FindProperty("m_ImpulseDefinition.m_AmplitudeGain").floatValue = 0.3f;
            so.FindProperty("m_ImpulseDefinition.m_ImpulseDuration").floatValue = PostPulseSeconds;
            so.FindProperty("m_ImpulseDefinition.m_TimeEnvelope.m_AttackTime").floatValue = 0f;
            so.FindProperty("m_ImpulseDefinition.m_TimeEnvelope.m_SustainTime").floatValue = 0.04f;
            so.FindProperty("m_ImpulseDefinition.m_TimeEnvelope.m_DecayTime").floatValue = PostPulseSeconds - 0.04f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(impulse, PalmImpulsePath);
            PrefabUtility.UnloadPrefabContents(impulse);
            Component impulseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PalmImpulsePath).GetComponents<Component>()
                .First(c => c != null && c.GetType().Name == "CinemachineImpulseSource");

            MakeFeel(SkillLocalFeel, PalmFeelPath, "Palm_Launch_Local", impulseAsset, false);
            MakeFeel(AntiLocalFeel, PalmAntiFeelPath, "PalmAnti_Launch_Local", impulseAsset, true);
        }

        static void MakeFeel(string source, string destination, string rootName, Component impulse, bool keepWhiteBalance)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destination) == null)
                AssetDatabase.CopyAsset(source, destination);
            GameObject root = PrefabUtility.LoadPrefabContents(destination);
            root.name = rootName;
            Component player = root.GetComponentsInChildren<Component>(true).First(c => c != null && c.GetType().Name == "MMF_Player");
            var so = new SerializedObject(player);
            SerializedProperty list = so.FindProperty("FeedbacksList");
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty feedback = list.GetArrayElementAtIndex(i);
                string label = feedback.FindPropertyRelative("Label").stringValue;
                feedback.FindPropertyRelative("Timing.InitialDelay").floatValue = 0f;
                SerializedProperty shake = feedback.FindPropertyRelative("ShakeDuration");
                if (shake != null)
                    shake.floatValue = PostPulseSeconds;
                if (label.Contains("Impulse"))
                    feedback.FindPropertyRelative("ImpulseSource").objectReferenceValue = impulse;
                if (label.Contains("White Balance") && !keepWhiteBalance)
                    feedback.FindPropertyRelative("Active").boolValue = false;
                if (label.Contains("Bloom"))
                    feedback.FindPropertyRelative("RemapIntensityOne").floatValue = 2.5f;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, destination);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // Nests the two Feel players next to the existing reflection players and maps them in SkillFeelRouter.
        static void RegisterFeelOnBuddah()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BuddahPath);
            try
            {
                SkillFeelRouter router = root.GetComponentInChildren<SkillFeelRouter>(true);
                var so = new SerializedObject(router);
                SerializedProperty entries = so.FindProperty("entries");
                Transform parent = null;
                for (int i = 0; i < entries.arraySize; i++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                    var player = entry.FindPropertyRelative("feedbackPlayer").objectReferenceValue as Component;
                    if (entry.FindPropertyRelative("eventId").stringValue == "Skill_Reflection_Local" && player != null)
                        parent = PrefabUtility.GetOutermostPrefabInstanceRoot(player.gameObject).transform.parent;
                }
                if (parent == null)
                    parent = router.transform;

                MapFeel(entries, parent, "palm_launch_local", PalmFeelPath);
                MapFeel(entries, parent, "palm_anti_launch_local", PalmAntiFeelPath);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, BuddahPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void MapFeel(SerializedProperty entries, Transform parent, string eventId, string feelPath)
        {
            var feelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(feelPath);
            Transform existing = null;
            foreach (Transform child in parent)
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == feelAsset)
                    existing = child;
            }
            GameObject instance = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(feelAsset, parent);
            Component player = instance.GetComponentsInChildren<Component>(true).First(c => c != null && c.GetType().Name == "MMF_Player");

            int index = -1;
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("eventId").stringValue == eventId)
                    index = i;
            }
            if (index < 0)
            {
                index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
            }
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("eventId").stringValue = eventId;
            entry.FindPropertyRelative("feedbackPlayer").objectReferenceValue = player;
        }

        static void RegisterWarmup()
        {
            var scene = EditorSceneManager.OpenScene(RaceMapPath, OpenSceneMode.Additive);
            try
            {
                var warmup = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<VfxStartupWarmup>(true)).FirstOrDefault();
                if (warmup == null)
                {
                    Debug.LogWarning("[PalmVfxRegistration] RaceMap has no VfxStartupWarmup");
                    return;
                }
                var so = new SerializedObject(warmup);
                so.FindProperty("skillVfxDatabase").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SkillVfxDatabase>(DatabasePath);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
