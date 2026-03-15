using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AssetUsageReport
{
    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".meta",
        ".asmdef",
        ".asmref",
        ".dll",
        ".json",
        ".md",
        ".txt",
        ".cginc",
        ".hlsl",
        ".shader",
        ".uss",
        ".uxml",
        ".rsp"
    };

    [MenuItem("Tools/Asset Audit/Report Unused Asset Candidates")]
    public static void ReportUnusedAssetCandidates()
    {
        var enabledScenePaths = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();

        if (enabledScenePaths.Length == 0)
        {
            Debug.LogWarning("Asset audit aborted: no enabled scenes in Build Settings.");
            return;
        }

        var dependencies = new HashSet<string>(
            AssetDatabase.GetDependencies(enabledScenePaths, true),
            StringComparer.OrdinalIgnoreCase);

        foreach (var resourcePath in AssetDatabase.GetAllAssetPaths().Where(path => path.Contains("/Resources/")))
        {
            dependencies.Add(resourcePath);
        }

        var candidates = AssetDatabase.GetAllAssetPaths()
            .Where(path => path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.Exists(path))
            .Where(path => !dependencies.Contains(path))
            .Where(path => !ShouldIgnore(path))
            .Select(path => new UnusedAssetCandidate(path, new FileInfo(path).Length))
            .OrderByDescending(candidate => candidate.SizeBytes)
            .ToList();

        Debug.Log(
            $"Asset audit scanned {enabledScenePaths.Length} build scenes and found {candidates.Count} unused asset candidates. " +
            "Review carefully before deleting anything loaded dynamically.");

        foreach (var candidate in candidates.Take(200))
        {
            Debug.Log($"{FormatSize(candidate.SizeBytes),8}  {candidate.Path}");
        }
    }

    private static bool ShouldIgnore(string assetPath)
    {
        if (assetPath.StartsWith("Assets/Editor/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (assetPath.Contains("/Gizmos/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(assetPath);
        return IgnoredExtensions.Contains(extension);
    }

    private static string FormatSize(long sizeBytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        double size = sizeBytes;
        var suffixIndex = 0;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:0.##} {suffixes[suffixIndex]}";
    }

    private struct UnusedAssetCandidate
    {
        public readonly string Path;
        public readonly long SizeBytes;

        public UnusedAssetCandidate(string path, long sizeBytes)
        {
            Path = path;
            SizeBytes = sizeBytes;
        }
    }
}
