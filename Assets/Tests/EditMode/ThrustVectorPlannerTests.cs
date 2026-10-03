using System;
using System.IO;
using System.Collections.Generic;
using BuddahGo.AI;
using NewBuddah.PredictionV2.Core;
using NUnit.Framework;
using UnityEngine;

[Explicit("Historical revision-six experiments, retained with their original known failures; current acceptance is ThrustVectorSpecTests") ]
public class ThrustVectorPlannerTests
{
    [Serializable] public class Fixture { public float length; public Vector3[] points; public Sample[] states; }
    [Serializable] public class Sample { public Vector3 position, velocity; public float yaw, yawRate; }
    [Serializable] public class Result
    {
        public string controller; public bool complete; public float seconds, rms, maxError, minHeading, maxHeading;
        public float tau, kp, kd, deadband, factor, targetSpeed; public int plans, sideChanges, brakingTicks, holdingTicks, recoveryTicks; public string failure;
        public float terminalAdvance, terminalLateral, terminalProgress;
    }
    [Serializable] class Results { public Result[] runs; }
    static Fixture Read() => JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tools/ai/fixtures/spin-entry.json")));
    static MotionParameters Parameters() => new MotionParameters { Mass=2, InverseYawInertia=.08028992265f,
        AngularDrag=.05f, MaxAngularVelocity=50, TurnDecay=3, TurnMultiplier=2, GroundDeceleration=1.962f,
        Stats=new BuddahPredictedMotorComputedStats { FinalForwardForce=50, FinalTurnTorque=30, FinalMaxSpeed=80, FinalSteeringSign=1 } };
    static void Save(string name, List<Result> rows)
    {
        string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--thrust-offline-output");
        if(at>=0 && at+1<args.Length)
        {Directory.CreateDirectory(args[at+1]);File.WriteAllText(Path.Combine(args[at+1],name),JsonUtility.ToJson(new Results {runs=rows.ToArray()},true));}
        foreach(var row in rows)TestContext.WriteLine(JsonUtility.ToJson(row));
    }
    public static Result Lap(Fixture fixture, AIDifficultyProfile profile, bool thrust, bool trace = false, string traceName = null)
    {
        var line=new SplineRacingLine(fixture.points,fixture.length);var parameters=Parameters();
        ISteeringPlanner planner=thrust ? (ISteeringPlanner)new ThrustVectorRevisionSixFixture() : new ForwardSimPlanner();
        var start=line.SampleDistance(0);
        var state=new MotionState {Position=start.Point,Velocity=start.Tangent*60,Yaw=Mathf.Atan2(start.Tangent.x,start.Tangent.z)};
        var result=new Result {controller=thrust?"thrust":"beam",tau=profile.LookaheadSeconds,kp=profile.LateralGain,kd=profile.LateralDamping,deadband=profile.AttitudeDeadbandDegrees,factor=profile.CorneringFactor,targetSpeed=profile.TargetSpeed};
        var csv=trace ? new System.Text.StringBuilder("time,x,z,speed,yaw,relative_yaw,progress,advance,lateral,segment,planner_segment,pace,theta,braking,holding,key,plan_time,body_yaw_before,requested_a_lat,internal_route_error,phi,desired_yaw,plan_tangent_yaw,omega,remaining,velocity_error,target_error,target_bounded,heading_offset,recovery,recent_collision,backwards,dt,curvature_term,position_term,damping_term,raw_lateral,planner_lateral,cross_speed,pre_speed,pre_vx,pre_vz,planner_progress\n") : null;
        int side=0;
        const float dt=1f/60;int key=0,near=0;float previousDistance=0,progress=0,sum=0,lastTangent=state.Yaw,relative=0;
        for(int tick=0;tick<60*240;tick++)
        {
            if(thrust || tick%3==0){key=planner.Plan(state,parameters,line,profile,dt,key);result.plans++;}
            var observation=thrust ? ((ThrustVectorRevisionSixFixture)planner).LastObservation : ((ForwardSimPlanner)planner).LastObservation;
            if(thrust) { if(observation.recoveryBranch)result.recoveryTicks++;else if(observation.holdingBranch)result.holdingTicks++;else if(observation.brakingBranch)result.brakingTicks++;
                int nextSide=observation.thrustAngle<0 ? -1 : observation.thrustAngle>0 ? 1 : side;
                if(side!=0 && nextSide!=side)result.sideChanges++;side=nextSide; }
            // Diagnostic only: record the exact signed terms before the production clamp.
            float preSpeed=observation.start.Velocity.magnitude;
            float curvatureTerm=preSpeed*preSpeed*line.CurvatureAtDistance(observation.progress);
            float positionTerm=profile.LateralGain*(observation.lateral-profile.LateralOffset);
            float crossSpeed=Vector3.Dot(observation.start.Velocity,Vector3.Cross(Vector3.up,observation.tangent));
            float dampingTerm=profile.LateralDamping*crossSpeed;
            float rawLateral=curvatureTerm-positionTerm-dampingTerm;
            float oldYaw=state.Yaw;BuddahMotionModel.Step(ref state,parameters,key,dt);
            var projection=line.Project(state.Position,near,40);near=projection.Segment;
            float advance=Mathf.Repeat(projection.Distance-previousDistance+fixture.length*.5f,fixture.length)-fixture.length*.5f;
            previousDistance=projection.Distance;progress+=advance;
            float e=projection.Lateral-profile.LateralOffset;sum+=e*e;result.maxError=Mathf.Max(result.maxError,Mathf.Abs(e));
            float yaw=Mathf.Atan2(projection.Tangent.x,projection.Tangent.z);
            relative+=(state.Yaw-oldYaw)*Mathf.Rad2Deg-Mathf.DeltaAngle(lastTangent*Mathf.Rad2Deg,yaw*Mathf.Rad2Deg);lastTangent=yaw;
            result.minHeading=Mathf.Min(result.minHeading,relative);result.maxHeading=Mathf.Max(result.maxHeading,relative);
            result.seconds=(tick+1)*dt;result.rms=Mathf.Sqrt(sum/(tick+1));
            result.terminalAdvance=advance;result.terminalLateral=e;result.terminalProgress=progress;
            if(csv!=null)csv.AppendLine(FormattableString.Invariant($"{result.seconds},{state.Position.x},{state.Position.z},{state.Velocity.magnitude},{state.Yaw*Mathf.Rad2Deg},{relative},{progress},{advance},{e},{projection.Segment},{observation.segment},{observation.pace},{observation.thrustAngle*Mathf.Rad2Deg},{observation.brakingBranch},{observation.holdingBranch},{key},{tick*dt},{oldYaw*Mathf.Rad2Deg},{observation.requestedLateralAcceleration},{observation.routeHeadingErrorDegrees},{observation.headingError*Mathf.Rad2Deg},{observation.desiredYaw*Mathf.Rad2Deg},{Mathf.Atan2(observation.tangent.x,observation.tangent.z)*Mathf.Rad2Deg},{observation.observedYawRate},{observation.attitudeRemaining*Mathf.Rad2Deg},{observation.velocityHeadingErrorDegrees},{observation.targetHeadingErrorDegrees},{observation.boundedTargetHeadingDegrees},{observation.headingCoordinateOffsetDegrees},{observation.recoveryBranch},{observation.recentCollision},{observation.backwardsRecovery},{dt},{curvatureTerm:R},{positionTerm:R},{dampingTerm:R},{rawLateral:R},{observation.lateral:R},{crossSpeed:R},{preSpeed:R},{observation.start.Velocity.x:R},{observation.start.Velocity.z:R},{observation.progress:R}"));
            if(float.IsNaN(state.Yaw)||Mathf.Abs(advance)>10||Mathf.Abs(e)>200)
            {result.failure=(float.IsNaN(state.Yaw)?"nonfinite yaw; ":"")+(Mathf.Abs(advance)>10?"projection advance >10m; ":"")+(Mathf.Abs(e)>200?"lateral error >200m":"");break;}
            if(progress>=fixture.length){result.complete=true;break;}
        }
        if(!result.complete && result.failure==null)result.failure="240s timeout";
        if(csv!=null) { string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--thrust-offline-output");
            if(at>=0 && at+1<args.Length){Directory.CreateDirectory(args[at+1]);File.WriteAllText(Path.Combine(args[at+1],traceName ?? result.controller+"-lap.csv"),csv.ToString());} }
        return result;
    }
    [Test,Explicit("Record all unclamped lateral terms on the two unchanged revision-six profiles")]
    public void CaptureLateralTermDecomposition()
    {
        var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();p.AttitudeDeadbandDegrees=2;var runs=new List<Result>();
        try {
            p.LookaheadSeconds=3;p.LateralGain=.3f;p.LateralDamping=1;p.AttitudeDeadbandDegrees=3;p.CorneringFactor=.9f;
            runs.Add(Lap(Read(),p,true,true,"formal-terms.csv"));
            p.LateralDamping=.5f;p.AttitudeDeadbandDegrees=1;
            runs.Add(Lap(Read(),p,true,true,"diagnostic-terms.csv"));
            Save("lateral-term-replay.json",runs);
        } finally{UnityEngine.Object.DestroyImmediate(p);}
    }
    // Infinite straight reference avoids a closed spline's artificial return bend.
    sealed class StraightReferenceLine : IRacingLine
    {
        public float Length => 10000f;
        public LineProjection Project(Vector3 position,int nearSegment,int searchSegments=12)
            => new LineProjection(new Vector3(0,0,position.z),Vector3.forward,position.z,position.x,0,80);
    }
    [Serializable] public class StraightResult
    {
        public float kp,kd,deadband,steeringSign,seconds,firstTheta,firstLateralRequest,finalError,minError,maxError;
        public float maximumIncrease,firstIncreaseTime,firstIncreaseBefore,firstIncreaseAfter,minYaw,maxYaw,finalCrossSpeed;
        public int firstKey,ticks,increasingTicks; public bool firstTowardLine,monotonic,overshootWithinTwo;
    }
    [TestCase(1f,1f,3f)] [TestCase(-1f,1f,3f)]
    [TestCase(1f,.5f,1f)] [TestCase(-1f,.5f,1f)]
    public void StraightOffsetConvergesWithoutExcessOvershoot(float steeringSign,float kd,float deadband)
    {
        var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();p.AttitudeDeadbandDegrees=2;
        try {
            p.LookaheadSeconds=3;p.LateralGain=.3f;p.LateralDamping=kd;p.AttitudeDeadbandDegrees=deadband;p.CorneringFactor=.9f;
            var parameters=Parameters();parameters.Stats.FinalSteeringSign=steeringSign;
            var line=new StraightReferenceLine();var planner=new ThrustVectorRevisionSixFixture();
            var state=new MotionState{Position=new Vector3(10,0,0),Velocity=Vector3.forward*50,Yaw=0,YawRate=0};
            var result=new StraightResult{kp=p.LateralGain,kd=kd,deadband=deadband,steeringSign=steeringSign,
                minError=10,maxError=10,firstIncreaseTime=-1};
            var csv=new System.Text.StringBuilder("time,e_before,e_after,z,vx,vz,body_yaw,omega,theta,key,a_lat,phi,remaining\n");
            int key=0;const float dt=1f/60;
            for(int tick=0;tick<240;tick++) {
                float before=state.Position.x;
                key=planner.Plan(state,parameters,line,p,dt,key);var o=planner.LastObservation;
                if(tick==0){result.firstTheta=o.thrustAngle*Mathf.Rad2Deg;result.firstKey=key;
                    result.firstLateralRequest=o.requestedLateralAcceleration;result.firstTowardLine=o.thrustAngle<0;}
                BuddahMotionModel.Step(ref state,parameters,key,dt);
                float after=state.Position.x,increase=after-before;
                result.ticks=tick+1;result.seconds=(tick+1)*dt;
                result.minError=Mathf.Min(result.minError,after);result.maxError=Mathf.Max(result.maxError,after);
                result.minYaw=Mathf.Min(result.minYaw,state.Yaw*Mathf.Rad2Deg);result.maxYaw=Mathf.Max(result.maxYaw,state.Yaw*Mathf.Rad2Deg);
                result.maximumIncrease=Mathf.Max(result.maximumIncrease,increase);
                if(increase>0) {
                    result.increasingTicks++;
                    if(result.firstIncreaseTime<0){result.firstIncreaseTime=result.seconds;result.firstIncreaseBefore=before;result.firstIncreaseAfter=after;}
                }
                csv.AppendLine(FormattableString.Invariant($"{result.seconds:R},{before:R},{after:R},{state.Position.z:R},{state.Velocity.x:R},{state.Velocity.z:R},{state.Yaw*Mathf.Rad2Deg:R},{state.YawRate:R},{o.thrustAngle*Mathf.Rad2Deg:R},{key},{o.requestedLateralAcceleration:R},{o.headingError*Mathf.Rad2Deg:R},{o.attitudeRemaining*Mathf.Rad2Deg:R}"));
            }
            result.finalError=state.Position.x;result.finalCrossSpeed=state.Velocity.x;
            result.monotonic=result.increasingTicks==0;result.overshootWithinTwo=result.minError>=-2;
            string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--thrust-offline-output");
            if(at>=0 && at+1<args.Length) {
                Directory.CreateDirectory(args[at+1]);string stem=FormattableString.Invariant($"straight-kd-{kd:F1}-sign-{steeringSign:F0}");
                File.WriteAllText(Path.Combine(args[at+1],stem+".json"),JsonUtility.ToJson(result,true));
                File.WriteAllText(Path.Combine(args[at+1],stem+".csv"),csv.ToString());
            }
            TestContext.WriteLine(JsonUtility.ToJson(result));
            CollectionAssert.AreEqual(new[]{true,true,true},
                new[]{result.firstTowardLine,result.monotonic,result.overshootWithinTwo},
                $"Gates: first theta toward line / signed e monotonic / overshoot<=2m; first increase at {result.firstIncreaseTime:R}s: {result.firstIncreaseBefore:R}->{result.firstIncreaseAfter:R}; minimum e={result.minError:R}");
        } finally{UnityEngine.Object.DestroyImmediate(p);}
    }

