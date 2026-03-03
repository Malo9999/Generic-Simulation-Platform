using UnityEngine;

namespace GSP.TrackEditor
{
    public class TrackRendererV1 : MonoBehaviour
    {
        private const float Epsilon = 0.0001f;

        [SerializeField] private Material lineMaterial;
        [SerializeField] private bool showStartFinishDebugMarker = true;
        [SerializeField, Min(0.1f)] private float startFinishDebugMarkerSize = 0.6f;

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

            if (data.pitLeftBoundary != null && data.pitLeftBoundary.Length > 1)
            {
                CreateLine("PitBorderLeft", data.pitLeftBoundary, Color.white, 0.2f);
            }

            if (data.pitRightBoundary != null && data.pitRightBoundary.Length > 1)
            {
                CreateLine("PitBorderRight", data.pitRightBoundary, Color.white, 0.2f);
            }

            if (data.startFinishDir.sqrMagnitude > Epsilon)
            {
                var right = new Vector2(-data.startFinishDir.y, data.startFinishDir.x).normalized * (data.trackWidth * 0.5f);
                var startLine = CreateLine("StartLine", new[] { -right, right }, Color.yellow, 0.3f);
                if (startLine != null)
                {
                    startLine.transform.localPosition = new Vector3(data.startFinishPos.x, data.startFinishPos.y, 0f);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    VerifyStartLinePlacement(startLine.transform, data.startFinishPos);
                    if (showStartFinishDebugMarker)
                    {
                        CreateStartFinishDebugMarker(data.startFinishPos);
                    }
#endif
                }
            }
        }

        private GameObject CreateLine(string name, Vector2[] points, Color color, float width)
        {
            if (points == null || points.Length < 2)
            {
                return null;
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

            return go;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void VerifyStartLinePlacement(Transform startLineTransform, Vector2 expectedStartFinishPos)
        {
            var actual = (Vector2)startLineTransform.localPosition;
            var deltaSqr = (actual - expectedStartFinishPos).sqrMagnitude;
            if (deltaSqr > 0.000001f)
            {
                Debug.LogWarning(
                    $"TrackRendererV1[{name}] StartLine mismatch. expected={expectedStartFinishPos}, actual={actual}, delta={Mathf.Sqrt(deltaSqr):F6}");
                return;
            }

            Debug.Log($"TrackRendererV1[{name}] StartLine verified at {actual} (matches TrackBakedData.startFinishPos).");
        }

        private void CreateStartFinishDebugMarker(Vector2 center)
        {
            var half = Vector2.one * (startFinishDebugMarkerSize * 0.5f);
            CreateLine("StartFinishDebugMarkerDiagA", new[] { center - half, center + half }, Color.magenta, 0.08f);
            CreateLine("StartFinishDebugMarkerDiagB", new[] { new Vector2(center.x - half.x, center.y + half.y), new Vector2(center.x + half.x, center.y - half.y) }, Color.magenta, 0.08f);
        }
#endif
    }
}
