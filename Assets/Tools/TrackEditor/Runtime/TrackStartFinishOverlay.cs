using UnityEngine;
using UnityEngine.Rendering;

namespace GSP.TrackEditor
{
    public class TrackStartFinishOverlay : MonoBehaviour
    {
        private const float Epsilon = 0.0001f;
        private const int OverlaySortingOrder = 2000;

        [SerializeField] private Material lineMaterial;

        public void Render(TrackBakedData track)
        {
            if (track == null)
            {
                return;
            }

            EnsureSortingGroup();

            DrawStartFinishStripe(track);
            DrawStartGrid(track);
        }

        private void DrawStartFinishStripe(TrackBakedData track)
        {
            if (track.startFinishDir.sqrMagnitude < Epsilon)
            {
                return;
            }

            var forward = track.startFinishDir.normalized;
            var right = new Vector2(-forward.y, forward.x);
            var width = track.trackWidth;
            var center = track.startFinishPos;
            var halfSpan = right * (width * 0.5f);

            CreateLine(
                "StartFinishStripe",
                new[] { center - halfSpan, center + halfSpan },
                Color.white,
                Mathf.Max(0.2f, width * 0.08f),
                false);
        }

        private void DrawStartGrid(TrackBakedData track)
        {
            if (track.startGridSlots == null || track.startGridSlots.Count == 0)
            {
                return;
            }

            var fallbackForward = track.startFinishDir.sqrMagnitude > Epsilon ? track.startFinishDir.normalized : Vector2.right;
            var boxW = track.trackWidth * 0.28f;
            var boxL = track.trackWidth * 0.40f;

            for (var i = 0; i < track.startGridSlots.Count; i++)
            {
                var slot = track.startGridSlots[i];
                var forward = slot.dir.sqrMagnitude > Epsilon ? slot.dir.normalized : fallbackForward;
                var right = new Vector2(-forward.y, forward.x);

                var halfW = right * (boxW * 0.5f);
                var halfL = forward * (boxL * 0.5f);
                var center = slot.pos;

                var p0 = center - halfW - halfL;
                var p1 = center + halfW - halfL;
                var p2 = center + halfW + halfL;
                var p3 = center - halfW + halfL;

                CreateLine($"GridSlot_{i:00}", new[] { p0, p1, p2, p3 }, Color.yellow, 0.08f * track.trackWidth, true);
            }
        }

        private void EnsureSortingGroup()
        {
            var sortingGroup = GetComponent<SortingGroup>();
            if (sortingGroup == null)
            {
                sortingGroup = gameObject.AddComponent<SortingGroup>();
            }

            sortingGroup.sortingOrder = OverlaySortingOrder;
        }

        private void CreateLine(string objectName, Vector2[] points, Color color, float width, bool loop)
        {
            if (points == null || points.Length < 2)
            {
                return;
            }

            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);

            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = points.Length;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = OverlaySortingOrder;

            for (var i = 0; i < points.Length; i++)
            {
                line.SetPosition(i, new Vector3(points[i].x, points[i].y, 0f));
            }
        }
    }
}
