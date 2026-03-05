using UnityEngine;

public class VoidNeonRecipe : WorldRecipeBase<VoidNeonSettingsSO>
{
    public override string RecipeId => "VoidNeon";
    public override int Version => 1;

    protected override WorldMap GenerateTyped(VoidNeonSettingsSO settings, int seed, WorldGridSpec grid, NoiseDescriptorSet noise, IWorldGenLogger log)
    {
        var map = new WorldMap { recipeId = RecipeId, seed = seed, grid = grid };
        var rng = new WorldGenRng(seed);

        var walkable = new MaskField("walkable", grid, MaskEncoding.Boolean);
        var zones = new MaskField("zones", grid, MaskEncoding.Categorical) { categories = new[] { "arena" } };
        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var edge = x < settings.marginCells || y < settings.marginCells || x >= grid.width - settings.marginCells || y >= grid.height - settings.marginCells;
            walkable[x, y] = (byte)(edge ? 0 : 1);
            zones[x, y] = 0;
        }
        map.masks["walkable"] = walkable;
        map.masks["zones"] = zones;

        var glow = new ScalarField("glow", grid);
        var railCount = Mathf.Max(1, settings.railsCount);
        for (var r = 0; r < railCount; r++)
        {
            var spline = new WorldSpline { id = $"neon_rails_{r}", baseWidth = settings.railWidth };
            var points = Mathf.Max(8, Mathf.RoundToInt(grid.width * settings.railLengthFactor));
            var startY = (r + 1f) / (railCount + 1f) * grid.height;
            for (var i = 0; i < points; i++)
            {
                var t = i / (float)(points - 1);
                var x = Mathf.Lerp(settings.marginCells, grid.width - 1 - settings.marginCells, t);
                var y = startY + Mathf.Sin((t + r) * Mathf.PI * 2f) * settings.railCurvature * grid.height;
                spline.points.Add(grid.CellCenterWorld(Mathf.Clamp(Mathf.RoundToInt(x), 0, grid.width - 1), Mathf.Clamp(Mathf.RoundToInt(y), 0, grid.height - 1)));
            }
            map.splines.Add(spline);

            var emitters = map.scatters.ContainsKey("emitters") ? map.scatters["emitters"] : new ScatterSet { id = "emitters" };
            var distanceStep = Mathf.Max(1f, settings.emitterSpacing);
            float carry = 0f;
            for (var i = 1; i < spline.points.Count; i++)
            {
                var a = spline.points[i - 1];
                var b = spline.points[i];
                var segLen = Vector2.Distance(a, b);
                var d = carry;
                while (d <= segLen)
                {
                    var t = segLen < 0.0001f ? 0f : d / segLen;
                    var pos = Vector2.Lerp(a, b, t);
                    emitters.points.Add(new ScatterPoint { pos = pos, rotDeg = 0f, scale = 1f, typeId = 0, tags = new[] { "rail_emitter" } });
                    d += distanceStep;
                }
                carry = d - segLen;
            }
            map.scatters["emitters"] = emitters;
        }

        var glowNoise = noise.GetOrCreate("voidneon_glow", rng.Fork("noise"));
        glowNoise.scale = settings.noiseScale;
        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var p = grid.CellCenterWorld(x, y);
            var minDist = float.MaxValue;
            for (var s = 0; s < map.splines.Count; s++)
            {
                var sp = map.splines[s];
                for (var i = 1; i < sp.points.Count; i++)
                {
                    var d = DistanceToSegment(p, sp.points[i - 1], sp.points[i]);
                    if (d < minDist) minDist = d;
                }
            }
            var railGlow = Mathf.Exp(-minDist / Mathf.Max(0.01f, settings.glowFalloff));
            var n = NoiseUtil.Sample2D(glowNoise, x, y);
            glow[x, y] = Mathf.Clamp01(railGlow * 0.75f + n * 0.25f);
        }

        glow.Normalize01InPlace();
        map.scalars["glow"] = glow;
        map.zones["arena"] = new ZoneDef { zoneId = 0, name = "arena" };
        map.EnsureRequiredOutputs();
        log.Log($"Generated VoidNeon rails={map.splines.Count}, emitters={map.scatters["emitters"].points.Count}");
        return map;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var denom = ab.sqrMagnitude;
        if (denom < 1e-6f) return Vector2.Distance(p, a);
        var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / denom);
        return Vector2.Distance(p, a + ab * t);
    }
}