    [Serializable] public class TargetSeamFrame
    {
        public int tick, previousKey, oldKey; public Vector3 position, velocity;
        public float yaw, yawRate, tangentYaw, velocityError, theta, oldCarError, oldTarget, oldPhi;
    }
    [Serializable] public class TargetSeamFixture
    {
        public string source; public float tau=3, kp=.3f, kd=.5f, deadband=1, factor=.9f, targetSpeed=80, margin=2, hysteresis=.5f;
        public TargetSeamFrame[] frames;
    }
    [Test,Explicit("Freeze exact revision-five ticks435/436; no rounded CSV reconstruction")]
    public void CaptureTargetSeamFixture()
    {
        var f=Read();var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();p.AttitudeDeadbandDegrees=2;
        try {
            p.LookaheadSeconds=3;p.LateralGain=.3f;p.LateralDamping=.5f;p.AttitudeDeadbandDegrees=1;p.CorneringFactor=.9f;
            var line=new SplineRacingLine(f.points,f.length);var start=line.SampleDistance(0);
            var planner=new ThrustVectorRevisionFiveFixture();
            var state=new MotionState{Position=start.Point,Velocity=start.Tangent*60,Yaw=Mathf.Atan2(start.Tangent.x,start.Tangent.z)};
            var frames=new List<TargetSeamFrame>();int key=0;
            for(int tick=0;tick<=436;tick++) {
                int previous=key;key=planner.Plan(state,Parameters(),line,p,1f/60,key);var o=planner.LastObservation;
                if(tick>=435) {
                    float tangent=Mathf.Atan2(o.tangent.x,o.tangent.z)*Mathf.Rad2Deg;
                    frames.Add(new TargetSeamFrame{tick=tick,previousKey=previous,oldKey=key,position=state.Position,velocity=state.Velocity,
                        yaw=state.Yaw,yawRate=state.YawRate,tangentYaw=tangent,
                        velocityError=Mathf.DeltaAngle(tangent,Mathf.Atan2(state.Velocity.x,state.Velocity.z)*Mathf.Rad2Deg),
                        theta=o.thrustAngle*Mathf.Rad2Deg,oldCarError=o.routeHeadingErrorDegrees,
                        oldTarget=Mathf.DeltaAngle(tangent,o.desiredYaw*Mathf.Rad2Deg),oldPhi=o.headingError*Mathf.Rad2Deg});
                }
                BuddahMotionModel.Step(ref state,Parameters(),key,1f/60);
            }
            Assert.That(frames[0].oldPhi,Is.EqualTo(-.653f).Within(.01f));
            Assert.That(frames[1].oldPhi,Is.EqualTo(359.245f).Within(.01f));
            var saved=new TargetSeamFixture{source="Frozen revision-five pure-model replay, exact pre-step states at 435/60 and 436/60 seconds; diagnostic profile, not formal lap profile",frames=frames.ToArray()};
            File.WriteAllText(Path.Combine(Application.dataPath,"../Tools/ai/fixtures/target-seam-revision-five.json"),JsonUtility.ToJson(saved,true));
            TestContext.WriteLine(JsonUtility.ToJson(saved));
        } finally{UnityEngine.Object.DestroyImmediate(p);}
    }
    [Test] public void RecordedTargetSeamKeepsPhiContinuousAndInputUnreversed()
    {
        var f=JsonUtility.FromJson<TargetSeamFixture>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tools/ai/fixtures/target-seam-revision-five.json")));
        var a=f.frames[0];var b=f.frames[1];
        Assert.That(b.oldPhi-a.oldPhi,Is.GreaterThan(300));Assert.That(a.oldKey,Is.Zero);Assert.That(b.oldKey,Is.EqualTo(1));
        var state=new ThrustVectorRevisionSixFixture.HeadingBranchState{Car=a.oldCarError};
        float phiA=ThrustVectorRevisionSixFixture.BranchHeadingError(ref state,0,0,a.velocityError,a.theta,false,out float targetA,out bool recoveryA);
        int keyA=ThrustVectorRevisionSixFixture.AttitudeKey(phiA,a.yawRate,3,f.deadband*Mathf.Deg2Rad,f.hysteresis*Mathf.Deg2Rad,a.previousKey);
        float phiB=ThrustVectorRevisionSixFixture.BranchHeadingError(ref state,Mathf.DeltaAngle(a.yaw*Mathf.Rad2Deg,b.yaw*Mathf.Rad2Deg),
            Mathf.DeltaAngle(a.tangentYaw,b.tangentYaw),b.velocityError,b.theta,false,out float targetB,out bool recoveryB);
        int keyB=ThrustVectorRevisionSixFixture.AttitudeKey(phiB,b.yawRate,3,f.deadband*Mathf.Deg2Rad,f.hysteresis*Mathf.Deg2Rad,keyA);
        Assert.That(recoveryA||recoveryB,Is.False);Assert.That(targetA,Is.EqualTo(-178));Assert.That(targetB,Is.EqualTo(-178));
        Assert.That(Mathf.Abs(phiB-phiA)*Mathf.Rad2Deg,Is.LessThan(5));Assert.That(keyB,Is.EqualTo(keyA));Assert.That(keyB,Is.Zero);
        TestContext.WriteLine($"Exact seam phi(deg)={phiA*Mathf.Rad2Deg:R} -> {phiB*Mathf.Rad2Deg:R}; key={keyA}->{keyB}; omega={a.yawRate:R}->{b.yawRate:R}");
    }
    [TestCase(179f,2f)] [TestCase(-179f,-2f)]
    public void SharedRebasePreservesPhiAndNextTickTarget(float car,float change)
    {
        var state=new ThrustVectorRevisionSixFixture.HeadingBranchState{Car=car};
        float expected=150-(car+change);
        float phi=ThrustVectorRevisionSixFixture.BranchHeadingError(ref state,change,0,0,150,false,out float target,out bool recovery);
        Assert.That(recovery,Is.False);Assert.That(state.Car,Is.InRange(-180f,180f));
        Assert.That(phi*Mathf.Rad2Deg,Is.EqualTo(expected).Within(.001f));
        Assert.That(target+state.Offset,Is.EqualTo(150));Assert.That(state.Car+state.Offset,Is.EqualTo(car+change));
        float next=ThrustVectorRevisionSixFixture.BranchHeadingError(ref state,0,0,0,150,false,out _,out bool nextRecovery);
        Assert.That(nextRecovery,Is.False);Assert.That(next,Is.EqualTo(phi).Within(.00001f));
    }
    [TestCase(180f,true,false)] [TestCase(180.01f,true,true)]
    [TestCase(200f,false,false)] [TestCase(200.01f,false,true)]
    [TestCase(-180f,true,false)] [TestCase(-180.01f,true,true)]
    [TestCase(-200f,false,false)] [TestCase(-200.01f,false,true)]
    public void RecoveryUsesIndependentTwoHundredAndCollisionOneEightyGates(float car,bool collision,bool expected)
    {
        var state=new ThrustVectorRevisionSixFixture.HeadingBranchState{Car=car};
        float phi=ThrustVectorRevisionSixFixture.BranchHeadingError(ref state,0,0,0,0,collision,out _,out bool recovery);
        Assert.That(recovery,Is.EqualTo(expected));
        Assert.That(phi*Mathf.Rad2Deg,Is.EqualTo(expected ? Mathf.DeltaAngle(car,0) : -car).Within(.001f));
    }
    [Test] public void RecoveryThresholdSurvivesEarlierNumericalRebase()
    {
        var state=new ThrustVectorRevisionSixFixture.HeadingBranchState{Car=179};
        ThrustVectorRevisionSixFixture.BranchHeadingError(ref state,2,0,0,0,false,out _,out bool first);
        Assert.That(first,Is.False);Assert.That(state.Offset,Is.EqualTo(360));
        float phi=ThrustVectorRevisionSixFixture.BranchHeadingError(ref state,20,0,0,0,false,out _,out bool second);
        Assert.That(second,Is.True);Assert.That(phi*Mathf.Rad2Deg,Is.EqualTo(159).Within(.001f));
    }
    [TestCase(90f,false)] [TestCase(90.01f,true)] [TestCase(-90f,false)] [TestCase(-90.01f,true)]
    public void BackwardVelocityUsesTangentRecovery(float velocityError,bool expected)
    {
        var state=new ThrustVectorRevisionSixFixture.HeadingBranchState{Car=150};
        float phi=ThrustVectorRevisionSixFixture.BranchHeadingError(ref state,0,0,velocityError,120,false,out float target,out bool recovery);
        Assert.That(recovery,Is.EqualTo(expected));
        Assert.That(target+state.Offset,Is.EqualTo(expected ? 0 : Mathf.Clamp(velocityError+120,-178,178)));
        if(expected)Assert.That(phi*Mathf.Rad2Deg,Is.EqualTo(-150).Within(.001f));
    }
    [Test] public void SideSwitchRetainsThreeHundredDegreeFrontArc()
    {
        var state=new ThrustVectorRevisionSixFixture.HeadingBranchState{Car=150};
        float phi=ThrustVectorRevisionSixFixture.BranchHeadingError(ref state,0,0,0,-150,false,out _,out bool recovery);
        Assert.That(recovery,Is.False);Assert.That(phi*Mathf.Rad2Deg,Is.EqualTo(-300).Within(.001f));
    }
    [Test] public void CollisionRecencyExpiresAfterHalfSecondOfPlanning()
    {
        var f=Read();var line=new SplineRacingLine(f.points,f.length);var start=line.SampleDistance(0);
        var state=new MotionState{Position=start.Point,Velocity=start.Tangent*60,Yaw=Mathf.Atan2(start.Tangent.x,start.Tangent.z)};
        var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();p.AttitudeDeadbandDegrees=2;var planner=new ThrustVectorRevisionSixFixture();
        try {
            planner.NotifyCollision();planner.Plan(state,Parameters(),line,p,.5f,0);
            Assert.That(planner.LastObservation.recentCollision,Is.True);
            planner.Plan(state,Parameters(),line,p,1f/60,0);Assert.That(planner.LastObservation.recentCollision,Is.False);
        } finally{UnityEngine.Object.DestroyImmediate(p);}
    }
    [Test,Explicit("Sixth revision exact diagnostic-profile comparison; no parameter search")]
    public void TraceSixthRevisionDiagnosticProfile()
    {
        var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();p.AttitudeDeadbandDegrees=2;
        try {
            p.LookaheadSeconds=3;p.LateralGain=.3f;p.LateralDamping=.5f;p.AttitudeDeadbandDegrees=1;p.CorneringFactor=.9f;
            Save("seam-profile-comparison.json",new List<Result>{Lap(Read(),p,true,true,"seam-profile-lap.csv")});
        } finally{UnityEngine.Object.DestroyImmediate(p);}
    }

