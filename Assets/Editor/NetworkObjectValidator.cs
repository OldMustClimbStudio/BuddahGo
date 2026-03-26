using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class NetworkObjectValidator
{
    static NetworkObjectValidator()
    {
        // Delay to avoid competing with domain reload/asset import startup work.
        EditorApplication.delayCall += ValidateAllScenes;
    }

    [MenuItem("Tools/Network/Validate Scene NetworkObjects")]
    public static void ValidateAllScenes()
    {
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        int issueCount = 0;

        foreach (string guid in sceneGuids)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(scenePath) || !File.Exists(scenePath))
                continue;

            string[] lines = File.ReadAllLines(scenePath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                int lineNumber = i + 1;

                if (line == "NetworkBehaviours: []")
                {
                    issueCount++;
                    Debug.LogWarning($"[NetworkObjectValidator] Empty NetworkBehaviours in scene {scenePath}:{lineNumber}. This can break client sync.");
                }
                else if (line == "_componentIndexCache: 255")
                {
                    issueCount++;
                    Debug.LogWarning($"[NetworkObjectValidator] Suspicious _componentIndexCache=255 in scene {scenePath}:{lineNumber}. Check NetworkBehaviour binding.");
                }
                else if (line.StartsWith("_networkObjectCache:", StringComparison.Ordinal) && line.Contains("{fileID: 0}"))
                {
                    issueCount++;
                    Debug.LogWarning($"[NetworkObjectValidator] _networkObjectCache points to fileID 0 in scene {scenePath}:{lineNumber}. Client references may fail.");
                }
            }
        }

        if (issueCount == 0)
            Debug.Log("[NetworkObjectValidator] Validation complete. No scene network object binding issues found.");
        else
            Debug.LogWarning($"[NetworkObjectValidator] Validation complete. Found {issueCount} potential scene network object binding issues.");
    }
}
