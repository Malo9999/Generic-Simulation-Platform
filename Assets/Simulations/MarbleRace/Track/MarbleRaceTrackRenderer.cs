using System.Collections.Generic;
using UnityEngine;

public sealed class MarbleRaceTrackRenderer
{
    private static readonly string[] RequiredNames =
    {
        "TrackLane",
        "TrackInnerBorder",
        "TrackOuterBorder",
        "StartFinishLine",
        "CenterLine",
        "BridgeShadow"
    };

    private static Material sharedMaterial;
    private Transform trackRoot;

    private struct CrossingData
    {
        public int SegmentA;
        public int SegmentB;
        public Vector2 Point;
        public float Angle;
    }

    public void Apply(Transform decorRoot, MarbleRaceTrack track)
    {
        if (decorRoot == null || track == null || track.SampleCount <= 3)
        {
            return;
        }

        trackRoot = EnsureTrackRoot(decorRoot);
        CleanupDynamicChildren();

        var avgHalfWidth = 0f;
        for (var i = 0; i < track.SampleCount; i++)
        {
            avgHalfWidth += track.HalfWidth[i];
        }

        avgHalfWidth /= track.SampleCount;
        var roadWidth = Mathf.Clamp(avgHalfWidth * 2f, 1.8f, 4.8f);
        var borderWidth = Mathf.Clamp(roadWidth * 0.10f, 0.18f, 0.30f);
        var centerLineWidth = Mathf.Clamp(roadWidth * 0.06f, 0.12f, 0.22f);

        var centerPoints = new Vector3[track.SampleCount];
        var innerPoints = new Vector3[track.SampleCount];
        var outerPoints = new Vector3[track.SampleCount];
        for (var i = 0; i < track.SampleCount; i++)
        {
            var center = track.Center[i];
            var boundaryOffset = track.Normal[i] * track.HalfWidth[i];
            centerPoints[i] = new Vector3(center.x, center.y, 0f);
            innerPoints[i] = new Vector3(center.x + boundaryOffset.x, center.y + boundaryOffset.y, 0f);
            outerPoints[i] = new Vector3(center.x - boundaryOffset.x, center.y - boundaryOffset.y, 0f);
        }

        RenderLaneAndBorders(track, centerPoints, innerPoints, outerPoints, roadWidth, borderWidth);
        RenderDashedCenterLine(centerPoints, centerLineWidth);
        RenderStartFinish(track, avgHalfWidth);
    }

    public void Clear()
    {
        if (trackRoot != null)
        {
            Object.Destroy(trackRoot.gameObject);
            trackRoot = null;
        }
    }

