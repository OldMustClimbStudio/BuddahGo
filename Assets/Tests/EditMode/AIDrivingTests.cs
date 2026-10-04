using BuddahGo.AI;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AIDrivingTests
{
    [TestCase(0, 30)]
    [TestCase(1, 30)]
    [TestCase(2, 30)]
    [TestCase(3, 30)]
    [TestCase(0, 60)]
    [TestCase(1, 60)]
    [TestCase(2, 60)]
    [TestCase(3, 60)]
    public void ModelMatchesSixtyRealPhysicsTicks(int sequence, int rate)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var go = new GameObject("Actual Physics Reference"); SceneManager.MoveGameObjectToScene(go, scene);
            var shape = go.AddComponent<CapsuleCollider>(); shape.radius = 3.8869286f; shape.height = 8.493212f;
            var body = go.AddComponent<Rigidbody>(); body.mass = 2; body.useGravity = false;
            body.drag = 0; body.angularDrag = 0.05f; body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.maxAngularVelocity = 50; body.velocity = new Vector3(12, 0, 72); body.angularVelocity = new Vector3(0, 0.3f, 0);
            body.ResetInertiaTensor();
            var parameters = new MotionParameters {
                Stats = new BuddahPredictedMotorComputedStats { FinalForwardForce = 50, FinalTurnTorque = 30, FinalMaxSpeed = 80,
                    FinalSteeringSign = sequence == 3 ? -1 : 1, IsSteeringSuppressed = sequence == 2 },
                Mass = body.mass, InverseYawInertia = MotionParameters.ReadInverseYawInertia(body),
                AngularDrag = body.angularDrag, MaxAngularVelocity = body.maxAngularVelocity, TurnDecay = 3, TurnMultiplier = 1 };
            var forecast = MotionState.Read(body);
            float dt = 1f / rate;
            for (int tick = 0; tick < 60; tick++)
            {
                int key = sequence == 0 ? 0 : tick < 15 ? 1 : tick < 35 ? 0 : -1;
                BuddahMotionModel.Step(ref forecast, parameters, key, dt);
                body.velocity = Vector3.ClampMagnitude(body.velocity, parameters.Stats.FinalMaxSpeed);
                float steering = parameters.Stats.IsSteeringSuppressed ? 0 : key * parameters.Stats.FinalSteeringSign;
                BuddahLocomotionStep.Compute(body.rotation * Vector3.forward, 1, steering, parameters.Stats, out var force, out float torque);
                body.AddForce(force, ForceMode.Force);
                if (steering != 0) body.AddTorque(Vector3.up * torque, ForceMode.Force);
                else body.angularVelocity = new Vector3(0, Mathf.MoveTowards(body.angularVelocity.y, 0, 3 * dt), 0);
                scene.GetPhysicsScene().Simulate(dt);
            }
            float position = Vector3.Distance(forecast.Position, body.position);
            float velocity = Vector3.Distance(forecast.Velocity, body.velocity);
            float yaw = Mathf.Abs(Mathf.DeltaAngle(forecast.Yaw * Mathf.Rad2Deg, body.rotation.eulerAngles.y));
            TestContext.WriteLine($"sequence={sequence}; position={position}; velocity={velocity}; yaw={yaw}");
            Assert.That(position, Is.LessThan(0.5f)); Assert.That(velocity, Is.LessThan(0.5f)); Assert.That(yaw, Is.LessThan(3f));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void ModelIncludesExistingFloorSlidingFriction()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var ground = new GameObject("Floor"); SceneManager.MoveGameObjectToScene(ground, scene);
            var floor = ground.AddComponent<BoxCollider>(); floor.size = new Vector3(10000, 1, 10000); ground.transform.position = new Vector3(0, -0.5f, 0);
            floor.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicMaterial>("Assets/Material/Floor.physicMaterial");
            var go = new GameObject("Sliding reference"); SceneManager.MoveGameObjectToScene(go, scene);
            var shape = go.AddComponent<CapsuleCollider>(); shape.radius = 3.8869286f; shape.height = 8.493212f;
            shape.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicMaterial>("Assets/Material/Character.physicMaterial");
            var body = go.AddComponent<Rigidbody>(); body.position = new Vector3(0, 4.25f, 0); body.mass = 2; body.drag = 0; body.angularDrag = 0.05f;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            var physics = scene.GetPhysicsScene(); const float dt = 1f / 60;
            for (int i = 0; i < 60; i++) physics.Simulate(dt);
            body.velocity = new Vector3(0, 0, 40);
            var parameters = new MotionParameters { Mass = 2, TurnMultiplier = 1, TurnDecay = 3,
                InverseYawInertia = MotionParameters.ReadInverseYawInertia(body), AngularDrag = body.angularDrag, MaxAngularVelocity = body.maxAngularVelocity,
                GroundDeceleration = (shape.sharedMaterial.dynamicFriction + floor.sharedMaterial.dynamicFriction) * 0.5f * Mathf.Abs(Physics.gravity.y),
                Stats = new BuddahPredictedMotorComputedStats { FinalForwardForce = 50, FinalTurnTorque = 30, FinalMaxSpeed = 80, FinalSteeringSign = 1 } };
            var forecast = MotionState.Read(body);
            for (int i = 0; i < 60; i++)
            {
                BuddahMotionModel.Step(ref forecast, parameters, 0, dt);
                body.AddForce(Vector3.forward * 50); physics.Simulate(dt);
            }
            Vector3 positionError = forecast.Position - body.position; positionError.y = 0;
            Vector3 velocityError = forecast.Velocity - body.velocity; velocityError.y = 0;
            TestContext.WriteLine($"floor position={positionError.magnitude}; velocity={velocityError.magnitude}; deceleration={parameters.GroundDeceleration}");
            Assert.That(positionError.magnitude, Is.LessThan(0.5f)); Assert.That(velocityError.magnitude, Is.LessThan(0.5f));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
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
    public void DriverIsSerializedDisabledWithoutChangingMotorConfiguration()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Prefab/Buddah.prefab");
        Assert.That(prefab.GetComponent<AIRacerDriver>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<AIRacerDriver>().enabled, Is.False);
        Assert.That(prefab.GetComponent<Rigidbody>().mass, Is.EqualTo(2));
        Assert.That(prefab.GetComponent<Rigidbody>().drag, Is.Zero);
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

    [Test]
    public void ProfileRejectsNonFiniteAndOutOfRangeValues()
    {
        var profile = ScriptableObject.CreateInstance<AIDifficultyProfile>();
        try
        {
            profile.ValidateConfiguration();
            profile.TargetSpeed = float.NaN;
            Assert.Throws<System.ArgumentException>(() => profile.ValidateConfiguration());
            profile.TargetSpeed = 80f; profile.RolloutSampleTicks = 0;
            Assert.Throws<System.ArgumentException>(() => profile.ValidateConfiguration());
        }
        finally { Object.DestroyImmediate(profile); }
    }
}
