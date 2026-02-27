using UnityEngine;

public sealed class MarbleRaceTrackRenderer
{
    private static Material sharedRoadMaterial;
    private static Material sharedBorderMaterial;
    private static Material sharedStartMaterial;

    private GameObject trackRoot;

    public void Apply(SimulationSceneGraph sceneGraph, MarbleRaceTrack track)
    {
        if (sceneGraph == null || track == null || track.SampleCount == 0)
        {
            return;
        }

        var arenaRoot = sceneGraph.WorldRoot != null ? sceneGraph.WorldRoot.Find("ArenaRoot") : null;
        if (arenaRoot == null)
        {
            arenaRoot = sceneGraph.WorldRoot;
        }

        var existing = arenaRoot.Find("TrackRoot");
        if (existing != null)
        {
            Object.Destroy(existing.gameObject);
        }

        trackRoot = new GameObject("TrackRoot");
        trackRoot.transform.SetParent(arenaRoot, false);

        var road = CreateLine("Road", trackRoot.transform, GetRoadMaterial(), new Color(0.12f, 0.12f, 0.14f, 0.96f), 5, true);
        var left = CreateLine("LeftBorder", trackRoot.transform, GetBorderMaterial(), new Color(1f, 0.76f, 0.22f, 0.98f), 10, true);
        var right = CreateLine("RightBorder", trackRoot.transform, GetBorderMaterial(), new Color(1f, 0.76f, 0.22f, 0.98f), 10, true);
        var start = CreateLine("StartLine", trackRoot.transform, GetStartMaterial(), new Color(1f, 1f, 1f, 0.96f), 12, false);
        var center = CreateLine("CenterLine", trackRoot.transform, GetStartMaterial(), new Color(1f, 1f, 1f, 0.35f), 8, true);

        var avgHalf = 0f;
        for (var i = 0; i < track.SampleCount; i++)
        {
            avgHalf += track.HalfWidth[i];
        }

        avgHalf /= track.SampleCount;
        road.widthMultiplier = avgHalf * 2f;
        left.widthMultiplier = avgHalf * 0.12f;
        right.widthMultiplier = avgHalf * 0.12f;
        center.widthMultiplier = avgHalf * 0.04f;
        start.widthMultiplier = avgHalf * 0.20f;

        var c = new Vector3[track.SampleCount];
        var l = new Vector3[track.SampleCount];
        var r = new Vector3[track.SampleCount];
        for (var i = 0; i < track.SampleCount; i++)
        {
            var p = track.Center[i];
            c[i] = new Vector3(p.x, p.y, 0f);
            var n = track.Normal[i] * track.HalfWidth[i];
            l[i] = new Vector3(p.x + n.x, p.y + n.y, 0f);
            r[i] = new Vector3(p.x - n.x, p.y - n.y, 0f);
        }

        road.positionCount = c.Length;
        road.SetPositions(c);
        left.positionCount = l.Length;
        left.SetPositions(l);
        right.positionCount = r.Length;
        right.SetPositions(r);
        center.positionCount = c.Length;
        center.SetPositions(c);

        var s0 = track.Center[0] + (track.Normal[0] * track.HalfWidth[0]);
        var s1 = track.Center[0] - (track.Normal[0] * track.HalfWidth[0]);
        start.positionCount = 2;
        start.SetPosition(0, new Vector3(s0.x, s0.y, 0f));
        start.SetPosition(1, new Vector3(s1.x, s1.y, 0f));
    }

    public void Clear()
    {
        if (trackRoot != null)
        {
            Object.Destroy(trackRoot);
            trackRoot = null;
        }
    }

    private static LineRenderer CreateLine(string name, Transform parent, Material material, Color color, int order, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.material = material;
        lr.textureMode = LineTextureMode.Stretch;
        lr.alignment = LineAlignment.TransformZ;
        lr.loop = loop;
        lr.useWorldSpace = false;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;
        lr.startColor = color;
        lr.endColor = color;
        lr.sortingOrder = order;
        return lr;
    }

    private static Material GetRoadMaterial()
    {
        return sharedRoadMaterial ??= BuildMaterial();
    }

    private static Material GetBorderMaterial()
    {
        return sharedBorderMaterial ??= BuildMaterial();
    }

    private static Material GetStartMaterial()
    {
        return sharedStartMaterial ??= BuildMaterial();
    }

    private static Material BuildMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        return new Material(shader);
    }
}
