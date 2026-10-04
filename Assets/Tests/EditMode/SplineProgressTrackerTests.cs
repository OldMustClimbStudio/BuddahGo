using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using Object = UnityEngine.Object;

namespace BuddahGo.Tests
{
    public class SplineProgressTrackerTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject trackObject, body;
        TrackSplineRef track, previousTrack;
        SplineProgressTracker tracker;
        Rigidbody rb;
        static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        static void SetInstance(TrackSplineRef value) => typeof(TrackSplineRef).GetProperty("Instance").SetValue(null, value);
        [SetUp] public void SetUp()
        {
            previousTrack = TrackSplineRef.Instance;
            trackObject = new GameObject("tracker regression track"); trackObject.SetActive(false);
            var container = trackObject.AddComponent<SplineContainer>();
            track = trackObject.AddComponent<TrackSplineRef>(); track.container = container;
            Configure(new[] { new Vector3(0,0,0), new Vector3(400,0,0), new Vector3(400,0,400), new Vector3(0,0,400) });
            body = new GameObject("tracker regression body"); body.SetActive(false);
            rb = body.AddComponent<Rigidbody>(); rb.useGravity = false;
            tracker = body.AddComponent<SplineProgressTracker>(); typeof(FishNet.Object.NetworkBehaviour).GetField("_networkObjectCache",Flags).SetValue(tracker,(body.GetComponent<FishNet.Object.NetworkObject>() ?? body.AddComponent<FishNet.Object.NetworkObject>())); Set(tracker,"rb",rb);
            ApplyPrefabSettings();
        }
        void ApplyPrefabSettings()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Prefab/Buddah.prefab").GetComponent<SplineProgressTracker>();
            foreach(string field in new[]{"maxProjectionDistance","jumpMetersThreshold","windowRadiusT","windowSteps","maxStepFactor"}) Set(tracker,field,Get(prefab,field));
        }
        void Configure(Vector3[] points)
        {
            var spline = new Spline();
            foreach(var point in points) spline.Add(new BezierKnot((float3)point), TangentMode.Linear);
            spline.Closed = true; track.container.Spline = spline;
            typeof(TrackSplineRef).GetMethod("Awake",Flags).Invoke(track,null);
        }
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(body); Object.DestroyImmediate(trackObject); SetInstance(previousTrack);
        }
        void Step(Vector3 position, Vector3 velocity, float dt = 1f/60)
        {
            body.transform.position = position; rb.velocity = velocity;
            typeof(SplineProgressTracker).GetMethod("UpdateProgress",Flags).Invoke(tracker,new object[]{dt});
        }
        float Delta(float a, float b) => Mathf.Repeat(a-b+track.TrackLength/2,track.TrackLength)-track.TrackLength/2;
        void AssertConsistent() => Assert.That(Mathf.Abs(Delta(track.DistanceAtT((float)Get(tracker,"_lastT01")),tracker.distanceOnTrack)),Is.LessThan(.02f));

        [TestCase(100f,1)] [TestCase(100f,-1)] [TestCase(1599f,1)] [TestCase(1f,-1)]
        public void ClampKeepsDistanceParameterAndWrapConsistent(float start, int direction)
        {
            tracker.SnapToTrackProgress(start/track.TrackLength);
            float destination = Mathf.Repeat(start+direction*20,track.TrackLength);
            track.TryEvaluateWorldPoseAtProgress01(destination/track.TrackLength,out var position,out var tangent);
            Step(position,tangent*direction); // Low speed forces the unchanged 2m step clamp.
            Assert.That(Delta(tracker.distanceOnTrack,start),Is.EqualTo(direction*2).Within(.01f));
            AssertConsistent();
            Assert.That(tracker.WrappedFromEndToStartThisFrame,Is.EqualTo(start>1500&&direction>0));
            Assert.That(tracker.WrappedFromStartToEndThisFrame,Is.EqualTo(start<2&&direction<0));
            track.TryEvaluateWorldPoseAtProgress01(tracker.progress01,out _,out var acceptedTangent);
            Assert.That(tracker.forwardDot,Is.EqualTo(Vector3.Dot(tangent*direction,acceptedTangent)).Within(.005f));
        }

        [TestCase(false)] [TestCase(true)]
        public void RejectedSampleClearsOldWrapAndUpdatesDirection(bool vertical)
        {
            tracker.SnapToTrackProgress(.9995f); tracker.SnapToTrackProgress(.0005f);
            Assert.True(tracker.WrappedFromEndToStartThisFrame);
            tracker.SnapToTrackProgress(100/track.TrackLength);
            // Seed a real reverse wrap, then reject; no repeated crossing event may escape.
            tracker.SnapToTrackProgress(.0005f); tracker.SnapToTrackProgress(.9995f);
            float before=tracker.distanceOnTrack;
            Step(vertical?new Vector3(100,31,0):new Vector3(100,0,-31),Vector3.forward);
            Assert.That(tracker.distanceOnTrack,Is.EqualTo(before));
            Assert.That(tracker.PreviousProgress01,Is.EqualTo(tracker.progress01));
            Assert.That(tracker.RawProgressDelta01,Is.Zero);
            Assert.False(tracker.WrappedFromEndToStartThisFrame); Assert.False(tracker.WrappedFromStartToEndThisFrame);
            Assert.That(tracker.forwardDot,Is.LessThan(0)); AssertConsistent();
        }

        [Test] public void FirstOutOfCorridorSampleDoesNotSeedProgress()
        {
            Step(new Vector3(100,100,0),Vector3.right);
            Assert.False((bool)Get(tracker,"_hasLast")); Assert.That(tracker.distanceOnTrack,Is.Zero);
            Step(new Vector3(100,0,0),Vector3.right);
            Assert.True((bool)Get(tracker,"_hasLast")); Assert.That(tracker.distanceOnTrack,Is.EqualTo(100).Within(.1f));
        }

        [TestCase(20f,false)] [TestCase(60f,false)] [TestCase(60f,true)]
        public void ParallelOrElevatedGlobalRoadCannotReplaceHistoryBranch(float separation, bool elevated)
        {
            var offset=elevated?Vector3.up*separation:Vector3.forward*separation;
            Configure(new[]{Vector3.zero,Vector3.right*400,Vector3.right*400+offset,offset});
            tracker.SnapToTrackProgress(100/track.TrackLength);
            for(int i=0;i<10;i++) Step(Vector3.right*103+offset,Vector3.right);
            if(separation>30) Assert.That(tracker.distanceOnTrack,Is.EqualTo(100).Within(.01f),"Selected local candidate also needs a distance check.");
            else Assert.That(tracker.distanceOnTrack,Is.EqualTo(103).Within(.2f),"Nearby opposite road must not replace the continuous branch.");
            AssertConsistent();
        }

        [Test] public void CrossingKeepsContinuousRoadInsteadOfCloserDistantBranch()
        {
            Configure(new[]{new Vector3(-200,0,0),new Vector3(200,0,0),new Vector3(200,0,200),new Vector3(0,0,200),new Vector3(0,0,-200),new Vector3(-200,0,-200)});
            tracker.SnapToTrackProgress(195/track.TrackLength);
            for(int i=0;i<5;i++) Step(new Vector3(0,0,1),Vector3.right);
            Assert.That(tracker.distanceOnTrack,Is.EqualTo(200).Within(.2f)); AssertConsistent();
        }

        [TestCase(1f)] [TestCase(-1f)]
        public void TrackerDirectionPreservesSequentialCheckpointGate(float direction)
        {
            var lap=body.AddComponent<LapProgress>(); Set(lap,"_tracker",tracker);
            var advance=typeof(LapProgress).GetMethod("TryAdvanceSplineCheckpoints",Flags);
            // Exercise the existing checkpoint rule with real tracker samples. No lap
            // counters, trigger crossings, race finish, or owner authority are fabricated.
            foreach(float gate in new[]{.25f,.5f,.75f})
            {
                tracker.SnapToTrackProgress(gate-.0005f);
                Set(lap,"_lastProgress01",tracker.progress01);
                track.TryEvaluateWorldPoseAtProgress01(gate+.0005f,out var point,out var tangent);
                Step(point,tangent*direction);
                advance.Invoke(lap,new object[]{tracker.progress01,tracker.RawProgressDelta01});
            }
            Assert.That(lap.NextCheckpointIndex,Is.EqualTo(direction>0?4:1));
            Assert.That(lap.CurrentLap,Is.Zero); // These are checkpoint unit tests, not a race.
        }

        [Serializable] class Replay { public double elapsed; public Vector3 position, velocity; public float yaw, progress; }
        [Test] public void SavedRaceMapPositionsDoNotPlateauOrAccumulateCatchup()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/RaceMap.unity",OpenSceneMode.Additive);
            try
            {
                track=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<TrackSplineRef>(true)).Single();
                typeof(TrackSplineRef).GetMethod("Awake",Flags).Invoke(track,null);
                ApplyPrefabSettings();
                var rows=File.ReadAllLines(Path.Combine(Application.dataPath, "../Tools/tracker/fixtures/stall-entry.jsonl")).Select(JsonUtility.FromJson<Replay>).ToArray();
                tracker.SnapToTrackProgress(rows[0].progress);
                float maxLag=0, maxStep=0; int plateau=0;
                for(int j=1;j<rows.Length;j++)
                {
                    var a=rows[j-1]; var b=rows[j]; int count=Math.Max(1,(int)Math.Round((b.elapsed-a.elapsed)*60));
                    float previous=tracker.distanceOnTrack;
                    for(int k=1;k<=count;k++)
                    {
                        float before=tracker.distanceOnTrack;
                        Step(Vector3.Lerp(a.position,b.position,(float)k/count),Vector3.Lerp(a.velocity,b.velocity,(float)k/count),(float)(b.elapsed-a.elapsed)/count);
                        maxStep=Mathf.Max(maxStep,Mathf.Abs(Delta(tracker.distanceOnTrack,before))); AssertConsistent();
                    }
                    SplineUtility.GetNearestPoint(track.container.Spline,(float3)track.container.transform.InverseTransformPoint(b.position),out _,out float t);
                    maxLag=Mathf.Max(maxLag,Mathf.Abs(Delta(track.DistanceAtT(t),tracker.distanceOnTrack)));
                    if(b.elapsed>=360.3 && b.elapsed<=362.6 && Mathf.Abs(Delta(tracker.distanceOnTrack,previous))<.01f) plateau++;
                }
                Assert.That(plateau,Is.Zero); Assert.That(maxLag,Is.LessThan(5f)); Assert.That(maxStep,Is.LessThanOrEqualTo(3.51f));
                TestContext.WriteLine($"maxLag={maxLag}, maxFrameStep={maxStep}, plateau={plateau}");
            }
            finally { EditorSceneManager.CloseScene(scene,true); }
        }
    }
}
