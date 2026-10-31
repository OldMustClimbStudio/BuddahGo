using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BuddahGo.Tests
{
    public class AimGroundIndicatorTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void HiddenWithoutAnAimGroundPoint()
        {
            bool shown = AimGroundIndicatorMath.TryGetDrawPosition(false, new Vector3(3f, 0f, 4f), false, true, 0.05f, out _);
            Assert.That(shown, Is.False);
        }

        [Test]
        public void ShownAtTheAimPointLiftedByTheHeightOffset()
        {
            var point = new Vector3(3f, 1.2f, -4f);
            bool shown = AimGroundIndicatorMath.TryGetDrawPosition(true, point, false, true, 0.05f, out Vector3 draw);

            Assert.That(shown, Is.True);
            Assert.That(Vector3.Distance(draw, point + new Vector3(0f, 0.05f, 0f)), Is.LessThan(Tolerance));
        }

        [Test]
        public void InsideTheDeadRadiusHidesOnlyWhenAsked()
        {
            Assert.That(AimGroundIndicatorMath.TryGetDrawPosition(true, Vector3.zero, true, true, 0.05f, out _), Is.False);
            Assert.That(AimGroundIndicatorMath.TryGetDrawPosition(true, Vector3.zero, true, false, 0.05f, out _), Is.True);
        }

        [Test]
        public void DeadRadiusIsHorizontalDistanceInclusive()
        {
            var body = new Vector3(2f, 0f, 2f);
            Assert.That(HandAimMath.IsInsideDeadRadius(body + new Vector3(1.5f, 0f, 0f), body, 1.5f), Is.True);
            Assert.That(HandAimMath.IsInsideDeadRadius(body + new Vector3(1.51f, 0f, 0f), body, 1.5f), Is.False);
            // Height does not count: a point 10 m above the body is still at the body.
            Assert.That(HandAimMath.IsInsideDeadRadius(body + new Vector3(0.3f, 10f, 0f), body, 1.5f), Is.True);
            // Without a dead radius only the body point itself is inside.
            Assert.That(HandAimMath.IsInsideDeadRadius(body, body, 0f), Is.True);
            Assert.That(HandAimMath.IsInsideDeadRadius(body + new Vector3(0.1f, 0f, 0f), body, 0f), Is.False);
            Assert.That(HandAimMath.IsInsideDeadRadius(body + new Vector3(0.1f, 0f, 0f), body, -1f), Is.False);
        }

        [Test]
        public void IndicatorDeadRadiusAgreesWithTheAimItself()
        {
            // The indicator hides exactly where mouse aim holds its target.
            var body = new Vector3(1f, 0f, -3f);
            const float radius = 1.5f;
            // Steps of 0.25 m from 0.1 m never land on the 1.5 m boundary, where the ray hit has float noise.
            for (float d = 0.1f; d <= 3f; d += 0.25f)
            {
                Vector3 cursor = body + new Vector3(d * 0.6f, 0f, d * 0.8f);
                var origin = cursor + new Vector3(3f, 20f, -12f);
                var ray = new Ray(origin, (cursor - origin).normalized);
                HandAimSampleKind kind = HandAimMath.TryGetTargetYawOffset(ray, 0f, body, Vector3.forward, radius, out _);

                bool aimHolds = kind == HandAimSampleKind.InsideDeadRadius;
                Assert.That(HandAimMath.IsInsideDeadRadius(cursor, body, radius), Is.EqualTo(aimHolds), $"distance {d}");
            }
        }

        [Test]
        public void MeshIsAFlatUpFacingRingPlusCross()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            const float radius = 0.5f, width = 0.08f, cross = 0.2f;
            const int segments = 48;
            AimGroundIndicatorMath.BuildRingAndCross(radius, width, cross, segments, vertices, triangles);

            Assert.That(vertices.Count, Is.EqualTo(segments * 2 + 8));
            Assert.That(triangles.Count, Is.EqualTo(segments * 6 + 12));
            foreach (Vector3 v in vertices)
                Assert.That(v.y, Is.EqualTo(0f));

            for (int i = 0; i < segments * 2; i++)
            {
                float r = new Vector2(vertices[i].x, vertices[i].z).magnitude;
                Assert.That(r, Is.EqualTo(i % 2 == 0 ? radius - width / 2f : radius + width / 2f).Within(Tolerance));
            }

            for (int i = segments * 2; i < vertices.Count; i++)
            {
                Assert.That(Mathf.Max(Mathf.Abs(vertices[i].x), Mathf.Abs(vertices[i].z)), Is.EqualTo(cross).Within(Tolerance));
                Assert.That(Mathf.Min(Mathf.Abs(vertices[i].x), Mathf.Abs(vertices[i].z)), Is.EqualTo(width / 2f).Within(Tolerance));
            }

            for (int t = 0; t < triangles.Count; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                Assert.That(Vector3.Cross(b - a, c - a).y, Is.GreaterThan(0f), $"triangle {t / 3} faces up");
            }
        }

        [Test]
        public void ZeroCrossLengthGivesTheRingOnlyAndSegmentsAreClamped()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AimGroundIndicatorMath.BuildRingAndCross(0.5f, 0.08f, 0f, 1, vertices, triangles);

            Assert.That(vertices.Count, Is.EqualTo(3 * 2));
            Assert.That(triangles.Count, Is.EqualTo(3 * 6));
        }
    }
}
