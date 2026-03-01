using UnityEngine;

namespace GSP.TrackEditor
{
    public static class TrackBakedDataUtil
    {
        public static Rect ComputeBounds(TrackBakedData track)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            void Encapsulate(Vector2 point)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            void EncapsulateArray(Vector2[] points)
            {
                if (points == null)
                {
                    return;
                }

                for (var i = 0; i < points.Length; i++)
                {
                    Encapsulate(points[i]);
                }
            }

            var hasMainBoundaries = track != null
                && track.mainLeftBoundary != null
                && track.mainRightBoundary != null
                && track.mainLeftBoundary.Length > 0
                && track.mainRightBoundary.Length > 0;

            if (hasMainBoundaries)
            {
                EncapsulateArray(track.mainLeftBoundary);
                EncapsulateArray(track.mainRightBoundary);
            }
            else if (track != null)
            {
                EncapsulateArray(track.mainCenterline);
            }

            if (track != null)
            {
                if ((track.pitLeftBoundary != null && track.pitLeftBoundary.Length > 0)
                    || (track.pitRightBoundary != null && track.pitRightBoundary.Length > 0))
                {
                    EncapsulateArray(track.pitLeftBoundary);
                    EncapsulateArray(track.pitRightBoundary);
                }
                else
                {
                    EncapsulateArray(track.pitCenterline);
                }

                if (track.startGridSlots != null)
                {
                    for (var i = 0; i < track.startGridSlots.Count; i++)
                    {
                        Encapsulate(track.startGridSlots[i].pos);
                    }
                }
            }

            if (!float.IsFinite(min.x) || !float.IsFinite(min.y) || !float.IsFinite(max.x) || !float.IsFinite(max.y))
            {
                var defaultExtent = Mathf.Max(1f, (track != null ? track.trackWidth : 8f) * 2f);
                min = new Vector2(-defaultExtent, -defaultExtent);
                max = new Vector2(defaultExtent, defaultExtent);
            }

            var padding = Mathf.Max(1f, (track != null ? track.trackWidth : 8f) * 1.25f);
            min -= Vector2.one * padding;
            max += Vector2.one * padding;

            if (max.x <= min.x)
            {
                max.x = min.x + 1f;
            }

            if (max.y <= min.y)
            {
                max.y = min.y + 1f;
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
