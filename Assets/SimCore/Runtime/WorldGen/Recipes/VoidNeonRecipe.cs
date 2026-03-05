using UnityEngine;
using System.Collections.Generic;

public class VoidNeonRecipe : WorldRecipeBase<VoidNeonSettingsSO>
{
    public override string RecipeId => "VoidNeon";
    public override int Version => 2;

    protected override WorldMap GenerateTyped(VoidNeonSettingsSO settings, int seed, WorldGridSpec grid, NoiseDescriptorSet noise, IWorldGenLogger log)
    {
        var map = new WorldMap { recipeId = RecipeId, seed = seed, grid = grid };
        var rng = new WorldGenRng(seed);

        var marginWorld = Mathf.Max(0, settings.marginCells) * grid.cellSize;
        var usable = new Rect(
            grid.originWorld.x + marginWorld,
            grid.originWorld.y + marginWorld,
            Mathf.Max(grid.cellSize, grid.width * grid.cellSize - marginWorld * 2f),
            Mathf.Max(grid.cellSize, grid.height * grid.cellSize - marginWorld * 2f));

        var nodeMinDist = Mathf.Max(1f, settings.nodeMinDist * grid.cellSize);
        var graph = GraphGenerator.GenerateOrganicRect(
            usable,
            Mathf.Max(8, settings.nodeCount),
            nodeMinDist,
            settings.kNearest,
            Mathf.Max(0.1f, settings.edgeWidthMin),
            Mathf.Max(settings.edgeWidthMin, settings.edgeWidthMax),
            rng.Fork("graph"));

        map.splines = GraphToSplines.Build(graph, Mathf.Max(0f, settings.organicJitter), settings.smoothIterations, rng.Fork("splines"));

        var mapRect = new Rect(
            grid.originWorld.x,
            grid.originWorld.y,
            grid.width * grid.cellSize,
            grid.height * grid.cellSize);

        var clippedSplines = new List<WorldSpline>();
        for (var i = 0; i < map.splines.Count; i++)
        {
            var parts = SplineClipper.ClipToRectParts(map.splines[i], mapRect);
            if (parts.Count == 0) continue;
            clippedSplines.AddRange(parts);
        }

        map.splines = clippedSplines;

        var lanes = SplineRasterizer.RasterizeLanes("lanes", grid, map.splines, 0.25f * grid.cellSize);
        map.masks["lanes"] = lanes;

        var walkable = new MaskField("walkable", grid, MaskEncoding.Boolean);
        for (var i = 0; i < walkable.values.Length; i++) walkable.values[i] = lanes.values[i] > 0 ? (byte)1 : (byte)0;
        map.masks["walkable"] = walkable;

        var zones = new MaskField("zones", grid, MaskEncoding.Categorical) { categories = new[] { "arena" } };
        map.masks["zones"] = zones;
        map.zones["arena"] = new ZoneDef { zoneId = 0, name = "arena" };

        AnchorGenerator.PopulateAnchors(map, graph, settings.anchorSpacing * grid.cellSize);

        var glow = new ScalarField("glow", grid);
        var glowNoise = noise.GetOrCreate("voidneon_glow", rng.Fork("noise"));
        glowNoise.scale = settings.noiseScale;
        for (var y = 0; y < grid.height; y++)
        for (var x = 0; x < grid.width; x++)
        {
            var pos = grid.CellCenterWorld(x, y);
            var nearestSpline = WorldMapQuery.GetNearestSpline(map, pos);
            var d = nearestSpline == null ? float.MaxValue : Vector2.Distance(pos, WorldMapQuery.GetNearestPointOnSpline(nearestSpline, pos));
            var g = Mathf.Exp(-d / Mathf.Max(0.01f, settings.glowFalloff));
            var n = NoiseUtil.Sample2D(glowNoise, x, y);
            glow[x, y] = Mathf.Clamp01(g * 0.8f + n * 0.2f);
        }

        glow.Normalize01InPlace();
        map.scalars["glow"] = glow;

        map.EnsureRequiredOutputs();
        log.Log($"Generated VoidNeon nodes={graph.nodes.Count}, edges={graph.edges.Count}, lanes={map.scatters["anchors_lane"].points.Count}");
        return map;
    }
}
