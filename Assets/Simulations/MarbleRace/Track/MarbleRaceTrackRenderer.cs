using UnityEngine;

public sealed class MarbleRaceTrackRenderer
{
    private static readonly string[] RequiredNames =
    {
        "TrackLane",
        "TrackInnerBorder",
        "TrackOuterBorder",
        "StartFinishLine"
    };

    private static Material sharedMaterial;

    private Transform trackRoot;

    public void Apply(Transform decorRoot, MarbleRaceTrack track)
    {
        if (decorRoot == null || track == null || track.SampleCount <= 0)
        {
            return;
        }

        trackRoot = EnsureTrackRoot(decorRoot);

        var avgHalfWidth = 0f;
        for (var i = 0; i < track.SampleCount; i++)
        {
            avgHalfWidth += track.HalfWidth[i];
        }

        avgHalfWidth /= track.SampleCount;

        var lane = EnsureLineRenderer("TrackLane", new Color(0.12f, 0.12f, 0.14f, 0.96f), 5, true, avgHalfWidth * 2f);
        var innerBorder = EnsureLineRenderer("TrackInnerBorder", new Color(0.93f, 0.93f, 0.9f, 0.98f), 10, true, Mathf.Max(0.08f, avgHalfWidth * 0.12f));
        var outerBorder = EnsureLineRenderer("TrackOuterBorder", new Color(0.93f, 0.93f, 0.9f, 0.98f), 10, true, Mathf.Max(0.08f, avgHalfWidth * 0.12f));
        var startFinish = EnsureLineRenderer("StartFinishLine", new Color(1f, 1f, 1f, 0.98f), 12, false, Mathf.Max(0.12f, avgHalfWidth * 0.24f));

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

        lane.positionCount = centerPoints.Length;
        lane.SetPositions(centerPoints);

        innerBorder.positionCount = innerPoints.Length;
        innerBorder.SetPositions(innerPoints);

        outerBorder.positionCount = outerPoints.Length;
        outerBorder.SetPositions(outerPoints);

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

    private Transform EnsureTrackRoot(Transform decorRoot)
    {
        var existing = decorRoot.Find("TrackRoot");
        if (existing == null)
        {
            var go = new GameObject("TrackRoot");
            existing = go.transform;
            existing.SetParent(decorRoot, false);
        }

        for (var i = existing.childCount - 1; i >= 0; i--)
        {
            var child = existing.GetChild(i);
            if (!IsRequiredName(child.name))
            {
                Object.Destroy(child.gameObject);
                continue;
            }

            var first = existing.Find(child.name);
            if (first != child)
            {
                Object.Destroy(child.gameObject);
            }
        }

        return existing;
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

        var lr = lineTransform.GetComponent<LineRenderer>();
        if (lr == null)
        {
            lr = lineTransform.gameObject.AddComponent<LineRenderer>();
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
