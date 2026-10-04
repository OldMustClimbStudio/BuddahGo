using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BuddahGo.Editor
{
    /// <summary>Rebuilds the original VFX imports without replacing their art or shader bindings.</summary>
    internal static class VfxImportRecovery
    {
        private static readonly string[] AssetGuids =
        {
            "7f11fe28133f1a74594785107b98f2fe", // Piloto UberFXSG
            "6c750d3209196614bbf876bb4bd7644e", // Skill reflection
            "7dc83248cbf2c54468946e8fe16e0605", // Backfire reflection
            "ea3d04c1a681abd4eb4cb98da1918ae8", // Charged palm
            "d2d715d4a221c8645bf39f837f752d7a"  // Slow zone
        };

        [MenuItem("Tools/BuddahGo/Repair Original VFX Imports", false, 210)]
        private static void Repair()
        {
            foreach (string guid in AssetGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogError($"[VFX import] Original asset is missing: {guid}");
                    continue;
                }

                // A cached GraphErrorShader can report isSupported=true and no compiler
                // messages. Reimport the original source, not the material or a substitute.
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Shader[] shaders = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Shader>().ToArray();
                bool valid = shaders.Length > 0;
                foreach (Shader shader in shaders)
                {
                    if (!shader.isSupported || shader.name.StartsWith("Hidden/GraphErrorShader") ||
                        shader.name == "Hidden/InternalErrorShader" || ShaderUtil.ShaderHasError(shader))
                        valid = false;
                    foreach (var message in ShaderUtil.GetShaderMessages(shader))
                        Debug.LogWarning($"[VFX import] {path}: {message.severity} {message.platform} {message.file}:{message.line} {message.message}");
                }

                if (valid)
                    Debug.Log($"[VFX import] Rebuilt {path}: {shaders.Length} supported shader(s). Verify the visible effect in Play mode.");
                else
                    Debug.LogError($"[VFX import] {path} still has missing, unsupported or error shaders. Inspect importer and compiler messages.");
            }
        }

        [MenuItem("Tools/BuddahGo/Repair Original VFX Imports", true)]
        private static bool CanRepair() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
    }
}
