using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class MarbleRaceTrackRenderer
{
    private const float TargetSpacing = 0.35f;
    private const int TangentWindow = 2;
    private const float MiterLimit = 3.0f;
    private const int LineCornerVertices = 8;
    private const int LineCapVertices = 8;
    private const float BorderRoundAngleThresholdDeg = 12f;
    private const int BorderRoundStepsPerCorner = 5;
    private const float BorderRoundInset = 0.35f;
    private const bool EnableDebugGizmos = false;
    private const int DebugNormalStride = 8;
    private const float HighAngleThresholdDeg = 35f;

    private static Material sharedMaterial;
    private Transform trackRoot;
    private RenderDebugStats lastDebugStats;

    public Transform TrackRoot => trackRoot;
    public RenderDebugStats LastDebugStats => lastDebugStats;

    public readonly struct RenderDebugStats
    {
        public readonly int CenterPointCount;
        public readonly int LeftEdgePointCount;
        public readonly int RightEdgePointCount;
        public readonly int RoundedLeftBorderPointCount;
        public readonly int RoundedRightBorderPointCount;

        public RenderDebugStats(int centerPointCount, int leftEdgePointCount, int rightEdgePointCount, int roundedLeftBorderPointCount, int roundedRightBorderPointCount)
        {
            CenterPointCount = centerPointCount;
            LeftEdgePointCount = leftEdgePointCount;
            RightEdgePointCount = rightEdgePointCount;
            RoundedLeftBorderPointCount = roundedLeftBorderPointCount;
            RoundedRightBorderPointCount = roundedRightBorderPointCount;
        }
    }

    public void Apply(Transform decorRoot, MarbleRaceTrack track)
    {
        if (decorRoot == null || track == null || track.SampleCount <= 3)
        {
            return;
        }

        trackRoot = EnsureTrackRoot(decorRoot);
        CleanupTrackRootChildren();

        var avgHalfWidth = 0f;
        for (var i = 0; i < track.SampleCount; i++)
        {
            avgHalfWidth += track.HalfWidth[i];
        }

        avgHalfWidth /= track.SampleCount;

        var roadWidth = Mathf.Clamp(avgHalfWidth * 2f, 2.0f, 5.0f);
        var borderWidth = Mathf.Clamp(roadWidth * 0.10f, 0.18f, 0.32f);
        var startWidth = Mathf.Clamp(roadWidth * 0.18f, 0.25f, 0.45f);

        var sampled = BuildStableTrackData(track, TargetSpacing, TangentWindow, MiterLimit);

        BuildLayeredLines(sampled, roadWidth, borderWidth, out var roundedLeftCount, out var roundedRightCount);
        BuildBridgeShadow(sampled, roadWidth);
        BuildBoundaryColliders(sampled);
        ConfigureDebugGizmos(sampled);

        lastDebugStats = new RenderDebugStats(
            sampled.Center.Length,
            sampled.LeftEdge.Length,
            sampled.RightEdge.Length,
            roundedLeftCount,
            roundedRightCount);

        var startFinish = EnsureLineRenderer("StartFinishLine", Color.white, 12, false, startWidth);
        var startA = sampled.LeftEdge[0];
        var startB = sampled.RightEdge[0];
        startFinish.positionCount = 2;
        startFinish.SetPosition(0, new Vector3(startA.x, startA.y, 0f));
        startFinish.SetPosition(1, new Vector3(startB.x, startB.y, 0f));
    }

    private void BuildBoundaryColliders(SampledTrackData sampled)
    {
        var leftCollider = EnsureEdgeCollider("TrackBoundaryInnerCollider");
        var rightCollider = EnsureEdgeCollider("TrackBoundaryOuterCollider");

        leftCollider.points = BuildBoundaryPoints(sampled.LeftEdge);
        rightCollider.points = BuildBoundaryPoints(sampled.RightEdge);
    }

    private static Vector2[] BuildBoundaryPoints(Vector2[] edge)
    {
        var n = edge.Length;
        var points = new Vector2[n + 1];
        for (var i = 0; i < n; i++)
        {
            points[i] = edge[i];
        }

        points[n] = points[0];
        return points;
    }

    public void Clear()
    {
        if (trackRoot != null)
        {
            DestroyTrackObject(trackRoot.gameObject);
            trackRoot = null;
        }
    }

    private static void DestroyTrackObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(target);
            return;
        }

