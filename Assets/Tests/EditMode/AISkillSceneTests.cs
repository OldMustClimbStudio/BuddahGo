using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class AISkillSceneTests
{
    [Test] public void NetworkedTestObstaclesHaveCompleteSerializedBehaviours()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/RaceMap.unity", OpenSceneMode.Additive);
        try
        {
            var obstacles = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FishNet.Object.NetworkObject>(true))
                .Where(n => n.name.StartsWith("DebugboxCanPush", System.StringComparison.Ordinal)).ToArray();
            Assert.That(obstacles.Length, Is.EqualTo(4));
            foreach (var obstacle in obstacles)
            {
                Assert.That(obstacle.GetComponents<FishNet.Object.NetworkBehaviour>().Length, Is.GreaterThan(0), obstacle.name);
                Assert.That(obstacle.NetworkBehaviours, Is.Not.Empty, obstacle.name);
                Assert.That(obstacle.NetworkBehaviours.All(behaviour => behaviour != null), Is.True, obstacle.name + " cannot initialize/despawn with a null behaviour cache.");
                // Read the saved flag: OnValidate may repair the in-memory value when an EditMode scene opens.
                string yaml = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "Scenes/RaceMap.unity"));
                ulong id = GlobalObjectId.GetGlobalObjectIdSlow(obstacle).targetObjectId;
                string block = System.Text.RegularExpressions.Regex.Match(yaml, @"--- !u!114 &" + id + @"\r?\n.*?(?=--- !u!|\z)",
                    System.Text.RegularExpressions.RegexOptions.Singleline).Value;
                Assert.That(block, Does.Contain("WasActiveDuringEdit: " + (obstacle.gameObject.activeInHierarchy ? "1" : "0")),
                    obstacle.name + " must not spawn beneath a disabled parent.");
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    // ProductRaceHasActiveTrackDataBeforeAIStarts moved into SplineProgressTrackerTests.SavedRaceMapPositionsDoNotPlateau-
    // OrAccumulateCatchup and RacerPrefabProvidesExecutorAndDisabledCasterWithoutComboInput into SixRacerFoundationTests.
    // SerializedRacerContainsIdentityAndDisabledNormalDriver: the same scene and prefab are loaded there (04-tests.md 3, #4).
}
