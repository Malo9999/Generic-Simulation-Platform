using System.Collections.Generic;
using UnityEngine;

public static class AntPixelRig
{
    private static readonly Vector2Int Pivot = new(16, 17);

    public static void Draw(PixelBlueprint2D blueprint, AntPoseLibrary.AntPose pose)
    {
        if (blueprint == null) return;

        ClearLayers(blueprint);

        var center = Pivot + new Vector2Int(pose.bodyShiftX, pose.bodyShiftY);
        var leanY = pose.forwardLeanPx;

        var head = center + new Vector2Int(0, -7 + leanY);
        var thorax = center + new Vector2Int(0, -1 + leanY);
        var abdomen = center + new Vector2Int(0, 7 + leanY);

        FillEllipse(blueprint, "body", abdomen, 5, 6);
        FillEllipse(blueprint, "body", thorax, 4, 4);
        FillEllipse(blueprint, "body", head, 4, 4);

        DrawLegs(blueprint, thorax, pose);
        DrawAntennae(blueprint, head, pose);
        DrawMandibles(blueprint, head, pose.mandibleSpread);
        DrawEyes(blueprint, head);
    }

    private static void ClearLayers(PixelBlueprint2D blueprint)
    {
        blueprint.Clear("body");
        blueprint.Clear("legs");
        blueprint.Clear("antennae");
        blueprint.Clear("mandibles");
        blueprint.Clear("eyes");
        blueprint.Clear("stripe");
    }

    private static void DrawLegs(PixelBlueprint2D bp, Vector2Int thorax, AntPoseLibrary.AntPose pose)
    {
        var anchors = new[]
        {
            thorax + new Vector2Int(-3, -2), thorax + new Vector2Int(-4, 0), thorax + new Vector2Int(-3, 2),
            thorax + new Vector2Int(3, -2), thorax + new Vector2Int(4, 0), thorax + new Vector2Int(3, 2)
        };

        for (var i = 0; i < anchors.Length; i++)
        {
            var side = i < 3 ? -1 : 1;
            var row = i % 3;
            var reach = pose.legReach[i];
            var lift = pose.legLift[i];
            var tip = anchors[i] + new Vector2Int(side * (5 + Mathf.Abs(reach)), row - 1 + lift);
            var knee = anchors[i] + new Vector2Int(side * (2 + Mathf.Abs(reach / 2)), row - 1 + (lift / 2));
            DrawPolyline(bp, "legs", anchors[i], knee, tip);
        }
    }

    private static void DrawAntennae(PixelBlueprint2D bp, Vector2Int head, AntPoseLibrary.AntPose pose)
    {
        var leftRoot = head + new Vector2Int(-1, -3);
        var rightRoot = head + new Vector2Int(1, -3);

        DrawPolyline(bp, "antennae", leftRoot, leftRoot + new Vector2Int(-2, -2 + pose.antennaSweep[0]), leftRoot + new Vector2Int(-4, -4 + pose.antennaSweep[0]));
        DrawPolyline(bp, "antennae", rightRoot, rightRoot + new Vector2Int(2, -2 + pose.antennaSweep[1]), rightRoot + new Vector2Int(4, -4 + pose.antennaSweep[1]));
    }

    private static void DrawMandibles(PixelBlueprint2D bp, Vector2Int head, int spread)
    {
        var left = head + new Vector2Int(-1, 3);
        var right = head + new Vector2Int(1, 3);
        DrawPolyline(bp, "mandibles", left, left + new Vector2Int(-2 - spread, 1));
        DrawPolyline(bp, "mandibles", right, right + new Vector2Int(2 + spread, 1));
    }

    private static void DrawEyes(PixelBlueprint2D bp, Vector2Int head)
    {
        bp.Set("eyes", head.x - 2, head.y - 1, 1);
        bp.Set("eyes", head.x + 2, head.y - 1, 1);
    }

    private static void FillEllipse(PixelBlueprint2D bp, string layer, Vector2Int center, int rx, int ry)
    {
        for (var y = -ry; y <= ry; y++)
        for (var x = -rx; x <= rx; x++)
        {
            var nx = x / (float)Mathf.Max(1, rx);
            var ny = y / (float)Mathf.Max(1, ry);
            if (nx * nx + ny * ny <= 1f)
            {
                bp.Set(layer, center.x + x, center.y + y, 1);
            }
        }
    }

    private static void DrawPolyline(PixelBlueprint2D bp, string layer, params Vector2Int[] points)
    {
        for (var i = 0; i < points.Length - 1; i++)
        {
            foreach (var p in BuildLine(points[i], points[i + 1]))
            {
                bp.Set(layer, p.x, p.y, 1);
            }
        }
    }

    private static IEnumerable<Vector2Int> BuildLine(Vector2Int a, Vector2Int b)
    {
        var x0 = a.x;
        var y0 = a.y;
        var x1 = b.x;
        var y1 = b.y;
        var dx = Mathf.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Mathf.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;

        while (true)
        {
            yield return new Vector2Int(x0, y0);
            if (x0 == x1 && y0 == y1) break;
            var e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }
}
