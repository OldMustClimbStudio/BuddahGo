using NUnit.Framework;
using UnityEngine;

namespace BuddahGo.Tests
{
    public class ProjectileBurstPlannerTests
    {
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void BurstIsCenteredAndSpacingScalesAlongRequestedSideDirection(int count)
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
        }

        [Test]
        public void ColliderSizeUsesMagnitudeAndMinimumOnEachAxis()
        {
            Assert.That(ProjectileBurstPlanner.SanitizeColliderSize(new Vector3(-2, 0, .01f)),
                Is.EqualTo(new Vector3(2, .05f, .05f)));
            Assert.That(ProjectileBurstPlanner.SanitizeColliderSize(new Vector3(60, 90, 20)),
                Is.EqualTo(new Vector3(60, 90, 20)));
        }

        [TestCase(false, 9f, .25f, .25f)]
        [TestCase(true, 9f, .25f, 9f)]
        [TestCase(true, 0f, .25f, .25f)]
        [TestCase(true, -1f, -2f, 0f)]
        public void CooldownUsesPositiveChargedValueOrNonnegativeNormalFallback(bool charged, float special, float normal, float expected)
        {
            Assert.That(PushAttackTiming.GetCooldown(charged, special, normal), Is.EqualTo(expected));
        }

        [TestCase(false, 2f, 0f)]
        [TestCase(true, 2f, 2f)]
        [TestCase(true, -1f, 0f)]
        public void OnlyActiveChargedModeAddsNonnegativeActionDelay(bool charged, float delay, float expected)
        {
            Assert.That(PushAttackTiming.GetActionDelay(charged, delay), Is.EqualTo(expected));
        }
    }
}
