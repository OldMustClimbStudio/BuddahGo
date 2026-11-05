// Copies the gameplay renderer features (RenderingModules.GameplayFeatures: the black curtain, the racers, the map edge,
// the trails and the skill effects) from the reference renderer (URP-HighFidelity-Renderer) into the default renderer
// of every other quality level, with the reference's settings, and gives them the reference's transparent layer mask
// (the character layer is drawn by the gameplay passes, not the transparent pass). Cosmetic features (SSAO) stay as
// each renderer has them and keep their place in front of the gameplay ones.
// Menu: Tools/BuddahGo/Rendering/Sync Gameplay Passes. Re-run it after changing a gameplay feature on the reference.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BuddahGo.Rendering.Editor
{
    public static class RendererGameplayPasses
    {
        [MenuItem("Tools/BuddahGo/Rendering/Sync Gameplay Passes")]
        static void SyncMenu() => Debug.Log("[RendererGameplayPasses] " + Sync());

        /// <summary>Brings every quality level's default renderer to the reference's gameplay features; returns a report.</summary>
        public static string Sync()
        {
            var reference = FindReference();
            if (reference == null)
                return "FAILED: no renderer named " + RenderingModules.ReferenceRenderer;
            var report = new List<string>();
            var done = new HashSet<ScriptableRendererData>();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                var asset = (QualitySettings.GetRenderPipelineAssetAt(i) ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
                var data = RenderingModules.DefaultRenderer(asset) as UniversalRendererData;
                if (data == null || data == reference || !done.Add(data))
                    continue;
                report.Add(QualitySettings.names[i] + ": " + SyncOne(reference, data));
            }
            AssetDatabase.SaveAssets();
            var left = RenderingModules.CheckQualityLevels();
            report.Add(left.Count == 0 ? "contract holds on every level" : "still violated: " + string.Join("; ", left));
            return string.Join("\n", report);
        }

        static UniversalRendererData FindReference()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData " + RenderingModules.ReferenceRenderer))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data != null && data.name == RenderingModules.ReferenceRenderer)
                    return data;
            }
            return null;
        }

        static string SyncOne(UniversalRendererData reference, UniversalRendererData target)
        {
            var changes = new List<string>();
            var so = new SerializedObject(target);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");

            // Current features: cosmetic ones keep their order, gameplay ones are rebuilt after them in the reference order.
            var current = Enumerable.Range(0, features.arraySize)
                .Select(i => features.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererFeature)
                .Where(f => f != null).ToList();
            var ordered = current.Where(f => !RenderingModules.GameplayFeatures.Contains(f.name)).ToList();
            foreach (string name in RenderingModules.GameplayFeatures)
            {
                var source = reference.rendererFeatures.FirstOrDefault(f => f != null && f.name == name);
                if (source == null)
                    return "FAILED: the reference has no " + name;
                var own = current.FirstOrDefault(f => f.name == name);
                if (own == null || own.GetType() != source.GetType())
                {
                    own = (ScriptableRendererFeature)ScriptableObject.CreateInstance(source.GetType());
                    EditorUtility.CopySerialized(source, own);
                    own.name = name;
                    AssetDatabase.AddObjectToAsset(own, target);
                    changes.Add("added " + name);
                }
                else if (EditorJsonUtility.ToJson(own) != EditorJsonUtility.ToJson(source))
                {
                    EditorUtility.CopySerialized(source, own);
                    own.name = name;
                    changes.Add("updated " + name);
                }
                ordered.Add(own);
            }
            // Sub-assets no longer listed (a replaced feature of another type) are removed from the file.
            foreach (var stale in current.Where(f => !ordered.Contains(f)))
            {
                changes.Add("removed " + stale.name);
                Object.DestroyImmediate(stale, true);
            }

            features.arraySize = ordered.Count;
            map.arraySize = ordered.Count;
            for (int i = 0; i < ordered.Count; i++)
            {
                features.GetArrayElementAtIndex(i).objectReferenceValue = ordered[i];
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ordered[i], out _, out long localId);
                map.GetArrayElementAtIndex(i).longValue = localId;
            }
            var mask = so.FindProperty("m_TransparentLayerMask");
            if (mask.intValue != reference.transparentLayerMask.value)
            {
                mask.intValue = reference.transparentLayerMask.value;
                changes.Add("transparent layer mask");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            target.SetDirty();
            EditorUtility.SetDirty(target);
            return changes.Count == 0 ? "already in sync" : string.Join(", ", changes);
        }
    }
}
