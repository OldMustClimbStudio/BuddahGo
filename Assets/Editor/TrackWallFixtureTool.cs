using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BuddahGo.AI;

// Measures the wall distances of the recorded RaceMap racing line (Tools/ai/fixtures/spin-entry.json)
// against the RaceMap scene colliders and writes Tools/ai/fixtures/track-walls.json for the EditMode
// rollout tests. Read-only with respect to the scene; nothing is saved back.
public static class TrackWallFixtureTool
{
    [Serializable] class Route { public float length; public Vector3[] points; }
    [Serializable] class Walls { public string source; public int count; public float length; public float[] left, right; }

    public static void Build()
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/.worktree/single-player-mode/Assets")) throw new Exception("Wrong project");
        string fixtures = Path.Combine(Application.dataPath, "../Tools/ai/fixtures");
        var route = JsonUtility.FromJson<Route>(File.ReadAllText(Path.Combine(fixtures, "spin-entry.json")));
        EditorSceneManager.OpenScene("Assets/Scenes/RaceMap.unity", OpenSceneMode.Single);
        Physics.SyncTransforms();
        TrackWallProbe.Measure(route.points, out float[] left, out float[] right);
        int leftHits = 0, rightHits = 0;
        for (int i = 0; i < route.points.Length; i++) { if (!float.IsPositiveInfinity(left[i])) leftHits++; if (!float.IsPositiveInfinity(right[i])) rightHits++; }
        // JsonUtility cannot encode infinity: store -1 for open sides.
        for (int i = 0; i < route.points.Length; i++) { if (float.IsPositiveInfinity(left[i])) left[i] = -1f; if (float.IsPositiveInfinity(right[i])) right[i] = -1f; }
        File.WriteAllText(Path.Combine(fixtures, "track-walls.json"), JsonUtility.ToJson(new Walls {
            source = "RaceMap colliders, sideways rays 1.5 m above each spin-entry.json sample, 80 m max, triggers ignored",
            count = route.points.Length, length = route.length, left = left, right = right }));
        Debug.Log($"[TrackWallFixtureTool] samples={route.points.Length} leftHits={leftHits} rightHits={rightHits}");
        EditorApplication.Exit(0);
    }
}
