using NUnit.Framework;
using NewBuddah.PredictionV2.Core;

namespace BuddahGo.Tests
{
    public class HandoffGroundSnapTests
    {
        // TryResolveRestY(rootY, clearanceBelowRoot, groundY, maxSnapDistance): whether the body snaps and where it rests.
        // A null rest height is not checked.
        static readonly (string name, float rootY, float clearance, float groundY, float maxSnap, bool snaps, float? restY, float within)[] Cases =
        {
            // Probe evidence: root 3.300 with 1.236 clearance rested at 3.916 after PhysX pushed it out.
            ("penetrating body is lifted onto its rest height", 3.300f, 1.236f, 2.680f, 1.5f, true, 3.916f, 1e-4f),
            ("hovering body is lowered onto the ground", 5f, 1f, 3f, 1.5f, true, 4f, 1e-5f),
            ("moves beyond the limit are left to physics", 10f, 1f, 2f, 1.5f, false, 10f, 0f),
            ("already resting body is not moved", 3f, 1f, 2f, 1.5f, false, 3f, 0f),
            ("unknown clearance: no colliders", 3f, -1f, 2f, 1.5f, false, null, 0f),
            ("disabled snap", 3f, 1f, 2.5f, 0f, false, null, 0f),
        };

        [Test]
        public void RestHeightSnapsOnlyWithinTheLimit()
        {
            EveryCase.Check(Cases, c =>
            {
                Assert.That(BuddahHandoffGroundSnap.TryResolveRestY(c.rootY, c.clearance, c.groundY, c.maxSnap, out float restY), Is.EqualTo(c.snaps));
                if (c.restY.HasValue)
                    Assert.That(restY, Is.EqualTo(c.restY.Value).Within(c.within));
            }, c => c.name);
        }
    }
}
