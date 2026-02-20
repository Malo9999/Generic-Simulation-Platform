using System.Collections.Generic;
using UnityEngine;

public static class AntAtlasLibrary
{
    private enum AntAtlasLayer
    {
        Outline,
        Fill,
        Details
    }

    private readonly struct AntAtlasKey
    {
        public AntAtlasKey(AntRole role, int dir, AntAtlasLayer layer, int sizePx)
        {
            Role = role;
            Dir = ((dir % 8) + 8) % 8;
            Layer = layer;
            SizePx = Mathf.Max(8, sizePx);
        }

        public AntRole Role { get; }
        public int Dir { get; }
        public AntAtlasLayer Layer { get; }
        public int SizePx { get; }
    }

    private struct RoleShape
    {
        public float AbdomenRx;
        public float AbdomenRy;
        public float ThoraxRx;
        public float ThoraxRy;
        public float HeadR;
    }

    private static readonly Dictionary<AntAtlasKey, Sprite> SpriteCache = new();

    public static Sprite GetOutline(AntRole role, int dir, int sizePx = 64) => GetOrCreate(role, dir, AntAtlasLayer.Outline, sizePx);

    public static Sprite GetFill(AntRole role, int dir, int sizePx = 64) => GetOrCreate(role, dir, AntAtlasLayer.Fill, sizePx);

    public static Sprite GetDetails(AntRole role, int dir, int sizePx = 64) => GetOrCreate(role, dir, AntAtlasLayer.Details, sizePx);

    public static void ClearCache()
    {
        foreach (var kvp in SpriteCache)
        {
            if (kvp.Value == null)
            {
                continue;
            }

            if (kvp.Value.texture != null)
            {
                Object.Destroy(kvp.Value.texture);
            }

            Object.Destroy(kvp.Value);
        }

        SpriteCache.Clear();
    }

    private static Sprite GetOrCreate(AntRole role, int dir, AntAtlasLayer layer, int sizePx)
    {
        var key = new AntAtlasKey(role, dir, layer, sizePx);
        if (SpriteCache.TryGetValue(key, out var cached) && cached != null)
        {
            return cached;
        }

        var effectiveSize = Mathf.Max(8, sizePx);
        var bodyMask = BuildBodyMask(role, key.Dir, effectiveSize);
        var detailMask = BuildDetailMask(role, key.Dir, effectiveSize);
        var alpha = layer switch
        {
            AntAtlasLayer.Fill => bodyMask,
            AntAtlasLayer.Details => detailMask,
            _ => OutlineFromMask(bodyMask, effectiveSize)
        };

        var pixels = new Color32[effectiveSize * effectiveSize];
        var color = layer == AntAtlasLayer.Fill
            ? new Color32(255, 255, 255, 255)
            : new Color32(0, 0, 0, 255);

        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(color.r, color.g, color.b, alpha[i]);
        }

