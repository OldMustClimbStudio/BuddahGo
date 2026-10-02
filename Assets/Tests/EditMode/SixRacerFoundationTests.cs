using System;
using System.Linq;
using System.Reflection;
using BuddahGo.Match;
using FishNet.Object;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SteamMultiplayer.Network.Match;

namespace BuddahGo.Tests
{
    public class SixRacerFoundationTests
    {
        [Test]
        public void AIIdsAreUniqueAndOutsideHumanNamespace()
        {
            var ids=Enumerable.Range(0,5).Select(RacerId.ForAI).ToArray();
            Assert.That(ids.Distinct().Count(),Is.EqualTo(5));
            Assert.That(ids.All(x=>x.IsAI && x.Value>=10000),Is.True);
            Assert.That(RacerId.FromClient(0).IsAI,Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(()=>RacerId.ForAI(-1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>RacerId.ForAI(5));
        }
        [Test]
        public void RegistryRejectsDuplicatesAndUnregistersOnlyTheSameBody()
        {
            var root=new GameObject("registry");var a=new GameObject("a");var b=new GameObject("b");
            try
            {
                var registry=root.AddComponent<RacerRegistry>();
                a.AddComponent<NetworkObject>();b.AddComponent<NetworkObject>();
                var first=a.AddComponent<RacerIdentity>();var second=b.AddComponent<RacerIdentity>();
                first.AssignBeforeSpawn(RacerId.ForAI(0),"Alpha");second.AssignBeforeSpawn(RacerId.ForAI(0),"Beta");
                registry.Register(first);registry.Register(first);
                Assert.Throws<InvalidOperationException>(()=>registry.Register(second));
                registry.Unregister(second);
                Assert.That(registry.TryGet(RacerId.ForAI(0),out var found),Is.True);
                Assert.That(found,Is.SameAs(first));Assert.That(first.ClientId,Is.EqualTo(-1));
                registry.Unregister(first);Assert.That(registry.All.Count,Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void SerializedRaceMapHasSixDistinctSpawnsAndCompleteLayouts()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/RaceMap.unity");
            var spawner=UnityEngine.Object.FindFirstObjectByType<MatchSpawnManager>();
            var points=new SerializedObject(spawner).FindProperty("_spawnPoints");
            Assert.That(points.arraySize,Is.EqualTo(6));
            for(int i=0;i<6;i++)for(int j=0;j<i;j++)
                Assert.That(Vector3.Distance(((Component)points.GetArrayElementAtIndex(i).objectReferenceValue).transform.position,
                    ((Component)points.GetArrayElementAtIndex(j).objectReferenceValue).transform.position),Is.GreaterThan(8f));
            Assert.That(spawner.GetComponent<RacerRegistry>(),Is.Not.Null);
            var intro=UnityEngine.Object.FindFirstObjectByType<IntroSequenceManager>();
            var layouts=new SerializedObject(intro).FindProperty("layouts");
            for(int count=4;count<=6;count++)
            {
                var layout=layouts.GetArrayElementAtIndex(count-1);Assert.That(layout.FindPropertyRelative("playerCount").intValue,Is.EqualTo(count));
                var slots=layout.FindPropertyRelative("slots");Assert.That(slots.arraySize,Is.EqualTo(count));
                for(int i=0;i<count;i++)
                {
                    var slot=(IntroSlot)slots.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.That(slot.SlotIndex,Is.EqualTo(i));Assert.That(slot.LaunchPoint,Is.Not.Null);Assert.That(slot.ForwardReference,Is.Not.Null);
                    Assert.That(UnityEngine.Object.FindFirstObjectByType<IntroSplineRegistry>().GetPathById(((SplineIntroPath)slot.IntroPath).SplineId),Is.SameAs(slot.IntroPath));
                    slot.BindPathToSlot();Assert.That(((SplineIntroPath)slot.IntroPath).TotalLength,Is.GreaterThan(1));
                    Assert.That(Vector3.Distance(slot.IntroPath.EvaluatePosition(.9999f),slot.GetLaunchPosition()),Is.LessThan(.1f));
                    for(int j=0;j<i;j++)
                    {
                        var other=(IntroSlot)slots.GetArrayElementAtIndex(j).objectReferenceValue;
                        Assert.That(((SplineIntroPath)other.IntroPath).SplineId,Is.Not.EqualTo(((SplineIntroPath)slot.IntroPath).SplineId));
                        Assert.That(Vector3.Distance(slot.GetLaunchPosition(),other.GetLaunchPosition()),Is.GreaterThan(8f));
                    }
                }
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }
        [Test]
        public void SerializedRacerContainsIdentityAndDisabledNormalDriver()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Prefab/Buddah.prefab");
            Assert.That(prefab.GetComponent<RacerIdentity>(),Is.Not.Null);
            var driver=prefab.GetComponent<BuddahGo.AI.AIRacerDriver>();
            Assert.That(driver,Is.Not.Null);Assert.That(driver.enabled,Is.False);
        }
    }
}
