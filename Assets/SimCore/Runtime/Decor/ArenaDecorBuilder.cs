using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class ArenaDecorBuilder
{
    private const int HardCap = 300;
    private const int GenericCap = 60;
    private const int DecorOrderMin = -8;
    private const int DecorOrderMax = -2;

    private static Sprite whitePixelSprite;
    private static readonly HashSet<string> warnedUnknownSims = new();

    private enum DecorTheme
    {
        AntColonies,
        MarbleRace,
        RaceCar,
        FantasySport,
        Generic
    }

    public static void BuildDecor(Transform decorRootParent, Transform arenaRoot, ScenarioConfig config, string simId)
    {
        if (arenaRoot == null || config == null)
        {
            return;
        }

        var decorRoot = EnsureDecorRoot(decorRootParent);
        if (decorRoot == null)
        {
            return;
        }

        ClearChildren(decorRoot);

        var bounds = ResolveBounds(arenaRoot, config);
        var normalizedId = NormalizeSimId(ResolveSimId(config, simId));
        var theme = ResolveTheme(normalizedId);

        var rng = RngService.Fork($"DECOR:{normalizedId}");
        var budget = Mathf.Clamp(Mathf.RoundToInt((bounds.halfWidth * bounds.halfHeight) * 0.12f), 80, 250);
        if (theme == DecorTheme.Generic)
        {
            budget = Mathf.Min(GenericCap, budget);
            if (warnedUnknownSims.Add(normalizedId))
            {
                Debug.LogWarning($"Unknown simId for decor: {normalizedId}");
            }
        }

        budget = Mathf.Min(HardCap, budget);

        switch (theme)
        {
            case DecorTheme.AntColonies:
                BuildAntDecor(decorRoot, bounds, rng, budget);
                break;
            case DecorTheme.MarbleRace:
                BuildMarbleDecor(decorRoot, bounds, rng, budget);
                break;
            case DecorTheme.RaceCar:
                BuildRaceCarDecor(decorRoot, bounds, rng, budget);
                break;
            case DecorTheme.FantasySport:
                BuildFantasySportDecor(decorRoot, bounds, rng, budget);
                break;
            default:
                BuildGenericDecor(decorRoot, bounds, rng, budget);
                break;
        }
    }


    public static Transform EnsureDecorRoot(Transform decorRootParent)
    {
        if (decorRootParent == null)
        {
            return null;
        }

        if (string.Equals(decorRootParent.name, "DecorRoot", StringComparison.Ordinal))
        {
            return decorRootParent;
        }

        var existing = decorRootParent.Find("DecorRoot");
        if (existing != null)
        {
            return existing;
        }

        var decorRoot = new GameObject("DecorRoot");
        decorRoot.transform.SetParent(decorRootParent, false);
        decorRoot.transform.localPosition = Vector3.zero;
        return decorRoot.transform;
    }

    public static void ClearChildren(Transform t)
    {
        if (t == null)
        {
            return;
        }

        for (var i = t.childCount - 1; i >= 0; i--)
        {
            var child = t.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(child);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(child);
            }
        }
    }

    private static (float halfWidth, float halfHeight, float margin, float clearRadius) ResolveBounds(Transform arenaRoot, ScenarioConfig config)
    {
        var halfWidth = Mathf.Max(2f, (config.world?.arenaWidth ?? 64) * 0.5f);
        var halfHeight = Mathf.Max(2f, (config.world?.arenaHeight ?? 64) * 0.5f);

        var layout = arenaRoot.GetComponent<ArenaLayout>();
        if (layout != null)
        {
            halfWidth = Mathf.Max(2f, layout.HalfWidth);
            halfHeight = Mathf.Max(2f, layout.HalfHeight);
        }

        var minHalf = Mathf.Min(halfWidth, halfHeight);
        var margin = Mathf.Clamp(minHalf * 0.07f, 1.5f, 2.5f);
        var clearRadius = Mathf.Clamp(minHalf * 0.2f, 5f, 8f);
        return (halfWidth, halfHeight, margin, clearRadius);
    }

    private static string ResolveSimId(ScenarioConfig config, string simId)
    {
        if (!string.IsNullOrWhiteSpace(config.activeSimulation))
        {
            return config.activeSimulation;
        }

        if (!string.IsNullOrWhiteSpace(simId))
        {
            return simId;
        }

        var reflectionFields = new[] { "simulationId", "simulation", "scenarioName" };
        foreach (var fieldName in reflectionFields)
        {
            var field = config.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.GetValue(config) is string value && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var property = config.GetType().GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(config, null) is string propValue && !string.IsNullOrWhiteSpace(propValue))
            {
                return propValue;
            }
        }

        return "unknown";
    }

    private static string NormalizeSimId(string value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();
        return text.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
    }

    private static DecorTheme ResolveTheme(string normalizedId)
    {
        if (normalizedId == "antcolonies" || normalizedId == "ants" || normalizedId == "antcolony")
        {
            return DecorTheme.AntColonies;
        }

        if (normalizedId == "marblerace" || normalizedId == "marbles")
        {
            return DecorTheme.MarbleRace;
        }

        if (normalizedId == "racecar" || normalizedId == "racing" || normalizedId == "cars")
        {
            return DecorTheme.RaceCar;
        }

        if (normalizedId == "fantasysport" || normalizedId == "sport" || normalizedId == "field")
        {
            return DecorTheme.FantasySport;
        }

        return DecorTheme.Generic;
    }

    private static int BuildAntDecor(Transform root, (float halfWidth, float halfHeight, float margin, float clearRadius) b, IRng rng, int cap)
    {
        var count = 0;
        var grass = Mathf.Min(cap - count, rng.Range(40, 121));
        for (var i = 0; i < grass && count < cap; i++)
        {
            var p = EdgeBiasedPoint(rng, b.halfWidth, b.halfHeight, b.margin);
            if (!IsOutsideClearZone(p, b.clearRadius)) continue;
            CreateSpriteGO(root, "Grass", PrimitiveSpriteLibrary.RoundedRectFill(), p, RandomScale(rng, 0.24f, 0.44f), rng.Range(-15f, 15f), new Color(0.24f, 0.52f, 0.26f, rng.Range(0.35f, 0.7f)), rng.Range(-8, -5));
            count++;
        }

        var pebbleClusters = rng.Range(4, 10);
        for (var c = 0; c < pebbleClusters && count < cap; c++)
        {
            var center = RandomPoint(rng, b.halfWidth, b.halfHeight, b.margin);
            var pebbles = rng.Range(3, 9);
            for (var i = 0; i < pebbles && count < cap; i++)
            {
                var p = center + RandomInCircle(rng) * rng.Range(0.15f, 1f);
                if (!IsInsideBounds(p, b.margin, b.halfWidth, b.halfHeight) || !IsOutsideClearZone(p, b.clearRadius)) continue;
                CreateSpriteGO(root, "Pebble", PrimitiveSpriteLibrary.CircleFill(), p, RandomScale(rng, 0.12f, 0.26f), 0f, new Color(0.47f, 0.42f, 0.35f, rng.Range(0.35f, 0.65f)), rng.Range(-8, -4));
                count++;
            }
        }

        for (var i = 0; i < rng.Range(6, 15) && count < cap; i++)
        {
            var p = EdgeBiasedPoint(rng, b.halfWidth, b.halfHeight, b.margin * 0.5f);
            CreateSpriteGO(root, "LeafBlob", PrimitiveSpriteLibrary.RoundedRectFill(), p, RandomScale(rng, 0.7f, 1.4f), rng.Range(0f, 360f), new Color(0.46f, 0.36f, 0.2f, rng.Range(0.35f, 0.55f)), rng.Range(-7, -3));
            count++;
        }

        var crumbs = rng.Range(2, 6);
        for (var c = 0; c < crumbs && count < cap; c++)
        {
            var center = RandomPoint(rng, b.halfWidth, b.halfHeight, b.margin);
            if (!IsOutsideClearZone(center, b.clearRadius)) continue;
            var dots = rng.Range(8, 21);
            for (var i = 0; i < dots && count < cap; i++)
            {
                var p = center + RandomInCircle(rng) * rng.Range(0.1f, 0.8f);
                if (!IsInsideBounds(p, b.margin, b.halfWidth, b.halfHeight)) continue;
                CreateSpriteGO(root, "Crumb", PrimitiveSpriteLibrary.CircleFill(), p, RandomScale(rng, 0.08f, 0.16f), 0f, new Color(0.78f, 0.7f, 0.4f, rng.Range(0.45f, 0.75f)), -3);
                count++;
            }
        }

        if (count + 3 <= cap)
        {
            var nestPos = EdgeBiasedPoint(rng, b.halfWidth, b.halfHeight, b.margin + 0.5f);
            CreateSpriteGO(root, "NestBase", PrimitiveSpriteLibrary.CircleFill(), nestPos, Vector2.one * 1.2f, 0f, new Color(0.14f, 0.11f, 0.08f, 0.7f), -3);
            CreateSpriteGO(root, "NestRing", PrimitiveSpriteLibrary.CircleOutline(), nestPos, Vector2.one * 1.45f, 0f, new Color(0.55f, 0.45f, 0.2f, 0.65f), -2);
            CreateSpriteGO(root, "NestHole", PrimitiveSpriteLibrary.CircleFill(), nestPos + new Vector2(0.1f, -0.08f), Vector2.one * 0.4f, 0f, new Color(0.05f, 0.04f, 0.03f, 0.8f), -2);
        }

        return count;
    }

    private static int BuildMarbleDecor(Transform root, (float halfWidth, float halfHeight, float margin, float clearRadius) b, IRng rng, int cap)
    {
        rng = RngService.Fork("DECOR:MARBLE_RACE");
        var count = 0;
        var minHalf = Mathf.Min(b.halfWidth, b.halfHeight);
        var laneWidth = Mathf.Clamp(minHalf * 0.2f, 5f, 12f);
        var halfLane = laneWidth * 0.5f;
        var centerJitterX = rng.Range(-minHalf * 0.05f, minHalf * 0.05f);
        var centerJitterY = rng.Range(-minHalf * 0.05f, minHalf * 0.05f);
        var trackCenter = new Vector2(centerJitterX, centerJitterY);
        var radiusX = Mathf.Max(6f, b.halfWidth - b.margin - halfLane - 1.2f);
        var radiusY = Mathf.Max(6f, b.halfHeight - b.margin - halfLane - 1.2f);
        var segmentCount = Mathf.Clamp(Mathf.RoundToInt((radiusX + radiusY) * 1.6f), 64, 96);
        var laneColor = new Color(0.12f, 0.13f, 0.15f, 0.85f);
        var borderColor = new Color(0.92f, 0.94f, 0.98f, 0.9f);
        var borderThickness = Mathf.Clamp(laneWidth * 0.12f, 0.35f, 0.8f);

        Vector2 EllipsePoint(float angle, float rx, float ry)
        {
            return trackCenter + new Vector2(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry);
        }

        for (var i = 0; i < segmentCount && count < cap; i++)
        {
            var a0 = (i / (float)segmentCount) * Mathf.PI * 2f;
            var a1 = ((i + 1) / (float)segmentCount) * Mathf.PI * 2f;
            var p0 = EllipsePoint(a0, radiusX, radiusY);
            var p1 = EllipsePoint(a1, radiusX, radiusY);
            var mid = (p0 + p1) * 0.5f;
            var delta = p1 - p0;
            if (delta.sqrMagnitude < 0.0001f)
            {
                continue;
            }

            var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            CreateSpriteGO(root, "TrackLane", PrimitiveSpriteLibrary.CapsuleFill(), mid, new Vector2(delta.magnitude + 0.1f, laneWidth), angle, laneColor, -5);
            count++;
        }

        var outerRx = radiusX + halfLane;
        var outerRy = radiusY + halfLane;
        var innerRx = Mathf.Max(2f, radiusX - halfLane);
        var innerRy = Mathf.Max(2f, radiusY - halfLane);
        for (var i = 0; i < segmentCount && count + 1 < cap; i++)
        {
            var a0 = (i / (float)segmentCount) * Mathf.PI * 2f;
            var a1 = ((i + 1) / (float)segmentCount) * Mathf.PI * 2f;
            count += CreateLineSegment(root, "TrackOuterBorder", EllipsePoint(a0, outerRx, outerRy), EllipsePoint(a1, outerRx, outerRy), borderThickness, borderColor, -3);
            count += CreateLineSegment(root, "TrackInnerBorder", EllipsePoint(a0, innerRx, innerRy), EllipsePoint(a1, innerRx, innerRy), borderThickness, borderColor, -3);
        }

        var startAngle = 0f;
        var startCenter = EllipsePoint(startAngle, radiusX, radiusY);
        var startNormal = new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle));
        var startTangent = new Vector2(-Mathf.Sin(startAngle), Mathf.Cos(startAngle));
        var stripeLength = Mathf.Clamp(laneWidth * 0.35f, 1.8f, 3.4f);
        var cols = 6;
        var rows = 2;
        var tileW = laneWidth / cols;
        var tileH = stripeLength / rows;
        for (var r = 0; r < rows && count < cap; r++)
        {
            for (var c = 0; c < cols && count < cap; c++)
            {
                var u = -halfLane + (c + 0.5f) * tileW;
                var v = -stripeLength * 0.5f + (r + 0.5f) * tileH;
                var tilePos = startCenter + (startNormal * u) + (startTangent * v);
                var color = ((r + c) & 1) == 0 ? new Color(0.95f, 0.95f, 0.95f, 0.95f) : new Color(0.08f, 0.08f, 0.08f, 0.95f);
                CreateSpriteGO(root, "StartFinishTile", GetWhitePixelSprite(), tilePos, new Vector2(tileW * 0.92f, tileH * 0.92f), 90f, color, -2);
                count++;
            }
        }

        var banners = Mathf.Min(14, cap - count);
        for (var i = 0; i < banners && count + 2 <= cap; i++)
        {
            var x = Mathf.Lerp(-b.halfWidth + b.margin, b.halfWidth - b.margin, (i + 1f) / (banners + 1f));
            var y = b.halfHeight - (b.margin * 0.8f);
            CreateSpriteGO(root, "BannerPole", GetWhitePixelSprite(), new Vector2(x, y - 0.2f), new Vector2(0.04f, 0.4f), 0f, new Color(0.9f, 0.9f, 0.95f, 0.45f), -4);
            CreateSpriteGO(root, "Banner", GetWhitePixelSprite(), new Vector2(x + 0.15f, y - 0.05f), new Vector2(0.35f, 0.18f), 0f, new Color(rng.Range(0.2f, 0.9f), rng.Range(0.2f, 0.9f), rng.Range(0.2f, 0.9f), 0.5f), -4);
            count += 2;
        }

        for (var i = 0; i < Mathf.Min(cap - count, rng.Range(30, 121)); i++)
        {
            var x = Mathf.Lerp(-b.halfWidth + b.margin, b.halfWidth - b.margin, i / 120f);
            var y = (i & 1) == 0 ? b.halfHeight - b.margin : -b.halfHeight + b.margin;
            CreateSpriteGO(root, "CrowdDot", PrimitiveSpriteLibrary.CircleFill(), new Vector2(x + rng.Range(-0.1f, 0.1f), y + rng.Range(-0.25f, 0.25f)), RandomScale(rng, 0.06f, 0.12f), 0f, new Color(0.9f, 0.9f, 0.9f, 0.35f), -6);
            count++;
            if (count >= cap) break;
        }

        return count;
    }

    private static int BuildRaceCarDecor(Transform root, (float halfWidth, float halfHeight, float margin, float clearRadius) b, IRng rng, int cap)
    {
        var count = 0;
        var curbTiles = Mathf.Min(cap - count, rng.Range(10, 31));
        for (var i = 0; i < curbTiles && count < cap; i++)
        {
            var corner = new Vector2((i % 2 == 0 ? 1 : -1) * (b.halfWidth - b.margin), (i % 3 == 0 ? 1 : -1) * (b.halfHeight - b.margin));
            var jitter = new Vector2(rng.Range(-1.8f, 1.8f), rng.Range(-1.8f, 1.8f));
            var p = corner + jitter;
            if (!IsInsideBounds(p, b.margin, b.halfWidth, b.halfHeight)) continue;
            var color = (i & 1) == 0 ? new Color(0.82f, 0.15f, 0.15f, 0.55f) : new Color(0.92f, 0.92f, 0.92f, 0.55f);
            CreateSpriteGO(root, "Curb", GetWhitePixelSprite(), p, new Vector2(rng.Range(0.28f, 0.5f), rng.Range(0.2f, 0.34f)), rng.Range(-20f, 20f), color, -3);
            count++;
        }

        var cones = Mathf.Min(cap - count, rng.Range(6, 21));
        for (var i = 0; i < cones && count + 2 <= cap; i++)
        {
            var p = RandomPoint(rng, b.halfWidth, b.halfHeight, b.margin);
            if (!IsOutsideClearZone(p, b.clearRadius * 0.9f)) continue;
            CreateSpriteGO(root, "ConeBase", PrimitiveSpriteLibrary.CircleFill(), p + new Vector2(0f, -0.06f), new Vector2(0.2f, 0.08f), 0f, new Color(0.3f, 0.2f, 0.15f, 0.45f), -4);
            CreateSpriteGO(root, "Cone", PrimitiveSpriteLibrary.CapsuleFill(), p + new Vector2(0f, 0.08f), new Vector2(0.1f, 0.24f), 0f, new Color(0.95f, 0.48f, 0.13f, 0.65f), -3);
            count += 2;
        }

        var stacks = Mathf.Min(10, (cap - count) / 3);
        for (var i = 0; i < stacks && count + 3 <= cap; i++)
        {
            var p = EdgeBiasedPoint(rng, b.halfWidth, b.halfHeight, b.margin + 0.5f);
            for (var t = 0; t < 3 && count < cap; t++)
            {
                CreateSpriteGO(root, "Tire", PrimitiveSpriteLibrary.CircleFill(), p + new Vector2(t * 0.15f, 0f), Vector2.one * 0.24f, 0f, new Color(0.12f, 0.12f, 0.12f, 0.6f), -4);
                count++;
            }
        }

        var skids = Mathf.Min(cap - count, rng.Range(6, 19));
        for (var i = 0; i < skids && count < cap; i++)
        {
            var p = RandomPoint(rng, b.halfWidth, b.halfHeight, b.margin);
            if (!IsOutsideClearZone(p, b.clearRadius * 0.6f)) continue;
            CreateSpriteGO(root, "Skid", PrimitiveSpriteLibrary.CapsuleFill(), p, new Vector2(rng.Range(0.6f, 1.8f), rng.Range(0.05f, 0.11f)), rng.Range(0f, 360f), new Color(0.05f, 0.05f, 0.05f, rng.Range(0.25f, 0.45f)), -6);
            count++;
        }

        return count;
    }

    private static int BuildFantasySportDecor(Transform root, (float halfWidth, float halfHeight, float margin, float clearRadius) b, IRng rng, int cap)
    {
        rng = RngService.Fork("DECOR:FANTASY_SPORT");
        var count = 0;
        var lineColor = new Color(0.95f, 0.95f, 0.9f, 0.6f);
        var insetW = b.halfWidth - b.margin;
        var insetH = b.halfHeight - b.margin;
        count += CreateLineSegment(root, "OuterTop", new Vector2(-insetW, insetH), new Vector2(insetW, insetH), 0.12f, lineColor, -3);
        count += CreateLineSegment(root, "OuterBottom", new Vector2(-insetW, -insetH), new Vector2(insetW, -insetH), 0.12f, lineColor, -3);
        count += CreateLineSegment(root, "OuterLeft", new Vector2(-insetW, -insetH), new Vector2(-insetW, insetH), 0.12f, lineColor, -3);
        count += CreateLineSegment(root, "OuterRight", new Vector2(insetW, -insetH), new Vector2(insetW, insetH), 0.12f, lineColor, -3);
        count += CreateLineSegment(root, "Halfway", new Vector2(0f, -insetH), new Vector2(0f, insetH), 0.1f, lineColor, -3);

        var ringSegments = 24;
        var r = Mathf.Min(insetW, insetH) * 0.22f;
        var prev = new Vector2(Mathf.Cos(0f), Mathf.Sin(0f)) * r;
        for (var i = 1; i <= ringSegments && count < cap; i++)
        {
            var ang = (i / (float)ringSegments) * Mathf.PI * 2f;
            var next = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            count += CreateLineSegment(root, "CenterCircle", prev, next, 0.08f, lineColor, -3);
            prev = next;
        }

        if (count + 8 <= cap)
        {
            var goalW = 1.5f;
            var goalH = 3.5f;
            count += CreateRectOutline(root, new Vector2(-insetW + goalW * 0.5f, 0f), goalW, goalH, 0.09f, lineColor, -3);
            count += CreateRectOutline(root, new Vector2(insetW - goalW * 0.5f, 0f), goalW, goalH, 0.09f, lineColor, -3);
        }

        var crowd = Mathf.Min(cap - count, rng.Range(20, 101));
        for (var i = 0; i < crowd && count < cap; i++)
        {
            var sideY = (i & 1) == 0 ? (insetH + 0.65f) : (-insetH - 0.65f);
            var p = new Vector2(
                rng.Range(-b.halfWidth + b.margin, b.halfWidth - b.margin),
                sideY) + new Vector2(0f, rng.Range(-0.25f, 0.25f));
            CreateSpriteGO(root, "StandDot", PrimitiveSpriteLibrary.CircleFill(), p, RandomScale(rng, 0.06f, 0.13f), 0f, new Color(0.8f, 0.86f, 0.9f, 0.35f), -6);
            count++;
        }

        return count;
    }

    private static int BuildGenericDecor(Transform root, (float halfWidth, float halfHeight, float margin, float clearRadius) b, IRng rng, int cap)
    {
        var count = 0;
        var dots = Mathf.Min(cap, rng.Range(20, 60));
        for (var i = 0; i < dots && count < cap; i++)
        {
            var p = RandomPoint(rng, b.halfWidth, b.halfHeight, b.margin);
            if (!IsOutsideClearZone(p, b.clearRadius)) continue;
            CreateSpriteGO(root, "GenericDot", PrimitiveSpriteLibrary.CircleFill(), p, RandomScale(rng, 0.12f, 0.3f), 0f, new Color(0.6f, 0.65f, 0.7f, rng.Range(0.3f, 0.55f)), rng.Range(-7, -3));
            count++;
        }

        return count;
    }

    private static Vector2 RandomPoint(IRng rng, float halfW, float halfH, float margin)
    {
        return new Vector2(rng.Range(-halfW + margin, halfW - margin), rng.Range(-halfH + margin, halfH - margin));
    }

    private static Vector2 EdgeBiasedPoint(IRng rng, float halfW, float halfH, float margin)
    {
        var p = RandomPoint(rng, halfW, halfH, margin);
        p *= rng.Range(1.05f, 1.35f);
        p.x = Mathf.Clamp(p.x, -halfW + margin, halfW - margin);
        p.y = Mathf.Clamp(p.y, -halfH + margin, halfH - margin);
        return p;
    }


    private static Vector2 RandomInCircle(IRng rng)
    {
        return rng.InsideUnitCircle();
    }
    private static Vector2 RandomScale(IRng rng, float min, float max)
    {
        var scale = rng.Range(min, max) * rng.Range(0.8f, 1.3f);
        return Vector2.one * scale;
    }

    private static int CreateLineSegment(Transform parent, string name, Vector2 start, Vector2 end, float thickness, Color color, int sortingOrder)
    {
        var delta = end - start;
        var length = delta.magnitude;
        if (length <= 0.001f)
        {
            return 0;
        }

        var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        CreateSpriteGO(parent, name, GetWhitePixelSprite(), (start + end) * 0.5f, new Vector2(length, thickness), angle, color, sortingOrder);
        return 1;
    }

    private static int CreateDashedRect(Transform parent, float halfW, float halfH, float thickness, Color color, int sortingOrder, int budget)
    {
        var created = 0;
        var dash = 0.8f;
        var gap = 0.5f;

        for (var x = -halfW; x < halfW && created < budget; x += dash + gap)
        {
            created += CreateLineSegment(parent, "DashTop", new Vector2(x, halfH), new Vector2(Mathf.Min(x + dash, halfW), halfH), thickness, color, sortingOrder);
            if (created >= budget) break;
            created += CreateLineSegment(parent, "DashBottom", new Vector2(x, -halfH), new Vector2(Mathf.Min(x + dash, halfW), -halfH), thickness, color, sortingOrder);
        }

        for (var y = -halfH; y < halfH && created < budget; y += dash + gap)
        {
            created += CreateLineSegment(parent, "DashLeft", new Vector2(-halfW, y), new Vector2(-halfW, Mathf.Min(y + dash, halfH)), thickness, color, sortingOrder);
            if (created >= budget) break;
            created += CreateLineSegment(parent, "DashRight", new Vector2(halfW, y), new Vector2(halfW, Mathf.Min(y + dash, halfH)), thickness, color, sortingOrder);
        }

        return created;
    }

    private static int CreateRectOutline(Transform parent, Vector2 center, float width, float height, float thickness, Color color, int sortingOrder)
    {
        var hw = width * 0.5f;
        var hh = height * 0.5f;
        var count = 0;
        count += CreateLineSegment(parent, "RectTop", center + new Vector2(-hw, hh), center + new Vector2(hw, hh), thickness, color, sortingOrder);
        count += CreateLineSegment(parent, "RectBottom", center + new Vector2(-hw, -hh), center + new Vector2(hw, -hh), thickness, color, sortingOrder);
        count += CreateLineSegment(parent, "RectLeft", center + new Vector2(-hw, -hh), center + new Vector2(-hw, hh), thickness, color, sortingOrder);
        count += CreateLineSegment(parent, "RectRight", center + new Vector2(hw, -hh), center + new Vector2(hw, hh), thickness, color, sortingOrder);
        return count;
    }

    public static bool IsInsideBounds(Vector2 p, float margin, float halfW, float halfH)
    {
        return p.x >= (-halfW + margin)
               && p.x <= (halfW - margin)
               && p.y >= (-halfH + margin)
               && p.y <= (halfH - margin);
    }

    public static bool IsOutsideClearZone(Vector2 p, float clearRadius)
    {
        return p.sqrMagnitude >= clearRadius * clearRadius;
    }

    public static GameObject CreateSpriteGO(
        Transform parent,
        string name,
        Sprite sprite,
        Vector2 pos,
        Vector2 scale,
        float rotZ,
        Color color,
        int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : GetWhitePixelSprite();
        renderer.color = color;
        renderer.sortingOrder = Mathf.Clamp(sortingOrder, DecorOrderMin, DecorOrderMax);
        return go;
    }

    private static Sprite GetWhitePixelSprite()
    {
        if (whitePixelSprite != null)
        {
            return whitePixelSprite;
        }

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, false);
        whitePixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return whitePixelSprite;
    }
}
