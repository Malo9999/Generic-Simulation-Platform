using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AntBlueprintSynthesizer
{
    public static (PixelBlueprint2D body, PixelBlueprint2D stripe) Build(AntSpeciesProfile profile, ArchetypeSynthesisRequest req)
    {
        var useAssetBackedBlueprint = !string.IsNullOrWhiteSpace(req.blueprintPath);
        PixelBlueprint2D body;

        if (useAssetBackedBlueprint)
        {
            body = AssetDatabase.LoadAssetAtPath<PixelBlueprint2D>(req.blueprintPath);
            if (body == null)
            {
                body = ScriptableObject.CreateInstance<PixelBlueprint2D>();
                body.width = 32;
                body.height = 32;
                AssetDatabase.CreateAsset(body, req.blueprintPath);
            }
        }
        else
        {
            body = ScriptableObject.CreateInstance<PixelBlueprint2D>();
            body.width = 32;
            body.height = 32;
        }

        body.Clear("body");
        body.Clear("stripe");

        if (req.stage == "egg") Oval(body, "body", 16, 16, 6, 4);
        else if (req.stage == "larva") { for (var i = 0; i < 5; i++) Oval(body, "body", 10 + i * 3, 14 + (i % 2), 3, 2); }
        else if (req.stage == "pupa") Oval(body, "body", 16, 16, 8, 4);
        else DrawAdult(body, profile, req);

        if (useAssetBackedBlueprint) EditorUtility.SetDirty(body);
        return (body, body);
    }

    private static void DrawAdult(PixelBlueprint2D bp, AntSpeciesProfile profile, ArchetypeSynthesisRequest req)
    {
        var model = profile != null && profile.hasFittedModel && profile.fittedModel != null ? profile.fittedModel : GoldenModel();
        if (!(profile != null && profile.hasFittedModel && profile.fittedModel != null)) Debug.LogWarning($"[AntBlueprint] Using golden ant fallback for species '{profile?.speciesId}'.");

        var gait = req.state == "run" ? 2f : req.state == "walk" ? 1f : 0.4f;
        var phase = req.frameIndex % 2 == 0 ? -1f : 1f;
        if (req.state == "idle") phase *= 0.5f;

        DrawEllipse01(bp, "body", model.abdomenCenter01, model.abdomenRadii01);
        DrawEllipse01(bp, "body", model.thoraxCenter01, model.thoraxRadii01);
        DrawEllipse01(bp, "body", model.headCenter01, model.headRadii01 * (req.role == "soldier" ? profile.soldierHeadMultiplier : 1f));

        for (var i = 0; i < 6; i++)
        {
            var angle = model.legAnglesDeg[i] + (req.state is "walk" or "run" ? phase * gait * (i % 2 == 0 ? 8f : -8f) : 0f);
            var a = ToPx(bp, model.legAnchors01[i]);
            var b = a + Dir(angle) * Mathf.RoundToInt(model.legLengths01[i] * bp.width * (req.state == "run" ? 1.12f : 1f));
            DrawThickLine(bp, "body", a.x, a.y, b.x, b.y, 2);
        }

        for (var i = 0; i < 2; i++)
        {
            var wiggle = req.state == "idle" ? phase * 10f : 0f;
            if (req.state == "fight") wiggle += (i == 0 ? -8f : 8f);
            var angle = model.antennaAnglesDeg[i] + wiggle;
            var a = ToPx(bp, model.antennaAnchors01[i]);
            var b = a + Dir(angle) * Mathf.RoundToInt(model.antennaLen01 * bp.width);
            DrawThickLine(bp, "body", a.x, a.y, b.x, b.y, 2);
        }

        if (req.state == "fight")
        {
            var hc = ToPx(bp, model.headCenter01);
            var hr = Mathf.RoundToInt(model.headRadii01.x * bp.width);
            DrawThickLine(bp, "body", hc.x - 1, hc.y - 1, hc.x - hr - 1, hc.y - 3, 2);
            DrawThickLine(bp, "body", hc.x + 1, hc.y - 1, hc.x + hr + 1, hc.y - 3, 2);
        }

        DrawAbdomenStripe(bp, model);
    }

    private static void DrawAbdomenStripe(PixelBlueprint2D bp, AntTopdownModel model)
    {
        var c = ToPx(bp, model.abdomenCenter01);
        var rx = Mathf.Max(2, Mathf.RoundToInt(model.abdomenRadii01.x * bp.width));
        var ry = Mathf.Max(2, Mathf.RoundToInt(model.abdomenRadii01.y * bp.height));
        for (var y = -ry; y <= ry; y++)
        for (var x = -rx; x <= rx; x++)
        {
            var nx = x / (float)rx; var ny = y / (float)ry;
            var inside = nx * nx + ny * ny <= 1f;
            var curvedBand = inside && ny > -0.15f && ny < 0.15f && nx < 0.35f;
            if (curvedBand) bp.Set("stripe", c.x + x, c.y + y, 1);
        }
    }

    private static void DrawEllipse01(PixelBlueprint2D bp, string layer, Vector2 c01, Vector2 r01)
    {
        var c = ToPx(bp, c01);
        var rx = Mathf.Max(1, Mathf.RoundToInt(r01.x * bp.width));
        var ry = Mathf.Max(1, Mathf.RoundToInt(r01.y * bp.height));
        Oval(bp, layer, c.x, c.y, rx, ry);
    }

    private static Vector2Int ToPx(PixelBlueprint2D bp, Vector2 p01) => new(Mathf.RoundToInt(Mathf.Clamp01(p01.x) * (bp.width - 1)), Mathf.RoundToInt(Mathf.Clamp01(p01.y) * (bp.height - 1)));
    private static Vector2Int Dir(float deg) { var r = deg * Mathf.Deg2Rad; return new Vector2Int(Mathf.RoundToInt(Mathf.Cos(r)), Mathf.RoundToInt(Mathf.Sin(r))); }

    private static AntTopdownModel GoldenModel()
    {
        var m = new AntTopdownModel
        {
            headCenter01 = new Vector2(0.5f, 0.25f),
            thoraxCenter01 = new Vector2(0.5f, 0.48f),
            abdomenCenter01 = new Vector2(0.5f, 0.72f),
            headRadii01 = new Vector2(0.09f, 0.08f),
            thoraxRadii01 = new Vector2(0.11f, 0.10f),
            abdomenRadii01 = new Vector2(0.16f, 0.18f),
            pinchStrength = 0.5f,
            antennaLen01 = 0.16f
        };
        for (var i = 0; i < 3; i++)
        {
            var t = i / 2f;
            m.legAnchors01[i] = new Vector2(0.42f, 0.40f + t * 0.14f);
            m.legAnchors01[i + 3] = new Vector2(0.58f, 0.40f + t * 0.14f);
            m.legAnglesDeg[i] = -155f + i * 18f;
            m.legAnglesDeg[i + 3] = -25f + i * 18f;
            m.legLengths01[i] = m.legLengths01[i + 3] = 0.18f;
        }
        m.antennaAnchors01[0] = new Vector2(0.46f, 0.17f);
        m.antennaAnchors01[1] = new Vector2(0.54f, 0.17f);
        m.antennaAnglesDeg[0] = -125f;
        m.antennaAnglesDeg[1] = -55f;
        return m;
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
