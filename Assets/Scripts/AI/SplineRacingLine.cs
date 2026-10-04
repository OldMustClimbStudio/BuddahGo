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
            internal readonly float[] Denominators, TangentYaws;
            internal float[] Curvatures, LeftWalls, RightWalls;
            internal float CurvatureStep;
            internal Geometry(Vector3[] points)
            {
                Points = (Vector3[])points.Clone();
                Deltas = new Vector3[points.Length]; Tangents = new Vector3[points.Length];
                Denominators = new float[points.Length]; TangentYaws = new float[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 delta = points[(i + 1) % points.Length] - points[i]; delta.y = 0f;
                    Deltas[i] = delta; Tangents[i] = delta.normalized;
                    TangentYaws[i] = Mathf.Atan2(delta.x, delta.z);
                    Denominators[i] = Mathf.Max(.0001f, delta.sqrMagnitude);
                }
            }
        }
        // Weak keys release geometry with its track; pace and planner state stay per driver.
        private sealed class CaptureCache { internal Geometry Geometry; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TrackSplineRef, CaptureCache> Captures
            = new System.Runtime.CompilerServices.ConditionalWeakTable<TrackSplineRef, CaptureCache>();
        public float Length { get; }
        // Wall distances per segment (metres from the line, +inf where open). Static scenery, so they live
        // with the shared geometry; the thrust-vector rollout uses them as a frictionless corridor.
        public bool HasWalls => _geometry.LeftWalls != null;
        public float LeftWall(int segment) => _geometry.LeftWalls != null ? _geometry.LeftWalls[Wrap(segment)] : float.PositiveInfinity;
        public float RightWall(int segment) => _geometry.RightWalls != null ? _geometry.RightWalls[Wrap(segment)] : float.PositiveInfinity;
        private int Wrap(int segment) => (segment % _geometry.Points.Length + _geometry.Points.Length) % _geometry.Points.Length;
        public void SetWalls(float[] left, float[] right)
        {
            if (left == null || right == null || left.Length != _geometry.Points.Length || right.Length != _geometry.Points.Length)
                throw new ArgumentException("Wall arrays must match the sample count.");
            _geometry.LeftWalls = (float[])left.Clone(); _geometry.RightWalls = (float[])right.Clone();
        }
        private readonly float _step;
        private float[] _pace, _windowPace;
        private float[] _curvatures;
        private float _paceAcceleration, _paceTarget, _paceFactor, _paceBrakeAcceleration;
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
            if (cache.Geometry.LeftWalls == null && Application.isPlaying)
            {
                // Server-side, once per track: sideways rays to the wall colliders (ignores triggers).
                TrackWallProbe.Measure(cache.Geometry.Points, out float[] left, out float[] right);
                cache.Geometry.LeftWalls = left; cache.Geometry.RightWalls = right;
            }
            return new SplineRacingLine(cache.Geometry, track.TrackLength);
        }
        // A planning preference derived from route curvature and current available force, with backward
        // braking propagation so tight bends are anticipated without extending the search horizon.
        // The vehicle has one fixed-magnitude thrust, so braking and cornering share a friction circle of
        // radius acceleration*factor: the longitudinal deceleration available while the next sample needs
        // lateral v^2*k is sqrt(circle^2 - lateral^2), capped by the planned braking deceleration (which
        // leaves margin for the heading swing). Constant braking would promise deceleration inside bends
        // that the thrust vector cannot deliver.
        // Curvature: the angle between the two span-long chords equals the tangent rotation between their
        // midpoints, which are one span apart, so the angle is divided by the mean chord length. A circle
        // fixture (radius 150 m) reproduces 1/150 exactly; dividing by both chords would halve it.
        public void PreparePace(float acceleration, float target, float factor, float brakingAcceleration)
        {
            if (_pace != null && _paceAcceleration == acceleration && _paceTarget == target && _paceFactor == factor
                && _paceBrakeAcceleration == brakingAcceleration) return;
            _paceAcceleration = acceleration; _paceTarget = target; _paceFactor = factor; _paceBrakeAcceleration = brakingAcceleration;
            _pace = new float[_geometry.Points.Length];
            // Unsigned curvature is recomputed here rather than taken from PrepareCurvatures: Mono evaluates this
            // one-expression float chain at higher intermediate precision, and the pace tests pin these values.
            var curvatures = new float[_geometry.Points.Length];
            int span = Mathf.Clamp(Mathf.RoundToInt(12f / _step), 1, Mathf.Max(1, (_geometry.Points.Length - 1) / 2));
            for (int i = 0; i < _geometry.Points.Length; i++)
            {
                Vector3 before = _geometry.Points[i] - _geometry.Points[(i - span + _geometry.Points.Length) % _geometry.Points.Length];
                Vector3 after = _geometry.Points[(i + span) % _geometry.Points.Length] - _geometry.Points[i];
                float curvature = Vector3.Angle(before, after) * Mathf.Deg2Rad / Mathf.Max(1f, (before.magnitude + after.magnitude) * .5f);
                _pace[i] = Mathf.Min(target, Mathf.Sqrt(Mathf.Max(1f, acceleration) * factor / Mathf.Max(.0001f, curvature)));
                curvatures[i] = curvature;
            }
            float circle = Mathf.Max(1f, acceleration) * factor;
            for (int k = _geometry.Points.Length * 2 - 1; k >= 0; k--)
            {
                int i = k % _geometry.Points.Length, n = (i + 1) % _geometry.Points.Length; float next = _pace[n];
                float lateral = next * next * curvatures[n];
                float longitudinal = Mathf.Min(brakingAcceleration, Mathf.Sqrt(Mathf.Max(0f, circle * circle - lateral * lateral)));
                _pace[i] = Mathf.Min(_pace[i], Mathf.Sqrt(next * next + 2f * longitudinal * _step));
            }
            // Fixed 80 m look-ahead minimum (top speed x 1 s) for rollout sampling: one pass here replaces a
            // window scan per rollout sample. Conservative at lower speeds (it looks further than needed).
            int window = Mathf.Clamp(Mathf.CeilToInt(80f / _step), 1, _pace.Length - 1);
            _windowPace = new float[_pace.Length];
            for (int i = 0; i < _pace.Length; i++)
            {
                float minimum = float.PositiveInfinity;
                for (int k = 0; k <= window; k++) minimum = Mathf.Min(minimum, _pace[(i + k) % _pace.Length]);
                _windowPace[i] = minimum;
            }
        }
        public float WindowPace(int segment) => _windowPace != null ? _windowPace[Wrap(segment)] : float.PositiveInfinity;
        private void PrepareCurvatures()
        {
            if (_curvatures != null) return;
            if (_geometry.Curvatures != null && _geometry.CurvatureStep == _step)
            { _curvatures = _geometry.Curvatures; return; }
            int count = _geometry.Points.Length;
            _curvatures = new float[count];
            int span = Mathf.Clamp(Mathf.RoundToInt(12f / _step), 1, Mathf.Max(1, (count - 1) / 2));
            for (int i = 0; i < count; i++)
            {
                Vector3 before = _geometry.Points[i] - _geometry.Points[(i - span + count) % count];
                Vector3 after = _geometry.Points[(i + span) % count] - _geometry.Points[i];
                float angle = Vector3.SignedAngle(before, after, Vector3.up) * Mathf.Deg2Rad;
                // Signed curvature with the same chord-midpoint spacing as PreparePace.
                _curvatures[i] = angle / Mathf.Max(1f, (before.magnitude + after.magnitude) * .5f);
            }
            _geometry.Curvatures = _curvatures; _geometry.CurvatureStep = _step;
        }
        public float CurvatureAtDistance(float distance)
        {
            PrepareCurvatures();
            float index = Mathf.Repeat(distance, Length) / _step;
            int i = Mathf.Min((int)index, _geometry.Points.Length - 1);
            return Mathf.Lerp(_curvatures[i], _curvatures[(i + 1) % _curvatures.Length], index - i);
        }
        public float TangentYaw(int segment) => _geometry.TangentYaws[Wrap(segment)];
        public LineProjection SampleDistance(float distance)
        {
            float wrapped = Mathf.Repeat(distance, Length), index = wrapped / _step;
            int i = Mathf.Min((int)index, _geometry.Points.Length - 1);
            return new LineProjection(_geometry.Points[i] + _geometry.Deltas[i] * (index - i),
                _geometry.Tangents[i], wrapped, 0f, i, _pace != null ? _pace[i] : float.PositiveInfinity);
        }
        // Pace is constant within each sampled segment. Include every intersected
        // segment, both endpoints, and wrap at the finish line; a full lap covers all samples.
        public float MinimumPace(float distance, float forwardDistance)
        {
            if (_pace == null) return float.PositiveInfinity;
            float index = Mathf.Repeat(distance, Length) / _step;
            int first = Mathf.Min((int)index, _pace.Length - 1);
            int count = forwardDistance >= Length ? _pace.Length
                : Mathf.Min(_pace.Length, Mathf.FloorToInt(index + Mathf.Max(0f, forwardDistance) / _step) - first + 1);
            float minimum = float.PositiveInfinity;
            for (int k = 0; k < count; k++) minimum = Mathf.Min(minimum, _pace[(first + k) % _pace.Length]);
            return minimum;
        }
        // Search all +/-40 candidates, preferring the local distance minimum nearest
        // the previous segment. This preserves its continuous projection branch instead
        // of jumping to a marginally nearer parallel branch. No new distance threshold.
        public LineProjection ProjectContinuous(Vector3 position, int nearSegment, int searchSegments = 40)
        {
            if (nearSegment < 0) return Project(position, nearSegment, searchSegments);
            float bestDistance = float.PositiveInfinity;
            int bestOffset = int.MaxValue;
            LineProjection best = default;
            for (int offset = -searchSegments; offset <= searchSegments; offset++)
            {
                int segment = (nearSegment + offset) % _geometry.Points.Length;
                if (segment < 0) segment += _geometry.Points.Length;
                var candidate = Project(position, segment, 0);
                Vector3 delta = position - candidate.Point; delta.y = 0f;
                float squared = delta.sqrMagnitude;
                bool minimum = true;
                if (offset > -searchSegments)
                {
                    var adjacent = Project(position, (segment - 1 + _geometry.Points.Length) % _geometry.Points.Length, 0);
                    delta = position - adjacent.Point; delta.y = 0f;
                    minimum &= squared <= delta.sqrMagnitude;
                }
                if (offset < searchSegments)
                {
                    var adjacent = Project(position, (segment + 1) % _geometry.Points.Length, 0);
                    delta = position - adjacent.Point; delta.y = 0f;
                    minimum &= squared <= delta.sqrMagnitude;
                }
                if (minimum && (Mathf.Abs(offset) < bestOffset || (Mathf.Abs(offset) == bestOffset && squared < bestDistance)))
                { best = candidate; bestOffset = Mathf.Abs(offset); bestDistance = squared; }
            }
            return best;
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