    private void RenderLaneAndBorders(MarbleRaceTrack track, Vector3[] centerPoints, Vector3[] innerPoints, Vector3[] outerPoints, float roadWidth, float borderWidth)
    {
        CrossingData crossing;
        var hasCrossing = TryFindBestCrossing(track, out crossing);
        if (!hasCrossing)
        {
            var lane = EnsureLineRenderer("TrackLane", new Color(0.12f, 0.12f, 0.14f, 0.95f), 5, true, roadWidth);
            lane.positionCount = centerPoints.Length;
            lane.SetPositions(centerPoints);

            var innerBorder = EnsureLineRenderer("TrackInnerBorder", new Color(0.90f, 0.90f, 0.92f, 0.95f), 10, true, borderWidth);
            innerBorder.positionCount = innerPoints.Length;
            innerBorder.SetPositions(innerPoints);

            var outerBorder = EnsureLineRenderer("TrackOuterBorder", new Color(0.90f, 0.90f, 0.92f, 0.95f), 10, true, borderWidth);
            outerBorder.positionCount = outerPoints.Length;
            outerBorder.SetPositions(outerPoints);

            EnsureLineRenderer("BridgeShadow", new Color(0f, 0f, 0f, 0f), 14, false, 0f).positionCount = 0;
            return;
        }

        var window = Mathf.Clamp(track.SampleCount / 12, 28, 42);
        var curvatureA = AverageCurvature(track.Curvature, crossing.SegmentA, window);
        var curvatureB = AverageCurvature(track.Curvature, crossing.SegmentB, window);

        var overCenterIndex = curvatureA <= curvatureB ? crossing.SegmentA : crossing.SegmentB;
        var underCenterIndex = overCenterIndex == crossing.SegmentA ? crossing.SegmentB : crossing.SegmentA;

        var overStart = Wrap(overCenterIndex - window, track.SampleCount);
        var overEnd = Wrap(overCenterIndex + window, track.SampleCount);

        var laneUnder = EnsureLineRenderer("TrackLane_Under", new Color(0.12f, 0.12f, 0.14f, 0.95f), 5, false, roadWidth);
        ApplyExtractedPolyline(laneUnder, centerPoints, overEnd, overStart);

        var laneOver = EnsureLineRenderer("TrackLane_Over", new Color(0.12f, 0.12f, 0.14f, 0.95f), 15, false, roadWidth);
        ApplyExtractedPolyline(laneOver, centerPoints, overStart, overEnd);

        var borderUnderInner = EnsureLineRenderer("TrackBorders_Under_Inner", new Color(0.90f, 0.90f, 0.92f, 0.95f), 10, false, borderWidth);
        var borderUnderOuter = EnsureLineRenderer("TrackBorders_Under_Outer", new Color(0.90f, 0.90f, 0.92f, 0.95f), 10, false, borderWidth);
        ApplyExtractedPolyline(borderUnderInner, innerPoints, overEnd, overStart);
        ApplyExtractedPolyline(borderUnderOuter, outerPoints, overEnd, overStart);

        var borderOverInner = EnsureLineRenderer("TrackBorders_Over_Inner", new Color(0.90f, 0.90f, 0.92f, 0.95f), 20, false, borderWidth);
        var borderOverOuter = EnsureLineRenderer("TrackBorders_Over_Outer", new Color(0.90f, 0.90f, 0.92f, 0.95f), 20, false, borderWidth);
        ApplyExtractedPolyline(borderOverInner, innerPoints, overStart, overEnd);
        ApplyExtractedPolyline(borderOverOuter, outerPoints, overStart, overEnd);

        EnsureLineRenderer("TrackLane", new Color(0f, 0f, 0f, 0f), 0, false, 0f).positionCount = 0;
        EnsureLineRenderer("TrackInnerBorder", new Color(0f, 0f, 0f, 0f), 0, false, 0f).positionCount = 0;
        EnsureLineRenderer("TrackOuterBorder", new Color(0f, 0f, 0f, 0f), 0, false, 0f).positionCount = 0;

        var shadow = EnsureLineRenderer("BridgeShadow", new Color(0.02f, 0.02f, 0.02f, 0.28f), 14, false, roadWidth * 1.12f);
        var shadowWindow = Mathf.Max(12, window / 2);
        var shadowStart = Wrap(underCenterIndex - shadowWindow, track.SampleCount);
        var shadowEnd = Wrap(underCenterIndex + shadowWindow, track.SampleCount);
        ApplyExtractedPolyline(shadow, centerPoints, shadowStart, shadowEnd);
    }

    private void RenderStartFinish(MarbleRaceTrack track, float avgHalfWidth)
    {
        var startFinish = EnsureLineRenderer("StartFinishLine", Color.white, 25, false, Mathf.Max(0.12f, avgHalfWidth * 0.24f));
        var startA = track.Center[0] + (track.Normal[0] * track.HalfWidth[0]);
        var startB = track.Center[0] - (track.Normal[0] * track.HalfWidth[0]);
        startFinish.positionCount = 2;
        startFinish.SetPosition(0, new Vector3(startA.x, startA.y, 0f));
        startFinish.SetPosition(1, new Vector3(startB.x, startB.y, 0f));
    }

