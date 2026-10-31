using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure logic of the mouse-aim ground indicator (<see cref="AimGroundIndicator"/>): when it shows, where it is drawn,
/// and its placeholder ring-and-cross mesh. Placeholder until the hand-direction guide is designed (占位，等手部朝向指引设计).
/// </summary>
internal static class AimGroundIndicatorMath
{
    /// <summary>
    /// Where to draw the marker this frame. Hidden (false) without an aim ground point, or inside the mouse-aim dead
    /// radius when <paramref name="hideInsideDeadRadius"/> is set (the hands hold their target there). The marker is
    /// lifted <paramref name="heightOffset"/> metres above the aim plane so it does not z-fight a ground at that height.
    /// </summary>
    internal static bool TryGetDrawPosition(bool hasAimGroundPoint, Vector3 aimGroundPoint, bool insideDeadRadius,
        bool hideInsideDeadRadius, float heightOffset, out Vector3 drawPosition)
    {
        drawPosition = aimGroundPoint;
        if (!hasAimGroundPoint)
            return false;
        if (insideDeadRadius && hideInsideDeadRadius)
            return false;
        drawPosition = aimGroundPoint + Vector3.up * heightOffset;
        return true;
    }

    /// <summary>
    /// A flat ring (centre line at <paramref name="ringRadius"/>, <paramref name="lineWidth"/> wide) plus a cross of two
    /// bars reaching <paramref name="crossHalfLength"/> from the centre, in the local XZ plane (y = 0, facing up).
    /// No cross when crossHalfLength is 0. Double-sided rendering is left to the material.
    /// </summary>
    internal static void BuildRingAndCross(float ringRadius, float lineWidth, float crossHalfLength, int segments,
        List<Vector3> vertices, List<int> triangles)
    {
        vertices.Clear();
        triangles.Clear();

        int safeSegments = Mathf.Max(3, segments);
        float halfWidth = Mathf.Max(0f, lineWidth) * 0.5f;
        float inner = Mathf.Max(0f, ringRadius - halfWidth);
        float outer = Mathf.Max(inner, ringRadius + halfWidth);

        for (int i = 0; i < safeSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / safeSegments;
            var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices.Add(dir * inner);
            vertices.Add(dir * outer);
        }

        for (int i = 0; i < safeSegments; i++)
        {
            int a = i * 2;
            int b = ((i + 1) % safeSegments) * 2;
            AddQuad(triangles, a, b, b + 1, a + 1);
        }

        if (crossHalfLength <= 0f || halfWidth <= 0f)
            return;

        AddBar(vertices, triangles, new Vector3(crossHalfLength, 0f, halfWidth));
        AddBar(vertices, triangles, new Vector3(halfWidth, 0f, crossHalfLength));
    }

    private static void AddBar(List<Vector3> vertices, List<int> triangles, Vector3 halfExtents)
    {
        int start = vertices.Count;
        vertices.Add(new Vector3(-halfExtents.x, 0f, -halfExtents.z));
        vertices.Add(new Vector3(-halfExtents.x, 0f, halfExtents.z));
        vertices.Add(new Vector3(halfExtents.x, 0f, halfExtents.z));
        vertices.Add(new Vector3(halfExtents.x, 0f, -halfExtents.z));
        AddQuad(triangles, start, start + 1, start + 2, start + 3);
    }

    // Clockwise seen from above (+Y), Unity's front face for an upward-facing quad: Cross(b - a, c - a).y > 0.
    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(d);
    }
}
