using NUnit.Framework;
using UnityEngine;

namespace BuddahGo.Tests
{
    public class HandAimMathTests
    {
        private const float Tolerance = 0.01f;

        private static Ray RayFromAboveTo(Vector3 groundPoint)
        {
            Vector3 origin = groundPoint + new Vector3(3f, 20f, -12f);
            return new Ray(origin, (groundPoint - origin).normalized);
        }

        [Test]
        public void CursorStraightAheadGivesZeroOffset()
        {
            var kind = HandAimMath.TryGetTargetYawOffset(RayFromAboveTo(new Vector3(0f, 0f, 10f)), 0f,
                Vector3.zero, Vector3.forward, 1.5f, out float yaw);

            Assert.That(kind, Is.EqualTo(HandAimSampleKind.Aim));
            Assert.That(yaw, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void CursorToTheRightIsPositiveAndRebuildsTheServerDirection()
        {
            var body = new Vector3(5f, 0f, 5f);
            var cursor = body + new Vector3(8f, 0f, 0f);
            var kind = HandAimMath.TryGetTargetYawOffset(RayFromAboveTo(cursor), 0f, body, Vector3.forward, 1.5f, out float yaw);

            Assert.That(kind, Is.EqualTo(HandAimSampleKind.Aim));
            Assert.That(yaw, Is.EqualTo(90f).Within(Tolerance));
            // The server rebuilds the push direction as AngleAxis(yaw, up) * body forward.
            Vector3 rebuilt = Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward;
            Assert.That(Vector3.Distance(rebuilt, Vector3.right), Is.LessThan(Tolerance));
        }

        [Test]
        public void OffsetIsRelativeToTheFlattenedBodyForward()
        {
            // Body faces world +X and is pitched a little; the cursor is at world +Z of the body.
            Vector3 bodyForward = new Vector3(1f, -0.2f, 0f).normalized;
            var kind = HandAimMath.TryGetTargetYawOffset(RayFromAboveTo(new Vector3(0f, 0f, 6f)), 0f,
                Vector3.zero, bodyForward, 1.5f, out float yaw);

            Assert.That(kind, Is.EqualTo(HandAimSampleKind.Aim));
            Assert.That(yaw, Is.EqualTo(-90f).Within(Tolerance));
        }

        [Test]
        public void RayIsProjectedOntoThePlaneAtTheGivenHeight()
        {
            // Straight down through (4, *, 0): only the plane height decides where it lands, the direction stays +X.
            var ray = new Ray(new Vector3(4f, 30f, 0f), Vector3.down);
            var kind = HandAimMath.TryGetTargetYawOffset(ray, 2f, new Vector3(0f, 2f, 0f), Vector3.forward, 1.5f, out float yaw);

            Assert.That(kind, Is.EqualTo(HandAimSampleKind.Aim));
            Assert.That(yaw, Is.EqualTo(90f).Within(Tolerance));

            // A slanted ray lands at (0, h, -h) on the plane y = h: in front of a body at z = -5 for h = 0,
            // behind it for h = 9. The body's own height does not matter, only the plane height does.
            var slanted = new Ray(new Vector3(0f, 10f, -10f), new Vector3(0f, -1f, 1f).normalized);
            var body2 = new Vector3(0f, 0f, -5f);
            HandAimMath.TryGetTargetYawOffset(slanted, 0f, body2, Vector3.forward, 1.5f, out float lowYaw);
            HandAimMath.TryGetTargetYawOffset(slanted, 9f, body2, Vector3.forward, 1.5f, out float highYaw);
            Assert.That(lowYaw, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(Mathf.Abs(highYaw), Is.EqualTo(180f).Within(Tolerance));
        }

        [Test]
        public void CursorInsideTheDeadRadiusAsksToHold()
        {
            var body = new Vector3(1f, 0f, 1f);
            var kind = HandAimMath.TryGetTargetYawOffset(RayFromAboveTo(body + new Vector3(0.5f, 0f, 0.5f)), 0f,
                body, Vector3.forward, 1.5f, out _);

            Assert.That(kind, Is.EqualTo(HandAimSampleKind.InsideDeadRadius));
        }

        [Test]
        public void RayThatNeverReachesThePlaneIsAMiss()
        {
            Assert.That(HandAimMath.TryGetTargetYawOffset(new Ray(new Vector3(0f, 10f, 0f), Vector3.up), 0f,
                Vector3.zero, Vector3.forward, 1.5f, out _), Is.EqualTo(HandAimSampleKind.Miss));
            Assert.That(HandAimMath.TryGetTargetYawOffset(new Ray(new Vector3(0f, 10f, 0f), Vector3.forward), 0f,
                Vector3.zero, Vector3.forward, 1.5f, out _), Is.EqualTo(HandAimSampleKind.Miss));
        }

        [Test]
        public void VerticalBodyForwardIsAMiss()
        {
            Assert.That(HandAimMath.TryGetTargetYawOffset(RayFromAboveTo(new Vector3(0f, 0f, 10f)), 0f,
                Vector3.zero, Vector3.up, 1.5f, out _), Is.EqualTo(HandAimSampleKind.Miss));
        }

        [Test]
        public void SmoothingWithoutTimeConstantSnapsToTarget()
        {
            Assert.That(HandAimMath.SmoothAngle(10f, 50f, 0.016f, 0f), Is.EqualTo(50f).Within(Tolerance));
        }

        [Test]
        public void SmoothingCoversOneMinusExpOfTheGapPerTimeConstant()
        {
            float result = HandAimMath.SmoothAngle(0f, 100f, 0.06f, 0.06f);
            Assert.That(result, Is.EqualTo(100f * (1f - Mathf.Exp(-1f))).Within(Tolerance));
        }

        [Test]
        public void SmoothingWithoutElapsedTimeKeepsCurrent()
        {
            Assert.That(HandAimMath.SmoothAngle(30f, 100f, 0f, 0.06f), Is.EqualTo(30f).Within(Tolerance));
        }

        [Test]
        public void SmoothingTakesTheShortWayAndStaysContinuous()
        {
            float acrossWrap = HandAimMath.SmoothAngle(170f, -170f, 0.06f, 0.06f);
            Assert.That(acrossWrap, Is.GreaterThan(170f).And.LessThan(190f));

            // Unwrapped current (two turns plus 10°) moves toward the target without jumping back by 720°.
            float unwrapped = HandAimMath.SmoothAngle(730f, 0f, 0.06f, 0.06f);
            Assert.That(unwrapped, Is.LessThan(730f).And.GreaterThan(720f));
        }

        [Test]
        public void StepIsTheExactGapWhenWithinTheRateLimit()
        {
            Assert.That(HandAimMath.RateLimitedStep(10f, 12f, 180f, 0.02f), Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void StepIsClampedToRateTimesDeltaInBothDirections()
        {
            Assert.That(HandAimMath.RateLimitedStep(0f, 90f, 180f, 0.02f), Is.EqualTo(3.6f).Within(Tolerance));
            Assert.That(HandAimMath.RateLimitedStep(0f, -90f, 180f, 0.02f), Is.EqualTo(-3.6f).Within(Tolerance));
        }

        [Test]
        public void StepTakesTheShortWayFromAnUnwrappedCurrent()
        {
            // 730° is 10° past two full turns: reaching 0° is a −10° step, not −730°.
            Assert.That(HandAimMath.RateLimitedStep(730f, 0f, 1000f, 1f), Is.EqualTo(-10f).Within(Tolerance));
            Assert.That(HandAimMath.RateLimitedStep(170f, -170f, 1000f, 1f), Is.EqualTo(20f).Within(Tolerance));
        }

        [Test]
        public void StepIsZeroWithoutTimeOrRate()
        {
            Assert.That(HandAimMath.RateLimitedStep(0f, 90f, 180f, 0f), Is.EqualTo(0f));
            Assert.That(HandAimMath.RateLimitedStep(0f, 90f, 0f, 0.02f), Is.EqualTo(0f));
            Assert.That(HandAimMath.RateLimitedStep(0f, 90f, -180f, 0.02f), Is.EqualTo(0f));
        }

        [Test]
        public void SmoothedAndRateLimitedAimNeverExceedsTheCapAndConverges()
        {
            const float dt = 1f / 60f, cap = 180f, tau = 0.06f, target = 170f;
            float smoothed = 0f, yaw = 0f;
            for (int frame = 0; frame < 90; frame++)
            {
                smoothed = HandAimMath.SmoothAngle(smoothed, target, dt, tau);
                float step = HandAimMath.RateLimitedStep(yaw, smoothed, cap, dt);
                Assert.That(Mathf.Abs(step), Is.LessThanOrEqualTo(cap * dt + 1e-4f));
                yaw += step;
            }

            Assert.That(yaw, Is.EqualTo(target).Within(0.5f));
        }
    }
}
