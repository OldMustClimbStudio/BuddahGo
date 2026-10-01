using System.Reflection;
using FishNet.Component.Transforming;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Object;
using NewBuddah.PredictionV2.Visual;
using NewBuddah.PredictionV2.Bootstrap;
using NewBuddah.PredictionV2.Core;
using NewBuddah.PredictionV2.Integration;
using NUnit.Framework;
using UnityEngine;

namespace BuddahGo.Tests
{
    public class IntroHandoffContinuityTests
    {
        private GameObject _body;
        private GameObject _pathObject;
        private RaceBodyIntroStateController _intro;
        private BuddahPredictedMotor _motor;
        private SplineIntroPath _path;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            // No session, input, physics simulation or gameplay Awake callbacks.
            _body = new GameObject("intro handoff continuity test");
            _body.SetActive(false);
            var rb = _body.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.position = new Vector3(0f, 0f, 9f); // Last fixed step trails the GO pose.
            var mode = _body.AddComponent<BuddahMovementModeSwitcher>();
            Set(mode, "runtimeMode", BuddahMovementRuntimeMode.PredictionV2);
            var bootstrap = _body.AddComponent<BuddahPredictionBootstrap>();
            Set(bootstrap, "modeSwitcher", mode);
            _motor = _body.AddComponent<BuddahPredictedMotor>();
            var bridge = _body.AddComponent<BuddahPredictionHandoffBridge>();
            Set(bridge, "bootstrap", bootstrap);
            Set(bridge, "predictedMotor", _motor);
            _intro = _body.AddComponent<RaceBodyIntroStateController>();
            Set(_intro, "targetRigidbody", rb);
            Set(_intro, "_predictionHandoffBridge", bridge);

            _pathObject = new GameObject("nonuniform arc length test");
            _pathObject.SetActive(false);
            _path = _pathObject.AddComponent<SplineIntroPath>();
            // Deliberately nonuniform lookup: parameter 0.5 lies at 10% distance.
            Set(_path, "_cacheDirty", false);
            Set(_path, "_tTable", new[] { 0f, 0.5f, 1f });
            Set(_path, "_distanceTable", new[] { 0f, 10f, 100f });
            Set(_path, "_totalLength", 100f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_pathObject);
        }

