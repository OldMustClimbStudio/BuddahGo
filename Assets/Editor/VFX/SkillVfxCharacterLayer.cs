using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuddahGo.VFX.EditorTools
{
    // Edit-time half of the render-order rule (SkillVfxPhaseController.MoveToCharacterLayer): skill effects live on the
    // character's layer so URP-HighFidelity-Renderer draws them in RenderSkillVfx, depth-tested against the character.
    // At runtime Configure moves them; in the editor nothing calls Configure, so effects authored in VFXtests or in
    // prefab mode stayed on Default and the character was painted over them. This keeps every Default-layer object
    // under a SkillVfxPhaseController on the character's layer whenever the hierarchy changes (new pack content dragged
    // in included), outside play mode.
    [InitializeOnLoad]
    public static class SkillVfxCharacterLayer
    {
        static SkillVfxCharacterLayer()
        {
            EditorApplication.hierarchyChanged += FixOpenEffects;
        }

        static void FixOpenEffects()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
                FixUnder(stage.prefabContentsRoot, stage.scene);

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    FixUnder(root, scene);
            }
        }

        static void FixUnder(GameObject root, Scene scene)
        {
            foreach (SkillVfxPhaseController controller in root.GetComponentsInChildren<SkillVfxPhaseController>(true))
            {
                if (SkillVfxPhaseController.MoveToCharacterLayer(controller.transform) == 0)
                    continue;
                foreach (Transform t in controller.GetComponentsInChildren<Transform>(true))
                    if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        // One-off for the slot prefabs themselves, so their instances in VFXtests carry no layer overrides.
        [MenuItem("Tools/BuddahGo/VFX/Move Slot Prefabs To Character Layer")]
        public static void FixSlotPrefabsMenu()
        {
            int total = 0;
            foreach (string path in VfxPackVendor.EffectPrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    continue;
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                int moved = SkillVfxPhaseController.MoveToCharacterLayer(root.transform);
                if (moved > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                total += moved;
                Debug.Log("[VFX layer] " + path + ": " + moved + " objects moved to " + SkillVfxPhaseController.CharacterLayerName);
            }
            Debug.Log("[VFX layer] slot prefabs done, " + total + " objects moved");
        }
    }
}
