using System.Linq;
using BuddahGo.AI;
using BuddahGo.Match;
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

    [Test] public void ProductRaceHasActiveTrackDataBeforeAIStarts()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/RaceMap.unity", OpenSceneMode.Additive);
        try
        {
            var tracks = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TrackSplineRef>(true)).ToArray();
            Assert.That(tracks.Length, Is.EqualTo(1));
            Assert.That(tracks[0].gameObject.activeInHierarchy, Is.True, "The race cannot steer or report progress without an active MapPathLine.");
            Assert.That(tracks[0].container, Is.Not.Null);
            Assert.That(tracks[0].container.Spline.Count, Is.GreaterThan(3));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [Test] public void RacerPrefabProvidesDisabledCasterAndThreeCompleteBindings()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Prefab/Buddah.prefab");
        Assert.That(prefab.GetComponent<AISkillCaster>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<AISkillCaster>().enabled, Is.False);
        Assert.That(prefab.GetComponent<SkillPerceptionState>(), Is.Not.Null);
        var input = prefab.GetComponent<ComboSkillInput>();
        for (int slot = 0; slot < 3; slot++)
        {
            Assert.That(input.TryGetSequenceForSlot(slot, out var sequence), Is.True);
            Assert.That(sequence.Length, Is.EqualTo(slot == 2 ? 4 : 3));
        }
    }
}
