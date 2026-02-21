using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AntBlueprintSynthesizer
{
    public static (PixelBlueprint2D body, PixelBlueprint2D stripe) Build(AntSpeciesProfile profile, ArchetypeSynthesisRequest req)
    {
        var body = AssetDatabase.LoadAssetAtPath<PixelBlueprint2D>(req.blueprintPath);
        if (body == null)
        {
            body = ScriptableObject.CreateInstance<PixelBlueprint2D>();
            body.width = 32;
            body.height = 32;
            AssetDatabase.CreateAsset(body, req.blueprintPath);
        }

        body.Clear("body");
        body.Clear("stripe");

        if (req.stage == "egg")
        {
            Oval(body, "body", 16, 16, 6, 4);
        }
        else if (req.stage == "larva")
        {
            for (var i = 0; i < 5; i++) Oval(body, "body", 10 + i * 3, 14 + (i % 2), 3, 2);
        }
        else if (req.stage == "pupa")
        {
            Oval(body, "body", 16, 16, 8, 4);
        }
        else
        {
            DrawAdult(body, profile, req);
        }

        EditorUtility.SetDirty(body);
        return (body, body);
    }

    private static void DrawAdult(PixelBlueprint2D bp, AntSpeciesProfile profile, ArchetypeSynthesisRequest req)
    {
        var head = Mathf.RoundToInt(4 * profile.headScale * (req.role == "soldier" ? profile.soldierHeadMultiplier : 1f));
        var thorax = Mathf.RoundToInt(4 * profile.thoraxScale);
        var abdomenScale = profile.abdomenScale * (req.role == "queen" ? profile.queenAbdomenMultiplier : 1f);
        var abdomen = Mathf.RoundToInt(6 * abdomenScale);

        var stride = req.state == "run" ? 2 : req.state == "walk" ? 1 : 0;
        var legPhase = req.frameIndex % 2 == 0 ? -1 : 1;
        var cx = 16 + (req.state == "run" ? 1 : 0);

        Oval(bp, "body", cx - 8, 16, head, head - 1);
        Oval(bp, "body", cx, 16, thorax, thorax - 1);
        Oval(bp, "body", cx + 9, 16, abdomen, abdomen - 1);
        // petiole pinch
        Rect(bp, "body", cx + 4, 15, Mathf.Max(1, Mathf.RoundToInt(2f / Mathf.Max(0.2f, profile.petiolePinchStrength))), 2);

        const int appendageThickness = 2;
        var legLen = Mathf.RoundToInt(6 * profile.legLengthScale);
        for (var i = -1; i <= 1; i++)
        {
            DrawThickLine(bp, "body", cx - 1, 16 + i, cx - 1 - legLen, 16 + i * 2 + legPhase * stride, appendageThickness);
            DrawThickLine(bp, "body", cx + 1, 16 + i, cx + 1 + legLen, 16 + i * 2 - legPhase * stride, appendageThickness);
        }

        var antLen = Mathf.RoundToInt(5 * profile.antennaLengthScale);
        DrawThickLine(bp, "body", cx - 10, 15, cx - 10 - antLen, 12 + legPhase, appendageThickness);
        DrawThickLine(bp, "body", cx - 10, 17, cx - 10 - antLen, 20 - legPhase, appendageThickness);

        if (req.role == "soldier")
        {
            var mand = Mathf.RoundToInt(3 * profile.soldierMandibleMultiplier);
            DrawThickLine(bp, "body", cx - 12, 15, cx - 12 - mand, 14, appendageThickness);
            DrawThickLine(bp, "body", cx - 12, 17, cx - 12 - mand, 18, appendageThickness);
        }

        for (var x = cx + 6; x <= cx + 11; x++)
        {
            bp.Set("stripe", x, 15, 1);
            bp.Set("stripe", x, 16, 1);
            bp.Set("stripe", x, 17, 1);
        }
    }

    private static void Rect(PixelBlueprint2D bp, string layer, int x, int y, int w, int h)
    {
        for (var yy = 0; yy < h; yy++)
        for (var xx = 0; xx < w; xx++) bp.Set(layer, x + xx, y + yy, 1);
    }

    private static void Oval(PixelBlueprint2D bp, string layer, int cx, int cy, int rx, int ry)
    {
        for (var y = -ry; y <= ry; y++)
        for (var x = -rx; x <= rx; x++)
        {
            var nx = x / (float)Mathf.Max(1, rx);
            var ny = y / (float)Mathf.Max(1, ry);
            if (nx * nx + ny * ny <= 1f) bp.Set(layer, cx + x, cy + y, 1);
        }
    }

    private static void DrawThickLine(PixelBlueprint2D bp, string layer, int x0, int y0, int x1, int y1, int thickness)
    {
        DrawThickStroke(bp, layer, BuildLinePoints(x0, y0, x1, y1), thickness);
    }

    private static void DrawThickStroke(PixelBlueprint2D bp, string layer, List<Vector2Int> points, int thickness)
    {
        if (points == null || points.Count == 0) return;

        var minOffset = -(thickness / 2);
        var maxOffset = (thickness - 1) / 2;
        foreach (var point in points)
        {
            for (var dy = minOffset; dy <= maxOffset; dy++)
            for (var dx = minOffset; dx <= maxOffset; dx++) bp.Set(layer, point.x + dx, point.y + dy, 1);
        }

        StampPoint(bp, layer, points[0], minOffset, maxOffset);
        StampPoint(bp, layer, points[points.Count - 1], minOffset, maxOffset);
    }

    private static void StampPoint(PixelBlueprint2D bp, string layer, Vector2Int point, int minOffset, int maxOffset)
    {
        for (var dy = minOffset; dy <= maxOffset; dy++)
        for (var dx = minOffset; dx <= maxOffset; dx++) bp.Set(layer, point.x + dx, point.y + dy, 1);
    }

    private static List<Vector2Int> BuildLinePoints(int x0, int y0, int x1, int y1)
    {
        var points = new List<Vector2Int>();
        var dx = Mathf.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Mathf.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;
        while (true)
        {
            points.Add(new Vector2Int(x0, y0));
            if (x0 == x1 && y0 == y1) break;
            var e2 = err * 2;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }


        return points;
    }
}
