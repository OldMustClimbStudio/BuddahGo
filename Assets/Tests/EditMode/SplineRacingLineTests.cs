using System;
using System.IO;
using BuddahGo.AI;
using NUnit.Framework;
using UnityEngine;
public class SplineRacingLineTests
{
    [Serializable] class Fixture {public float length;public Vector3[] points;}
    static Fixture Read()=>JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tools/ai/fixtures/spin-entry.json")));
    static void Equal(LineProjection a,LineProjection b)
    {Assert.That(b.Segment,Is.EqualTo(a.Segment));Assert.That(b.Point,Is.EqualTo(a.Point));Assert.That(b.Tangent,Is.EqualTo(a.Tangent));Assert.That(b.Distance,Is.EqualTo(a.Distance));Assert.That(b.Lateral,Is.EqualTo(a.Lateral));Assert.That(b.Pace,Is.EqualTo(a.Pace));}
    [Test] public void GeometrySnapshotCannotBeMutatedAndPaceIsPerLine()
    {
        var points = new[]{Vector3.zero, Vector3.right*10, Vector3.forward*10};
        var a = new SplineRacingLine(points, 30); var b = new SplineRacingLine(points, 30);
        var before = a.Project(Vector3.right*4, -1);
        points[0] = Vector3.one*999; a.Points[1] = Vector3.one*999;
        Equal(before, a.Project(Vector3.right*4,-1));
        a.PreparePace(25,80,.8f,10f); b.PreparePace(10,20,.2f,10f);
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
            a.PreparePace(25,80,.8f,10f);b.PreparePace(10,20,.2f,10f);
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
    [Test] public void PaceWindowIncludesInteriorSamplesAndWraps()
    {
        var f=Read();var line=new SplineRacingLine(f.points,f.length);line.PreparePace(25,80,.8f,10f);
        float step=f.length/f.points.Length;bool missedByEndpoints=false;
        for(int i=0;i<f.points.Length;i+=7) {
            float start=(i+.25f)*step,length=60.5f*step;
            float expected=Mathf.Min(line.SampleDistance(start).Pace,line.SampleDistance(start+length).Pace);
            float endpoints=expected;
            for(int k=1;k<=60;k++)expected=Mathf.Min(expected,line.SampleDistance((i+k+.01f)*step).Pace);
            Assert.That(line.MinimumPace(start,length),Is.EqualTo(expected).Within(.0001f));
            Assert.That(line.MinimumPace(start+f.length,length),Is.EqualTo(expected).Within(.0001f));
            missedByEndpoints|=expected<endpoints-.01f;
        }
        Assert.That(missedByEndpoints,Is.True,"Fixture must exercise a slow interior hidden by both endpoints");
        float global=float.PositiveInfinity;
        for(int i=0;i<f.points.Length;i++)global=Mathf.Min(global,line.SampleDistance((i+.25f)*step).Pace);
        Assert.That(line.MinimumPace(f.length-1,f.length*2),Is.EqualTo(global));
        Assert.That(line.MinimumPace(123,0),Is.EqualTo(line.SampleDistance(123).Pace));
    }
    [Test]
    public void RacingLineProjectionWrapsAcrossLastSegment()
    {
        var line = new SplineRacingLine(new[] { Vector3.zero, Vector3.right * 10, new Vector3(10, 0, 10), Vector3.forward * 10 }, 40);
        var projected = line.Project(new Vector3(-2, 0, 4), 0, 2);
        Assert.That(projected.Segment, Is.EqualTo(3)); Assert.That(projected.Distance, Is.EqualTo(36).Within(0.001));
        Assert.That(projected.Lateral, Is.EqualTo(2).Within(0.001));
    }
    [Test]
    public void CurvaturePaceUsesAvailableForceAndTighterCorners()
    {
        var points = new Vector3[120];
        for (int i = 0; i < points.Length; i++)
        { float angle = i * 2 * Mathf.PI / points.Length; points[i] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 40; }
        var line = new SplineRacingLine(points, 80 * Mathf.PI);
        line.PreparePace(25, 80, .8f, 10f);
        float pace = line.Project(points[0], 0).Pace;
        Assert.That(pace, Is.InRange(27f, 30f));
        line.PreparePace(12.5f, 80, .8f, 10f);
        Assert.That(line.Project(points[0], 0).Pace, Is.LessThan(pace));
    }
}
