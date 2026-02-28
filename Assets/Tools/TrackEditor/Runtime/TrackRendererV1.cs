using UnityEngine;

public class TrackRendererV1 : MonoBehaviour
{
    [SerializeField] private Material lineMaterial;

    public void Render(TrackBakedData data)
    {
        if (data == null)
        {
            return;
        }

        CreateLine("RoadCenter", data.mainCenterline, new Color(0.2f, 0.2f, 0.2f), data.trackWidth);
        CreateLine("BorderLeft", data.mainLeftBoundary, Color.white, 0.2f);
        CreateLine("BorderRight", data.mainRightBoundary, Color.white, 0.2f);

        if (data.pitCenterline != null && data.pitCenterline.Length > 1)
        {
            CreateLine("PitCenter", data.pitCenterline, new Color(0.35f, 0.35f, 0.35f), data.trackWidth * 0.6f);
        }

        if (data.startGridSlots != null && data.startGridSlots.Count > 0)
        {
            var slot = data.startGridSlots[0];
            var p = slot.pos;
            var right = new Vector2(-slot.dir.y, slot.dir.x).normalized * (data.trackWidth * 0.5f);
            CreateLine("StartLine", new[] { p - right, p + right }, Color.yellow, 0.3f);
        }
    }

    private void CreateLine(string name, Vector2[] points, Color color, float width)
    {
        if (points == null || points.Length < 2)
        {
            return;
        }

        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.positionCount = points.Length;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.numCapVertices = 2;
        lr.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        for (var i = 0; i < points.Length; i++)
        {
            lr.SetPosition(i, new Vector3(points[i].x, points[i].y, 0f));
        }
    }
}