        [Test]
        public void QueuedGoKeepsEndpointVisibleWithoutMovingPhysicsAndReleasesOnConsume()
        {
            var endpoint = new Vector3(0f, 0f, 10f);
            var rotation = Quaternion.Euler(0f, 25f, 0f);
            Set(_intro, "_goApplied", true);
            Set(_intro, "_runtimeState", IntroRuntimeState.AuthoritativeHandoffPending);
            Set(_intro, "_latestSplineSnapshot", new LaunchHandoffSnapshot { Position = endpoint, Rotation = rotation });
            Set(_motor, "_hasPendingLaunchHandoffEvent", true);
            Set(_motor, "_externalKinematicControlActive", true);
            Assert.That(_intro.HasAssignment, Is.False, "GO has already retired the spline assignment.");

            for (int frame = 0; frame < 3; frame++)
            {
                Assert.That(_intro.TrySampleVisualPoseAtRenderTime(out var position, out var sampledRotation), Is.True);
                Assert.That(position, Is.EqualTo(endpoint));
                Assert.That(sampledRotation, Is.EqualTo(rotation));
                Assert.That(_intro.TargetRigidbody.position, Is.EqualTo(new Vector3(0f, 0f, 9f)));
                Assert.That(_intro.TargetRigidbody.isKinematic, Is.True);
            }

            Set(_motor, "_hasPendingLaunchHandoffEvent", false);
            Set(_motor, "_externalKinematicControlActive", false);
            Assert.That(_intro.TrySampleVisualPoseAtRenderTime(out _, out _), Is.False,
                "After consume, the motor/smoother alone owns the pose.");
            Set(_motor, "_externalKinematicControlActive", true);
            Assert.That(_intro.TrySampleVisualPoseAtRenderTime(out _, out _), Is.False,
                "A later external presentation must not revive the old GO pose.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CancelAndNextAssignmentCannotReusePendingGoPose(bool cancelFirst)
        {
            Set(_intro, "_goApplied", true);
            Set(_intro, "_runtimeState", IntroRuntimeState.AuthoritativeHandoffPending);
            Set(_motor, "_hasPendingLaunchHandoffEvent", true);
            Assert.That(_intro.TrySampleVisualPoseAtRenderTime(out _, out _), Is.True);
            if (cancelFirst)
            {
                _intro.ForceExitIntroState();
                Assert.That(_intro.TrySampleVisualPoseAtRenderTime(out _, out _), Is.False);
            }

            _intro.ApplyIntroAssignment(new IntroAssignmentData { sequenceId = 2 }, _path);
            Assert.That(_intro.ActiveSequenceId, Is.EqualTo(2));
            Assert.That(_intro.HasAssignment, Is.True);
            Assert.That(_intro.TrySampleVisualPoseAtRenderTime(out _, out _), Is.False,
                "Rematch must wait for its own visual start, even with an old motor event pending.");
        }

        [TestCase(IntroRuntimeState.AuthoritativeHandoffApplied)]
        [TestCase(IntroRuntimeState.Cancelled)]
        public void NonPendingBodyDoesNotSupplyOldGoPose(IntroRuntimeState state)
        {
            Set(_intro, "_goApplied", true);
            Set(_intro, "_runtimeState", state);
            Set(_motor, "_hasPendingLaunchHandoffEvent", true);
            Assert.That(_intro.TrySampleVisualPoseAtRenderTime(out _, out _), Is.False);
        }

        [TestCase(9d, 0f)]
        [TestCase(10d, 0f)]
        [TestCase(11d, 0.1f)]
        [TestCase(12.5d, 0.25f)]
        [TestCase(17.5d, 0.75f)]
        [TestCase(20d, 1f)]
        [TestCase(25d, 1f)]
        public void SampleUsesNormalizedDistanceRatherThanConvertingArcLengthTwice(double now, float expected)
        {
            _intro.ApplyIntroAssignment(new IntroAssignmentData { sequenceId = 1, introSpeedMetersPerSecond = 10f }, _path);
            Set(_intro, "_resolvedIntroStartNetworkTime", 10d);
            float distance01 = (float)typeof(RaceBodyIntroStateController)
                .GetMethod("GetNormalizedDistanceT", PrivateInstance).Invoke(_intro, new object[] { now });
            Assert.That(distance01, Is.EqualTo(expected).Within(0.00001f));
        }

        [TestCase(true, true, true, false)]
        [TestCase(true, false, true, true)]
        [TestCase(false, true, true, true)]
        [TestCase(false, false, true, true)]
        [TestCase(true, true, false, true)]
        public void PostIntroLockDoesNotOvertakeAuthoritativeSmootherBuffer(
            bool server, bool hasSmoother, bool activeLaunch, bool expectedLock)
        {
            var managerObject = new GameObject("inactive ownership fixture");
            managerObject.SetActive(false);
            var visual = new GameObject("graphical root");
            visual.transform.SetParent(_body.transform);
            var network = _body.GetComponent<NetworkObject>() ?? _body.AddComponent<NetworkObject>();
            try
            {
                // Wire ownership only. No transports, sessions or simulation are started.
                var manager = managerObject.AddComponent<NetworkManager>();
                var client = managerObject.AddComponent<ClientManager>();
                SetProperty(manager, "ClientManager", client);
                var owner = new NetworkConnection();
                SetProperty(owner, "NetworkManager", manager);
                client.Connection = owner;
                SetProperty(network, "Owner", owner);
                SetProperty(network, "IsClientInitialized", true);
                SetProperty(network, "IsServerInitialized", server);
                Set(network, "_graphicalObject", visual.transform);
                SetProperty(network, "PredictionSmoother", hasSmoother ? new TransformTickSmoother() : null);
                Assert.That(network.IsOwner, Is.True);

                Set(_motor, "_handoffState", new BuddahPredictedLaunchHandoffState { IsActive = activeLaunch });
                var bridge = _body.AddComponent<BuddahPredictionVisualRootBridge>();
                Set(bridge, "bootstrap", _body.GetComponent<BuddahPredictionBootstrap>());
                Set(bridge, "predictedMotor", _motor);
                Set(bridge, "_networkObject", network);
                Set(bridge, "_lastIntroOrExternalActiveFrame", Time.frameCount);
                // A one-tick lag at 60 m/s exceeds the old 0.75m snap threshold.
                visual.transform.position = _body.transform.position - Vector3.forward;
                bool held = (bool)typeof(BuddahPredictionVisualRootBridge)
                    .GetMethod("ShouldHoldPostIntroVisualLock", PrivateInstance)
                    .Invoke(bridge, new object[] { _body.transform, visual.transform });
                Assert.That(held, Is.EqualTo(expectedLock));
            }
            finally
            {
                SetProperty(network, "IsClientInitialized", false);
                SetProperty(network, "IsServerInitialized", false);
                SetProperty(network, "PredictionSmoother", null);
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(visual);
            }
        }

        private static void SetProperty(object target, string property, object value)
        {
            target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
        }
    }
}
