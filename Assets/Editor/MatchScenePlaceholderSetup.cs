#if UNITY_EDITOR
using System.Collections.Generic;
using FishNet.Component.Transforming;
using FishNet.Object;
using SteamMultiplayer.Network.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MatchScenePlaceholderSetup
{
    private const string ScenePath = "Assets/Scenes/MatchScene.unity";
    private const string PrefabPath = "Assets/Prefabs/PlaceholderPlayer.prefab";

    [MenuItem("Tools/Match/Generate Minimal Match Scene")]
    public static void GenerateMinimalMatchScene()
    {
        EnsureFolder("Assets", "Prefabs");

        NetworkObject playerPrefab = CreatePlaceholderPlayerPrefab();
        CreateMatchScene(playerPrefab);
        EnsureSceneInBuildSettings(ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "MatchScene Ready",
            "Minimal MatchScene and PlaceholderPlayer prefab have been generated.",
            "OK");
    }

    private static void EnsureFolder(string parentFolder, string childFolderName)
    {
        string combinedPath = $"{parentFolder}/{childFolderName}";
        if (AssetDatabase.IsValidFolder(combinedPath))
            return;

        AssetDatabase.CreateFolder(parentFolder, childFolderName);
    }

    private static NetworkObject CreatePlaceholderPlayerPrefab()
    {
        GameObject root = new GameObject("PlaceholderPlayer");
        root.transform.position = Vector3.zero;

        CharacterController characterController = root.AddComponent<CharacterController>();
        characterController.center = new Vector3(0f, 1f, 0f);
        characterController.height = 2f;
        characterController.radius = 0.5f;
        characterController.minMoveDistance = 0f;
        characterController.skinWidth = 0.08f;

        root.AddComponent<NetworkObject>();
        root.AddComponent<NetworkTransform>();
        root.AddComponent<PlaceholderPlayerController>();

        GameObject capsuleVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsuleVisual.name = "Visual";
        capsuleVisual.transform.SetParent(root.transform, false);
        capsuleVisual.transform.localPosition = new Vector3(0f, 1f, 0f);
        capsuleVisual.transform.localRotation = Quaternion.identity;
        capsuleVisual.transform.localScale = Vector3.one;

        Collider visualCollider = capsuleVisual.GetComponent<Collider>();
        if (visualCollider != null)
            Object.DestroyImmediate(visualCollider);

        Renderer renderer = capsuleVisual.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            renderer.sharedMaterial = material;
        }

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(PrefabPath);

        return savedPrefab != null ? savedPrefab.GetComponent<NetworkObject>() : AssetDatabase.LoadAssetAtPath<NetworkObject>(PrefabPath);
    }

    private static void CreateMatchScene(NetworkObject playerPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject directionalLight = new GameObject("Directional Light");
        Light light = directionalLight.AddComponent<Light>();
        light.type = LightType.Directional;
        directionalLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        GameObject camera = new GameObject("Main Camera");
        camera.tag = "MainCamera";
        Camera cameraComponent = camera.AddComponent<Camera>();
        cameraComponent.clearFlags = CameraClearFlags.Skybox;
        camera.transform.position = new Vector3(0f, 18f, -18f);
        camera.transform.rotation = Quaternion.Euler(35f, 0f, 0f);

        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Ground";
        plane.transform.position = Vector3.zero;
        plane.transform.localScale = new Vector3(4f, 1f, 4f);

        GameObject matchRoot = new GameObject("MatchSceneRoot");

        GameObject spawnRoot = new GameObject("SpawnPoints");
        spawnRoot.transform.SetParent(matchRoot.transform, false);

        CreateSpawnPoint(spawnRoot.transform, "SpawnPoint_01", new Vector3(-6f, 0f, -6f), Quaternion.Euler(0f, 45f, 0f));
        CreateSpawnPoint(spawnRoot.transform, "SpawnPoint_02", new Vector3(6f, 0f, -6f), Quaternion.Euler(0f, 135f, 0f));
        CreateSpawnPoint(spawnRoot.transform, "SpawnPoint_03", new Vector3(-6f, 0f, 6f), Quaternion.Euler(0f, -45f, 0f));
        CreateSpawnPoint(spawnRoot.transform, "SpawnPoint_04", new Vector3(6f, 0f, 6f), Quaternion.Euler(0f, -135f, 0f));
        MatchSpawnPoint[] spawnPoints = spawnRoot.GetComponentsInChildren<MatchSpawnPoint>(true);

        GameObject spawnManagerObject = new GameObject("MatchSpawnManager");
        spawnManagerObject.transform.SetParent(matchRoot.transform, false);
        MatchSpawnManager spawnManager = spawnManagerObject.AddComponent<MatchSpawnManager>();

        SerializedObject serializedObject = new SerializedObject(spawnManager);
        serializedObject.FindProperty("_playerPrefab").objectReferenceValue = playerPrefab;
        SerializedProperty spawnPointsProperty = serializedObject.FindProperty("_spawnPoints");
        spawnPointsProperty.arraySize = spawnPoints.Length;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            spawnPointsProperty.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
        }
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void CreateSpawnPoint(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        GameObject spawnPoint = new GameObject(name);
        spawnPoint.transform.SetParent(parent, false);
        spawnPoint.transform.position = position;
        spawnPoint.transform.rotation = rotation;
        spawnPoint.AddComponent<MatchSpawnPoint>();
    }

    private static void EnsureSceneInBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.path == scenePath)
                return;
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
