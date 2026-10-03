using System;
using System.IO;
using BuddahGo.AI;
using NewBuddah.PredictionV2.Core;
using NUnit.Framework;
using UnityEngine;
public class AICostEquivalenceTests
{
    [Serializable] class Fixture {public float length;public Vector3[] points;public Sample[] states;}
    [Serializable] class Sample {public Vector3 position,velocity;public float yaw,yawRate;}
    static Fixture Read()=>JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tools/ai/fixtures/spin-entry.json")));
    static MotionParameters Params(float sign=1)=>new MotionParameters {Mass=2,InverseYawInertia=1f/12.45f,AngularDrag=.05f,MaxAngularVelocity=50,TurnDecay=3,TurnMultiplier=2,GroundDeceleration=1.962f,Stats=new BuddahPredictedMotorComputedStats {FinalForwardForce=50,FinalTurnTorque=30,FinalMaxSpeed=80,FinalSteeringSign=sign}};
    static void Equal(LineProjection a,LineProjection b)
    {Assert.That(b.Segment,Is.EqualTo(a.Segment));Assert.That(b.Point,Is.EqualTo(a.Point));Assert.That(b.Tangent,Is.EqualTo(a.Tangent));Assert.That(b.Distance,Is.EqualTo(a.Distance));Assert.That(b.Lateral,Is.EqualTo(a.Lateral));Assert.That(b.Pace,Is.EqualTo(a.Pace));}
    [Test] public void ProjectionMatchesLegacyIncludingTiesDegeneratesWrapAndSearchWindows()
    {
        var f=Read();var random=new System.Random(5931);
        foreach(var points in new[]{f.points,new[]{Vector3.zero,Vector3.zero,new Vector3(0,7,0),Vector3.right*10,new Vector3(10,3,10),Vector3.forward*10}})
        {
            var old=new LegacySplineRacingLine(points,f.length);var current=new SplineRacingLine(points,f.length);old.PreparePace(25,80,.8f);current.PreparePace(25,80,.8f);
            for(int i=0;i<2000;i++){int index=random.Next(points.Length);var position=points[index]+new Vector3((float)random.NextDouble()*100-50,(float)random.NextDouble()*30,(float)random.NextDouble()*100-50);int near=i%4==0?-1:index;int range=i%3==0?40:i%3==1?12:points.Length+2;Equal(old.Project(position,near,range),current.Project(position,near,range));}
            foreach(var point in points)Equal(old.Project(point,0,points.Length+2),current.Project(point,0,points.Length+2));
        }
    }
    [TestCase(1f)] [TestCase(-1f)]
    public void SavedCornerAndSequentialPlansMatchLegacyCostsChoicesAndWinding(float sign)
    {
        var f=Read();var profile=ScriptableObject.CreateInstance<AIDifficultyProfile>();
        try
        {
            var oldLine=new LegacySplineRacingLine(f.points,f.length);var line=new SplineRacingLine(f.points,f.length);
            var old=new LegacyForwardSimPlanner();var current=new ForwardSimPlanner();int previous=0;int n=0;
            foreach(var sample in f.states)
            {
                var state=new MotionState {Position=sample.position,Velocity=sample.velocity,Yaw=sample.yaw*Mathf.Deg2Rad,YawRate=sample.yawRate};
                for(int k=0;k<5;k++)
                {
                    var parameters=Params(sign);int expected=old.Plan(state,parameters,oldLine,profile,1f/60,previous);int actual=current.Plan(state,parameters,line,profile,1f/60,previous);
                    Assert.That(actual,Is.EqualTo(expected),"plan "+n++);var a=old.LastObservation;var b=current.LastObservation;
                    Assert.That(b.neutralCost,Is.EqualTo(a.neutralCost));Assert.That(b.leftCost,Is.EqualTo(a.leftCost));Assert.That(b.rightCost,Is.EqualTo(a.rightCost));Assert.That(b.rejectedWinding,Is.EqualTo(a.rejectedWinding));Assert.That(b.viableFirstKeys,Is.EqualTo(a.viableFirstKeys));Assert.That(b.selectedYawChange,Is.EqualTo(a.selectedYawChange));
                    previous=actual;for(int t=0;t<3;t++)LegacyBuddahMotionModel.Step(ref state,parameters,actual,1f/60);
                }
            }
            TestContext.WriteLine("Exact plan comparisons: "+n);
        }
        finally {UnityEngine.Object.DestroyImmediate(profile);}
    }
    [Test] public void GeometrySnapshotCannotBeMutatedAndPaceIsPerLine()
    {
        var points = new[]{Vector3.zero, Vector3.right*10, Vector3.forward*10};
        var a = new SplineRacingLine(points, 30); var b = new SplineRacingLine(points, 30);
        var before = a.Project(Vector3.right*4, -1);
        points[0] = Vector3.one*999; a.Points[1] = Vector3.one*999;
        Equal(before, a.Project(Vector3.right*4,-1));
        a.PreparePace(25,80,.8f); b.PreparePace(10,20,.2f);
        Assert.That(a.Project(Vector3.zero,-1).Pace,Is.Not.EqualTo(b.Project(Vector3.zero,-1).Pace));
    }
    [Test] public void CapturesShareOnlyMatchingGeometryAndKeepIndependentPace()
    {
        var previous = TrackSplineRef.Instance;
        var go = new GameObject("capture cache test"); go.SetActive(false);
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            var container = go.AddComponent<UnityEngine.Splines.SplineContainer>();
            var spline = new UnityEngine.Splines.Spline();
            foreach(var point in new[]{Vector3.zero,Vector3.right*50,new Vector3(50,0,50),Vector3.forward*50})
                spline.Add(new UnityEngine.Splines.BezierKnot((Unity.Mathematics.float3)point),UnityEngine.Splines.TangentMode.Linear);
            spline.Closed = true; container.Spline = spline;
            var track = go.AddComponent<TrackSplineRef>(); track.container = container;
            typeof(TrackSplineRef).GetMethod("Awake",flags).Invoke(track,null);
            var a = SplineRacingLine.Capture(track); var b = SplineRacingLine.Capture(track);
            var geometry = typeof(SplineRacingLine).GetField("_geometry",flags);
            Assert.That(geometry.GetValue(a),Is.SameAs(geometry.GetValue(b)));
            a.PreparePace(25,80,.8f);b.PreparePace(10,20,.2f);
            Assert.That(a.Project(Vector3.zero,-1).Pace,Is.Not.EqualTo(b.Project(Vector3.zero,-1).Pace));
            go.transform.position = Vector3.up*10;
            var moved = SplineRacingLine.Capture(track);
            Assert.That(geometry.GetValue(moved),Is.Not.SameAs(geometry.GetValue(a)));
            Assert.That(moved.Points[0].y,Is.EqualTo(a.Points[0].y+10));
            var resampled = SplineRacingLine.Capture(track,4);
            Assert.That(geometry.GetValue(resampled),Is.Not.SameAs(geometry.GetValue(moved)));
        }
        finally {UnityEngine.Object.DestroyImmediate(go);typeof(TrackSplineRef).GetProperty("Instance").SetValue(null,previous);}
    }
    [Test] public void ModelMatchesLegacyAcrossParameterChanges()
    {
        for(int scenario=0;scenario<16;scenario++)
        {
            var p=Params(scenario%2==0?1:-1);p.Stats.IsRooted=scenario==1;p.Stats.IsSteeringSuppressed=scenario==2;p.Stats.IsPushGraceActive=scenario==3;p.PushExtraSpeed=13;p.Drag=scenario*.02f;p.Mass=scenario==4?0:2;p.GroundDeceleration=scenario*.3f;
            var a=new MotionState {Position=new Vector3(2,1,6),Velocity=new Vector3(17,0,86),Yaw=.4f,YawRate=.3f};var b=a;
            for(int t=0;t<180;t++){int key=(t/15)%3-1;LegacyBuddahMotionModel.Step(ref a,p,key,1f/60);BuddahMotionModel.Step(ref b,p,key,1f/60);Assert.That(b.Position,Is.EqualTo(a.Position));Assert.That(b.Velocity,Is.EqualTo(a.Velocity));Assert.That(b.Yaw,Is.EqualTo(a.Yaw));Assert.That(b.YawRate,Is.EqualTo(a.YawRate));}
        }
    }
}
