using System.Collections.Generic;
using UnityEngine;

public sealed class MarbleRaceTrackRenderer
{
    private static Material sharedMaterial;
    private Transform trackRoot;

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

        BuildLayeredLines(track, roadWidth, borderWidth);
        BuildBridgeShadow(track, roadWidth);

        var startFinish = EnsureLineRenderer("StartFinishLine", Color.white, 12, false, startWidth);
        var startA = track.Center[0] + (track.Normal[0] * track.HalfWidth[0]);
        var startB = track.Center[0] - (track.Normal[0] * track.HalfWidth[0]);
        startFinish.positionCount = 2;
        startFinish.SetPosition(0, new Vector3(startA.x, startA.y, 0f));
        startFinish.SetPosition(1, new Vector3(startB.x, startB.y, 0f));
    }

    public void Clear()
    {
        if (trackRoot != null)
        {
            Object.Destroy(trackRoot.gameObject);
            trackRoot = null;
        }
    }

    private void BuildLayeredLines(MarbleRaceTrack track, float roadWidth, float borderWidth)
    {
        var runs = BuildLayerRuns(track.Layer);
        if (runs.Count == 0)
        {
            runs.Add(new LayerRun(0, track.SampleCount - 1, 0));
        }

        var laneGround = EnsureLineRenderer("TrackLane_Ground", new Color(0.12f, 0.12f, 0.14f, 0.95f), 5, false, roadWidth);
        var laneBridge = EnsureLineRenderer("TrackLane_Bridge", new Color(0.13f, 0.13f, 0.15f, 0.98f), 15, false, roadWidth);
        var innerGround = EnsureLineRenderer("TrackInnerBorder_Ground", new Color(0.90f, 0.90f, 0.92f, 0.95f), 10, false, borderWidth);
        var innerBridge = EnsureLineRenderer("TrackInnerBorder_Bridge", new Color(0.93f, 0.93f, 0.95f, 1f), 20, false, borderWidth);
        var outerGround = EnsureLineRenderer("TrackOuterBorder_Ground", new Color(0.90f, 0.90f, 0.92f, 0.95f), 10, false, borderWidth);
        var outerBridge = EnsureLineRenderer("TrackOuterBorder_Bridge", new Color(0.93f, 0.93f, 0.95f, 1f), 20, false, borderWidth);

        SetLineFromRuns(track, laneGround, runs, 0, false);
        SetLineFromRuns(track, laneBridge, runs, 1, false);
        SetLineFromRuns(track, innerGround, runs, 0, true);
        SetLineFromRuns(track, innerBridge, runs, 1, true);
        SetLineFromRuns(track, outerGround, runs, 0, true, -1f);
        SetLineFromRuns(track, outerBridge, runs, 1, true, -1f);
    }

    private void BuildBridgeShadow(MarbleRaceTrack track, float roadWidth)
    {
        var shadow = EnsureLineRenderer("TrackBridgeShadow", new Color(0f, 0f, 0f, 0.3f), 14, false, roadWidth * 1.2f);
        var runs = BuildLayerRuns(track.Layer);
        SetLineFromRuns(track, shadow, runs, 1, false);
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

    private static void SetLineFromRuns(MarbleRaceTrack track, LineRenderer lr, List<LayerRun> runs, int targetLayer, bool border, float side = 1f)
    {
        var points = new List<Vector3>(track.SampleCount + 8);
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
                var idx = (run.Start + o) % track.SampleCount;
                var p = track.Center[idx];
                if (border)
                {
                    var boundary = track.Normal[idx] * track.HalfWidth[idx] * side;
                    p += boundary;
                }

                points.Add(new Vector3(p.x, p.y, 0f));
            }
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
                Object.Destroy(child.gameObject);
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
}
