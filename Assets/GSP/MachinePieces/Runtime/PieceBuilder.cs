using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PieceBuilder
{
    private readonly SurfaceProfileResolver surfaceResolver = new();

    public BuiltPieceRuntime BuildPiece(Transform parent, PieceSpec spec, PieceInstance instance, MachinePieceLibrary lib, bool showDebug)
    {
        var go = new GameObject($"Piece_{instance.instanceId}_{spec.id}");
        go.transform.SetParent(parent, false);
        ApplyTransform(go.transform, instance.transform);

        var resolvedSurface = surfaceResolver.ResolveSurface(spec, instance, lib);
        var runtime = new BuiltPieceRuntime
        {
            InstanceId = instance.instanceId,
            PieceId = instance.pieceId,
            Spec = spec,
            Instance = instance,
            Surface = resolvedSurface,
            Root = go
        };

        BuildVisual(go, spec.shape, resolvedSurface);
        runtime.Collider = BuildCollider(go, spec.shape, spec.collision);
        BuildAnchors(go, spec.anchors, runtime);

        var mover = go.AddComponent<MachinePieceMechanicsDriver>();
        mover.Initialize(runtime);

        if (showDebug)
        {
            var debug = go.AddComponent<MachinePieceDebugGizmos>();
            debug.Initialize(runtime);
        }

        var defaults = instance.stateDefaults?.ToDictionary() ?? new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in defaults)
        {
            runtime.RuntimeState[kv.Key] = kv.Value;
        }

        return runtime;
    }

    private static void ApplyTransform(Transform transform, PieceTransform t)
    {
        var resolved = t ?? new PieceTransform { scale = new SerializableVector2 { x = 1f, y = 1f } };
        transform.localPosition = new Vector3(resolved.position.x, resolved.position.y, 0f);
        transform.localRotation = Quaternion.Euler(0f, 0f, resolved.rotation);
        var sx = Mathf.Approximately(resolved.scale.x, 0f) ? 1f : resolved.scale.x;
        var sy = Mathf.Approximately(resolved.scale.y, 0f) ? 1f : resolved.scale.y;
        transform.localScale = new Vector3(sx, sy, 1f);
    }

    private Material BuildVisual(GameObject go, PieceShape shape, SurfaceProfile surface)
    {
        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        var spriteRenderer = visual.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = CreateUnitSprite();

        if (shape != null)
        {
            if (shape.kind == "box")
            {
                visual.transform.localScale = new Vector3(shape.size.x, shape.size.y, 1f);
            }
            else if (shape.kind == "polygon")
            {
                var bounds = ComputeBounds(shape.points);
                visual.transform.localScale = new Vector3(Mathf.Max(0.1f, bounds.size.x), Mathf.Max(0.1f, bounds.size.y), 1f);
                visual.transform.localPosition = bounds.center;
            }
            else if (shape.kind == "segment")
            {
                var a = shape.start.ToVector2();
                var b = shape.end.ToVector2();
                var delta = b - a;
                visual.transform.localScale = new Vector3(Mathf.Max(0.1f, delta.magnitude), 0.1f, 1f);
                visual.transform.localPosition = (a + b) * 0.5f;
                visual.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
        }

        var material = surfaceResolver.BuildMaterial(surface);
        spriteRenderer.sharedMaterial = material;
        return material;
    }

    private static Sprite CreateUnitSprite()
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
    }

    private static Bounds ComputeBounds(SerializableVector2[] points)
    {
        if (points == null || points.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        var min = points[0].ToVector2();
        var max = min;
        for (var i = 1; i < points.Length; i++)
        {
            var p = points[i].ToVector2();
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        return new Bounds((min + max) * 0.5f, max - min);
    }

    private static Collider2D BuildCollider(GameObject go, PieceShape shape, PieceCollision collision)
    {
        if (collision != null && !collision.enabled)
        {
            return null;
        }

        Collider2D col;
        switch (shape.kind)
        {
            case "box":
                var box = go.AddComponent<BoxCollider2D>();
                box.size = shape.size.ToVector2();
                col = box;
                break;
            case "polygon":
                var poly = go.AddComponent<PolygonCollider2D>();
                var points = new Vector2[shape.points.Length];
                for (var i = 0; i < points.Length; i++) points[i] = shape.points[i].ToVector2();
                poly.points = points;
                col = poly;
                break;
            default:
                var edge = go.AddComponent<EdgeCollider2D>();
                edge.points = new[] { shape.start.ToVector2(), shape.end.ToVector2() };
                col = edge;
                break;
        }

        col.isTrigger = collision != null && collision.isTrigger;
        return col;
    }

    private static void BuildAnchors(GameObject go, PieceAnchor[] anchors, BuiltPieceRuntime runtime)
    {
        foreach (var anchor in anchors ?? Array.Empty<PieceAnchor>())
        {
            var anchorGo = new GameObject($"Anchor_{anchor.id}");
            anchorGo.transform.SetParent(go.transform, false);
            anchorGo.transform.localPosition = new Vector3(anchor.position.x, anchor.position.y, 0f);
            anchorGo.transform.localRotation = Quaternion.Euler(0f, 0f, anchor.angle);
            runtime.Anchors[anchor.id] = anchorGo.transform;
        }
    }
}
