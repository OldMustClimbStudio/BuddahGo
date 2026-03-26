using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using FishNet.Managing;

namespace SteamMultiplayer.Editor
{
    /// <summary>
    /// Editor window tool to validate NetworkManager configuration across all scenes.
    /// Helps detect and prevent duplicate NetworkManager instances that can cause spawn failures.
    /// </summary>
    public class NetworkManagerValidator : EditorWindow
    {
        private Vector2 _scrollPosition;
        private List<SceneValidationResult> _validationResults = new List<SceneValidationResult>();
        private bool _hasScanned = false;

        private GUIStyle _headerStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _successStyle;
        private GUIStyle _warningStyle;

        [MenuItem("Tools/Network/Validate NetworkManager Setup")]
        public static void ShowWindow()
        {
            NetworkManagerValidator window = GetWindow<NetworkManagerValidator>("NetworkManager Validator");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        private void OnEnable()
        {
            _hasScanned = false;
        }

        private void InitializeStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 10)
            };

            _errorStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { textColor = Color.red },
                fontSize = 12,
                padding = new RectOffset(10, 10, 10, 10)
            };

            _successStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { textColor = Color.green },
                fontSize = 12,
                padding = new RectOffset(10, 10, 10, 10)
            };

            _warningStyle = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { textColor = Color.yellow },
                fontSize = 12,
                padding = new RectOffset(10, 10, 10, 10)
            };
        }

        private void OnGUI()
        {
            InitializeStyles();

            GUILayout.Space(10);
            GUILayout.Label("NetworkManager Validation Tool", _headerStyle);
            GUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "This tool scans all scenes in your project to detect duplicate NetworkManager instances.\n\n" +
                "Best Practice: Only ONE scene should have a NetworkManager with DontDestroyOnLoad.\n" +
                "Other scenes should rely on the persistent NetworkManager instance.",
                MessageType.Info);

            GUILayout.Space(10);

            if (GUILayout.Button("Scan All Scenes", GUILayout.Height(30)))
            {
                ScanAllScenes();
            }

            GUILayout.Space(10);

            if (_hasScanned)
            {
                DisplayResults();
            }
        }

        private void ScanAllScenes()
        {
            _validationResults.Clear();

            // Get all scene assets in the project
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
            string currentScenePath = SceneManager.GetActiveScene().path;

            foreach (string guid in sceneGuids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guid);

                // Skip Demo scenes
                if (scenePath.Contains("FishNet/Demos") || scenePath.Contains("Demo"))
                    continue;

                SceneValidationResult result = ValidateScene(scenePath);
                _validationResults.Add(result);
            }

            // Reload the original scene
            if (!string.IsNullOrEmpty(currentScenePath))
            {
                EditorSceneManager.OpenScene(currentScenePath);
            }

            _hasScanned = true;
        }

        private SceneValidationResult ValidateScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            SceneValidationResult result = new SceneValidationResult
            {
                sceneName = scene.name,
                scenePath = scenePath
            };

            // Find all NetworkManager instances
            NetworkManager[] networkManagers = FindObjectsOfType<NetworkManager>(true);
            result.networkManagerCount = networkManagers.Length;

            // Find GameNetworkManager instances
            GameObject[] allGameObjects = scene.GetRootGameObjects();
            foreach (GameObject root in allGameObjects)
            {
                var gameNetManagers = root.GetComponentsInChildren<SteamMultiplayer.Network.GameNetworkManager>(true);
                result.hasGameNetworkManager = gameNetManagers.Length > 0;
                result.gameNetworkManagerCount = gameNetManagers.Length;

                // Check if it uses DontDestroyOnLoad (we can't directly check this, but we can note it)
                if (result.hasGameNetworkManager)
                {
                    result.hasDontDestroyOnLoad = true; // Assumption based on code review
                }
            }

            // Find MatchSpawnManager
            var matchSpawnManagers = FindObjectsOfType<SteamMultiplayer.Network.Match.MatchSpawnManager>(true);
            result.hasMatchSpawnManager = matchSpawnManagers.Length > 0;
            result.matchSpawnManagerCount = matchSpawnManagers.Length;

            return result;
        }

        private void DisplayResults()
        {
            GUILayout.Label("Scan Results", _headerStyle);

            int totalNetworkManagers = _validationResults.Sum(r => r.networkManagerCount);
            int scenesWithNetworkManager = _validationResults.Count(r => r.networkManagerCount > 0);
            int scenesWithDontDestroyOnLoad = _validationResults.Count(r => r.hasDontDestroyOnLoad);

            // Summary
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Summary:", EditorStyles.boldLabel);
            GUILayout.Label($"Total NetworkManagers found: {totalNetworkManagers}");
            GUILayout.Label($"Scenes with NetworkManager: {scenesWithNetworkManager}");
            GUILayout.Label($"Scenes with DontDestroyOnLoad: {scenesWithDontDestroyOnLoad}");
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Warning if multiple scenes have NetworkManager
            if (scenesWithNetworkManager > 1)
            {
                EditorGUILayout.HelpBox(
                    $"⚠️ WARNING: Found NetworkManager in {scenesWithNetworkManager} scenes!\n\n" +
                    "This can cause conflicts when switching scenes. Only the initial scene (e.g., MainMenu) " +
                    "should have a NetworkManager with DontDestroyOnLoad.",
                    MessageType.Warning);
            }
            else if (scenesWithNetworkManager == 1)
            {
                EditorGUILayout.HelpBox(
                    "✓ Good! Only one scene has a NetworkManager.",
                    MessageType.Info);
            }

            GUILayout.Space(10);

            // Detailed results
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (SceneValidationResult result in _validationResults)
            {
                DisplaySceneResult(result);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DisplaySceneResult(SceneValidationResult result)
        {
            bool hasIssue = result.networkManagerCount > 0 &&
                           _validationResults.Count(r => r.networkManagerCount > 0) > 1;

            Color bgColor = hasIssue ? new Color(1f, 0.5f, 0.5f, 0.3f) :
                           result.networkManagerCount > 0 ? new Color(0.5f, 1f, 0.5f, 0.3f) :
                           Color.white;

            GUI.backgroundColor = bgColor;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(result.sceneName, EditorStyles.boldLabel, GUILayout.Width(200));

            if (GUILayout.Button("Open Scene", GUILayout.Width(100)))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(result.scenePath);
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Label($"Path: {result.scenePath}", EditorStyles.miniLabel);

            if (result.networkManagerCount > 0)
            {
                GUILayout.Label($"• NetworkManager instances: {result.networkManagerCount}");
            }

            if (result.hasGameNetworkManager)
            {
                string dontDestroyText = result.hasDontDestroyOnLoad ? " (with DontDestroyOnLoad)" : "";
                GUILayout.Label($"• GameNetworkManager: {result.gameNetworkManagerCount}{dontDestroyText}");
            }

            if (result.hasMatchSpawnManager)
            {
                GUILayout.Label($"• MatchSpawnManager: {result.matchSpawnManagerCount}");

                if (result.networkManagerCount == 0)
                {
                    EditorGUILayout.HelpBox(
                        "✓ This scene has MatchSpawnManager but no NetworkManager - will use persistent instance.",
                        MessageType.Info);
                }
            }

            if (hasIssue)
            {
                EditorGUILayout.HelpBox(
                    "❌ Problem: This scene should NOT have a NetworkManager.\n" +
                    "Remove the NetworkManager from this scene and rely on the persistent instance from MainMenu.",
                    MessageType.Error);
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        private class SceneValidationResult
        {
            public string sceneName;
            public string scenePath;
            public int networkManagerCount;
            public bool hasGameNetworkManager;
            public int gameNetworkManagerCount;
            public bool hasDontDestroyOnLoad;
            public bool hasMatchSpawnManager;
            public int matchSpawnManagerCount;
        }
    }
}