    private void RenderDashedCenterLine(Vector3[] centerPoints, float width)
    {
        var centerRoot = EnsureChild("CenterLine");
        var color = new Color(0.95f, 0.80f, 0.20f, 0.90f);
        const int onSamples = 12;
        const int offSamples = 8;
        const int maxSegments = 80;

        var used = 0;
        var idx = 0;
        while (idx < centerPoints.Length && used < maxSegments)
        {
            var dashStart = idx;
            var dashEnd = Mathf.Min(centerPoints.Length - 1, dashStart + onSamples);
            if (dashEnd > dashStart + 1)
            {
                var segment = EnsurePooledLine(centerRoot, "CenterLine_", used, color, 12, width);
                segment.loop = false;
                var count = dashEnd - dashStart + 1;
                segment.positionCount = count;
                for (var p = 0; p < count; p++)
                {
                    segment.SetPosition(p, centerPoints[dashStart + p]);
                }

                used++;
            }

            idx += onSamples + offSamples;
        }

        for (var i = used; i < centerRoot.childCount; i++)
        {
            var childLine = centerRoot.GetChild(i).GetComponent<LineRenderer>();
            if (childLine != null)
            {
                childLine.positionCount = 0;
            }
        }
    }

    private static float AverageCurvature(float[] curvature, int centerIndex, int radius)
    {
        var sum = 0f;
        var n = curvature.Length;
        for (var i = -radius; i <= radius; i++)
        {
            sum += curvature[Wrap(centerIndex + i, n)];
        }

        return sum / ((radius * 2) + 1);
    }

    private static bool TryFindBestCrossing(MarbleRaceTrack track, out CrossingData best)
    {
        best = default;
        var n = track.SampleCount;
        var found = false;
        var stride = Mathf.Max(1, n / 256);
        var minGap = Mathf.Max(6, n / 32);

        for (var i = 0; i < n; i += stride)
        {
            var i2 = Wrap(i + stride, n);
            var a1 = track.Center[i];
            var a2 = track.Center[i2];
            var aDir = (a2 - a1).normalized;

            for (var j = i + minGap; j < n; j += stride)
            {
                var cyclic = Mathf.Abs(i - j);
                if (cyclic < minGap || cyclic > n - minGap)
                {
                    continue;
                }

                var j2 = Wrap(j + stride, n);
                var b1 = track.Center[j];
                var b2 = track.Center[j2];
                if (!TrySegmentIntersection(a1, a2, b1, b2, out var intersection))
                {
                    continue;
                }

                var bDir = (b2 - b1).normalized;
                var angle = Vector2.Angle(aDir, bDir);
                if (!found || angle > best.Angle)
                {
                    best = new CrossingData
                    {
                        SegmentA = i,
                        SegmentB = j,
                        Point = intersection,
                        Angle = angle
                    };

                    found = true;
                }
            }
        }

        return found;
    }

    private static bool TrySegmentIntersection(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2, out Vector2 intersection)
    {
        intersection = Vector2.zero;
        var r = p2 - p1;
        var s = q2 - q1;
        var denom = Cross(r, s);
        if (Mathf.Abs(denom) < 0.0001f)
        {
            return false;
        }

        var qp = q1 - p1;
        var t = Cross(qp, s) / denom;
        var u = Cross(qp, r) / denom;
        if (t < 0f || t > 1f || u < 0f || u > 1f)
        {
            return false;
        }

        intersection = p1 + (r * t);
        return true;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return (a.x * b.y) - (a.y * b.x);
    }

    private static int Wrap(int i, int n)
    {
        if (n <= 0)
        {
            return 0;
        }

        var v = i % n;
        return v < 0 ? v + n : v;
    }

