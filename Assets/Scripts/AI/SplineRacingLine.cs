using System;
using UnityEngine;

namespace BuddahGo.AI
{
    public readonly struct LineProjection
    {
        public readonly Vector3 Point, Tangent;
        public readonly float Distance, Lateral;
        public readonly float Pace;
        public readonly int Segment;
        public LineProjection(Vector3 point, Vector3 tangent, float distance, float lateral, int segment, float pace = float.PositiveInfinity)
        { Point = point; Tangent = tangent; Distance = distance; Lateral = lateral; Segment = segment; Pace = pace; }
    }

    public interface IRacingLine
    {
        float Length { get; }
        LineProjection Project(Vector3 position, int nearSegment, int searchSegments = 12);
    }

    public sealed class SplineRacingLine : IRacingLine
    {
        // A snapshot: callers cannot invalidate the cached geometry by mutating samples.
        public Vector3[] Points => (Vector3[])_geometry.Points.Clone();
        private readonly Geometry _geometry;
        private sealed class Geometry
        {
            internal readonly Vector3[] Points, Deltas, Tangents;
            internal readonly float[] Denominators;
            internal Geometry(Vector3[] points)
            {
                Points = (Vector3[])points.Clone();
                Deltas = new Vector3[points.Length]; Tangents = new Vector3[points.Length];
                Denominators = new float[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 delta = points[(i + 1) % points.Length] - points[i]; delta.y = 0f;
                    Deltas[i] = delta; Tangents[i] = delta.normalized;
                    Denominators[i] = Mathf.Max(.0001f, delta.sqrMagnitude);
                }
            }
        }
        // Weak keys release geometry with its track; pace and planner state stay per driver.
        private sealed class CaptureCache { internal Geometry Geometry; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TrackSplineRef, CaptureCache> Captures
            = new System.Runtime.CompilerServices.ConditionalWeakTable<TrackSplineRef, CaptureCache>();
        public float Length { get; }
        private readonly float _step;
        private float[] _pace;
        private float _paceAcceleration, _paceTarget, _paceFactor;
        public SplineRacingLine(Vector3[] points, float length)
        {
            if (points == null || points.Length < 3 || length <= 0f) throw new ArgumentException("A closed racing line needs at least three samples.");
            _geometry = new Geometry(points); Length = length; _step = length / points.Length;
        }
        private SplineRacingLine(Geometry geometry, float length)
        { _geometry = geometry; Length = length; _step = length / geometry.Points.Length; }
        public static SplineRacingLine Capture(TrackSplineRef track, float spacing = 2f)
        {
            int count = Mathf.Max(16, Mathf.CeilToInt(track.TrackLength / spacing));
            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
                track.TryEvaluateWorldPoseAtProgress01((float)i / count, out points[i], out _);
            var cache = Captures.GetValue(track, _ => new CaptureCache());
            bool same = cache.Geometry != null && cache.Geometry.Points.Length == count;
            // Re-sample before reusing: changed spline/transform/spacing must never use stale geometry.
            for (int i = 0; same && i < count; i++) same = cache.Geometry.Points[i].Equals(points[i]);
            if (!same) cache.Geometry = new Geometry(points);
            return new SplineRacingLine(cache.Geometry, track.TrackLength);
        }
        // A planning preference derived from route curvature and current available force.
        // Backward braking propagation anticipates tight bends without extending the search horizon.
        public void PreparePace(float acceleration, float target, float factor)
        {
            if (_pace != null && _paceAcceleration == acceleration && _paceTarget == target && _paceFactor == factor) return;
            _paceAcceleration = acceleration; _paceTarget = target; _paceFactor = factor;
            _pace = new float[_geometry.Points.Length];
            int span = Mathf.Clamp(Mathf.RoundToInt(12f / _step), 1, Mathf.Max(1, (_geometry.Points.Length - 1) / 2));
            for (int i = 0; i < _geometry.Points.Length; i++)
            {
                Vector3 before = _geometry.Points[i] - _geometry.Points[(i - span + _geometry.Points.Length) % _geometry.Points.Length];
                Vector3 after = _geometry.Points[(i + span) % _geometry.Points.Length] - _geometry.Points[i];
                float curvature = Vector3.Angle(before, after) * Mathf.Deg2Rad / Mathf.Max(1f, (before.magnitude + after.magnitude) * .5f);
                _pace[i] = Mathf.Min(target, Mathf.Sqrt(Mathf.Max(1f, acceleration) * factor / Mathf.Max(.0001f, curvature)));
            }
            for (int k = _geometry.Points.Length * 2 - 1; k >= 0; k--)
            {
                int i = k % _geometry.Points.Length; float next = _pace[(i + 1) % _geometry.Points.Length];
                _pace[i] = Mathf.Min(_pace[i], Mathf.Sqrt(next * next + 2f * acceleration * .5f * _step));
            }
        }
        public LineProjection Project(Vector3 position, int nearSegment, int searchSegments = 12)
        {
            float best = float.PositiveInfinity, distance = 0f, bestT = 0f;
            bool found = false;
            int segment = 0;
            Vector3 point = default, tangent = default;
            int start = nearSegment < 0 ? 0 : nearSegment - searchSegments;
            int count = nearSegment < 0 ? _geometry.Points.Length : 2 * searchSegments + 1;
            for (int k = start; k < start + count; k++)
            {
                int i = (k % _geometry.Points.Length + _geometry.Points.Length) % _geometry.Points.Length;
                Vector3 a = _geometry.Points[i], delta = _geometry.Deltas[i];
                Vector3 toPosition = position - a; toPosition.y = 0f;
                float t = Mathf.Clamp01(Vector3.Dot(toPosition, delta) / _geometry.Denominators[i]);
                float squared = (toPosition - delta * t).sqrMagnitude;
                if (squared >= best) continue;
                best = squared; segment = i; bestT = t; found = true;
            }
            // Intermediate winners only influence the next distance comparison.
            if (found)
            {
                point = _geometry.Points[segment] + _geometry.Deltas[segment] * bestT;
                tangent = _geometry.Tangents[segment]; distance = (segment + bestT) * _step;
            }
            float lateral = Vector3.Dot(position - point, Vector3.Cross(Vector3.up, tangent));
            return new LineProjection(point, tangent, distance, lateral, segment,
                _pace != null ? _pace[segment] : float.PositiveInfinity);
        }
    }
}
