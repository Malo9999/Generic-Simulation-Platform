using UnityEngine;

namespace GSP.TrackEditor
{
    public static class TrackMathUtil
    {
        public const float Epsilon = 0.01f;

        public static Vector2 Rotate45(Vector2 value, int steps45)
        {
            var radians = steps45 * 45f * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
        }

        public static Vector2 ToWorld(in PlacedPiece placed, Vector2 local)
        {
            return placed.position + Rotate45(local, placed.rotationSteps45);
        }

        public static Dir8 ToWorld(in PlacedPiece placed, Dir8 local)
        {
            return local.RotateSteps45(placed.rotationSteps45);
        }

        public static float PolylineLength(Vector2[] points)
        {
            if (points == null || points.Length < 2)
            {
                return 0f;
            }

            var sum = 0f;
            for (var i = 1; i < points.Length; i++)
            {
                sum += Vector2.Distance(points[i - 1], points[i]);
            }

            return sum;
        }

        public static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            var ab = b - a;
            var t = Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f);
            t = Mathf.Clamp01(t);
            return a + ab * t;
        }
    }
}