    private static List<Vector3> ExtractPolyline(Vector3[] points, int fromInclusive, int toInclusive)
    {
        var n = points.Length;
        var result = new List<Vector3>(n);
        var idx = Wrap(fromInclusive, n);
        var end = Wrap(toInclusive, n);
        result.Add(points[idx]);

        while (idx != end)
        {
            idx = Wrap(idx + 1, n);
            result.Add(points[idx]);
            if (result.Count > n + 1)
            {
                break;
            }
        }

        return result;
    }

    private static void ApplyExtractedPolyline(LineRenderer line, Vector3[] points, int fromInclusive, int toInclusive)
    {
        var extracted = ExtractPolyline(points, fromInclusive, toInclusive);
        line.positionCount = extracted.Count;
        line.SetPositions(extracted.ToArray());
    }

    private Transform EnsureTrackRoot(Transform decorRoot)
    {
        var existing = decorRoot.Find("TrackRoot");
        if (existing == null)
        {
            var go = new GameObject("TrackRoot");
            existing = go.transform;
            existing.SetParent(decorRoot, false);
        }

        return existing;
    }

    private void CleanupDynamicChildren()
    {
        for (var i = trackRoot.childCount - 1; i >= 0; i--)
        {
            var child = trackRoot.GetChild(i);
            var name = child.name;
            var isRequired = IsRequiredName(name);
            var isDynamic =
                name.StartsWith("TrackLane_Under") ||
                name.StartsWith("TrackLane_Over") ||
                name.StartsWith("TrackBorders_Under") ||
                name.StartsWith("TrackBorders_Over") ||
                name.StartsWith("CenterLine_");

            if (!isRequired && !isDynamic)
            {
                Object.Destroy(child.gameObject);
                continue;
            }

            if (isDynamic)
            {
                Object.Destroy(child.gameObject);
            }
        }
    }

    private LineRenderer EnsurePooledLine(Transform parent, string prefix, int index, Color color, int sortingOrder, float width)
    {
        var name = prefix + index.ToString("00");
        var t = parent.Find(name);
        if (t == null)
        {
            var go = new GameObject(name);
            t = go.transform;
            t.SetParent(parent, false);
        }

        return ConfigureLineRenderer(t.gameObject, color, sortingOrder, false, width);
    }

    private Transform EnsureChild(string name)
    {
        var child = trackRoot.Find(name);
        if (child == null)
        {
            var go = new GameObject(name);
            child = go.transform;
            child.SetParent(trackRoot, false);
        }

        return child;
    }

    private LineRenderer EnsureLineRenderer(string name, Color color, int sortingOrder, bool loop, float width)
    {
        var lineTransform = trackRoot.Find(name);
        if (lineTransform == null)
        {
            var go = new GameObject(name);
            lineTransform = go.transform;
            lineTransform.SetParent(trackRoot, false);
        }

        return ConfigureLineRenderer(lineTransform.gameObject, color, sortingOrder, loop, width);
    }

    private static LineRenderer ConfigureLineRenderer(GameObject go, Color color, int sortingOrder, bool loop, float width)
    {
        var lr = go.GetComponent<LineRenderer>();
        if (lr == null)
        {
            lr = go.AddComponent<LineRenderer>();
        }

        lr.material = GetSharedMaterial();
        lr.loop = loop;
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 8;
        lr.numCornerVertices = 8;
        lr.widthMultiplier = width;
        lr.startColor = color;
        lr.endColor = color;
        lr.sortingLayerName = "Default";
        lr.sortingOrder = sortingOrder;
        return lr;
    }

    private static bool IsRequiredName(string name)
    {
        for (var i = 0; i < RequiredNames.Length; i++)
        {
            if (RequiredNames[i] == name)
            {
                return true;
            }
        }

        return false;
    }

    private static Material GetSharedMaterial()
    {
        if (sharedMaterial != null)
        {
            return sharedMaterial;
        }

        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        sharedMaterial = new Material(shader);
        return sharedMaterial;
    }
}