#if UNITY_EDITOR
        EditorApplication.delayCall += () =>
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        };
#else
        Object.Destroy(target);
#endif
    }

    private void BuildLayeredLines(SampledTrackData sampled, float roadWidth, float borderWidth, out int roundedLeftCount, out int roundedRightCount)
    {
        var runs = BuildLayerRuns(sampled.Layer);
        if (runs.Count == 0)
        {
            runs.Add(new LayerRun(0, sampled.SampleCount - 1, 0));
        }

        var laneGround = EnsureLineRenderer("TrackLane_Ground", new Color(0.12f, 0.12f, 0.14f, 0.95f), 5, false, roadWidth);
        var laneBridge = EnsureLineRenderer("TrackLane_Bridge", new Color(0.13f, 0.13f, 0.15f, 0.98f), 15, false, roadWidth);
        var innerGround = EnsureLineRenderer("TrackInnerBorder_Ground", new Color(0.90f, 0.90f, 0.92f, 0.95f), 10, false, borderWidth);
        var innerBridge = EnsureLineRenderer("TrackInnerBorder_Bridge", new Color(0.93f, 0.93f, 0.95f, 1f), 20, false, borderWidth);
        var outerGround = EnsureLineRenderer("TrackOuterBorder_Ground", new Color(0.90f, 0.90f, 0.92f, 0.95f), 10, false, borderWidth);
        var outerBridge = EnsureLineRenderer("TrackOuterBorder_Bridge", new Color(0.93f, 0.93f, 0.95f, 1f), 20, false, borderWidth);

        SetLineFromRuns(sampled, laneGround, runs, 0, sampled.Center, false);
        SetLineFromRuns(sampled, laneBridge, runs, 1, sampled.Center, false);
        roundedLeftCount = SetLineFromRuns(sampled, innerGround, runs, 0, sampled.LeftEdge, true);
        SetLineFromRuns(sampled, innerBridge, runs, 1, sampled.LeftEdge, true);
        roundedRightCount = SetLineFromRuns(sampled, outerGround, runs, 0, sampled.RightEdge, true);
        SetLineFromRuns(sampled, outerBridge, runs, 1, sampled.RightEdge, true);
    }

    private void BuildBridgeShadow(SampledTrackData sampled, float roadWidth)
    {
        var shadow = EnsureLineRenderer("TrackBridgeShadow", new Color(0f, 0f, 0f, 0.3f), 14, false, roadWidth * 1.2f);
        var runs = BuildLayerRuns(sampled.Layer);
        SetLineFromRuns(sampled, shadow, runs, 1, sampled.Center, false);
    }

    private static List<LayerRun> BuildLayerRuns(sbyte[] layer)
    {
        var runs = new List<LayerRun>();
        if (layer == null || layer.Length == 0)
        {
            return runs;
        }

        var n = layer.Length;
        var start = 0;
        var current = layer[0];
        for (var i = 1; i < n; i++)
        {
            if (layer[i] == current)
            {
                continue;
            }

            runs.Add(new LayerRun(start, i - 1, current));
            start = i;
            current = layer[i];
        }

        runs.Add(new LayerRun(start, n - 1, current));
        if (runs.Count > 1 && runs[0].Layer == runs[runs.Count - 1].Layer)
        {
            var first = runs[0];
            var last = runs[runs.Count - 1];
            runs[0] = new LayerRun(last.Start, first.End, first.Layer, last.Length + first.Length);
            runs.RemoveAt(runs.Count - 1);
        }

        if (runs.Count > 3)
        {
            runs.Sort((a, b) => (b.Length).CompareTo(a.Length));
            runs.RemoveRange(3, runs.Count - 3);
        }

        return runs;
    }

    private static int SetLineFromRuns(SampledTrackData sampled, LineRenderer lr, List<LayerRun> runs, int targetLayer, Vector2[] source, bool applyRoundedCorners)
    {
        var points = new List<Vector3>(sampled.SampleCount + 8);
        for (var r = 0; r < runs.Count; r++)
        {
            var run = runs[r];
            if (run.Layer != targetLayer)
            {
                continue;
            }

            var len = run.Length;
            for (var o = 0; o <= len; o++)
            {
                var idx = (run.Start + o) % sampled.SampleCount;
                var p = source[idx];

                points.Add(new Vector3(p.x, p.y, 0f));
            }
        }

        if (applyRoundedCorners)
        {
            points = RoundCorners(points, BorderRoundAngleThresholdDeg, BorderRoundStepsPerCorner);
        }

        lr.positionCount = points.Count;
        if (points.Count > 0)
        {
            lr.SetPositions(points.ToArray());
            lr.enabled = true;
        }
        else
        {
            lr.enabled = false;
        }

        return points.Count;
    }

    private static List<Vector3> RoundCorners(List<Vector3> points, float angleThresholdDeg, int stepsPerCorner)
    {
        if (points == null || points.Count < 3)
        {
            return points;
        }

        var rounded = new List<Vector3>(points.Count + (stepsPerCorner * points.Count / 2));
        rounded.Add(points[0]);

        for (var i = 1; i < points.Count - 1; i++)
        {
            var prev = (Vector2)points[i - 1];
            var current = (Vector2)points[i];
            var next = (Vector2)points[i + 1];

            var incoming = current - prev;
            var outgoing = next - current;
            var incomingLength = incoming.magnitude;
            var outgoingLength = outgoing.magnitude;
            if (incomingLength <= 1e-4f || outgoingLength <= 1e-4f)
            {
                rounded.Add(points[i]);
                continue;
            }

            var incomingDir = incoming / incomingLength;
            var outgoingDir = outgoing / outgoingLength;
            var angle = Vector2.Angle(incomingDir, outgoingDir);
            if (angle < angleThresholdDeg)
            {
                rounded.Add(points[i]);
                continue;
            }

            var inset = Mathf.Min(incomingLength, outgoingLength) * BorderRoundInset;
            inset = Mathf.Clamp(inset, 0.05f, 1.0f);

            var inPoint = current - (incomingDir * inset);
            var outPoint = current + (outgoingDir * inset);
            rounded.Add(inPoint);

            for (var step = 1; step <= stepsPerCorner; step++)
            {
                var t = step / (float)(stepsPerCorner + 1);
                var oneMinusT = 1f - t;
                var bezier = (oneMinusT * oneMinusT * inPoint) + (2f * oneMinusT * t * current) + (t * t * outPoint);
                rounded.Add(bezier);
            }

            rounded.Add(outPoint);
        }

        rounded.Add(points[points.Count - 1]);
        return rounded;
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

    private void CleanupTrackRootChildren()
    {
        for (var i = trackRoot.childCount - 1; i >= 0; i--)
        {
            var child = trackRoot.GetChild(i);
            if (!child.name.StartsWith("Track") && child.name != "StartFinishLine")
            {
                DestroyTrackObject(child.gameObject);
            }
        }
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

    private EdgeCollider2D EnsureEdgeCollider(string name)
    {
        var colliderTransform = trackRoot.Find(name);
        if (colliderTransform == null)
        {
            var go = new GameObject(name);
            colliderTransform = go.transform;
            colliderTransform.SetParent(trackRoot, false);
        }

        var edgeCollider = colliderTransform.GetComponent<EdgeCollider2D>();
        if (edgeCollider == null)
        {
            edgeCollider = colliderTransform.gameObject.AddComponent<EdgeCollider2D>();
        }

        edgeCollider.edgeRadius = 0.02f;
        edgeCollider.enabled = true;
        return edgeCollider;
    }

    private void ConfigureDebugGizmos(SampledTrackData sampled)
    {
        var gizmoTransform = trackRoot.Find("TrackEdgeDebugGizmos");
        TrackEdgeDebugGizmos gizmos;
        if (gizmoTransform == null)
        {
            var go = new GameObject("TrackEdgeDebugGizmos");
            go.transform.SetParent(trackRoot, false);
            gizmos = go.AddComponent<TrackEdgeDebugGizmos>();
        }
        else
        {
            gizmos = gizmoTransform.GetComponent<TrackEdgeDebugGizmos>();
            if (gizmos == null)
            {
                gizmos = gizmoTransform.gameObject.AddComponent<TrackEdgeDebugGizmos>();
            }
        }

        gizmos.enabled = EnableDebugGizmos;
        gizmos.Configure(sampled.Center, sampled.Normal, sampled.HighAngleOrMiter, DebugNormalStride);
    }

    private static LineRenderer ConfigureLineRenderer(GameObject go, Color color, int sortingOrder, bool loop, float width)
    {
        var lr = go.GetComponent<LineRenderer>();
        if (lr == null)
        {
            lr = go.AddComponent<LineRenderer>();
        }

        lr.sharedMaterial = GetSharedMaterial();
        lr.loop = loop;
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = LineCapVertices;
        lr.numCornerVertices = LineCornerVertices;
        lr.widthMultiplier = width;
        lr.startColor = color;
        lr.endColor = color;
        lr.sortingLayerName = "Default";
        lr.sortingOrder = sortingOrder;
        return lr;
    }

    private static Material GetSharedMaterial()
    {
        if (sharedMaterial != null)
        {
            return sharedMaterial;
        }

        var shader =
            Shader.Find("Sprites/Default") ??
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Legacy Shaders/Particles/Alpha Blended") ??
            Shader.Find("Unlit/Color");

        sharedMaterial = new Material(shader);
        return sharedMaterial;
    }

    private readonly struct LayerRun
    {
        public readonly int Start;
        public readonly int End;
        public readonly int Length;
        public readonly sbyte Layer;

        public LayerRun(int start, int end, sbyte layer, int length = -1)
        {
            Start = start;
            End = end;
            Layer = layer;
            Length = length >= 0 ? length : Mathf.Max(0, end - start + 1);
        }
    }

    private readonly struct SampledTrackData
    {
        public readonly Vector2[] Center;
        public readonly Vector2[] Tangent;
        public readonly Vector2[] Normal;
        public readonly float[] HalfWidth;
        public readonly sbyte[] Layer;
        public readonly Vector2[] LeftEdge;
        public readonly Vector2[] RightEdge;
        public readonly bool[] HighAngleOrMiter;

        public int SampleCount => Center.Length;

        public SampledTrackData(
            Vector2[] center,
            Vector2[] tangent,
            Vector2[] normal,
            float[] halfWidth,
            sbyte[] layer,
            Vector2[] leftEdge,
            Vector2[] rightEdge,
            bool[] highAngleOrMiter)
        {
            Center = center;
            Tangent = tangent;
            Normal = normal;
            HalfWidth = halfWidth;
            Layer = layer;
            LeftEdge = leftEdge;
            RightEdge = rightEdge;
            HighAngleOrMiter = highAngleOrMiter;
        }
    }

    private static SampledTrackData BuildStableTrackData(MarbleRaceTrack track, float targetSpacing, int tangentWindow, float miterLimit)
    {
        var center = ResampleClosed(track.Center, targetSpacing, out var sourceIndex, out var sourceT);
        var sampleCount = center.Length;
        var halfWidth = new float[sampleCount];
        var layer = new sbyte[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var a = sourceIndex[i];
            var b = (a + 1) % track.SampleCount;
            var t = sourceT[i];
            halfWidth[i] = Mathf.Lerp(track.HalfWidth[a], track.HalfWidth[b], t);
            layer[i] = t < 0.5f ? track.Layer[a] : track.Layer[b];
        }

        var tangent = new Vector2[sampleCount];
        var normal = new Vector2[sampleCount];
        var left = new Vector2[sampleCount];
        var right = new Vector2[sampleCount];
        var highAngleOrMiter = new bool[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var forward = center[Wrap(i + tangentWindow, sampleCount)];
            var backward = center[Wrap(i - tangentWindow, sampleCount)];
            var tan = (forward - backward).normalized;
            if (tan.sqrMagnitude < 1e-6f)
            {
                tan = (center[Wrap(i + 1, sampleCount)] - center[Wrap(i - 1, sampleCount)]).normalized;
            }

            tangent[i] = tan;
            normal[i] = new Vector2(-tan.y, tan.x);
            if (i > 0 && Vector2.Dot(normal[i], normal[i - 1]) < 0f)
            {
                normal[i] = -normal[i];
            }

            left[i] = center[i] + (normal[i] * halfWidth[i]);
            right[i] = center[i] - (normal[i] * halfWidth[i]);

            var prevDir = (center[i] - center[Wrap(i - 1, sampleCount)]).normalized;
            var nextDir = (center[Wrap(i + 1, sampleCount)] - center[i]).normalized;
            var angleDelta = Vector2.Angle(prevDir, nextDir);
            var bisector = (prevDir + nextDir).normalized;
            var prevNormal = new Vector2(-prevDir.y, prevDir.x);
            var denom = Mathf.Abs(Vector2.Dot(bisector, prevNormal));
            var miterLength = denom > 1e-4f ? halfWidth[i] / denom : float.MaxValue;
            highAngleOrMiter[i] = angleDelta >= HighAngleThresholdDeg || miterLength > (miterLimit * halfWidth[i]);
        }

        return new SampledTrackData(center, tangent, normal, halfWidth, layer, left, right, highAngleOrMiter);
    }

    private static Vector2[] ResampleClosed(Vector2[] points, float targetSpacing, out int[] sourceIndex, out float[] sourceT)
    {
        var n = points.Length;
        var segmentLengths = new float[n];
        var cumulative = new float[n + 1];
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            segmentLengths[i] = Vector2.Distance(points[i], points[j]);
            cumulative[i + 1] = cumulative[i] + segmentLengths[i];
        }

        var totalLength = cumulative[n];
        var sampleCount = Mathf.Max(8, Mathf.RoundToInt(totalLength / Mathf.Max(0.05f, targetSpacing)));
        var step = totalLength / sampleCount;

        var result = new Vector2[sampleCount];
        sourceIndex = new int[sampleCount];
        sourceT = new float[sampleCount];
        var seg = 0;
        for (var i = 0; i < sampleCount; i++)
        {
            var distance = i * step;
            while (seg < n - 1 && cumulative[seg + 1] < distance)
            {
                seg++;
            }

            var segLen = Mathf.Max(1e-5f, segmentLengths[seg]);
            var t = Mathf.Clamp01((distance - cumulative[seg]) / segLen);
            var next = (seg + 1) % n;

            result[i] = Vector2.Lerp(points[seg], points[next], t);
            sourceIndex[i] = seg;
            sourceT[i] = t;
        }

        return result;
    }

    private static int Wrap(int idx, int count)
    {
        var wrapped = idx % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }
}

public sealed class TrackEdgeDebugGizmos : MonoBehaviour
{
    private Vector2[] center;
    private Vector2[] normal;
    private bool[] highAngleOrMiter;
    private int normalStride = 8;

    public void Configure(Vector2[] centerPoints, Vector2[] normalVectors, bool[] highPoints, int stride)
    {
        center = centerPoints;
        normal = normalVectors;
        highAngleOrMiter = highPoints;
        normalStride = Mathf.Max(1, stride);
    }

    private void OnDrawGizmos()
    {
        if (center == null || normal == null || center.Length == 0)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        for (var i = 0; i < center.Length; i += normalStride)
        {
            var c = center[i];
            var n = normal[i];
            Gizmos.DrawLine(new Vector3(c.x, c.y, 0f), new Vector3(c.x + (n.x * 0.35f), c.y + (n.y * 0.35f), 0f));
        }

        if (highAngleOrMiter == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        for (var i = 0; i < center.Length && i < highAngleOrMiter.Length; i++)
        {
            if (!highAngleOrMiter[i])
            {
                continue;
            }

            var c = center[i];
            Gizmos.DrawSphere(new Vector3(c.x, c.y, 0f), 0.12f);
        }
    }
}
