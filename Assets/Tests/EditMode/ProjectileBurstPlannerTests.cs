using NUnit.Framework;
using UnityEngine;

namespace BuddahGo.Tests
{
    public class ProjectileBurstPlannerTests
    {
        [Test]
        public void BurstGeometry()
        {
            EveryCase.Parts(
                ("the burst is centred and spaced along the requested side direction", () =>
                    EveryCase.Check(new[] { 1, 3, 4, 5 }, count =>
                    {
                        var anchor = new Vector3(10, 20, 30);
                        var side = new Vector3(0, 0, -1);
                        var sum = Vector3.zero;
                        Vector3 previous = default;
                        for (int i = 0; i < count; i++)
                        {
                            var position = ProjectileBurstPlanner.GetSpawnPosition(anchor, side, i, count, 3, 2);
                            Assert.That(position.x, Is.EqualTo(anchor.x));
                            Assert.That(position.y, Is.EqualTo(anchor.y));
                            if (i > 0) Assert.That(position - previous, Is.EqualTo(side * 6));
                            sum += position; previous = position;
                        }
                        Assert.That(sum / count, Is.EqualTo(anchor));
                    }, count => "count=" + count)),
                ("the collider size uses the magnitude and a minimum on each axis", () =>
                {
                    Assert.That(ProjectileBurstPlanner.SanitizeColliderSize(new Vector3(-2, 0, .01f)),
                        Is.EqualTo(new Vector3(2, .05f, .05f)));
                    Assert.That(ProjectileBurstPlanner.SanitizeColliderSize(new Vector3(60, 90, 20)),
                        Is.EqualTo(new Vector3(60, 90, 20)));
                }));
        }

        [Test]
        public void PushAttackTimingFallbacks()
        {
            EveryCase.Parts(
                ("the cooldown uses a positive charged value or the non-negative normal fallback", () =>
                {
                    var cases = new[]
                    {
                        (charged: false, special: 9f, normal: .25f, expected: .25f),
                        (charged: true, special: 9f, normal: .25f, expected: 9f),
                        (charged: true, special: 0f, normal: .25f, expected: .25f),
                        (charged: true, special: -1f, normal: -2f, expected: 0f),
                    };
                    EveryCase.Check(cases, c =>
                        Assert.That(PushAttackTiming.GetCooldown(c.charged, c.special, c.normal), Is.EqualTo(c.expected)));
                }),
                ("only the active charged mode adds a non-negative action delay", () =>
                    EveryCase.Check(new[] { (charged: false, delay: 2f, expected: 0f), (charged: true, delay: 2f, expected: 2f), (charged: true, delay: -1f, expected: 0f) }, c =>
                        Assert.That(PushAttackTiming.GetActionDelay(c.charged, c.delay), Is.EqualTo(c.expected)))));
        }
    }
}
