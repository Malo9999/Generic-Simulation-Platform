using UnityEngine;

public static class PlaceholderColorPalette
{
    private static readonly Color32[] Palette =
    {
        new(231, 76, 60, 255),
        new(52, 152, 219, 255),
        new(46, 204, 113, 255),
        new(241, 196, 15, 255),
        new(155, 89, 182, 255),
        new(26, 188, 156, 255),
        new(230, 126, 34, 255),
        new(236, 240, 241, 255),
        new(52, 73, 94, 255),
        new(127, 140, 141, 255)
    };

    public static Color GetColor(in VisualKey key)
    {
        if (key.groupId >= 0)
        {
            return Palette[key.groupId % Palette.Length];
        }

        if (!string.IsNullOrEmpty(key.kind))
        {
            return Palette[ComputePaletteIndex(key.kind)];
        }

        return Palette[ComputePaletteIndex(key.entityId)];
    }

    private static int ComputePaletteIndex(string value)
    {
        var hash = ComputeStableHash(value);
        return (int)(hash % (uint)Palette.Length);
    }

    private static uint ComputeStableHash(string value)
    {
        const uint fnvPrime = 16777619u;
        var hash = 2166136261u;
        if (string.IsNullOrEmpty(value))
        {
            return hash;
        }

        for (var i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= fnvPrime;
        }

        return hash;
    }
}
