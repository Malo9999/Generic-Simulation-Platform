using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class WorldMapRuntime
{
    public WorldMapAsset asset;
    public string recipeId;
    public string mapId;
    public int seed;
    public WorldGridSpec grid;

    public readonly Dictionary<string, ScalarField> scalars = new(StringComparer.Ordinal);
    public readonly Dictionary<string, MaskField> masks = new(StringComparer.Ordinal);
    public readonly Dictionary<string, WorldSpline> splines = new(StringComparer.Ordinal);
    public readonly Dictionary<string, ScatterSet> scatters = new(StringComparer.Ordinal);

    public bool TryGetScatter(string id, out ScatterSet scatter) => scatters.TryGetValue(id, out scatter);
    public bool TryGetMask(string id, out MaskField mask) => masks.TryGetValue(id, out mask);
    public bool TryGetSpline(string id, out WorldSpline spline) => splines.TryGetValue(id, out spline);
}

public static class WorldRuntime
{
    public static WorldMapRuntime Load(WorldMapAsset asset)
    {
        if (asset == null) return null;

        var runtime = new WorldMapRuntime
        {
            asset = asset,
            recipeId = asset.recipeId,
            mapId = asset.mapId,
            seed = asset.seed,
            grid = asset.grid
        };

        if (asset.scalarRefs != null)
        {
            for (var i = 0; i < asset.scalarRefs.Count; i++)
            {
                var field = LoadScalar(asset.scalarRefs[i], asset.grid);
                if (field != null) runtime.scalars[field.id] = field;
            }
        }

        if (asset.maskRefs != null)
        {
            for (var i = 0; i < asset.maskRefs.Count; i++)
            {
                var mask = LoadMask(asset.maskRefs[i], asset.grid);
                if (mask != null) runtime.masks[mask.id] = mask;
            }
        }

        if (asset.splineRefs != null)
        {
            for (var i = 0; i < asset.splineRefs.Count; i++)
            {
                var spline = LoadSpline(asset.splineRefs[i]);
                if (spline != null && !string.IsNullOrEmpty(spline.id)) runtime.splines[spline.id] = spline;
            }
        }

        if (asset.scatterRefs != null)
        {
            for (var i = 0; i < asset.scatterRefs.Count; i++)
            {
                var scatter = LoadScatter(asset.scatterRefs[i]);
                if (scatter != null && !string.IsNullOrEmpty(scatter.id)) runtime.scatters[scatter.id] = scatter;
            }
        }

        return runtime;
    }

    private static ScalarField LoadScalar(FieldRef fieldRef, WorldGridSpec grid)
    {
        if (fieldRef == null || string.IsNullOrEmpty(fieldRef.id)) return null;
        var tex = fieldRef.asset as Texture2D;
        if (tex == null) return null;

        var pixels = ReadTexture(tex, grid.width, grid.height);
        if (pixels == null || pixels.Length == 0) return null;

        var scalar = new ScalarField(fieldRef.id, grid);
        var len = Mathf.Min(scalar.values.Length, pixels.Length);
        for (var i = 0; i < len; i++) scalar.values[i] = pixels[i].r;
        return scalar;
    }

    private static MaskField LoadMask(MaskRef maskRef, WorldGridSpec grid)
    {
        if (maskRef == null || string.IsNullOrEmpty(maskRef.id)) return null;
        var tex = maskRef.asset as Texture2D;
        if (tex == null) return null;

        var pixels = ReadTexture(tex, grid.width, grid.height);
        if (pixels == null || pixels.Length == 0) return null;

        var mask = new MaskField(maskRef.id, grid, maskRef.encoding)
        {
            categories = maskRef.categories ?? Array.Empty<string>()
        };

        var len = Mathf.Min(mask.values.Length, pixels.Length);
        for (var i = 0; i < len; i++) mask.values[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(pixels[i].r) * 255f);
        return mask;
    }

    private static WorldSpline LoadSpline(SplineRef splineRef)
    {
        if (splineRef == null) return null;
        var text = splineRef.asset as TextAsset;
        if (text == null || string.IsNullOrWhiteSpace(text.text)) return null;
        var spline = JsonUtility.FromJson<WorldSpline>(text.text);
        if (spline != null && string.IsNullOrEmpty(spline.id)) spline.id = splineRef.id;
        return spline;
    }

    private static ScatterSet LoadScatter(ScatterRef scatterRef)
    {
        if (scatterRef == null) return null;
        var text = scatterRef.asset as TextAsset;
        if (text == null || string.IsNullOrWhiteSpace(text.text)) return null;
        var scatter = JsonUtility.FromJson<ScatterSet>(text.text);
        if (scatter != null && string.IsNullOrEmpty(scatter.id)) scatter.id = scatterRef.id;
        return scatter;
    }

    private static Color[] ReadTexture(Texture2D texture, int width, int height)
    {
        var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            var readable = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readable.Apply(false, false);
            var pixels = readable.GetPixels();
            UnityEngine.Object.Destroy(readable);
            return pixels;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}
