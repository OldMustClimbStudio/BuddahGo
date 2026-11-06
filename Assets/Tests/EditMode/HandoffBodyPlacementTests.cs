using NUnit.Framework;
using NewBuddah.PredictionV2.Core;
using UnityEngine;

namespace BuddahGo.Tests
{
    // Online launch regression (2026-11-06): the consume path wrote the snapshot velocity and then called
    // Sleep/WakeUp, which zeroes it, so racers left GO from rest on host and client. Practice restored it separately.
    public class HandoffBodyPlacementTests
    {
        GameObject _go;
        Rigidbody _body;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("HandoffBodyPlacementTests") { hideFlags = HideFlags.HideAndDontSave };
            _body = _go.AddComponent<Rigidbody>();
            _body.useGravity = false;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void SleepZeroesVelocity_PremiseOfTheFix()
        {
            _body.velocity = new Vector3(0f, 0f, 30f);
            _body.Sleep();
            Assert.That(_body.velocity.magnitude, Is.LessThan(1e-4f), "If Sleep keeps velocity, the placement order no longer matters");
        }

        [Test]
        public void PlaceKeepsSnapshotPoseAndVelocity()
        {
            Vector3 position = new Vector3(1f, 2f, 3f);
            Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);
            Vector3 velocity = new Vector3(0f, 0f, 30f);
            Vector3 angular = new Vector3(0f, 0.5f, 0f);

            BuddahHandoffBodyPlacement.Place(_body, position, rotation, velocity, angular);

            Assert.That(Vector3.Distance(_body.position, position), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(_body.rotation, rotation), Is.LessThan(0.01f));
            Assert.That(Vector3.Distance(_body.velocity, velocity), Is.LessThan(1e-4f), "launch speed lost at handoff");
            Assert.That(Vector3.Distance(_body.angularVelocity, angular), Is.LessThan(1e-4f));
            Assert.That(_body.IsSleeping(), Is.False);
        }

        [Test]
        public void PlaceSkipsVelocityOnKinematicBody()
        {
            _body.isKinematic = true;
            Assert.DoesNotThrow(() => BuddahHandoffBodyPlacement.Place(_body, Vector3.one, Quaternion.identity, Vector3.forward * 5f, Vector3.zero));
            Assert.That(Vector3.Distance(_body.position, Vector3.one), Is.LessThan(1e-4f));
        }
    }
}
