// Frozen a9c9a17 implementation: independent cost-optimization equivalence oracle.
using System;
using UnityEngine;
namespace BuddahGo.AI {
    public sealed class LegacySplineRacingLine : IRacingLine
    {
        public Vector3[] Points { get; }
        public float Length { get; }
        private readonly float _step;
        private float[] _pace;
        private float _paceAcceleration, _paceTarget, _paceFactor;
        public LegacySplineRacingLine(Vector3[] points, float length)
        {
            if (points == null || points.Length < 3 || length <= 0f) throw new ArgumentException("A closed racing line needs at least three samples.");
            Points = points; Length = length; _step = length / points.Length;
        }
        public static LegacySplineRacingLine Capture(TrackSplineRef track, float spacing = 2f)
        {
            int count = Mathf.Max(16, Mathf.CeilToInt(track.TrackLength / spacing));
            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
                track.TryEvaluateWorldPoseAtProgress01((float)i / count, out points[i], out _);
            return new LegacySplineRacingLine(points, track.TrackLength);
        }
        // A planning preference derived from route curvature and current available force.
        // Backward braking propagation anticipates tight bends without extending the search horizon.
        public void PreparePace(float acceleration, float target, float factor)
        {
            if (_pace != null && _paceAcceleration == acceleration && _paceTarget == target && _paceFactor == factor) return;
            _paceAcceleration = acceleration; _paceTarget = target; _paceFactor = factor;
            _pace = new float[Points.Length];
            int span = Mathf.Clamp(Mathf.RoundToInt(12f / _step), 1, Mathf.Max(1, (Points.Length - 1) / 2));
            for (int i = 0; i < Points.Length; i++)
            {
                Vector3 before = Points[i] - Points[(i - span + Points.Length) % Points.Length];
                Vector3 after = Points[(i + span) % Points.Length] - Points[i];
                float curvature = Vector3.Angle(before, after) * Mathf.Deg2Rad / Mathf.Max(1f, (before.magnitude + after.magnitude) * .5f);
                _pace[i] = Mathf.Min(target, Mathf.Sqrt(Mathf.Max(1f, acceleration) * factor / Mathf.Max(.0001f, curvature)));
            }
            for (int k = Points.Length * 2 - 1; k >= 0; k--)
            {
                int i = k % Points.Length; float next = _pace[(i + 1) % Points.Length];
                _pace[i] = Mathf.Min(_pace[i], Mathf.Sqrt(next * next + 2f * acceleration * .5f * _step));
            }
        }
        public LineProjection Project(Vector3 position, int nearSegment, int searchSegments = 12)
        {
            float best = float.PositiveInfinity, distance = 0f;
            int segment = 0;
            Vector3 point = default, tangent = default;
            int start = nearSegment < 0 ? 0 : nearSegment - searchSegments;
            int count = nearSegment < 0 ? Points.Length : 2 * searchSegments + 1;
            for (int k = start; k < start + count; k++)
            {
                int i = (k % Points.Length + Points.Length) % Points.Length;
                Vector3 a = Points[i], delta = Points[(i + 1) % Points.Length] - a;
                delta.y = 0f;
                Vector3 toPosition = position - a; toPosition.y = 0f;
                float t = Mathf.Clamp01(Vector3.Dot(toPosition, delta) / Mathf.Max(0.0001f, delta.sqrMagnitude));
                float squared = (toPosition - delta * t).sqrMagnitude;
                if (squared >= best) continue;
                best = squared; segment = i; point = a + delta * t; tangent = delta.normalized;
                distance = (i + t) * _step;
            }
            float lateral = Vector3.Dot(position - point, Vector3.Cross(Vector3.up, tangent));
            return new LineProjection(point, tangent, distance, lateral, segment,
                _pace != null ? _pace[segment] : float.PositiveInfinity);
        }
    }
}
