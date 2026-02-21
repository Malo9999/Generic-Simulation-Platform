using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PixelBlueprint2D", menuName = "GSP/Art/Blueprints/Pixel Blueprint 2D")]
public sealed class PixelBlueprint2D : ScriptableObject
{
    [Serializable]
    public sealed class Layer
    {
        public string name;
        public byte[] pixels;
    }

    [Min(1)] public int width = 32;
    [Min(1)] public int height = 32;
    public List<Layer> layers = new();

    public byte Get(string layerName, int x, int y)
    {
        var layer = EnsureLayer(layerName);
        return IsInBounds(x, y) ? layer.pixels[(y * width) + x] : (byte)0;
    }

    public void Set(string layerName, int x, int y, byte value)
    {
        if (!IsInBounds(x, y))
        {
            return;
        }

        var layer = EnsureLayer(layerName);
        layer.pixels[(y * width) + x] = value > 0 ? (byte)1 : (byte)0;
    }

    public void Clear(string layerName)
    {
        var layer = EnsureLayer(layerName);
        Array.Clear(layer.pixels, 0, layer.pixels.Length);
    }

    public void CloneTo(PixelBlueprint2D newAsset)
    {
        if (newAsset == null)
        {
            return;
        }

        newAsset.width = width;
        newAsset.height = height;
        newAsset.layers = new List<Layer>(layers.Count);
        foreach (var layer in layers)
        {
            var copied = new Layer
            {
                name = layer.name,
                pixels = new byte[width * height]
            };

            if (layer.pixels != null)
            {
                var len = Mathf.Min(layer.pixels.Length, copied.pixels.Length);
                Array.Copy(layer.pixels, copied.pixels, len);
            }

            newAsset.layers.Add(copied);
        }
    }

    public Layer EnsureLayer(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            layerName = "body";
        }

        EnsureIntegrity();
        foreach (var layer in layers)
        {
            if (string.Equals(layer.name, layerName, StringComparison.OrdinalIgnoreCase))
            {
                EnsureLayerPixels(layer);
                return layer;
            }
        }

        var created = new Layer
        {
            name = layerName,
            pixels = new byte[width * height]
        };
        layers.Add(created);
        return created;
    }

    private bool IsInBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

    private void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        EnsureIntegrity();
    }

    private void EnsureIntegrity()
    {
        layers ??= new List<Layer>();
        foreach (var layer in layers)
        {
            EnsureLayerPixels(layer);
        }
    }

    private void EnsureLayerPixels(Layer layer)
    {
        if (layer.pixels == null || layer.pixels.Length != width * height)
        {
            var resized = new byte[width * height];
            if (layer.pixels != null)
            {
                var len = Mathf.Min(layer.pixels.Length, resized.Length);
                Array.Copy(layer.pixels, resized, len);
            }

            layer.pixels = resized;
        }
    }
}
