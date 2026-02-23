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
        body.Clear("head");
        body.Clear("thorax");
        body.Clear("abdomen");
        body.Clear("legs");
        body.Clear("antennae");
        body.Clear("eyes");
        body.Clear("mandibles");
        body.Clear("highlight");
        body.Clear("shadow");
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

        var pose = PoseFor(req.state, req.frameIndex);

        DrawEllipse01(bp, "abdomen", model.abdomenCenter01, model.abdomenRadii01);
        DrawEllipse01(bp, "thorax", model.thoraxCenter01, model.thoraxRadii01);
        DrawEllipse01(bp, "head", model.headCenter01, model.headRadii01 * (req.role == "soldier" ? profile.soldierHeadMultiplier : 1f));
        DrawWaistPinch(bp, model);

        for (var i = 0; i < 6; i++)
        {
            var angle = model.legAnglesDeg[i] + pose.legAngleOffsetsDeg[i] + pose.forwardLeanDeg;
            var a = ToPx(bp, model.legAnchors01[i]);
            var stride = Mathf.Max(0.7f, pose.legStrideScale[i]);
            var b = a + Dir(angle) * Mathf.RoundToInt(model.legLengths01[i] * bp.width * stride);
            DrawThickLine(bp, "legs", a.x, a.y, b.x, b.y, 1);
        }

        for (var i = 0; i < 2; i++)
        {
            var angle = model.antennaAnglesDeg[i] + pose.antennaWiggleDeg[i];
            var a = ToPx(bp, model.antennaAnchors01[i]);
            var antennaLen = Mathf.Max(0.8f, pose.antennaLengthScale[i]);
            var b = a + Dir(angle) * Mathf.RoundToInt(model.antennaLen01 * bp.width * antennaLen);
            DrawThickLine(bp, "antennae", a.x, a.y, b.x, b.y, 1);
        }

        DrawHeadDetails(bp, model, req, pose);
        DrawHighlightsAndShadows(bp, model);

        MergeToBody(bp, "abdomen");
        MergeToBody(bp, "thorax");
        MergeToBody(bp, "head");
        MergeToBody(bp, "legs");
        MergeToBody(bp, "antennae");
        MergeToBody(bp, "mandibles");

        DrawAbdomenStripe(bp, model);
    }

    private static void DrawWaistPinch(PixelBlueprint2D bp, AntTopdownModel model)
    {
        var c = ToPx(bp, new Vector2(0.5f, (model.thoraxCenter01.y + model.abdomenCenter01.y) * 0.5f));
        Oval(bp, "abdomen", c.x, c.y, 2, 1);
    }

    private static void DrawHeadDetails(PixelBlueprint2D bp, AntTopdownModel model, ArchetypeSynthesisRequest req, PoseState pose)
    {
        var hc = ToPx(bp, model.headCenter01);
        bp.Set("eyes", hc.x - 2, hc.y - 1, 1);
        bp.Set("eyes", hc.x + 2, hc.y - 1, 1);

        var spread = string.Equals(req.state, "fight", System.StringComparison.OrdinalIgnoreCase) ? 5 : 3;
        DrawThickLine(bp, "mandibles", hc.x - 1, hc.y + 1, hc.x - spread, hc.y + 2 + pose.mandibleOpen, 1);
        DrawThickLine(bp, "mandibles", hc.x + 1, hc.y + 1, hc.x + spread, hc.y + 2 + pose.mandibleOpen, 1);
    }

    private static void DrawHighlightsAndShadows(PixelBlueprint2D bp, AntTopdownModel model)
    {
        DrawEllipse01(bp, "highlight", model.headCenter01 + new Vector2(-0.02f, -0.02f), model.headRadii01 * 0.35f);
        DrawEllipse01(bp, "highlight", model.thoraxCenter01 + new Vector2(-0.02f, -0.02f), model.thoraxRadii01 * 0.30f);
        DrawEllipse01(bp, "highlight", model.abdomenCenter01 + new Vector2(-0.03f, -0.04f), model.abdomenRadii01 * 0.32f);

        DrawEllipse01(bp, "shadow", model.thoraxCenter01 + new Vector2(0.02f, 0.03f), model.thoraxRadii01 * 0.40f);
        DrawEllipse01(bp, "shadow", model.abdomenCenter01 + new Vector2(0.03f, 0.05f), model.abdomenRadii01 * 0.42f);
    }

    private static void MergeToBody(PixelBlueprint2D bp, string fromLayer)
    {
        var layer = bp.EnsureLayer(fromLayer);
        for (var y = 0; y < bp.height; y++)
        for (var x = 0; x < bp.width; x++)
            if (layer.pixels[(y * bp.width) + x] > 0) bp.Set("body", x, y, 1);
    }

    private struct PoseState
    {
        public readonly float[] legAngleOffsetsDeg;
        public readonly float[] legStrideScale;
        public readonly float[] antennaWiggleDeg;
        public readonly float[] antennaLengthScale;
        public readonly float forwardLeanDeg;
        public readonly int mandibleOpen;

        public PoseState(float[] legAngleOffsetsDeg, float[] legStrideScale, float[] antennaWiggleDeg, float[] antennaLengthScale, float forwardLeanDeg, int mandibleOpen)
        {
            this.legAngleOffsetsDeg = legAngleOffsetsDeg;
            this.legStrideScale = legStrideScale;
            this.antennaWiggleDeg = antennaWiggleDeg;
            this.antennaLengthScale = antennaLengthScale;
            this.forwardLeanDeg = forwardLeanDeg;
            this.mandibleOpen = mandibleOpen;
        }
    }

    private static readonly float[] TripodA = { 12f, -10f, 8f, -12f, 10f, -8f };
    private static readonly float[] TripodB = { -11f, 9f, -7f, 11f, -9f, 7f };
    private static readonly float[] TripodN = { 0f, 0f, 0f, 0f, 0f, 0f };
    private static readonly float[] RunA = { 18f, -16f, 14f, -18f, 16f, -14f };
    private static readonly float[] RunB = { 7f, -6f, 5f, -7f, 6f, -5f };
    private static readonly float[] RunC = { -8f, 7f, -6f, 8f, -7f, 6f };
    private static readonly float[] RunD = { -19f, 17f, -15f, 19f, -17f, 15f };
    private static readonly float[] StrideIdleStill = { 1f, 1f, 1f, 1f, 1f, 1f };
    private static readonly float[] StrideIdleTwitch = { 1.02f, 0.98f, 1.01f, 0.99f, 1.02f, 0.98f };
    private static readonly float[] StrideWalkA = { 1.10f, 0.92f, 1.05f, 0.90f, 1.08f, 0.93f };
    private static readonly float[] StrideWalkB = { 0.95f, 1.04f, 0.96f, 1.05f, 0.95f, 1.04f };
    private static readonly float[] StrideRunA = { 1.22f, 0.80f, 1.14f, 0.78f, 1.20f, 0.82f };
    private static readonly float[] StrideRunB = { 1.10f, 0.94f, 1.06f, 0.92f, 1.08f, 0.95f };
    private static readonly float[] StrideRunC = { 0.96f, 1.08f, 0.95f, 1.10f, 0.96f, 1.06f };
    private static readonly float[] StrideRunD = { 0.80f, 1.24f, 0.84f, 1.22f, 0.82f, 1.20f };

    private static PoseState PoseFor(string state, int frameIndex)
    {
        if (string.Equals(state, "idle", System.StringComparison.OrdinalIgnoreCase))
        {
            if (Mathf.Abs(frameIndex % 2) == 0)
                return new PoseState(TripodN, StrideIdleStill, new[] { -5f, 5f }, new[] { 1f, 1f }, 0f, 0);

            return new PoseState(new[] { 2f, -2f, 1f, -1f, 2f, -2f }, StrideIdleTwitch, new[] { 6f, -6f }, new[] { 1.04f, 1.04f }, 0f, 0);
        }

        if (string.Equals(state, "walk", System.StringComparison.OrdinalIgnoreCase))
        {
            var pose = Mathf.Abs(frameIndex % 3);
            return pose switch
            {
                0 => new PoseState(TripodA, StrideWalkA, new[] { -3f, 3f }, new[] { 1f, 1f }, 0f, 0),
                1 => new PoseState(TripodN, StrideWalkB, new[] { 0f, 0f }, new[] { 1.02f, 1.02f }, 0f, 0),
                _ => new PoseState(TripodB, StrideWalkA, new[] { 3f, -3f }, new[] { 1f, 1f }, 0f, 0)
            };
        }

        if (string.Equals(state, "run", System.StringComparison.OrdinalIgnoreCase))
        {
            var pose = Mathf.Abs(frameIndex % 4);
            return pose switch
            {
                0 => new PoseState(RunA, StrideRunA, new[] { -4f, 4f }, new[] { 1.06f, 1.06f }, 8f, 0),
                1 => new PoseState(RunB, StrideRunB, new[] { 0f, 0f }, new[] { 1.03f, 1.03f }, 8f, 0),
                2 => new PoseState(RunC, StrideRunC, new[] { 0f, 0f }, new[] { 1.03f, 1.03f }, 8f, 0),
                _ => new PoseState(RunD, StrideRunD, new[] { 4f, -4f }, new[] { 1.06f, 1.06f }, 8f, 0)
            };
        }

        if (string.Equals(state, "fight", System.StringComparison.OrdinalIgnoreCase))
        {
            return new PoseState(new[] { 16f, -12f, -6f, -16f, 12f, 6f }, new[] { 0.9f, 1f, 1f, 0.9f, 1f, 1f }, new[] { 2f, -2f }, new[] { 1f, 1f }, 2f, 3);
        }

        return new PoseState(TripodN, StrideIdleStill, new[] { 0f, 0f }, new[] { 1f, 1f }, 0f, 0);
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
