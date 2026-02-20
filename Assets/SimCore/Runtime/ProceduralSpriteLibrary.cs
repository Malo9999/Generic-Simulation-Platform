using System.Collections.Generic;
using UnityEngine;

public enum MarbleStripe { None, Single, Double, Diagonal }
public enum AntRole { Queen, Worker, Warrior }
public enum CarLivery { Solid, CenterStripe, SideStripes, DiagonalStripe }
public enum AthleteKit { Home, Away, Alt }

public static class ProceduralSpriteLibrary
{
    private static readonly Dictionary<string, Sprite> SpriteCache = new();

    public static Sprite GetMarbleBase(int sizePx = 64) => GetOrCreate($"marble_base_{sizePx}", sizePx, DrawMarbleBase);

    public static Sprite GetMarbleStripe(MarbleStripe stripe, int sizePx = 64) =>
        GetOrCreate($"marble_stripe_{stripe}_{sizePx}", sizePx, pixels => DrawMarbleStripe(pixels, sizePx, stripe));

    public static Sprite GetAnt(AntRole role, int sizePx = 64) =>
        GetOrCreate($"ant_{role}_{sizePx}", sizePx, pixels => DrawAnt(pixels, sizePx, role));

    public static Sprite GetCarBase(int sizePx = 64) => GetOrCreate($"car_base_{sizePx}", sizePx, DrawCarBase);

    public static Sprite GetCarLivery(CarLivery livery, int sizePx = 64) =>
        GetOrCreate($"car_livery_{livery}_{sizePx}", sizePx, pixels => DrawCarLivery(pixels, sizePx, livery));

    public static Sprite GetAthleteBase(int sizePx = 64) => GetOrCreate($"athlete_base_{sizePx}", sizePx, DrawAthleteBase);

    public static Sprite GetAthleteShoulderpads(AthleteKit kit, int sizePx = 64) =>
        GetOrCreate($"athlete_pads_{kit}_{sizePx}", sizePx, pixels => DrawAthleteShoulderpads(pixels, sizePx, kit));

