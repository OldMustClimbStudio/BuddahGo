using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using BuddahGo.AI;

// Generates Assets/Resources/AI/{Easy,Normal,Hard}.asset from Tools/ai/difficulty-{easy,normal,hard}.json
// so the profiles that were raced against each other are exactly the ones the product loads.
public static class AIDifficultyAssetTool
{
    public static void Build()
    {
        string tools = Path.Combine(Application.dataPath, "../Tools/ai");
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources/AI"));
        foreach (string tier in new[] { "Easy", "Normal", "Hard" })
        {
            string json = File.ReadAllText(Path.Combine(tools, "difficulty-" + tier.ToLowerInvariant() + ".json"));
            string assetPath = "Assets/Resources/AI/" + tier + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<AIDifficultyProfile>(assetPath);
            var profile = existing != null ? existing : ScriptableObject.CreateInstance<AIDifficultyProfile>();
            JsonUtility.FromJsonOverwrite(json, profile);
            profile.ValidateConfiguration();
            if (existing == null) AssetDatabase.CreateAsset(profile, assetPath); else EditorUtility.SetDirty(profile);
            Debug.Log($"[AIDifficultyAssetTool] {assetPath} walls={profile.UseWallCorridor} maxAngle={profile.MaxThrustAngleDegrees} target={profile.TargetSpeed} reaction={profile.ReactionTicks}");
        }
        AssetDatabase.SaveAssets();
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
