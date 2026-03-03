using UnityEngine;
using GSP.TrackEditor;

public static class TrackFitUtil
{
    private const float Epsilon = 0.0001f;

    public struct FitResult
    {
        public bool ok;
        public string reason;
        public Rect rawBounds;
        public Rect rotatedBounds;
        public float rotationDeg;
        public float scale;
        public Vector2 offset;
    }

    public static bool TryComputeFit(
        TrackBakedData track,
        float arenaWidth,
        float arenaHeight,
        float padding,
        bool rotateEnabled,
        out FitResult result)
    {
        result = new FitResult
        {
            ok = false,
            reason = "no-points",
            rawBounds = new Rect(0f, 0f, 0f, 0f),
            rotatedBounds = new Rect(0f, 0f, 0f, 0f),
            rotationDeg = 0f,
            scale = 1f,
            offset = Vector2.zero
        };

        if (!TryComputeRawBounds(track, out var rawBounds))
        {
            return false;
        }

        var rotationDeg = 0f;
        if (rotateEnabled && track != null && track.startGridSlots != null && track.startGridSlots.Count > 0)
        {
            var dir = track.startGridSlots[0].dir;
            if (dir.sqrMagnitude < Epsilon)
            {
                dir = Vector2.right;
            }

            dir.Normalize();
            var angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            rotationDeg = -angleDeg;
        }

        if (!TryComputeRotatedBounds(track, rawBounds, rotationDeg, out var rotatedBounds))
        {
            return false;
        }

        var boundsW = rotatedBounds.width;
        var boundsH = rotatedBounds.height;
        if (boundsW <= Epsilon || boundsH <= Epsilon)
        {
            result.rawBounds = rawBounds;
            result.rotatedBounds = rotatedBounds;
            result.rotationDeg = rotationDeg;
            result.reason = "degenerate-bounds";
            return false;
        }

        var targetW = Mathf.Max(1f, arenaWidth);
        var targetH = Mathf.Max(1f, arenaHeight);
        var availableW = Mathf.Max(1f, targetW - (2f * Mathf.Max(0f, padding)));
        var availableH = Mathf.Max(1f, targetH - (2f * Mathf.Max(0f, padding)));

        var scale = Mathf.Min(availableW / boundsW, availableH / boundsH);
        scale = Mathf.Clamp(scale, 0.05f, 20f);

        var center = rotatedBounds.center;
        var offset = -center * scale;

        result.ok = true;
        result.reason = "ok";
        result.rawBounds = rawBounds;
        result.rotatedBounds = rotatedBounds;
        result.rotationDeg = rotationDeg;
        result.scale = scale;
        result.offset = offset;
        return true;
    }

    private static bool TryComputeRawBounds(TrackBakedData track, out Rect bounds)
    {
        bounds = default;
        if (track == null)
        {
            return false;
        }

        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var hasPoint = false;

        void Encapsulate(Vector2 p)
        {
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
            hasPoint = true;
        }

        void EncapsulateAll(Vector2[] points)
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

        var hasMainBoundaries = track.mainLeftBoundary != null
            && track.mainLeftBoundary.Length > 0
            && track.mainRightBoundary != null
            && track.mainRightBoundary.Length > 0;

        if (hasMainBoundaries)
        {
            EncapsulateAll(track.mainLeftBoundary);
            EncapsulateAll(track.mainRightBoundary);
        }
        else if (track.mainCenterline != null && track.mainCenterline.Length > 0)
        {
            var halfWidth = Mathf.Max(0f, track.trackWidth * 0.5f);
            for (var i = 0; i < track.mainCenterline.Length; i++)
            {
                var point = track.mainCenterline[i];
                Encapsulate(new Vector2(point.x - halfWidth, point.y - halfWidth));
                Encapsulate(new Vector2(point.x + halfWidth, point.y + halfWidth));
            }
        }

        EncapsulateAll(track.pitLeftBoundary);
        EncapsulateAll(track.pitRightBoundary);

        if (!hasPoint || !float.IsFinite(min.x) || !float.IsFinite(min.y) || !float.IsFinite(max.x) || !float.IsFinite(max.y))
        {
            return false;
        }

        bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    private static bool TryComputeRotatedBounds(TrackBakedData track, Rect rawBounds, float rotationDeg, out Rect rotatedBounds)
    {
        rotatedBounds = default;

        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var hasPoint = false;
        var rotation = Quaternion.Euler(0f, 0f, rotationDeg);

        void EncapsulateRotated(Vector2 p)
        {
            var rotated = rotation * new Vector3(p.x, p.y, 0f);
            var point = new Vector2(rotated.x, rotated.y);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
            hasPoint = true;
        }

        void EncapsulateAll(Vector2[] points)
        {
            if (points == null)
            {
                return;
            }

            for (var i = 0; i < points.Length; i++)
            {
                EncapsulateRotated(points[i]);
            }
        }

        var hasMainBoundaries = track != null
            && track.mainLeftBoundary != null
            && track.mainLeftBoundary.Length > 0
            && track.mainRightBoundary != null
            && track.mainRightBoundary.Length > 0;

        if (hasMainBoundaries)
        {
            EncapsulateAll(track.mainLeftBoundary);
            EncapsulateAll(track.mainRightBoundary);
        }
        else if (track != null && track.mainCenterline != null && track.mainCenterline.Length > 0)
        {
            var halfWidth = Mathf.Max(0f, track.trackWidth * 0.5f);
            for (var i = 0; i < track.mainCenterline.Length; i++)
            {
                var point = track.mainCenterline[i];
                EncapsulateRotated(new Vector2(point.x - halfWidth, point.y - halfWidth));
                EncapsulateRotated(new Vector2(point.x + halfWidth, point.y + halfWidth));
            }
        }

        EncapsulateAll(track?.pitLeftBoundary);
        EncapsulateAll(track?.pitRightBoundary);

        if (!hasPoint)
        {
            rotatedBounds = rawBounds;
            return false;
        }

        rotatedBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }
}
