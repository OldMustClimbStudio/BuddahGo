using NUnit.Framework;
using NewBuddah.PredictionV2.Core;

namespace BuddahGo.Tests
{
    public class HandoffGroundSnapTests
    {
        [Test]
        public void PenetratingBodyIsLiftedOntoItsRestHeight()
        {
            // Probe evidence: root 3.300 with 1.236 clearance rested at 3.916 after PhysX pushed it out.
            Assert.That(BuddahHandoffGroundSnap.TryResolveRestY(3.300f, 1.236f, 2.680f, 1.5f, out float restY), Is.True);
            Assert.That(restY, Is.EqualTo(3.916f).Within(1e-4f));
        }

        [Test]
        public void HoveringBodyIsLoweredOntoTheGround()
        {
            Assert.That(BuddahHandoffGroundSnap.TryResolveRestY(5f, 1f, 3f, 1.5f, out float restY), Is.True);
            Assert.That(restY, Is.EqualTo(4f).Within(1e-5f));
        }

        [Test]
        public void MovesBeyondTheLimitAreLeftToPhysics()
        {
            Assert.That(BuddahHandoffGroundSnap.TryResolveRestY(10f, 1f, 2f, 1.5f, out float restY), Is.False);
            Assert.That(restY, Is.EqualTo(10f));
        }

        [Test]
        public void AlreadyRestingBodyIsNotMoved()
        {
            Assert.That(BuddahHandoffGroundSnap.TryResolveRestY(3f, 1f, 2f, 1.5f, out float restY), Is.False);
            Assert.That(restY, Is.EqualTo(3f));
        }

        [Test]
        public void UnknownClearanceOrDisabledSnapDoesNothing()
        {
            Assert.That(BuddahHandoffGroundSnap.TryResolveRestY(3f, -1f, 2f, 1.5f, out _), Is.False, "no colliders");
            Assert.That(BuddahHandoffGroundSnap.TryResolveRestY(3f, 1f, 2.5f, 0f, out _), Is.False, "disabled");
        }
    }
}
