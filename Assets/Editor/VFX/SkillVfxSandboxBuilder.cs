using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuddahGo.VFX.EditorTools
{
    // Builds and opens the VFX debug stage (SkillVfxSandbox): the palm preview stage plus a free orbit camera, every
    // SkillVfxDatabase entry, and the other prefabs under Assets/VFX as extras for comparison. Rebuild after adding an
    // effect; open it and enter Play Mode to use the panel.
    internal static class SkillVfxSandboxBuilder
    {
        internal const string Folder = "Assets/VFX/Sandbox";
        internal const string ScenePath = Folder + "/VFX_Sandbox.unity";
        const string FloorMaterialPath = Folder + "/Mat_Sandbox_Floor.mat";
        const string DatabasePath = "Assets/Skills_Assets/SkillVfxDatabase.asset";

        // Not effects or not playable on their own: Feel players, the shared template, throwaway prototypes.
        static readonly string[] ExcludedFolders = { "Assets/VFX/FeelPrefab/", "Assets/VFX/Shared/", "Assets/VFX/如来神掌/Proto/", Folder + "/" };

        [MenuItem("Tools/BuddahGo/VFX/Open VFX Sandbox")]
        internal static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (!File.Exists(ScenePath))
                Build();
            else
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/BuddahGo/VFX/Rebuild VFX Sandbox")]
        internal static void Rebuild()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Build();
        }

        internal static string Build()
        {
            Directory.CreateDirectory(Folder);
            Time.timeScale = 1f;
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Transform caster = PalmVfxPreview.BuildStage(FloorMaterial());

            Camera free = PalmVfxPreview.AddCamera("Cam_Free", new Vector3(0f, 150f, -250f), new Vector3(0f, 25f, 150f), 55f);
            free.tag = "Untagged";
            var cameras = new List<Camera>();
            foreach (string name in new[] { "Cam_Caster", "Cam_Side", "Cam_Victim" })
                cameras.Add(GameObject.Find(name).GetComponent<Camera>());
            cameras.Add(free);

            var go = new GameObject("VFX Sandbox");
            var sandbox = go.AddComponent<SkillVfxSandbox>();
            sandbox.caster = caster;
            sandbox.cameras = cameras.ToArray();
            sandbox.database = AssetDatabase.LoadAssetAtPath<SkillVfxDatabase>(DatabasePath);
            sandbox.projectiles = new List<SkillVfxSandbox.ProjectileBinding>
            {
                new SkillVfxSandbox.ProjectileBinding { vfxId = "palm", pushId = "charged", buffVfxId = "palm_buf" },
                new SkillVfxSandbox.ProjectileBinding { vfxId = "palm_anti", pushId = "charged_anti" },
            };
            sandbox.extraPrefabs = ExtraPrefabs(sandbox.database);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            return "sandbox saved: " + ScenePath + " (" + sandbox.extraPrefabs.Count + " extra prefabs)";
        }

        static Material FloorMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            if (material != null)
                return material;
            material = PalmVfxPreview.NewFloorMaterial();
            AssetDatabase.CreateAsset(material, FloorMaterialPath);
            return material;
        }

        static List<GameObject> ExtraPrefabs(SkillVfxDatabase database)
        {
            var known = new HashSet<GameObject>(database != null ? database.entries.Where(e => e != null).Select(e => e.prefab) : Enumerable.Empty<GameObject>());
            return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/VFX" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !ExcludedFolders.Any(path.StartsWith))
                .OrderBy(path => path)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(prefab => prefab != null && !known.Contains(prefab))
                .ToList();
        }
    }
}
