using UnityEngine;

public sealed class MarbleRaceTrackRenderer
{
    private static Material sharedMaterial;

    private GameObject trackRoot;

    public void Apply(Transform arenaRoot, MarbleRaceTrack track)
    {
        if (arenaRoot == null || track == null || track.SampleCount <= 0)
        {
            return;
        }

        var existing = arenaRoot.Find("TrackRoot");
        if (existing != null)
        {
            Object.Destroy(existing.gameObject);
        }

        trackRoot = new GameObject("TrackRoot");
        trackRoot.transform.SetParent(arenaRoot, false);

        var avgHalfWidth = 0f;
        for (var i = 0; i < track.SampleCount; i++)
        {
            avgHalfWidth += track.HalfWidth[i];
        }

        avgHalfWidth /= track.SampleCount;

        var road = CreateLine("Road", trackRoot.transform, new Color(0.1f, 0.1f, 0.12f, 0.9f), 5, true, avgHalfWidth * 2f);
        var leftBorder = CreateLine("LeftBorder", trackRoot.transform, new Color(1f, 0.86f, 0.28f, 0.98f), 10, true, avgHalfWidth * 0.12f);
        var rightBorder = CreateLine("RightBorder", trackRoot.transform, new Color(1f, 0.86f, 0.28f, 0.98f), 10, true, avgHalfWidth * 0.12f);
        var startLine = CreateLine("StartLine", trackRoot.transform, new Color(1f, 1f, 1f, 0.98f), 12, false, avgHalfWidth * 0.22f);

        var centerPoints = new Vector3[track.SampleCount];
        var leftPoints = new Vector3[track.SampleCount];
        var rightPoints = new Vector3[track.SampleCount];
        for (var i = 0; i < track.SampleCount; i++)
        {
            var center = track.Center[i];
            var boundaryOffset = track.Normal[i] * track.HalfWidth[i];
            centerPoints[i] = new Vector3(center.x, center.y, 0f);
            leftPoints[i] = new Vector3(center.x + boundaryOffset.x, center.y + boundaryOffset.y, 0f);
            rightPoints[i] = new Vector3(center.x - boundaryOffset.x, center.y - boundaryOffset.y, 0f);
        }

        road.positionCount = centerPoints.Length;
        road.SetPositions(centerPoints);

        leftBorder.positionCount = leftPoints.Length;
        leftBorder.SetPositions(leftPoints);

        rightBorder.positionCount = rightPoints.Length;
        rightBorder.SetPositions(rightPoints);

        var startA = track.Center[0] + (track.Normal[0] * track.HalfWidth[0]);
        var startB = track.Center[0] - (track.Normal[0] * track.HalfWidth[0]);
        startLine.positionCount = 2;
        startLine.SetPosition(0, new Vector3(startA.x, startA.y, 0f));
        startLine.SetPosition(1, new Vector3(startB.x, startB.y, 0f));
    }

    public void Clear()
    {
        if (trackRoot != null)
        {
            Object.Destroy(trackRoot);
            trackRoot = null;
        }
    }

    private static LineRenderer CreateLine(string name, Transform parent, Color color, int sortingOrder, bool loop, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.material = GetSharedMaterial();
        lr.loop = loop;
        lr.useWorldSpace = false;
        lr.alignment = LineAlignment.TransformZ;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.widthMultiplier = width;
        lr.startColor = color;
        lr.endColor = color;
        lr.sortingOrder = sortingOrder;
        return lr;
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