        var texture = new Texture2D(effectiveSize, effectiveSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, effectiveSize, effectiveSize),
            new Vector2(0.5f, 0.5f),
            effectiveSize);

        SpriteCache[key] = sprite;
        return sprite;
    }

    private static byte[] BuildBodyMask(AntRole role, int dir, int sizePx)
    {
        var mask = new byte[sizePx * sizePx];
        var scale = sizePx * 0.5f;
        var angle = Direction8.ToAngleDeg(dir);

        var body = GetRoleShape(role);
        DrawFilledEllipse(mask, sizePx, -0.30f, 0f, body.AbdomenRx, body.AbdomenRy, angle, scale);
        DrawFilledEllipse(mask, sizePx, 0f, 0f, body.ThoraxRx, body.ThoraxRy, angle, scale);
        DrawFilledEllipse(mask, sizePx, 0.30f, 0f, body.HeadR, body.HeadR, angle, scale);

        if (role == AntRole.Queen)
        {
            DrawQueenWings(mask, sizePx, angle, scale);
        }

        return mask;
    }

    private static byte[] BuildDetailMask(AntRole role, int dir, int sizePx)
    {
        var mask = new byte[sizePx * sizePx];
        var scale = sizePx * 0.5f;
        var angle = Direction8.ToAngleDeg(dir);

        DrawLegs(mask, sizePx, angle, scale);
        DrawAntennae(mask, sizePx, angle, scale);

        if (role == AntRole.Warrior)
        {
            DrawMandibles(mask, sizePx, angle, scale);
        }

        return mask;
    }

    private static RoleShape GetRoleShape(AntRole role)
    {
        return role switch
        {
            AntRole.Queen => new RoleShape
            {
                AbdomenRx = 0.26f,
                AbdomenRy = 0.20f,
                ThoraxRx = 0.14f,
                ThoraxRy = 0.12f,
                HeadR = 0.10f
            },
            AntRole.Warrior => new RoleShape
            {
                AbdomenRx = 0.23f,
                AbdomenRy = 0.19f,
                ThoraxRx = 0.13f,
                ThoraxRy = 0.11f,
                HeadR = 0.11f
            },
            _ => new RoleShape
            {
                AbdomenRx = 0.22f,
                AbdomenRy = 0.18f,
                ThoraxRx = 0.13f,
                ThoraxRy = 0.11f,
                HeadR = 0.09f
            }
        };
    }

    private static void DrawLegs(byte[] buffer, int sizePx, float angleDeg, float scale)
    {
        var thickness = Mathf.Max(2f, sizePx * 0.04f);
        var anchors = new[] { -0.07f, 0f, 0.07f };
        for (var i = 0; i < anchors.Length; i++)
        {
            var y = anchors[i];
            DrawModelLine(buffer, sizePx, 0.02f, y, -0.10f, -0.26f, thickness, angleDeg, scale);
            DrawModelLine(buffer, sizePx, 0.02f, y, -0.10f, 0.26f, thickness, angleDeg, scale);
        }
    }

    private static void DrawAntennae(byte[] buffer, int sizePx, float angleDeg, float scale)
    {
        var thickness = Mathf.Max(2f, sizePx * 0.03f);
        DrawModelLine(buffer, sizePx, 0.34f, -0.03f, 0.46f, -0.14f, thickness, angleDeg, scale);
        DrawModelLine(buffer, sizePx, 0.34f, 0.03f, 0.46f, 0.14f, thickness, angleDeg, scale);
    }

    private static void DrawMandibles(byte[] buffer, int sizePx, float angleDeg, float scale)
    {
        var thickness = Mathf.Max(2f, sizePx * 0.035f);
        DrawModelLine(buffer, sizePx, 0.36f, -0.02f, 0.48f, -0.08f, thickness, angleDeg, scale);
        DrawModelLine(buffer, sizePx, 0.36f, 0.02f, 0.48f, 0.08f, thickness, angleDeg, scale);
    }

    private static void DrawQueenWings(byte[] buffer, int sizePx, float angleDeg, float scale)
    {
        DrawFilledEllipse(buffer, sizePx, -0.05f, -0.15f, 0.17f, 0.09f, angleDeg, scale, 150);
        DrawFilledEllipse(buffer, sizePx, -0.05f, 0.15f, 0.17f, 0.09f, angleDeg, scale, 150);
    }

    private static void DrawFilledEllipse(byte[] buffer, int sizePx, float modelX, float modelY, float rx, float ry, float angleDeg, float scale, byte alpha = 255)
    {
        var center = RotatePoint(new Vector2(modelX, modelY), angleDeg);
        var centerPx = ModelToPixel(center, sizePx, scale);
        var rxPx = Mathf.Max(1f, rx * scale);
        var ryPx = Mathf.Max(1f, ry * scale);

        var minX = Mathf.Max(0, Mathf.FloorToInt(centerPx.x - rxPx - 1f));
        var maxX = Mathf.Min(sizePx - 1, Mathf.CeilToInt(centerPx.x + rxPx + 1f));
        var minY = Mathf.Max(0, Mathf.FloorToInt(centerPx.y - ryPx - 1f));
        var maxY = Mathf.Min(sizePx - 1, Mathf.CeilToInt(centerPx.y + ryPx + 1f));

        var rrX = rxPx * rxPx;
        var rrY = ryPx * ryPx;

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var dx = x - centerPx.x;
                var dy = y - centerPx.y;
                var d = (dx * dx) / rrX + (dy * dy) / rrY;
                if (d <= 1f)
                {
                    PlotAlpha(buffer, sizePx, x, y, alpha);
                }
            }
        }
    }

    private static void DrawModelLine(byte[] buffer, int sizePx, float x0, float y0, float x1, float y1, float thicknessPx, float angleDeg, float scale)
    {
        var p0 = ModelToPixel(RotatePoint(new Vector2(x0, y0), angleDeg), sizePx, scale);
        var p1 = ModelToPixel(RotatePoint(new Vector2(x1, y1), angleDeg), sizePx, scale);
        DrawThickLine(buffer, sizePx, p0.x, p0.y, p1.x, p1.y, thicknessPx);
    }

    private static void DrawThickLine(byte[] buffer, int sizePx, float x0, float y0, float x1, float y1, float thicknessPx)
    {
        var half = Mathf.Max(0.5f, thicknessPx * 0.5f);
        var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - half - 1f));
        var maxX = Mathf.Min(sizePx - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + half + 1f));
        var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - half - 1f));
        var maxY = Mathf.Min(sizePx - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + half + 1f));

        var segment = new Vector2(x1 - x0, y1 - y0);
        var segLenSq = segment.sqrMagnitude;
        if (segLenSq <= 0.0001f)
        {
            PlotAlpha(buffer, sizePx, Mathf.RoundToInt(x0), Mathf.RoundToInt(y0), 255);
            return;
        }

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var point = new Vector2(x, y);
                var t = Mathf.Clamp01(Vector2.Dot(point - new Vector2(x0, y0), segment) / segLenSq);
                var projection = new Vector2(x0, y0) + (segment * t);
                if ((point - projection).sqrMagnitude <= half * half)
                {
                    PlotAlpha(buffer, sizePx, x, y, 255);
                }
            }
        }
    }

    private static void PlotAlpha(byte[] buffer, int sizePx, int x, int y, byte alpha)
    {
        if (x < 0 || x >= sizePx || y < 0 || y >= sizePx)
        {
            return;
        }

        var index = (y * sizePx) + x;
        if (alpha > buffer[index])
        {
            buffer[index] = alpha;
        }
    }

    private static byte[] OutlineFromMask(byte[] mask, int sizePx)
    {
        var outline = new byte[mask.Length];
        for (var y = 0; y < sizePx; y++)
        {
            for (var x = 0; x < sizePx; x++)
            {
                var index = (y * sizePx) + x;
                if (mask[index] > 0)
                {
                    continue;
                }

                var neighborFilled = false;
                for (var oy = -1; oy <= 1 && !neighborFilled; oy++)
                {
                    for (var ox = -1; ox <= 1; ox++)
                    {
                        if (ox == 0 && oy == 0)
                        {
                            continue;
                        }

                        var nx = x + ox;
                        var ny = y + oy;
                        if (nx < 0 || nx >= sizePx || ny < 0 || ny >= sizePx)
                        {
                            continue;
                        }

                        if (mask[(ny * sizePx) + nx] > 0)
                        {
                            neighborFilled = true;
                            break;
                        }
                    }
                }

                if (neighborFilled)
                {
                    outline[index] = 255;
                }
            }
        }

        return outline;
    }

    private static Vector2 ModelToPixel(Vector2 modelPoint, int sizePx, float scale)
    {
        var center = (sizePx - 1) * 0.5f;
        return new Vector2(center + (modelPoint.x * scale), center + (modelPoint.y * scale));
    }

    private static Vector2 RotatePoint(Vector2 point, float angleDeg)
    {
        var rad = angleDeg * Mathf.Deg2Rad;
        var c = Mathf.Cos(rad);
        var s = Mathf.Sin(rad);
        return new Vector2((point.x * c) - (point.y * s), (point.x * s) + (point.y * c));
    }
}
