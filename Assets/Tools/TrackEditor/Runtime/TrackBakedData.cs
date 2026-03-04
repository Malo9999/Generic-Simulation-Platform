using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GSP.TrackEditor
{
    [CreateAssetMenu(menuName = "GSP/Track Editor/Track Baked Data", fileName = "TrackBakedData")]
    public class TrackBakedData : ScriptableObject
    {
        public float trackWidth = 8f;
        public Vector2[] mainCenterline;
        public Vector2[] mainLeftBoundary;
        public Vector2[] mainRightBoundary;
        public float lapLength;
        public List<TrackSlot> startGridSlots = new();
        public Vector2[] pitCenterline;
        public Vector2[] pitLeftBoundary;
        public Vector2[] pitRightBoundary;
        public float[] cumulativeMainLength;
        public Vector2 startFinishPos;
        public Vector2 startFinishDir;
        public float startFinishDistance;

        public string BuildDebugReport()
        {
            var report = new StringBuilder(384);
            var mainCenterlineLength = ComputePolylineLength(mainCenterline);
            var pitCenterlineLength = ComputePolylineLength(pitCenterline);
            var bounds = ComputeDebugMainBounds(this);
            var startFinishDirection = startFinishDir.sqrMagnitude > 0.0001f
                ? startFinishDir.normalized
                : Vector2.zero;

            report.AppendLine("Track Debug Report");
            report.AppendLine($"name: {name}");
            report.AppendLine($"trackWidth: {trackWidth:F3}");
            report.AppendLine($"mainCenterline: count={mainCenterline?.Length ?? 0}, length={mainCenterlineLength:F3}");
            report.AppendLine($"lapLength: {lapLength:F3}");
            report.AppendLine($"bounds: min={bounds.min}, max={bounds.max}");
            report.AppendLine(
                $"startFinish: pos={startFinishPos}, dir={startFinishDirection}, distance={startFinishDistance:F3}");
            report.AppendLine($"startGridSlots: {startGridSlots?.Count ?? 0}");
            report.Append($"pitCenterline: count={pitCenterline?.Length ?? 0}, length={pitCenterlineLength:F3}");
            return report.ToString();
        }

        public static float ComputePolylineLength(Vector2[] points)
        {
            if (points == null || points.Length < 2)
            {
                return 0f;
            }

            var total = 0f;
            for (var i = 1; i < points.Length; i++)
            {
                total += Vector2.Distance(points[i - 1], points[i]);
            }

            return total;
        }

        public static Rect ComputeDebugMainBounds(TrackBakedData track)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            void Encapsulate(Vector2[] points)
            {
                if (points == null)
                {
                    return;
                }

                for (var i = 0; i < points.Length; i++)
                {
                    min = Vector2.Min(min, points[i]);
                    max = Vector2.Max(max, points[i]);
                }
            }

            var hasMainBoundaries = track != null
                && track.mainLeftBoundary != null
                && track.mainRightBoundary != null
                && track.mainLeftBoundary.Length > 0
                && track.mainRightBoundary.Length > 0;

            if (hasMainBoundaries)
            {
                Encapsulate(track.mainLeftBoundary);
                Encapsulate(track.mainRightBoundary);
            }
            else if (track != null)
            {
                Encapsulate(track.mainCenterline);
            }

            if (!float.IsFinite(min.x) || !float.IsFinite(min.y) || !float.IsFinite(max.x) || !float.IsFinite(max.y))
            {
                min = Vector2.zero;
                max = Vector2.zero;
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
