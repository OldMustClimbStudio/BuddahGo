using System;
using UnityEngine;

namespace BuddahGo.AI
{
    public readonly struct LineProjection
    {
        public readonly Vector3 Point, Tangent;
        public readonly float Distance, Lateral;
        public readonly int Segment;
        public LineProjection(Vector3 point, Vector3 tangent, float distance, float lateral, int segment)
        { Point = point; Tangent = tangent; Distance = distance; Lateral = lateral; Segment = segment; }
    }

    public interface IRacingLine
    {
        float Length { get; }
        LineProjection Project(Vector3 position, int nearSegment, int searchSegments = 12);
    }

    public sealed class SplineRacingLine : IRacingLine
    {
        public Vector3[] Points { get; }
        public float Length { get; }
        private readonly float _step;
        public SplineRacingLine(Vector3[] points, float length)
        {
            if (points == null || points.Length < 3 || length <= 0f) throw new ArgumentException("A closed racing line needs at least three samples.");
            Points = points; Length = length; _step = length / points.Length;
        }
        public static SplineRacingLine Capture(TrackSplineRef track, float spacing = 2f)
        {
            int count = Mathf.Max(16, Mathf.CeilToInt(track.TrackLength / spacing));
            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
                track.TryEvaluateWorldPoseAtProgress01((float)i / count, out points[i], out _);
            return new SplineRacingLine(points, track.TrackLength);
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
            return new LineProjection(point, tangent, distance, lateral, segment);
        }
    }
}
