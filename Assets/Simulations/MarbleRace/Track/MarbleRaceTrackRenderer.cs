using UnityEngine;

public sealed class MarbleRaceTrackRenderer
{
    private static readonly string[] RequiredChildNames =
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
        if (decorRoot == null || track == null || track.SampleCount <= 3)
        {
            return;
        }

        trackRoot = EnsureTrackRoot(decorRoot);
        CleanupUnexpectedTrackRootChildren();

        var avgHalfWidth = 0f;
        for (var i = 0; i < track.SampleCount; i++)
        {
            avgHalfWidth += track.HalfWidth[i];
        }

        avgHalfWidth /= track.SampleCount;

        var roadWidth = Mathf.Clamp(avgHalfWidth * 2f, 2.0f, 5.0f);
        var borderWidth = Mathf.Clamp(roadWidth * 0.10f, 0.18f, 0.32f);
        var startWidth = Mathf.Clamp(roadWidth * 0.18f, 0.25f, 0.45f);

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

        var lane = EnsureLineRenderer("TrackLane", new Color(0.12f, 0.12f, 0.14f, 0.95f), 5, true, roadWidth);
        lane.positionCount = centerPoints.Length;
        lane.SetPositions(centerPoints);

        var innerBorder = EnsureLineRenderer("TrackInnerBorder", new Color(0.90f, 0.90f, 0.92f, 0.95f), 10, true, borderWidth);
        innerBorder.positionCount = innerPoints.Length;
        innerBorder.SetPositions(innerPoints);

        var outerBorder = EnsureLineRenderer("TrackOuterBorder", new Color(0.90f, 0.90f, 0.92f, 0.95f), 10, true, borderWidth);
        outerBorder.positionCount = outerPoints.Length;
        outerBorder.SetPositions(outerPoints);

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

    private void CleanupUnexpectedTrackRootChildren()
    {
        for (var i = trackRoot.childCount - 1; i >= 0; i--)
        {
            var child = trackRoot.GetChild(i);
            if (!IsRequiredChildName(child.name))
            {
                Object.Destroy(child.gameObject);
            }
        }
    }

    private static bool IsRequiredChildName(string name)
    {
        for (var i = 0; i < RequiredChildNames.Length; i++)
        {
            if (RequiredChildNames[i] == name)
            {
                return true;
            }
        }

        return false;
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
}
