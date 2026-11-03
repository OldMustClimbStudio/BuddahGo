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

        // Each table case starts from its own fixture, as the separate test cases did.
        private void Fresh()
        {
            TearDown();
            SetUp();
        }

        [Test]
        public void NoSplinePoseAfterGoOrOutsideThePendingHandoff()
        {
            EveryCase.Parts(
                ("GO releases the spline sampler even while the motor event is pending", () =>
                    EveryCase.Check(new[] { false, true }, solo =>
                    {
                        Fresh();
                        Set(_intro, "_soloPhysicsClock", solo);
                        Set(_intro, "_goApplied", true);
                        Set(_intro, "_runtimeState", IntroRuntimeState.AuthoritativeHandoffPending);
                        Set(_motor, "_hasPendingLaunchHandoffEvent", true);
                        Assert.False(_intro.TrySampleVisualPoseAtRenderTime(out _, out _),
                            "Solo history or the network smoother owns the graphical pose after GO.");
                        Assert.That(_intro.TargetRigidbody.position, Is.EqualTo(new Vector3(0f, 0f, 9f)));
                    }, solo => "solo=" + solo)),
                ("a non-pending body does not supply the old GO pose", () =>
                    EveryCase.Check(new[] { IntroRuntimeState.AuthoritativeHandoffApplied, IntroRuntimeState.Cancelled }, state =>
                    {
                        Fresh();
                        Set(_intro, "_goApplied", true);
                        Set(_intro, "_runtimeState", state);
                        Set(_motor, "_hasPendingLaunchHandoffEvent", true);
                        Assert.That(_intro.TrySampleVisualPoseAtRenderTime(out _, out _), Is.False);
                    }, state => "state=" + state)));
        }

        [Test]
        public void SoloIntroNeverSuppliesSecondDelayedSplinePose()
        {
            _intro.ApplyIntroAssignment(new IntroAssignmentData { sequenceId = 2 }, _path);
            Set(_intro, "_visualStarted", true);
            Set(_intro, "_soloPhysicsClock", true);
            Set(_intro, "_resolvedIntroStartNetworkTime", 0d);
            Set(_intro, "_resolvedGoNetworkTime", 100d);
            Assert.False(_intro.TrySampleVisualPoseAtRenderTime(out _, out _));
            _intro.ForceExitIntroState();
            Assert.False(_intro.TrySampleVisualPoseAtRenderTime(out _, out _));
            _intro.ApplyIntroAssignment(new IntroAssignmentData { sequenceId = 3 }, _path);
            Assert.That(_intro.ActiveSequenceId, Is.EqualTo(3));
            Assert.False(_intro.TrySampleVisualPoseAtRenderTime(out _, out _), "Rematch waits for its own start.");
        }

        [Test]
        public void LateSnapshotExtrapolatesOnlyTheNetworkPath()
        {
            EveryCase.Check(new[] { (solo: true, expectedDistance: 0f), (solo: false, expectedDistance: 0.4f) }, c =>
            {
                Fresh();
                _intro.ApplyIntroAssignment(new IntroAssignmentData { sequenceId = 1, introSpeedMetersPerSecond = 10f }, _path);
                Set(_intro, "_resolvedIntroStartNetworkTime", 10d);
                Set(_intro, "_resolvedGoNetworkTime", 20d);
                Set(_intro, "_soloPhysicsClock", c.solo);
                // This fixture deliberately has no authored spline knots. Supply the terminal
                // anchor/forward used by the real launch slots instead of sampling an empty spline.
                _path.BindTerminalPose(_body.transform, _body.transform);
                Set(_path, "lockTerminalForwardToSlot", true);
                Set(_path, "_cacheDirty", false);
                var method = typeof(RaceBodyIntroStateController).GetMethod("SampleSnapshotAtTime", PrivateInstance);
                object[] endpointArgs = { 20d, null };
                object[] lateArgs = { 20.04d, null };
                method.Invoke(_intro, endpointArgs);
                method.Invoke(_intro, lateArgs);
                var endpoint = (LaunchHandoffSnapshot)endpointArgs[1];
                var late = (LaunchHandoffSnapshot)lateArgs[1];
                Assert.That(Vector3.Distance(endpoint.Position, late.Position), Is.EqualTo(c.expectedDistance).Within(0.0001f));
                Assert.That(late.Velocity, Is.EqualTo(endpoint.Velocity), "Timing isolation does not retune launch speed.");
            });
        }

        [Test]
        public void SampleUsesNormalizedDistanceRatherThanConvertingArcLengthTwice()
        {
            var cases = new[]
            {
                (now: 9d, expected: 0f),
                (now: 10d, expected: 0f),
                (now: 11d, expected: 0.1f),
                (now: 12.5d, expected: 0.25f),
                (now: 17.5d, expected: 0.75f),
                (now: 20d, expected: 1f),
                (now: 25d, expected: 1f),
            };
            EveryCase.Check(cases, c =>
            {
                Fresh();
                _intro.ApplyIntroAssignment(new IntroAssignmentData { sequenceId = 1, introSpeedMetersPerSecond = 10f }, _path);
                Set(_intro, "_resolvedIntroStartNetworkTime", 10d);
                float distance01 = (float)typeof(RaceBodyIntroStateController)
                    .GetMethod("GetNormalizedDistanceT", PrivateInstance).Invoke(_intro, new object[] { c.now });
                Assert.That(distance01, Is.EqualTo(c.expected).Within(0.00001f));
            });
        }

        [Test]
        public void PostIntroLockDoesNotOvertakeAuthoritativeSmootherBuffer()
        {
            var cases = new[]
            {
                (server: true, hasSmoother: true, activeLaunch: true),
                (server: true, hasSmoother: false, activeLaunch: true),
                (server: false, hasSmoother: true, activeLaunch: true),
                (server: false, hasSmoother: false, activeLaunch: true),
                (server: true, hasSmoother: true, activeLaunch: false),
            };
            EveryCase.Check(cases, c =>
            {
                Fresh();
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
                    var owner = new NetworkConnection { ClientId = 0 };
                    SetProperty(owner, "NetworkManager", manager);
                    client.Connection = owner;
                    SetProperty(network, "Owner", owner);
                    SetProperty(network, "IsClientInitialized", true);
                    SetProperty(network, "IsServerInitialized", c.server);
                    Set(network, "_graphicalObject", visual.transform);
                    SetProperty(network, "PredictionSmoother", c.hasSmoother ? new TransformTickSmoother() : null);
                    Assert.That(network.IsOwner, Is.True);

                    Set(_motor, "_handoffState", new BuddahPredictedLaunchHandoffState { IsActive = c.activeLaunch });
                    var bridge = _body.AddComponent<BuddahPredictionVisualRootBridge>();
                    Set(bridge, "bootstrap", _body.GetComponent<BuddahPredictionBootstrap>());
                    Set(bridge, "predictedMotor", _motor);
                    Set(bridge, "_networkObject", network);
                    // A one-tick lag at 60 m/s exceeds the old 0.75m snap threshold.
                    visual.transform.position = _body.transform.position - Vector3.forward;
                    bool held = (bool)typeof(BuddahPredictionVisualRootBridge)
                        .GetMethod("ShouldLockVisualRootDuringIntroOrPresentation", PrivateInstance)
                        .Invoke(bridge, new object[] { _body.transform, visual.transform, null });
                    Assert.False(held, "PR60 removes the post-intro lock for every network owner.");
                }
                finally
                {
                    SetProperty(network, "IsClientInitialized", false);
                    SetProperty(network, "IsServerInitialized", false);
                    SetProperty(network, "PredictionSmoother", null);
                    Object.DestroyImmediate(managerObject);
                    Object.DestroyImmediate(visual);
                }
            });
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
