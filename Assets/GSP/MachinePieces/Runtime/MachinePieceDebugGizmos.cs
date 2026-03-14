using System;
using UnityEngine;

public sealed class MachinePieceDebugGizmos : MonoBehaviour
{
    private BuiltPieceRuntime runtime;

    public void Initialize(BuiltPieceRuntime built)
    {
        runtime = built;
    }

    private void OnDrawGizmos()
    {
        if (runtime?.Spec?.shape == null)
        {
            return;
        }

        Gizmos.color = new Color(0f, 1f, 0.7f, 0.8f);
        DrawShape(runtime.Spec.shape);

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        foreach (var anchor in runtime.Anchors)
        {
            if (anchor.Value == null) continue;
            Gizmos.DrawSphere(anchor.Value.position, 0.08f);
            var dir = anchor.Value.right * 0.4f;
            Gizmos.DrawLine(anchor.Value.position, anchor.Value.position + dir);
        }

        if (runtime.Spec.pieceType == "Bin")
        {
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.6f);
            var captureRadius = 0.5f;
            var payload = runtime.Spec.mechanics?.payload?.ToDictionary();
            if (payload != null && payload.TryGetValue("captureRadius", out var radiusText) && float.TryParse(radiusText, out var parsed))
            {
                captureRadius = Mathf.Max(0.1f, parsed);
            }

            Gizmos.DrawWireSphere(transform.position, captureRadius);
        }
    }

    private void DrawShape(PieceShape shape)
    {
        switch (shape.kind)
        {
            case "box":
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(shape.size.x, shape.size.y, 0f));
                Gizmos.matrix = Matrix4x4.identity;
                break;
            case "polygon":
                var points = shape.points ?? Array.Empty<SerializableVector2>();
                for (var i = 0; i < points.Length; i++)
                {
                    var a = transform.TransformPoint(points[i].x, points[i].y, 0f);
                    var bData = points[(i + 1) % points.Length];
                    var b = transform.TransformPoint(bData.x, bData.y, 0f);
                    Gizmos.DrawLine(a, b);
                }
                break;
            case "segment":
                var s = transform.TransformPoint(shape.start.x, shape.start.y, 0f);
                var e = transform.TransformPoint(shape.end.x, shape.end.y, 0f);
                Gizmos.DrawLine(s, e);
                break;
        }
    }
}