    private static Sprite GetOrCreate(string key, int sizePx, System.Action<Color32[]> drawAction)
    {
        if (SpriteCache.TryGetValue(key, out var cached) && cached != null)
        {
            return cached;
        }

        var pixels = new Color32[sizePx * sizePx];
        Clear(pixels, new Color32(255, 255, 255, 0));
        drawAction(pixels);

        var texture = new Texture2D(sizePx, sizePx, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        var sprite = Sprite.Create(texture, new Rect(0f, 0f, sizePx, sizePx), new Vector2(0.5f, 0.5f), sizePx);
        SpriteCache[key] = sprite;
        return sprite;
    }

    private static void DrawMarbleBase(Color32[] pixels)
    {
        var sizePx = Mathf.RoundToInt(Mathf.Sqrt(pixels.Length));
        var c = new Vector2((sizePx - 1) * 0.5f, (sizePx - 1) * 0.5f);
        var r = sizePx * 0.44f;
        DrawCircleFilled(pixels, sizePx, c, r, new Color32(255, 255, 255, 235), 1.25f);
        DrawCircleFilled(pixels, sizePx, c + new Vector2(-sizePx * 0.1f, sizePx * 0.1f), sizePx * 0.17f, new Color32(255, 255, 255, 120), 1f);
        DrawCircleOutline(pixels, sizePx, c, r, new Color32(255, 255, 255, 255), 1f);
    }

    private static void DrawMarbleStripe(Color32[] pixels, int sizePx, MarbleStripe stripe)
    {
        if (stripe == MarbleStripe.None)
        {
            return;
        }

        var center = new Vector2((sizePx - 1) * 0.5f, (sizePx - 1) * 0.5f);
        var radius = sizePx * 0.42f;
        var stripeColor = new Color32(255, 255, 255, 220);
        var thickness = Mathf.Max(2f, sizePx * 0.09f);

        switch (stripe)
        {
            case MarbleStripe.Single:
                DrawLine(pixels, sizePx, new Vector2(center.x, center.y - radius), new Vector2(center.x, center.y + radius), stripeColor, thickness);
                break;
            case MarbleStripe.Double:
                var offset = sizePx * 0.14f;
                DrawLine(pixels, sizePx, new Vector2(center.x - offset, center.y - radius), new Vector2(center.x - offset, center.y + radius), stripeColor, thickness * 0.8f);
                DrawLine(pixels, sizePx, new Vector2(center.x + offset, center.y - radius), new Vector2(center.x + offset, center.y + radius), stripeColor, thickness * 0.8f);
                break;
            case MarbleStripe.Diagonal:
                DrawLine(pixels, sizePx, new Vector2(center.x - radius * 0.85f, center.y - radius * 0.85f), new Vector2(center.x + radius * 0.85f, center.y + radius * 0.85f), stripeColor, thickness);
                break;
        }

        MaskOutsideCircle(pixels, sizePx, center, radius);
    }

    private static void DrawAnt(Color32[] pixels, int sizePx, AntRole role)
    {
        var c = new Vector2((sizePx - 1) * 0.5f, (sizePx - 1) * 0.5f);
        var bodyColor = new Color32(255, 255, 255, 235);
        var legColor = new Color32(255, 255, 255, 210);

        if (role == AntRole.Queen)
        {
            DrawEllipseFilled(pixels, sizePx, c + new Vector2(0f, -sizePx * 0.05f), sizePx * 0.16f, sizePx * 0.23f, bodyColor, 1f);
            DrawEllipseFilled(pixels, sizePx, c + new Vector2(0f, sizePx * 0.15f), sizePx * 0.2f, sizePx * 0.2f, bodyColor, 1f);
            DrawCircleFilled(pixels, sizePx, c + new Vector2(0f, -sizePx * 0.27f), sizePx * 0.1f, bodyColor, 1f);
            DrawLine(pixels, sizePx, c + new Vector2(-sizePx * 0.16f, 0f), c + new Vector2(-sizePx * 0.34f, sizePx * 0.02f), legColor, 2f);
            DrawLine(pixels, sizePx, c + new Vector2(sizePx * 0.16f, 0f), c + new Vector2(sizePx * 0.34f, sizePx * 0.02f), legColor, 2f);
        }
        else if (role == AntRole.Warrior)
        {
            DrawEllipseFilled(pixels, sizePx, c + new Vector2(0f, sizePx * 0.11f), sizePx * 0.2f, sizePx * 0.18f, bodyColor, 1f);
            DrawEllipseFilled(pixels, sizePx, c, sizePx * 0.14f, sizePx * 0.14f, bodyColor, 1f);
            DrawCircleFilled(pixels, sizePx, c + new Vector2(0f, -sizePx * 0.2f), sizePx * 0.13f, bodyColor, 1f);
            DrawLine(pixels, sizePx, c + new Vector2(-sizePx * 0.05f, -sizePx * 0.25f), c + new Vector2(-sizePx * 0.22f, -sizePx * 0.33f), legColor, 2f);
            DrawLine(pixels, sizePx, c + new Vector2(sizePx * 0.05f, -sizePx * 0.25f), c + new Vector2(sizePx * 0.22f, -sizePx * 0.33f), legColor, 2f);
        }
        else
        {
            DrawEllipseFilled(pixels, sizePx, c + new Vector2(0f, sizePx * 0.12f), sizePx * 0.17f, sizePx * 0.17f, bodyColor, 1f);
            DrawEllipseFilled(pixels, sizePx, c, sizePx * 0.12f, sizePx * 0.12f, bodyColor, 1f);
            DrawCircleFilled(pixels, sizePx, c + new Vector2(0f, -sizePx * 0.17f), sizePx * 0.09f, bodyColor, 1f);
        }

        for (var i = -1; i <= 1; i++)
        {
            var offset = i * sizePx * 0.08f;
            DrawLine(pixels, sizePx, c + new Vector2(-sizePx * 0.1f, offset), c + new Vector2(-sizePx * 0.3f, offset + sizePx * 0.06f), legColor, 1.5f);
            DrawLine(pixels, sizePx, c + new Vector2(sizePx * 0.1f, offset), c + new Vector2(sizePx * 0.3f, offset + sizePx * 0.06f), legColor, 1.5f);
        }

        OutlineAlpha(pixels, sizePx, new Color32(255, 255, 255, 255));
    }

    private static void DrawCarBase(Color32[] pixels)
    {
        var sizePx = Mathf.RoundToInt(Mathf.Sqrt(pixels.Length));
        var body = new Color32(255, 255, 255, 235);
        DrawRectFilled(pixels, sizePx, sizePx * 0.14f, sizePx * 0.24f, sizePx * 0.72f, sizePx * 0.33f, body, 1f);
        DrawRectFilled(pixels, sizePx, sizePx * 0.25f, sizePx * 0.53f, sizePx * 0.5f, sizePx * 0.18f, body, 1f);
        DrawCircleFilled(pixels, sizePx, new Vector2(sizePx * 0.28f, sizePx * 0.25f), sizePx * 0.08f, new Color32(255, 255, 255, 0), 1f);
        DrawCircleFilled(pixels, sizePx, new Vector2(sizePx * 0.72f, sizePx * 0.25f), sizePx * 0.08f, new Color32(255, 255, 255, 0), 1f);
        OutlineAlpha(pixels, sizePx, new Color32(255, 255, 255, 255));
    }

    private static void DrawCarLivery(Color32[] pixels, int sizePx, CarLivery livery)
    {
        if (livery == CarLivery.Solid)
        {
            DrawRectFilled(pixels, sizePx, sizePx * 0.14f, sizePx * 0.24f, sizePx * 0.72f, sizePx * 0.45f, new Color32(255, 255, 255, 90), 1f);
            return;
        }

        var col = new Color32(255, 255, 255, 220);
        if (livery == CarLivery.CenterStripe)
        {
            DrawRectFilled(pixels, sizePx, sizePx * 0.45f, sizePx * 0.24f, sizePx * 0.1f, sizePx * 0.45f, col, 1f);
        }
        else if (livery == CarLivery.SideStripes)
        {
            DrawRectFilled(pixels, sizePx, sizePx * 0.2f, sizePx * 0.29f, sizePx * 0.12f, sizePx * 0.3f, col, 1f);
            DrawRectFilled(pixels, sizePx, sizePx * 0.68f, sizePx * 0.29f, sizePx * 0.12f, sizePx * 0.3f, col, 1f);
        }
        else if (livery == CarLivery.DiagonalStripe)
        {
            DrawLine(pixels, sizePx, new Vector2(sizePx * 0.2f, sizePx * 0.3f), new Vector2(sizePx * 0.8f, sizePx * 0.58f), col, sizePx * 0.12f);
        }

        MaskOutsideCar(pixels, sizePx);
    }

    private static void DrawAthleteBase(Color32[] pixels)
    {
        var sizePx = Mathf.RoundToInt(Mathf.Sqrt(pixels.Length));
        var c = new Vector2((sizePx - 1) * 0.5f, (sizePx - 1) * 0.5f);
        var col = new Color32(255, 255, 255, 235);

        DrawCircleFilled(pixels, sizePx, c + new Vector2(0f, sizePx * 0.2f), sizePx * 0.1f, col, 1f);
        DrawEllipseFilled(pixels, sizePx, c + new Vector2(0f, 0f), sizePx * 0.16f, sizePx * 0.2f, col, 1f);
        DrawRectFilled(pixels, sizePx, sizePx * 0.28f, sizePx * 0.4f, sizePx * 0.12f, sizePx * 0.22f, col, 1f);
        DrawRectFilled(pixels, sizePx, sizePx * 0.6f, sizePx * 0.4f, sizePx * 0.12f, sizePx * 0.22f, col, 1f);
        DrawRectFilled(pixels, sizePx, sizePx * 0.4f, sizePx * 0.66f, sizePx * 0.08f, sizePx * 0.18f, col, 1f);
        DrawRectFilled(pixels, sizePx, sizePx * 0.52f, sizePx * 0.66f, sizePx * 0.08f, sizePx * 0.18f, col, 1f);
        OutlineAlpha(pixels, sizePx, new Color32(255, 255, 255, 255));
    }

    private static void DrawAthleteShoulderpads(Color32[] pixels, int sizePx, AthleteKit kit)
    {
        var col = new Color32(255, 255, 255, 220);
        var y = sizePx * 0.41f;
        var width = kit == AthleteKit.Alt ? sizePx * 0.2f : sizePx * 0.16f;
        var height = kit == AthleteKit.Home ? sizePx * 0.1f : sizePx * 0.12f;
        DrawEllipseFilled(pixels, sizePx, new Vector2(sizePx * 0.38f, y), width, height, col, 1f);
        DrawEllipseFilled(pixels, sizePx, new Vector2(sizePx * 0.62f, y), width, height, col, 1f);
        if (kit == AthleteKit.Away)
        {
            DrawRectFilled(pixels, sizePx, sizePx * 0.45f, sizePx * 0.36f, sizePx * 0.1f, sizePx * 0.08f, col, 1f);
        }
    }

    private static void MaskOutsideCircle(Color32[] pixels, int sizePx, Vector2 center, float radius)
    {
        var rr = radius * radius;
        for (var y = 0; y < sizePx; y++)
        {
            for (var x = 0; x < sizePx; x++)
            {
                var d = (new Vector2(x, y) - center).sqrMagnitude;
                if (d > rr)
                {
                    pixels[(y * sizePx) + x] = new Color32(255, 255, 255, 0);
                }
            }
        }
    }

    private static void MaskOutsideCar(Color32[] pixels, int sizePx)
    {
        for (var y = 0; y < sizePx; y++)
        {
            for (var x = 0; x < sizePx; x++)
            {
                var inLower = x >= sizePx * 0.14f && x <= sizePx * 0.86f && y >= sizePx * 0.24f && y <= sizePx * 0.57f;
                var inUpper = x >= sizePx * 0.25f && x <= sizePx * 0.75f && y >= sizePx * 0.53f && y <= sizePx * 0.71f;
                if (!(inLower || inUpper))
                {
                    pixels[(y * sizePx) + x] = new Color32(255, 255, 255, 0);
                }
            }
        }
    }

    private static void Clear(Color32[] pixels, Color32 color)
    {
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
    }

    private static void DrawCircleFilled(Color32[] pixels, int sizePx, Vector2 center, float radius, Color32 color, float featherPx = 0f)
    {
        DrawEllipseFilled(pixels, sizePx, center, radius, radius, color, featherPx);
    }

    private static void DrawEllipseFilled(Color32[] pixels, int sizePx, Vector2 center, float radiusX, float radiusY, Color32 color, float featherPx = 0f)
    {
        var minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radiusX - featherPx - 1f));
        var maxX = Mathf.Min(sizePx - 1, Mathf.CeilToInt(center.x + radiusX + featherPx + 1f));
        var minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radiusY - featherPx - 1f));
        var maxY = Mathf.Min(sizePx - 1, Mathf.CeilToInt(center.y + radiusY + featherPx + 1f));

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var nx = (x - center.x) / Mathf.Max(0.001f, radiusX);
                var ny = (y - center.y) / Mathf.Max(0.001f, radiusY);
                var d = Mathf.Sqrt((nx * nx) + (ny * ny));
                if (d > 1f + (featherPx / Mathf.Max(radiusX, radiusY)))
                {
                    continue;
                }

                var alpha = 1f;
                if (d > 1f && featherPx > 0f)
                {
                    alpha = Mathf.Clamp01(1f - ((d - 1f) * Mathf.Max(radiusX, radiusY) / featherPx));
                }

                BlendPixel(pixels, sizePx, x, y, color, alpha);
            }
        }
    }

    private static void DrawRectFilled(Color32[] pixels, int sizePx, float x, float y, float width, float height, Color32 color, float featherPx = 0f)
    {
        var minX = Mathf.Max(0, Mathf.FloorToInt(x - featherPx));
        var maxX = Mathf.Min(sizePx - 1, Mathf.CeilToInt(x + width + featherPx));
        var minY = Mathf.Max(0, Mathf.FloorToInt(y - featherPx));
        var maxY = Mathf.Min(sizePx - 1, Mathf.CeilToInt(y + height + featherPx));

        for (var py = minY; py <= maxY; py++)
        {
            for (var px = minX; px <= maxX; px++)
            {
                var dx = Mathf.Max(Mathf.Max(x - px, 0f), px - (x + width));
                var dy = Mathf.Max(Mathf.Max(y - py, 0f), py - (y + height));
                var edgeDist = Mathf.Sqrt((dx * dx) + (dy * dy));
                if (edgeDist > featherPx)
                {
                    continue;
                }

                var alpha = featherPx <= 0f ? 1f : Mathf.Clamp01(1f - (edgeDist / featherPx));
                BlendPixel(pixels, sizePx, px, py, color, alpha);
            }
        }
    }

    private static void DrawLine(Color32[] pixels, int sizePx, Vector2 from, Vector2 to, Color32 color, float thickness)
    {
        var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.x, to.x) - thickness));
        var maxX = Mathf.Min(sizePx - 1, Mathf.CeilToInt(Mathf.Max(from.x, to.x) + thickness));
        var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.y, to.y) - thickness));
        var maxY = Mathf.Min(sizePx - 1, Mathf.CeilToInt(Mathf.Max(from.y, to.y) + thickness));

        var dir = to - from;
        var lenSq = Mathf.Max(0.0001f, dir.sqrMagnitude);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var p = new Vector2(x, y);
                var t = Mathf.Clamp01(Vector2.Dot(p - from, dir) / lenSq);
                var nearest = from + (dir * t);
                var dist = Vector2.Distance(p, nearest);
                if (dist > thickness * 0.5f)
                {
                    continue;
                }

                var alpha = Mathf.Clamp01(1f - (dist / Mathf.Max(0.001f, thickness * 0.5f)));
                BlendPixel(pixels, sizePx, x, y, color, alpha);
            }
        }
    }

    private static void DrawCircleOutline(Color32[] pixels, int sizePx, Vector2 center, float radius, Color32 color, float thickness)
    {
        var inner = (radius - (thickness * 0.5f)) * (radius - (thickness * 0.5f));
        var outer = (radius + (thickness * 0.5f)) * (radius + (thickness * 0.5f));

        var minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius - thickness));
        var maxX = Mathf.Min(sizePx - 1, Mathf.CeilToInt(center.x + radius + thickness));
        var minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius - thickness));
        var maxY = Mathf.Min(sizePx - 1, Mathf.CeilToInt(center.y + radius + thickness));

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var d = (new Vector2(x, y) - center).sqrMagnitude;
                if (d >= inner && d <= outer)
                {
                    BlendPixel(pixels, sizePx, x, y, color, 1f);
                }
            }
        }
    }

    private static void OutlineAlpha(Color32[] pixels, int sizePx, Color32 outline)
    {
        var copy = (Color32[])pixels.Clone();
        for (var y = 1; y < sizePx - 1; y++)
        {
            for (var x = 1; x < sizePx - 1; x++)
            {
                var idx = (y * sizePx) + x;
                if (copy[idx].a > 0)
                {
                    continue;
                }

                var hasNeighbor = copy[idx - 1].a > 0 || copy[idx + 1].a > 0 || copy[idx - sizePx].a > 0 || copy[idx + sizePx].a > 0;
                if (hasNeighbor)
                {
                    pixels[idx] = outline;
                }
            }
        }
    }

    private static void BlendPixel(Color32[] pixels, int sizePx, int x, int y, Color32 color, float alphaScale)
    {
        if (x < 0 || x >= sizePx || y < 0 || y >= sizePx)
        {
            return;
        }

        var idx = (y * sizePx) + x;
        var targetAlpha = (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * Mathf.Clamp01(alphaScale)), 0, 255);
        if (targetAlpha > pixels[idx].a)
        {
            pixels[idx] = new Color32(255, 255, 255, targetAlpha);
        }
    }
}
