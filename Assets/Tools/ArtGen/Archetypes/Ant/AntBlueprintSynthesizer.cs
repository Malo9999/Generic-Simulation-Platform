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
        var head = Mathf.Max(2, Mathf.RoundToInt(4 * profile.headScale * (req.role == "soldier" ? profile.soldierHeadMultiplier : 1f)));
        var thorax = Mathf.Max(2, Mathf.RoundToInt(4 * profile.thoraxScale));
        var abdomenScale = profile.abdomenScale * (req.role == "queen" ? profile.queenAbdomenMultiplier : 1f);
        var abdomen = Mathf.Max(3, Mathf.RoundToInt(6 * abdomenScale));

        var stride = req.state == "run" ? 2 : req.state == "walk" ? 1 : 0;
        var legPhase = req.frameIndex % 2 == 0 ? -1 : 1;
        var thoraxCx = Mathf.RoundToInt(bp.width * 0.56f) + (req.state == "run" ? 1 : 0);
        var thoraxCy = Mathf.RoundToInt(bp.height * 0.52f);
        var headCx = thoraxCx + Mathf.Max(4, thorax + 2);
        var abdomenCx = thoraxCx - Mathf.Max(5, abdomen - 1);
        var headRy = Mathf.Max(2, head - 1);
        var thoraxRy = Mathf.Max(2, thorax - 1);
        var abdomenRy = Mathf.Max(2, abdomen - 1);

        // 3-segment body.
        Oval(bp, "body", abdomenCx, thoraxCy, abdomen, abdomenRy);
        Oval(bp, "body", thoraxCx, thoraxCy, thorax, thoraxRy);
        Oval(bp, "body", headCx, thoraxCy, head, headRy);

        // Carve a visible waist/petiole pinch and leave a 1px bridge.
        var pinchX = Mathf.RoundToInt((abdomenCx + thoraxCx) * 0.5f);
        var pinchHalfHeight = Mathf.Max(2, Mathf.RoundToInt(3f / Mathf.Max(0.3f, profile.petiolePinchStrength)));
        for (var y = thoraxCy - pinchHalfHeight; y <= thoraxCy + pinchHalfHeight; y++)
        {
            if (Mathf.Abs(y - thoraxCy) <= 0) continue;
            bp.Set("body", pinchX, y, 0);
            bp.Set("body", pinchX + 1, y, 0);
        }

        const int appendageThickness = 2;
        var legLen = Mathf.Max(3, Mathf.RoundToInt(5 * profile.legLengthScale));
        DrawThickLine(bp, "body", thoraxCx + 1, thoraxCy - 1, thoraxCx + legLen, thoraxCy - (3 + stride) + legPhase, appendageThickness); // front upper
        DrawThickLine(bp, "body", thoraxCx + 1, thoraxCy + 1, thoraxCx + legLen, thoraxCy + (3 + stride) - legPhase, appendageThickness); // front lower
        DrawThickLine(bp, "body", thoraxCx, thoraxCy - 1, thoraxCx + (legLen - 1), thoraxCy - 1 + legPhase, appendageThickness); // mid upper
        DrawThickLine(bp, "body", thoraxCx, thoraxCy + 1, thoraxCx + (legLen - 1), thoraxCy + 1 - legPhase, appendageThickness); // mid lower
        DrawThickLine(bp, "body", thoraxCx - 1, thoraxCy + 1, thoraxCx - legLen, thoraxCy + 3 + legPhase, appendageThickness); // rear lower
        DrawThickLine(bp, "body", thoraxCx - 1, thoraxCy - 1, thoraxCx - legLen, thoraxCy - 3 - legPhase, appendageThickness); // rear upper

        var antLen = Mathf.Max(2, Mathf.RoundToInt(4 * profile.antennaLengthScale));
        var antennaStartX = headCx + Mathf.Max(1, head - 1);
        DrawThickLine(bp, "body", antennaStartX, thoraxCy - 2, antennaStartX + antLen, thoraxCy - 5 + legPhase, appendageThickness);
        DrawThickLine(bp, "body", antennaStartX, thoraxCy + 1, antennaStartX + antLen, thoraxCy + 4 - legPhase, appendageThickness);

        if (req.role == "soldier")
        {
            var mand = Mathf.Max(2, Mathf.RoundToInt(3 * profile.soldierMandibleMultiplier));
            DrawThickLine(bp, "body", headCx + head, thoraxCy - 1, headCx + head + mand, thoraxCy - 2, appendageThickness);
            DrawThickLine(bp, "body", headCx + head, thoraxCy + 1, headCx + head + mand, thoraxCy + 2, appendageThickness);
        }

        // Abdomen-only curved stripe mask band, constrained to rear/mid abdomen.
        var bandHalfHeight = 1;
        for (var y = thoraxCy - abdomenRy; y <= thoraxCy + abdomenRy; y++)
        {
            for (var x = abdomenCx - abdomen; x <= abdomenCx + abdomen; x++)
            {
                if (!IsInsideEllipse(x, y, abdomenCx, thoraxCy, abdomen, abdomenRy))
                {
                    continue;
                }

                if (Mathf.Abs(y - thoraxCy) > bandHalfHeight)
                {
                    continue;
                }

                if (x > abdomenCx)
                {
                    continue;
                }

                bp.Set("stripe", x, y, 1);
            }
        }
    }

    private static bool IsInsideEllipse(int x, int y, int cx, int cy, int rx, int ry)
    {
        var nx = (x - cx) / (float)Mathf.Max(1, rx);
        var ny = (y - cy) / (float)Mathf.Max(1, ry);
        return nx * nx + ny * ny <= 1f;
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