    [TestCase(.5f,0f,0,1)] [TestCase(-.5f,0f,0,-1)] [TestCase(0f,0f,1,0)]
    [TestCase(.16666667f,1f,1,0)] [TestCase(-.16666667f,-1f,-1,0)]
    [TestCase(.1f,1f,1,-1)] [TestCase(-.1f,-1f,-1,1)]
    public void SwitchingCurveAccountsForCoasting(float phi,float omega,int previous,int expected)
        => Assert.That(ThrustVectorRevisionSixFixture.AttitudeKey(phi,omega,3,.02f,.01f,previous),Is.EqualTo(expected));
    [Test] public void NeutralHysteresisPreventsChatter()
    {
        Assert.That(ThrustVectorRevisionSixFixture.AttitudeKey(.025f,0,3,.02f,.01f,0),Is.Zero);
        Assert.That(ThrustVectorRevisionSixFixture.AttitudeKey(.025f,0,3,.02f,.01f,1),Is.EqualTo(1));
    }
    [Test] public void FixedThrustBranchesUseHysteresisCapAndStableBrakingSign()
    {
        bool brake=false;int sign=0;
        float angle=ThrustVectorRevisionSixFixture.GuidanceAngle(1,25,50,60,80,2,ref brake,ref sign);
        Assert.That(25*Mathf.Sin(angle),Is.EqualTo(1).Within(.0001f));Assert.That(brake,Is.False);
        angle=ThrustVectorRevisionSixFixture.GuidanceAngle(1,25,65,60,80,2,ref brake,ref sign);
        Assert.That(brake,Is.True);Assert.That(angle*Mathf.Rad2Deg,Is.EqualTo(180-Mathf.Asin(1f/25)*Mathf.Rad2Deg).Within(.001f));
        angle=ThrustVectorRevisionSixFixture.GuidanceAngle(-.1f,25,61,60,80,2,ref brake,ref sign);
        Assert.That(brake,Is.True);Assert.That(angle,Is.GreaterThan(0));
        angle=ThrustVectorRevisionSixFixture.GuidanceAngle(-1,25,80,60,80,2,ref brake,ref sign);
        Assert.That(brake,Is.True);
        angle=ThrustVectorRevisionSixFixture.GuidanceAngle(-4,25,80,80,80,2,ref brake,ref sign);
        Assert.That(brake,Is.False);Assert.That(25*Mathf.Sin(angle),Is.EqualTo(-4).Within(.0001f));
    }
    [TestCase(0f,178f)] [TestCase(3f,173.1079f)] [TestCase(25f,120f)]
    public void BrakingAngleRemovesUnrequestedLateralThrust(float lateral,float expectedDegrees)
    {
        bool braking=true;int sign=1;
        float theta=ThrustVectorRevisionSixFixture.GuidanceAngle(lateral,25,60,40,80,2,ref braking,ref sign);
        Assert.That(theta*Mathf.Rad2Deg,Is.EqualTo(expectedDegrees).Within(.001f));Assert.That(braking,Is.True);
        if(lateral==3)Assert.That(25*Mathf.Sin(theta),Is.EqualTo(lateral).Within(.0001f));
    }
    [TestCase(false,58f)] [TestCase(true,60f)] [TestCase(false,62f)]
    public void HoldingModeUsesNinetyDegreesAndRetainsLatch(bool prior,float speed)
    {
        bool braking=prior;int sign=1;
        float theta=ThrustVectorRevisionSixFixture.GuidanceAngle(18,25,speed,60,80,2,ref braking,ref sign);
        Assert.That(theta*Mathf.Rad2Deg,Is.EqualTo(90).Within(.001f));Assert.That(braking,Is.EqualTo(prior));
        Assert.That(ThrustVectorRevisionSixFixture.IsHolding(speed,60,2,18),Is.True);
        Assert.That(ThrustVectorRevisionSixFixture.IsHolding(speed,60,2,17.99f),Is.False);
        Assert.That(ThrustVectorRevisionSixFixture.IsHolding(62.01f,60,2,18),Is.False);
    }
    [Test,Explicit("Fifth revision: evaluate factor0.9 before0.95 without changing other parameters")]
    public void EvaluateOscillationRevision()
    {
        var f=Read();var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();p.AttitudeDeadbandDegrees=2;var runs=new List<Result>();
        try {
            p.LookaheadSeconds=3;p.LateralGain=.3f;p.LateralDamping=1;p.AttitudeDeadbandDegrees=3;
            foreach(float factor in new[]{.9f,.95f}) {
                p.CorneringFactor=factor;
                runs.Add(Lap(f,p,true,true,FormattableString.Invariant($"thrust-factor-{factor:F2}-lap.csv")));
            }
            Save("oscillation-comparison.json",runs);
        } finally{UnityEngine.Object.DestroyImmediate(p);}
    }
    [Test,Explicit("Bounded fifth-revision calibration; reports all failures, never a lap acceptance test")]
    public void CalibrateOscillationRevision()
    {
        var f=Read();var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();p.AttitudeDeadbandDegrees=2;var runs=new List<Result>();
        try {
            foreach(float factor in new[]{.9f,.95f})
            foreach(float tau in new[]{1f,2f,3f})
            foreach(float kp in new[]{.1f,.3f})
            foreach(float kd in new[]{.5f,1f})
            foreach(float deadband in new[]{1f,2f}) {
                p.LookaheadSeconds=tau;p.LateralGain=kp;p.LateralDamping=kd;p.AttitudeDeadbandDegrees=deadband;p.CorneringFactor=factor;
                runs.Add(Lap(f,p,true));
            }
            Save("oscillation-calibration.json",runs);
        } finally{UnityEngine.Object.DestroyImmediate(p);}
    }
    [Test,Explicit("Preserve full traces of completed fifth-revision laps that failed winding/side-change gates")]
    public void TraceOscillationFailures()
    {
        var f=Read();var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();p.AttitudeDeadbandDegrees=2;var runs=new List<Result>();
        try {
            p.LookaheadSeconds=3;p.LateralGain=.3f;p.LateralDamping=.5f;p.AttitudeDeadbandDegrees=1;
            foreach(float factor in new[]{.9f,.95f}) {
                p.CorneringFactor=factor;runs.Add(Lap(f,p,true,true,FormattableString.Invariant($"failed-winding-factor-{factor:F2}.csv")));
            }
            Save("failed-winding.json",runs);
        } finally{UnityEngine.Object.DestroyImmediate(p);}
    }
    [Test] public void FixtureReplayHonorsMotorSteeringSignAndSuppression()
    {
        var f=Read();var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();p.AttitudeDeadbandDegrees=2;
        try
        {
            foreach(var sample in f.states)
            {
                var state=new MotionState {Position=sample.position,Velocity=sample.velocity,Yaw=sample.yaw*Mathf.Deg2Rad,YawRate=sample.yawRate};
                var a=new ThrustVectorRevisionSixFixture();var b=new ThrustVectorRevisionSixFixture();var line=new SplineRacingLine(f.points,f.length);int previous=0;
                for(int i=0;i<5;i++)
                {
                    var normal=Parameters();var inverted=normal;inverted.Stats.FinalSteeringSign=-1;
                    int k=a.Plan(state,normal,line,p,1f/60,previous),inv=b.Plan(state,inverted,line,p,1f/60,-previous);
                    Assert.That(inv,Is.EqualTo(-k));Assert.That(a.LastObservation.desiredAcceleration.magnitude,Is.LessThanOrEqualTo(25.001f));
                    normal.Stats.IsSteeringSuppressed=true;
                    Assert.That(new ThrustVectorRevisionSixFixture().Plan(state,normal,line,p,1f/60,previous),Is.Zero);
                    BuddahMotionModel.Step(ref state,Parameters(),k,1f/60);previous=k;
                }
            }
            p.UseThrustVector=true;p.ReplanTicks=20;Assert.That(p.EffectiveReplanTicks,Is.EqualTo(1));
        }
        finally {UnityEngine.Object.DestroyImmediate(p);}
    }
    [Test] public void OfflineLapComparison()
    {
        var f=Read();var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();p.AttitudeDeadbandDegrees=2;
        try
        {
            p.LookaheadSeconds=3;p.LateralGain=.3f;p.LateralDamping=1;p.AttitudeDeadbandDegrees=3;p.CorneringFactor=.9f;
            var beam=Lap(f,p,false,true);var thrust=Lap(f,p,true,true);Save("comparison.json",new List<Result>{beam,thrust});
            Assert.That(thrust.complete,Is.True);
            Assert.That(thrust.minHeading,Is.GreaterThanOrEqualTo(-180));Assert.That(thrust.maxHeading,Is.LessThanOrEqualTo(180));
            Assert.That(thrust.sideChanges,Is.LessThan(15));
            Assert.That(thrust.seconds,Is.LessThan(165.633333f),"Interim comparison to the recorded A1; compare to the new A1 again after it runs");
            // A failed beam has no lap time to compare. The user-authorized Player gate
            // is a completed thrust lap with independent unwrapped heading within +/-180.
            if(beam.complete) TestContext.WriteLine($"Completed beam comparison: thrust minus beam seconds={thrust.seconds-beam.seconds}, RMS={thrust.rms-beam.rms}");
            else TestContext.WriteLine("Beam did not complete: no baseline lap-time or full-lap RMS comparison available.");
        }
        finally {UnityEngine.Object.DestroyImmediate(p);}
    }
    [Serializable] public class PaceReleaseFixture
    {
        public string source; public int tick,previousKey; public Vector3 position,velocity;
        public float yaw,yawRate,speed,currentPace,aheadPace,progress,lookahead,lateralAcceleration;
        public bool previousBraking;
    }
    [Test,Explicit("Capture third-revision tick175 without reconstructing rounded CSV state")]
    public void CapturePrematureBrakeReleaseFixture()
    {
        var f=Read();var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();p.AttitudeDeadbandDegrees=2;
        try {
            var line=new SplineRacingLine(f.points,f.length);var start=line.SampleDistance(0);
            var planner=new ThrustVectorRevisionThreeFixture();
            var state=new MotionState{Position=start.Point,Velocity=start.Tangent*60,Yaw=Mathf.Atan2(start.Tangent.x,start.Tangent.z)};
            int key=0;bool prior=false;
            for(int i=0;i<175;i++){key=planner.Plan(state,Parameters(),line,p,1f/60,key);prior=planner.LastObservation.brakingBranch;BuddahMotionModel.Step(ref state,Parameters(),key,1f/60);}
            int previous=key;planner.Plan(state,Parameters(),line,p,1f/60,key);var o=planner.LastObservation;
            Assert.That(state.Velocity.magnitude,Is.EqualTo(52.90829f).Within(.0001f));Assert.That(prior,Is.True);Assert.That(o.brakingBranch,Is.False);
            var current=line.SampleDistance(o.progress);
            Assert.That(current.Pace,Is.EqualTo(48.25436f).Within(.0001f));
            var saved=new PaceReleaseFixture{source="Exact frozen revision-three replay: premature release at zero-based tick175 (2.916667s)",tick=175,
                previousKey=previous,position=state.Position,velocity=state.Velocity,yaw=state.Yaw,yawRate=state.YawRate,speed=state.Velocity.magnitude,
                currentPace=current.Pace,aheadPace=o.pace,progress=o.progress,lookahead=state.Velocity.magnitude*p.LookaheadSeconds,
                lateralAcceleration=o.requestedLateralAcceleration,previousBraking=prior};
            File.WriteAllText(Path.Combine(Application.dataPath,"../Tools/ai/fixtures/premature-brake-release.json"),JsonUtility.ToJson(saved,true));
        } finally{UnityEngine.Object.DestroyImmediate(p);}
    }
    [Test] public void RecordedPaceReleaseKeepsBrakingAcrossWindow()
    {
        var f=Read();var saved=JsonUtility.FromJson<PaceReleaseFixture>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tools/ai/fixtures/premature-brake-release.json")));
        var line=new SplineRacingLine(f.points,f.length);var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();p.AttitudeDeadbandDegrees=2;
        try {
            line.PreparePace(25,p.TargetSpeed,p.CorneringFactor);float pace=line.MinimumPace(saved.progress,saved.lookahead);
            Assert.That(saved.currentPace,Is.EqualTo(48.25f).Within(.01f));Assert.That(saved.speed,Is.EqualTo(52.91f).Within(.01f));
            Assert.That(pace,Is.LessThanOrEqualTo(saved.currentPace));Assert.That(saved.aheadPace,Is.GreaterThan(saved.speed+2));
            bool braking=saved.previousBraking;int side=1;
            ThrustVectorRevisionSixFixture.GuidanceAngle(saved.lateralAcceleration,25,saved.speed,pace,80,2,ref braking,ref side);
            Assert.That(braking,Is.True);
            var planner=new ThrustVectorRevisionSixFixture();planner.Plan(new MotionState{Position=saved.position,Velocity=saved.velocity,Yaw=saved.yaw,YawRate=saved.yawRate},Parameters(),line,p,1f/60,saved.previousKey);
            Assert.That(planner.LastObservation.brakingBranch,Is.True);Assert.That(planner.LastObservation.pace,Is.EqualTo(pace).Within(.0001f));
        } finally{UnityEngine.Object.DestroyImmediate(p);}
    }
    [Test] public void PaceWindowIncludesInteriorSamplesAndWraps()
    {
        var f=Read();var line=new SplineRacingLine(f.points,f.length);line.PreparePace(25,80,.8f);
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
    [Serializable] public class BrakingFixture
    {
        public string source; public int tick; public float delta,speed,pace,lateralAcceleration,theta,cap,margin;
        public Vector3 position,velocity; public float yaw,yawRate,lateral; public bool braking;
    }
    [Test,Explicit("Reproduce the first-revision failure for a durable fixture")]
    public void CaptureSaturatedBrakingFixture()
    {
        var f=Read();var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();p.AttitudeDeadbandDegrees=2;
        try
        {
            p.LateralGain=.1f;p.LateralDamping=1;
            var line=new SplineRacingLine(f.points,f.length);var start=line.SampleDistance(0);var planner=new ThrustVectorRevisionOneFixture();
            var state=new MotionState{Position=start.Point,Velocity=start.Tangent*60,Yaw=Mathf.Atan2(start.Tangent.x,start.Tangent.z)};int key=0;
            for(int i=0;i<120;i++){key=planner.Plan(state,Parameters(),line,p,1f/60,key);BuddahMotionModel.Step(ref state,Parameters(),key,1f/60);}
            planner.Plan(state,Parameters(),line,p,1f/60,key);var o=planner.LastObservation;
            Assert.That(state.Velocity.magnitude,Is.EqualTo(76.56396f).Within(.0001f));Assert.That(o.pace,Is.EqualTo(41.10937f).Within(.0001f));
            var saved=new BrakingFixture{source="First-revision t=2s diagnostic, reproduced with frozen test-only planner and measured motion parameters",tick=120,delta=1f/60,
                speed=state.Velocity.magnitude,pace=o.pace,lateralAcceleration=o.requestedLateralAcceleration,theta=o.thrustAngle,
                position=state.Position,velocity=state.Velocity,yaw=state.Yaw,yawRate=state.YawRate,lateral=o.lateral,braking=o.brakingBranch,cap=80,margin=2};
            File.WriteAllText(Path.Combine(Application.dataPath,"../Tools/ai/fixtures/saturated-braking.json"),JsonUtility.ToJson(saved,true));
        }
        finally{UnityEngine.Object.DestroyImmediate(p);}
    }
    [Test] public void RecordedOverspeedTickPreservesLongitudinalBraking()
    {
        var f=JsonUtility.FromJson<BrakingFixture>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tools/ai/fixtures/saturated-braking.json")));
        bool braking=f.braking;int sign=1;
        float theta=ThrustVectorRevisionSixFixture.GuidanceAngle(f.lateralAcceleration,25,f.speed,f.pace,f.cap,f.margin,ref braking,ref sign);
        Assert.That(braking,Is.True);Assert.That(Mathf.Abs(theta)*Mathf.Rad2Deg,Is.GreaterThanOrEqualTo(119.999f));
        Assert.That(25*Mathf.Cos(theta),Is.LessThanOrEqualTo(-12.499f));
    }
    [Serializable] public class HeadingFixture
    {
        public string source; public int tick; public Vector3 position,velocity;
        public float yaw,yawRate,carErrorDegrees,targetErrorDegrees,oldShortestPhiDegrees,lateralAcceleration;
    }
    [Test,Explicit("Capture the original first side-change state without rounded CSV reconstruction")]
    public void CaptureHeadingSideChangeFixture()
    {
        var f=Read();var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();p.AttitudeDeadbandDegrees=2;
        try
        {
            p.LateralGain=.1f;p.LateralDamping=1;
            var line=new SplineRacingLine(f.points,f.length);var start=line.SampleDistance(0);var planner=new ThrustVectorRevisionTwoFixture();
            var state=new MotionState{Position=start.Point,Velocity=start.Tangent*60,Yaw=Mathf.Atan2(start.Tangent.x,start.Tangent.z)};
            float previousYaw=state.Yaw,previousTangent=state.Yaw*Mathf.Rad2Deg,relative=0;int key=0;
            for(int i=0;i<=361;i++)
            {
                key=planner.Plan(state,Parameters(),line,p,1f/60,key);var o=planner.LastObservation;
                float tangent=Mathf.Atan2(o.tangent.x,o.tangent.z)*Mathf.Rad2Deg;
                relative+=(state.Yaw-previousYaw)*Mathf.Rad2Deg-Mathf.DeltaAngle(previousTangent,tangent);
                previousYaw=state.Yaw;previousTangent=tangent;
                if(i==361)
                {
                    Assert.That(state.Yaw*Mathf.Rad2Deg,Is.EqualTo(138.8215f).Within(.001f));
                    var saved=new HeadingFixture{source="Second-revision first side change, exact model replay, t=361/60s",tick=i,
                        position=state.Position,velocity=state.Velocity,yaw=state.Yaw,yawRate=state.YawRate,carErrorDegrees=relative,
                        targetErrorDegrees=Mathf.DeltaAngle(tangent,o.desiredYaw*Mathf.Rad2Deg),oldShortestPhiDegrees=o.headingError*Mathf.Rad2Deg,
                        lateralAcceleration=o.requestedLateralAcceleration};
                    File.WriteAllText(Path.Combine(Application.dataPath,"../Tools/ai/fixtures/heading-side-change.json"),JsonUtility.ToJson(saved,true));
                }
                BuddahMotionModel.Step(ref state,Parameters(),key,1f/60);
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(p);}
    }
    [Test] public void RecordedSideChangeTurnsAcrossTangentInsteadOfTail()
    {
        var f=JsonUtility.FromJson<HeadingFixture>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tools/ai/fixtures/heading-side-change.json")));
        float error=f.carErrorDegrees;
        float phi=ThrustVectorRevisionSixFixture.BranchHeadingError(ref error,0,0,f.targetErrorDegrees);
        Assert.That(f.oldShortestPhiDegrees,Is.LessThan(0));Assert.That(phi,Is.GreaterThan(Mathf.PI));
        Assert.That(ThrustVectorRevisionSixFixture.AttitudeKey(phi,f.yawRate,3,2*Mathf.Deg2Rad,.5f*Mathf.Deg2Rad,-1),Is.EqualTo(1));
        var state=new MotionState{Yaw=f.carErrorDegrees*Mathf.Deg2Rad,YawRate=f.yawRate};int key=0;bool crossedTangent=false;
        for(int i=0;i<360;i++)
        {
            float old=state.Yaw;phi=ThrustVectorRevisionSixFixture.BranchHeadingError(ref error,0,0,f.targetErrorDegrees);
            key=ThrustVectorRevisionSixFixture.AttitudeKey(phi,state.YawRate,3,2*Mathf.Deg2Rad,.5f*Mathf.Deg2Rad,key);
            BuddahMotionModel.Step(ref state,Parameters(),key,1f/60);error+=(state.Yaw-old)*Mathf.Rad2Deg;
            // This assertion uses actual accumulated yaw, never the controller's reanchored value.
            Assert.That(state.Yaw*Mathf.Rad2Deg,Is.InRange(-180f,180f));crossedTangent|=state.Yaw>=0;
        }
        Assert.That(crossedTangent,Is.True);Assert.That(state.Yaw*Mathf.Rad2Deg,Is.EqualTo(f.targetErrorDegrees).Within(3));
    }
    [Test] public void CollisionRotationReanchorsToRemainingSixtyDegrees()
    {
        var f=JsonUtility.FromJson<HeadingFixture>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tools/ai/fixtures/collision-rotation-300.json")));
        float error=0;float phi=ThrustVectorRevisionSixFixture.BranchHeadingError(ref error,f.carErrorDegrees,0,f.targetErrorDegrees);
        Assert.That(error,Is.EqualTo(-60).Within(.001f));Assert.That(phi*Mathf.Rad2Deg,Is.EqualTo(60).Within(.001f));
        var state=new MotionState{Yaw=f.carErrorDegrees*Mathf.Deg2Rad,YawRate=0};int key=0;
        for(int i=0;i<240;i++)
        {
            float old=state.Yaw;phi=ThrustVectorRevisionSixFixture.BranchHeadingError(ref error,0,0,0);
            key=ThrustVectorRevisionSixFixture.AttitudeKey(phi,state.YawRate,3,2*Mathf.Deg2Rad,.5f*Mathf.Deg2Rad,key);
            BuddahMotionModel.Step(ref state,Parameters(),key,1f/60);error+=(state.Yaw-old)*Mathf.Rad2Deg;
            Assert.That(state.Yaw*Mathf.Rad2Deg,Is.GreaterThanOrEqualTo(299.999f));
        }
        Assert.That(state.Yaw*Mathf.Rad2Deg,Is.EqualTo(360).Within(3));
    }
    [Test,Explicit("Fixed-thrust diagnostic trace")]
    public void DiagnoseRevisedMapping()
    {
        var f=Read();var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();p.AttitudeDeadbandDegrees=2;
        try
        {
            p.LateralGain=.1f;p.LateralDamping=1f;
            var line=new SplineRacingLine(f.points,f.length);var start=line.SampleDistance(0);var planner=new ThrustVectorRevisionSixFixture();
            var state=new MotionState{Position=start.Point,Velocity=start.Tangent*60,Yaw=Mathf.Atan2(start.Tangent.x,start.Tangent.z)};
            var csv=new System.Text.StringBuilder("time,speed,pace,lateral,theta,requested_lateral,braking,key,yaw,yawrate,desired_yaw,full_projection_error,local_segment,full_segment,route_yaw,current_curvature,cross_speed,x,z,vx,vz,internal_route_error,phi,current_pace\n");int key=0;
            for(int tick=0;tick<60*20;tick++)
            {
                key=planner.Plan(state,Parameters(),line,p,1f/60,key);var o=planner.LastObservation;
                var full=line.Project(state.Position,-1);
                csv.AppendLine(FormattableString.Invariant($"{tick/60f},{state.Velocity.magnitude},{o.pace},{o.lateral},{o.thrustAngle*Mathf.Rad2Deg},{o.requestedLateralAcceleration},{o.brakingBranch},{key},{state.Yaw*Mathf.Rad2Deg},{state.YawRate},{o.desiredYaw*Mathf.Rad2Deg},{full.Lateral},{o.segment},{full.Segment},{Mathf.Atan2(o.tangent.x,o.tangent.z)*Mathf.Rad2Deg},{line.CurvatureAtDistance(o.progress)},{Vector3.Dot(state.Velocity,Vector3.Cross(Vector3.up,o.tangent))},{state.Position.x},{state.Position.z},{state.Velocity.x},{state.Velocity.z},{o.routeHeadingErrorDegrees},{o.headingError*Mathf.Rad2Deg},{full.Pace}"));
                BuddahMotionModel.Step(ref state,Parameters(),key,1f/60);
            }
            string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--thrust-offline-output");
            if(at>=0){Directory.CreateDirectory(args[at+1]);File.WriteAllText(Path.Combine(args[at+1],"trace.csv"),csv.ToString());}
        }
        finally{UnityEngine.Object.DestroyImmediate(p);}
    }
    [Test,Explicit("Offline parameter experiment; not a product acceptance test")]
    public void CalibrateGuidance()
    {
        var f=Read();var p=ScriptableObject.CreateInstance<AIDifficultyProfile>();p.AttitudeDeadbandDegrees=2;var runs=new List<Result>();
        try
        {
            // Lower curvature pace and bracket the attitude deadband after long-preview trials.
            foreach(float factor in new[]{.4f,.6f})
            foreach(float tau in new[]{2f,3f,4f})
            foreach(float kp in new[]{.1f,.3f})
            foreach(float kd in new[]{.3f,1f})
            foreach(float deadband in new[]{1f,3f})
            {p.LookaheadSeconds=tau;p.SpeedMargin=2;p.LateralGain=kp;p.LateralDamping=kd;
                p.CorneringFactor=factor;p.AttitudeDeadbandDegrees=deadband;runs.Add(Lap(f,p,true));}
            Save("calibration.json",runs);

        }
        finally {UnityEngine.Object.DestroyImmediate(p);}
    }
}
