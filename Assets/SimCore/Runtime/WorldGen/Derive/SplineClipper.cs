using System.Collections.Generic;
using UnityEngine;

public static class SplineClipper
{
    public static WorldSpline ClipToRect(WorldSpline spline, Rect rect, float epsilon = 1e-4f)
    {
        var parts = ClipToRectParts(spline, rect, epsilon);
        return parts.Count > 0 ? parts[0] : null;
    }

    public static List<WorldSpline> ClipToRectParts(WorldSpline spline, Rect rect, float epsilon = 1e-4f)
    {
        var result = new List<WorldSpline>();
        if (spline?.points == null || spline.points.Count < 2) return result;

        var parts = ClipPolylineParts(spline.points, rect, epsilon);
        if (parts.Count == 0) return result;

        var clipped = IsClipped(spline, parts, rect, epsilon);
        for (var i = 0; i < parts.Count; i++)
        {
            var id = parts.Count == 1 ? spline.id : $"{spline.id}_clip{i}";
            result.Add(new WorldSpline
            {
                id = id,
                points = parts[i],
                closed = clipped ? false : spline.closed,
                baseWidth = spline.baseWidth,
                tags = spline.tags != null ? new List<string>(spline.tags) : new List<string>()
            });
        }

        return result;
    }

    private static List<List<Vector2>> ClipPolylineParts(List<Vector2> points, Rect rect, float epsilon)
    {
        var parts = new List<List<Vector2>>();
        List<Vector2> current = null;

        for (var i = 1; i < points.Count; i++)
        {
            var p0 = points[i - 1];
            var p1 = points[i];
            if (!TryClipSegment(p0, p1, rect, out var c0, out var c1, epsilon))
            {
                FinalizePart(ref current, parts, epsilon);
                continue;
            }

            c0 = ClampToRect(c0, rect);
            c1 = ClampToRect(c1, rect);

            if (current == null)
            {
                current = new List<Vector2> { c0, c1 };
                continue;
            }

            var tail = current[current.Count - 1];
            if (!Approximately(tail, c0, epsilon))
            {
                FinalizePart(ref current, parts, epsilon);
                current = new List<Vector2> { c0, c1 };
                continue;
            }

            AppendIfUnique(current, c1, epsilon);
        }

        FinalizePart(ref current, parts, epsilon);
        return parts;
    }

    private static bool IsClipped(WorldSpline original, List<List<Vector2>> parts, Rect rect, float epsilon)
    {
        if (parts.Count != 1) return true;
        var part = parts[0];
        if (original.points.Count != part.Count) return true;
        for (var i = 0; i < original.points.Count; i++)
        {
            if (!IsInside(original.points[i], rect, epsilon)) return true;
            if (!Approximately(original.points[i], part[i], epsilon)) return true;
        }

        return false;
    }

    private static void FinalizePart(ref List<Vector2> current, List<List<Vector2>> parts, float epsilon)
    {
        if (current != null && current.Count >= 2)
        {
            DeduplicateInPlace(current, epsilon);
            if (current.Count >= 2) parts.Add(current);
        }

        current = null;
    }

    private static void AppendIfUnique(List<Vector2> points, Vector2 point, float epsilon)
    {
        if (points.Count == 0 || !Approximately(points[points.Count - 1], point, epsilon))
        {
            points.Add(point);
        }
    }

    private static void DeduplicateInPlace(List<Vector2> points, float epsilon)
    {
        if (points.Count < 2) return;
        for (var i = points.Count - 1; i > 0; i--)
        {
            if (Approximately(points[i], points[i - 1], epsilon)) points.RemoveAt(i);
        }
    }

    private static bool TryClipSegment(Vector2 p0, Vector2 p1, Rect rect, out Vector2 c0, out Vector2 c1, float epsilon)
    {
        c0 = p0;
        c1 = p1;

        var dx = p1.x - p0.x;
        var dy = p1.y - p0.y;
        var u0 = 0f;
        var u1 = 1f;

        if (!ClipTest(-dx, p0.x - rect.xMin, ref u0, ref u1, epsilon)) return false;
        if (!ClipTest(dx, rect.xMax - p0.x, ref u0, ref u1, epsilon)) return false;
        if (!ClipTest(-dy, p0.y - rect.yMin, ref u0, ref u1, epsilon)) return false;
        if (!ClipTest(dy, rect.yMax - p0.y, ref u0, ref u1, epsilon)) return false;

        c0 = new Vector2(p0.x + u0 * dx, p0.y + u0 * dy);
        c1 = new Vector2(p0.x + u1 * dx, p0.y + u1 * dy);
        return true;
    }

    private static bool ClipTest(float p, float q, ref float u0, ref float u1, float epsilon)
    {
        if (Mathf.Abs(p) <= epsilon) return q >= -epsilon;

        var r = q / p;
        if (p < 0f)
        {
            if (r > u1) return false;
            if (r > u0) u0 = r;
        }
        else
        {
            if (r < u0) return false;
            if (r < u1) u1 = r;
        }

        return true;
    }

    private static bool Approximately(Vector2 a, Vector2 b, float epsilon)
    {
        return (a - b).sqrMagnitude <= epsilon * epsilon;
    }

    private static bool IsInside(Vector2 p, Rect rect, float epsilon)
    {
        return p.x >= rect.xMin - epsilon && p.x <= rect.xMax + epsilon && p.y >= rect.yMin - epsilon && p.y <= rect.yMax + epsilon;
    }

    private static Vector2 ClampToRect(Vector2 p, Rect rect)
    {
        return new Vector2(Mathf.Clamp(p.x, rect.xMin, rect.xMax), Mathf.Clamp(p.y, rect.yMin, rect.yMax));
    }
}
