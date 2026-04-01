#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ProjectConfigValidationMenu
{
    [MenuItem("Tools/Project Config/Validate All Databases")]
    public static void ValidateAllDatabases()
    {
        string[] guids = AssetDatabase.FindAssets("t:ProjectConfigDatabase");
        if (guids == null || guids.Length == 0)
        {
            Debug.LogWarning("[ProjectConfigValidation] No ProjectConfigDatabase assets found.");
            return;
        }

        int errorCount = 0;
        int warningCount = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ProjectConfigDatabase database = AssetDatabase.LoadAssetAtPath<ProjectConfigDatabase>(path);
            List<ProjectConfigValidationMessage> messages = ProjectConfigValidator.Validate(database);

            for (int j = 0; j < messages.Count; j++)
            {
                ProjectConfigValidationMessage message = messages[j];
                string logMessage = "[ProjectConfigValidation] " + path + " :: " + message.context + " :: " + message.message;
                if (message.severity == ProjectConfigValidationSeverity.Error)
                {
                    errorCount++;
                    Debug.LogError(logMessage, database);
                }
                else if (message.severity == ProjectConfigValidationSeverity.Warning)
                {
                    warningCount++;
                    Debug.LogWarning(logMessage, database);
                }
                else
                {
                    Debug.Log(logMessage, database);
                }
            }
        }

        Debug.Log("[ProjectConfigValidation] Completed. errors=" + errorCount + ", warnings=" + warningCount + ".");
    }
}
#endif
