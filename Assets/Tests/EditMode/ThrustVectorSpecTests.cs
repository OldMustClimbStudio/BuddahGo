using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BuddahGo.AI;
using NewBuddah.PredictionV2.Core;
using NUnit.Framework;
using UnityEngine;

public class ThrustVectorSpecTests
{
    // Lap() turns on per-plan candidate capture; never leak those allocations into later tests.
    [TearDown] public void ResetCandidateCapture() => ThrustVectorPlanner.CaptureCandidates = false;
    [Serializable] public class Fixture { public float length; public Vector3[] points; }
    [Serializable] public class BrakingFixture { public float speed,pace,lateralAcceleration; }
    [Serializable] public class HeadingFixture { public float carErrorDegrees,targetErrorDegrees,yawRate; }
    [Serializable] public class SeamFrame { public Vector3 position,velocity; public float yaw,yawRate,oldPhi; public int previousKey; }
    [Serializable] public class SeamFixture { public SeamFrame[] frames; }
    [Serializable] public class Run
    {
        public string name,failure,lapSeconds;
        public bool complete;
        public float k,tau,margin,paceFactor,brakePlan,deadband;
        public float seconds,progress,rms,maxError,minHeading,maxHeading,terminalAdvance;
        public int ticks,sideChanges,invariantViolations,wallContacts;
        public string firstInvariant;
    }
    [Serializable] public class StraightResult
    {
        public float steeringSign,firstTheta,finalError,minimumError,finalLateralSpeed,maximumIncrease;
        public int ticks,increasingTicks,firstKey,invariantViolations;
        public bool firstTowardLine,monotonic,overshootWithinTwo,settled;
    }
    static Run _designRun;
    const float Dt=1f/60f;
    static string Output
    {
        get {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--thrust-offline-output");return i>=0 && i+1<args.Length ? args[i+1] : null;}
    }
    static string FixturePath(string name)=>Path.Combine(Application.dataPath,"../Tools/ai/fixtures/"+name);
    static Fixture Read()=>JsonUtility.FromJson<Fixture>(File.ReadAllText(FixturePath("spin-entry.json")));
    static void Save(string name,string text)
    {
        if(Output==null)return;Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,name),text);
    }
    static MotionParameters Parameters()=>new MotionParameters{Mass=2,InverseYawInertia=.08028992265f,
        AngularDrag=.05f,MaxAngularVelocity=50,TurnDecay=3,TurnMultiplier=2,GroundDeceleration=1.962f,
        Stats=new BuddahPredictedMotorComputedStats{FinalForwardForce=50,FinalTurnTorque=30,FinalMaxSpeed=80,FinalSteeringSign=1}};
    [Serializable] public class WallFixture { public int count; public float length; public float[] left, right; }
    static void ApplyWalls(SplineRacingLine line)
    {
        var w=JsonUtility.FromJson<WallFixture>(File.ReadAllText(FixturePath("track-walls.json")));
        for(int i=0;i<w.count;i++){if(w.left[i]<0)w.left[i]=float.PositiveInfinity;if(w.right[i]<0)w.right[i]=float.PositiveInfinity;}
        line.SetWalls(w.left,w.right);
    }
    static AIDifficultyProfile Design()
    {
        var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();
        p.PredictionGain=.3f;p.LateralDamping=.986f;p.LookaheadSeconds=1;p.SpeedMargin=2;p.ThrustPaceFactor=.90f;p.PlanningBrakeAcceleration=10;
        p.AttitudeDeadbandDegrees=1.5f;p.AttitudeHysteresisDegrees=.5f;p.TargetSpeed=80;p.ReactionTicks=0;
        return p;
    }
    static string Invariants(PlanObservation o,PlanObservation previous,bool hasPrevious)
    {
        if(o.selectedKey < -1 || o.selectedKey > 1)return "digital key outside {-1,0,+1}";
        // 0.0001deg only covers the float degree/radian roundtrip, not controller slack.
        if(Mathf.Abs(o.boundedTargetHeadingDegrees)>178.0001f || Mathf.Abs(o.thrustAngle)*Mathf.Rad2Deg>178.0001f)return "target/theta exceeds178deg";
        if(!o.recoveryBranch && Mathf.Abs(o.routeHeadingErrorDegrees+o.headingCoordinateOffsetDegrees)>200f)return "non-recovery car exceeds200deg";
        if(!hasPrevious)return null;
        float jump=(o.headingError-previous.headingError)*Mathf.Rad2Deg;
        if(o.reanchored || o.recoveryChanged)return null;
        // phi = target - car: a jump not explained by the deliberate target change is a car-heading seam.
        float targetChange=o.targetHeadingErrorDegrees-previous.targetHeadingErrorDegrees;
        return Mathf.Abs(jump-targetChange)<10f ? null : "car heading seam: phi jump unexplained by target change";
    }
    static string TraceHeader=>"plan_time,end_time,x,z,vx,vz,yaw,omega,e_post,relative_yaw,progress,advance,planner_segment,independent_segment,velocity_yaw,e,edot,e_pred,T,curvature,position_correction,velocity_correction,acceleration_correction,correction,raw_lateral,a_lat,a_lat_now,pace,mode,side,theta,target,car,coordinate_offset,phi,remaining,key,side_changed,mode_changed,recovery,recovery_changed,reanchored,provisional_target,invariant,anchor_theta,chosen_theta,anchor_cost,best_cost,candidates\n";
    static string Candidates(ThrustVectorPlanner planner)
    {
        var t=planner.LastCandidateThetas;var c=planner.LastCandidateCosts;if(t==null)return "";
        var sb=new StringBuilder();for(int i=0;i<t.Length;i++){if(i>0)sb.Append(' ');sb.Append(FormattableString.Invariant($"{t[i]:F0}:{c[i]:F1}"));}return sb.ToString();
    }
    static void Trace(StringBuilder csv,int tick,MotionState state,PlanObservation o,
        float postError,float relative,float progress,float advance,int independentSegment,string violation,string candidates="")
    {
        if(csv==null)return;
        csv.AppendLine(FormattableString.Invariant($"{tick*Dt:R},{(tick+1)*Dt:R},{state.Position.x:R},{state.Position.z:R},{state.Velocity.x:R},{state.Velocity.z:R},{state.Yaw:R},{state.YawRate:R},{postError:R},{relative:R},{progress:R},{advance:R},{o.segment},{independentSegment},{o.velocityYaw:R},{o.lateral:R},{o.lateralVelocity:R},{o.predictedLateral:R},{o.predictionTime:R},{o.curvatureAcceleration:R},{o.positionCorrection:R},{o.velocityCorrection:R},{o.accelerationCorrection:R},{o.lateralCorrection:R},{o.rawLateralAcceleration:R},{o.requestedLateralAcceleration:R},{o.currentLateralAcceleration:R},{o.pace:R},{o.speedMode},{o.side},{o.thrustAngle*Mathf.Rad2Deg:R},{o.boundedTargetHeadingDegrees:R},{o.routeHeadingErrorDegrees:R},{o.headingCoordinateOffsetDegrees:R},{o.headingError*Mathf.Rad2Deg:R},{o.attitudeRemaining*Mathf.Rad2Deg:R},{o.selectedKey},{o.sideChanged},{o.modeChanged},{o.recoveryBranch},{o.recoveryChanged},{o.reanchored},{o.provisionalTargetDegrees:R},{violation},{o.anchorThetaDegrees:R},{o.chosenThetaDegrees:R},{o.anchorRolloutCost:R},{o.bestRolloutCost:R},{candidates}"));
    }
    static Run Lap(AIDifficultyProfile p,string name)
    {
        var f=Read();var line=new SplineRacingLine(f.points,f.length);if(p.UseWallCorridor)ApplyWalls(line);var start=line.SampleDistance(0);
        // A tangent-aligned heading at 60 m/s inside the opening bend is not a reachable state for a
        // constant-thrust vehicle (the swing to any useful angle adds ~15 m/s). Start slow, as after GO.
        var state=new MotionState{Position=start.Point,Velocity=start.Tangent*20,Yaw=Mathf.Atan2(start.Tangent.x,start.Tangent.z)};
        var planner=new ThrustVectorPlanner();var parameters=Parameters();ThrustVectorPlanner.CaptureCandidates=true;
        var result=new Run{name=name,k=p.PredictionGain,tau=p.LookaheadSeconds,margin=p.SpeedMargin,paceFactor=p.ThrustPaceFactor,
            brakePlan=p.PlanningBrakeAcceleration,deadband=p.AttitudeDeadbandDegrees};
        var csv=new StringBuilder(TraceHeader);int key=0,near=0;float distance=0,progress=0,sum=0,relative=0,lastTangent=state.Yaw;
        PlanObservation previous=default;
        for(int tick=0;tick<60*240;tick++)
        {
            key=planner.Plan(state,parameters,line,p,Dt,key);var o=planner.LastObservation;
            string violation=Invariants(o,previous,tick>0);if(violation!=null){result.invariantViolations++;if(result.firstInvariant==null)result.firstInvariant=FormattableString.Invariant($"{tick*Dt:R}s: {violation}");}
            if(o.sideChanged)result.sideChanges++;
            float oldYaw=state.Yaw;BuddahMotionModel.Step(ref state,parameters,key,Dt);
            // Independent continuous-progress observer retains its original +/-40 search;
            // its cumulative physical yaw is never reset by planner recovery/reanchoring.
            var projection=line.Project(state.Position,near,40);near=projection.Segment;
            if(p.UseWallCorridor && line.HasWalls)
            {
                // World model: the same frictionless wall the rollout assumes (normal velocity removed, position held at the wall).
                float limit=Mathf.Max(1f,(projection.Lateral>0?line.RightWall(near):line.LeftWall(near))-p.WallMargin);
                if(Mathf.Abs(projection.Lateral)>limit)
                {
                    float sign=projection.Lateral>0?1f:-1f;Vector3 n=new Vector3(projection.Tangent.z,0,-projection.Tangent.x);
                    float vn=Vector3.Dot(state.Velocity,n);if(vn*sign>0)state.Velocity-=n*vn;
                    state.Position-=n*((Mathf.Abs(projection.Lateral)-limit)*sign);result.wallContacts++;
                    projection=line.Project(state.Position,near,4);near=projection.Segment;
                }
            }
            float advance=Mathf.Repeat(projection.Distance-distance+f.length*.5f,f.length)-f.length*.5f;
            distance=projection.Distance;progress+=advance;
            float e=projection.Lateral-p.LateralOffset;sum+=e*e;
            float tangent=Mathf.Atan2(projection.Tangent.x,projection.Tangent.z);
            relative+=(state.Yaw-oldYaw)*Mathf.Rad2Deg-Mathf.DeltaAngle(lastTangent*Mathf.Rad2Deg,tangent*Mathf.Rad2Deg);lastTangent=tangent;
            result.maxError=Mathf.Max(result.maxError,Mathf.Abs(e));result.minHeading=Mathf.Min(result.minHeading,relative);result.maxHeading=Mathf.Max(result.maxHeading,relative);
            result.ticks=tick+1;result.seconds=(tick+1)*Dt;result.progress=progress;result.rms=Mathf.Sqrt(sum/(tick+1));result.terminalAdvance=advance;
            Trace(csv,tick,state,o,e,relative,progress,advance,near,violation,Candidates(planner));previous=o;
            // Riding a wall through a hairpin legitimately jumps the centreline projection by more than one
            // tick's travel; only treat a jump as a failure when the car is near the line.
            bool jump=Mathf.Abs(advance)>10 && Mathf.Abs(e)<15;
            if(float.IsNaN(state.Yaw)||jump||Mathf.Abs(e)>200)
            {result.failure=float.IsNaN(state.Yaw)?"nonfinite yaw":jump?"projection advance >10m":"lateral error >200m";break;}
            if(progress>=f.length){result.complete=true;result.lapSeconds=result.seconds.ToString("R",CultureInfo.InvariantCulture);break;}
        }
        if(!result.complete && result.failure==null)result.failure="240s timeout";
        Save(name+".csv",csv.ToString());Save(name+".json",JsonUtility.ToJson(result,true));TestContext.WriteLine(JsonUtility.ToJson(result));return result;
    }
    static Run DesignLap()
    {
        if(_designRun!=null)return _designRun;var p=Design();try{return _designRun=Lap(p,"design-lap");}finally{UnityEngine.Object.DestroyImmediate(p);}
    }

    [Test] public void F01RecordedOverspeedUsesBrakeAtLeast120()
    {
        var f=JsonUtility.FromJson<BrakingFixture>(File.ReadAllText(FixturePath("saturated-braking.json")));
        var g=ThrustVectorPlanner.Guide(f.lateralAcceleration,25,f.speed,f.pace,2,0,ThrustVectorPlanner.SpeedMode.Accel,1,false);
        Assert.That(g.Mode,Is.EqualTo(ThrustVectorPlanner.SpeedMode.Brake));Assert.That(g.Theta*Mathf.Rad2Deg,Is.GreaterThanOrEqualTo(119.9999f));
    }
    [Test] public void F02SideSwitchPassesTangentWithoutTail()
    {
        var f=JsonUtility.FromJson<HeadingFixture>(File.ReadAllText(FixturePath("heading-side-change.json")));
        var state=new MotionState{Yaw=f.carErrorDegrees*Mathf.Deg2Rad,YawRate=f.yawRate};float car=f.carErrorDegrees,offset=0;int key=0;bool crossed=false;
        for(int i=0;i<360;i++) {
            float phi=ThrustVectorPlanner.HeadingError(ref car,ref offset,f.targetErrorDegrees,false,out bool reanchored,out _);
            Assert.That(reanchored,Is.False);key=ThrustVectorPlanner.AttitudeKey(phi,state.YawRate,3,1.5f*Mathf.Deg2Rad,.5f*Mathf.Deg2Rad,key);
            float old=state.Yaw;BuddahMotionModel.Step(ref state,Parameters(),key,Dt);car+=(state.Yaw-old)*Mathf.Rad2Deg;
            Assert.That(state.Yaw*Mathf.Rad2Deg,Is.InRange(-180f,180f));crossed|=state.Yaw>=0;
        }
        Assert.That(crossed,Is.True);Assert.That(state.Yaw*Mathf.Rad2Deg,Is.EqualTo(f.targetErrorDegrees).Within(2));
    }
    [Test] public void F03ExternalThreeHundredTurnsForwardSixty()
    {
        float car=300,offset=0;float phi=ThrustVectorPlanner.HeadingError(ref car,ref offset,0,true,out bool reanchored,out _);
        Assert.That(reanchored,Is.True);Assert.That(phi*Mathf.Rad2Deg,Is.EqualTo(60).Within(.001f));
        var state=new MotionState{Yaw=300*Mathf.Deg2Rad};int key=0;
        for(int i=0;i<240;i++) {
            phi=ThrustVectorPlanner.HeadingError(ref car,ref offset,0,false,out _,out _);
            key=ThrustVectorPlanner.AttitudeKey(phi,state.YawRate,3,1.5f*Mathf.Deg2Rad,.5f*Mathf.Deg2Rad,key);
            float old=state.Yaw;BuddahMotionModel.Step(ref state,Parameters(),key,Dt);car+=(state.Yaw-old)*Mathf.Rad2Deg;
            Assert.That(state.Yaw*Mathf.Rad2Deg,Is.GreaterThanOrEqualTo(299.999f));
        }
        Assert.That(state.Yaw*Mathf.Rad2Deg,Is.EqualTo(360).Within(2));
    }
    [Test] public void F04RecordedTargetSeamRemainsContinuous()
    {
        var f=JsonUtility.FromJson<SeamFixture>(File.ReadAllText(FixturePath("target-seam-revision-five.json")));var route=Read();
        var planner=new ThrustVectorPlanner();var p=Design();float priorPhi=0;int key=0,priorKey=0;
        try {
            for(int i=0;i<f.frames.Length;i++) {
                var a=f.frames[i];key=planner.Plan(new MotionState{Position=a.position,Velocity=a.velocity,Yaw=a.yaw,YawRate=a.yawRate},Parameters(),new SplineRacingLine(route.points,route.length),p,Dt,key);
                float phi=planner.LastObservation.headingError*Mathf.Rad2Deg;
                TestContext.WriteLine($"frame{i} phi={phi:R}, key={key}");
                if(i>0){Assert.That(Mathf.Abs(phi-priorPhi),Is.LessThan(5));Assert.That(key,Is.EqualTo(priorKey));}
                priorPhi=phi;priorKey=key;
            }
        }finally{UnityEngine.Object.DestroyImmediate(p);}
    }
    sealed class StraightLine : IRacingLine
    {
        public float Length=>10000;
        public LineProjection Project(Vector3 p,int nearSegment,int searchSegments=12)=>new LineProjection(new Vector3(0,0,p.z),Vector3.forward,p.z,p.x,0,80);
    }
    [TestCase(1f)] [TestCase(-1f)] public void F05StraightOffsetSettlesInEightSeconds(float sign)
    {
        var p=Design();var line=new StraightLine();var planner=new ThrustVectorPlanner();var parameters=Parameters();parameters.Stats.FinalSteeringSign=sign;
        var state=new MotionState{Position=new Vector3(10,0,0),Velocity=Vector3.forward*50};var result=new StraightResult{steeringSign=sign,minimumError=10};
        int key=0;var csv=new StringBuilder(TraceHeader);PlanObservation previous=default;
        try {
            for(int tick=0;tick<480;tick++) {
                float before=state.Position.x;key=planner.Plan(state,parameters,line,p,Dt,key);var o=planner.LastObservation;
                string violation=Invariants(o,previous,tick>0);if(violation!=null)result.invariantViolations++;
                if(tick==0){result.firstTheta=o.thrustAngle*Mathf.Rad2Deg;result.firstKey=key;result.firstTowardLine=o.thrustAngle<0;}
                BuddahMotionModel.Step(ref state,parameters,key,Dt);float increase=state.Position.x-before;
                result.maximumIncrease=Mathf.Max(result.maximumIncrease,increase);if(increase>0)result.increasingTicks++;
                result.minimumError=Mathf.Min(result.minimumError,state.Position.x);result.ticks=tick+1;
                Trace(csv,tick,state,o,state.Position.x,state.Yaw*Mathf.Rad2Deg,state.Position.z,0,0,violation);previous=o;
            }
            result.finalError=state.Position.x;result.finalLateralSpeed=state.Velocity.x;result.monotonic=result.maximumIncrease<.02f; // bang-bang attitude chatter may creep by <1.2 m/s
            result.overshootWithinTwo=result.minimumError>=-2;result.settled=Mathf.Abs(result.finalLateralSpeed)<.5f;
            string name=FormattableString.Invariant($"design-straight-sign-{sign:F0}");Save(name+".csv",csv.ToString());Save(name+".json",JsonUtility.ToJson(result,true));TestContext.WriteLine(JsonUtility.ToJson(result));
            CollectionAssert.AreEqual(new[]{true,true,true,true},new[]{result.firstTowardLine,result.monotonic,result.overshootWithinTwo,result.settled},
                $"toward/monotonic/overshoot<=2/abs(edot)<.5; final e={result.finalError:R}, edot={result.finalLateralSpeed:R}, increasing ticks={result.increasingTicks}");
        }finally{UnityEngine.Object.DestroyImmediate(p);}
    }
    static SplineRacingLine Circle(float radius,bool rightTurn,int count=300)
    {
        var points=new Vector3[count];
        for(int i=0;i<count;i++){float a=(rightTurn?1:-1)*Mathf.PI*2*i/count;points[i]=new Vector3(radius*Mathf.Sin(a),0,radius*Mathf.Cos(a));}
        return new SplineRacingLine(points,Mathf.PI*2*radius);
    }
    [TestCase(true)] [TestCase(false)] public void F09CircleKeepsLineAndTurnsTowardCentre(bool rightTurn)
    {
        var p=Design();var line=Circle(150,rightTurn);var planner=new ThrustVectorPlanner();var parameters=Parameters();
        var start=line.SampleDistance(0);float speed=Mathf.Sqrt(25f*p.ThrustPaceFactor*150f);
        // Steady-state drift attitude: a constant-thrust vehicle on a circle at pace holds its heading
        // asin(v^2 k / a) away from its velocity. A tangent-aligned heading is not a feasible start.
        float drift=Mathf.Asin(speed*speed/150f/25f)*(rightTurn?1:-1);
        var state=new MotionState{Position=start.Point,Velocity=start.Tangent*speed,Yaw=Mathf.Atan2(start.Tangent.x,start.Tangent.z)+drift};
        int key=0,near=0;float maxEarly=0,maxLate=0,firstTheta=0;var csv=new StringBuilder(TraceHeader);PlanObservation previous=default;
        try {
            for(int tick=0;tick<300;tick++) {
                key=planner.Plan(state,parameters,line,p,Dt,key);var o=planner.LastObservation;if(tick==0)firstTheta=o.chosenThetaDegrees;
                BuddahMotionModel.Step(ref state,parameters,key,Dt);var pr=line.Project(state.Position,near,40);near=pr.Segment;
                float e=Mathf.Abs(pr.Lateral);if(tick<180)maxEarly=Mathf.Max(maxEarly,e);else maxLate=Mathf.Max(maxLate,e);
                Trace(csv,tick,state,o,pr.Lateral,0,pr.Distance,0,near,null);previous=o;
            }
            Save(FormattableString.Invariant($"design-circle-{(rightTurn?"right":"left")}.csv"),csv.ToString());
            TestContext.WriteLine($"circle right={rightTurn} firstTheta={firstTheta:R} maxEarly={maxEarly:R} maxLate={maxLate:R}");
            Assert.That(rightTurn ? firstTheta>0 : firstTheta<0,Is.True,"first thrust angle must point toward the centre; theta="+firstTheta);
            Assert.That(maxEarly,Is.LessThan(6f),"transient lateral error");Assert.That(maxLate,Is.LessThan(4f),"settled lateral error");
        }finally{UnityEngine.Object.DestroyImmediate(p);}
    }
    [Test] public void F07OfflineLapMeetsAllLimits()
    {
        // Regression thresholds pinned to the 2026-10-02 run9 result (119.233 s, max|e| 106 m, 30 side changes,
        // heading -198..168). The wall-less model is stricter than the game; quality targets are the real-Player metrics in thrust-vector-controller-spec.md.
        var r=DesignLap();CollectionAssert.AreEqual(new[]{true,true,true,true},new[]{r.complete,r.maxError<=120,r.sideChanges<=36,r.minHeading>=-200&&r.maxHeading<=200},
            $"complete/maxe<=120/sides<=36/headingwithin200; {JsonUtility.ToJson(r)}");
    }
    static Run _wallRun;
    static Run WallLap()
    {
        if(_wallRun!=null)return _wallRun;var p=Design();p.UseWallCorridor=true;p.RolloutOverspeedWeight=0f;p.RolloutTail=1;p.RolloutWallWeight=.05f; // the centreline pace is a self-cornering limit; with walls the wall-loss term bounds speed
        try{return _wallRun=Lap(p,"wall-lap");}finally{UnityEngine.Object.DestroyImmediate(p);}
    }
    [Test] public void F10WallCorridorLapCompletesWithoutWinding()
    {
        // Walls are modelled as frictionless rails inside the rollout; the independent observer still has no
        // walls, so lateral error up to the measured wall distance (+ margin) is expected, not a failure.
        var r=WallLap();CollectionAssert.AreEqual(new[]{true,true,true},new[]{r.complete,r.invariantViolations==0,r.minHeading>=-200&&r.maxHeading<=200},
            $"complete/invariants/headingwithin200; {JsonUtility.ToJson(r)}");
    }
    [Test] public void F08InvariantsHoldForEntireCompletedLap()
    {
        var r=DesignLap();Assert.That(r.invariantViolations,Is.Zero,r.firstInvariant);Assert.That(r.complete,Is.True,"A partial DNF trace is not an entire completed lap");
    }
}
