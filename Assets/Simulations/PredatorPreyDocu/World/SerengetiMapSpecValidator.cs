using System;
using System.Collections.Generic;
using System.Linq;

public static class SerengetiMapSpecValidator
{
    private static readonly string[] RequiredRegionIds =
    {
        "south_ndutu", "central_seronera", "west_grumeti", "north_mara", "east_kopjes"
    };

    private static readonly string[] RequiredLegendSpecies =
    {
        "elephant", "giraffe", "buffalo", "zebra", "wildebeest", "hippo",
        "lion", "hyena", "leopard", "cheetah", "wilddog",
        "rhino", "crocodile", "impala", "gazelle", "waterbuck", "flamingo"
    };

    public static List<string> Validate(SerengetiMapSpec spec)
    {
        var errors = new List<string>();
        if (spec == null)
        {
            errors.Add("Map spec is null.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(spec.mapId))
        {
            errors.Add("mapId is required.");
        }

        if (spec.version < 1)
        {
            errors.Add("version must be >= 1.");
        }

        if (spec.arena == null || spec.arena.width < 200 || spec.arena.height < 200)
        {
            errors.Add("arena.width and arena.height must both be >= 200.");
        }

        var regions = spec.regions ?? new List<RegionSpec>();
        if (regions.Count != 5)
        {
            errors.Add($"regions must contain exactly 5 items, found {regions.Count}.");
        }

        var regionIds = regions.Select(region => region?.id ?? string.Empty).ToList();
        if (regionIds.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("each region requires a non-empty id.");
        }

        if (regionIds.Distinct(StringComparer.Ordinal).Count() != regionIds.Count)
        {
            errors.Add("region ids must be unique.");
        }

        var missingRegionIds = RequiredRegionIds.Where(required => !regionIds.Contains(required)).ToList();
        if (missingRegionIds.Count > 0)
        {
            errors.Add($"regions missing required ids: {string.Join(", ", missingRegionIds)}.");
        }

        for (var i = 0; i < regions.Count; i++)
        {
            var shape = regions[i]?.shape;
            if (shape == null)
            {
                errors.Add($"regions[{i}] shape is required.");
                continue;
            }

            ValidateNormalized(shape.xMin, $"regions[{i}].shape.xMin", errors);
            ValidateNormalized(shape.xMax, $"regions[{i}].shape.xMax", errors);
            ValidateNormalized(shape.yMin, $"regions[{i}].shape.yMin", errors);
            ValidateNormalized(shape.yMax, $"regions[{i}].shape.yMax", errors);
        }

        ValidateRiver(spec.water?.mainRiver, "water.mainRiver", errors);
        ValidateRiver(spec.water?.grumeti, "water.grumeti", errors);

        var pools = spec.water?.pools ?? new List<PoolSpec>();
        for (var i = 0; i < pools.Count; i++)
        {
            ValidateNormalized(pools[i].x, $"water.pools[{i}].x", errors);
            ValidateNormalized(pools[i].y, $"water.pools[{i}].y", errors);
        }

        var kopjes = spec.landmarks?.kopjes ?? new List<LandmarkNodeSpec>();
        for (var i = 0; i < kopjes.Count; i++)
        {
            ValidateNormalized(kopjes[i].x, $"landmarks.kopjes[{i}].x", errors);
            ValidateNormalized(kopjes[i].y, $"landmarks.kopjes[{i}].y", errors);
        }

        var wetlands = spec.landmarks?.wetlands ?? new List<LandmarkNodeSpec>();
        for (var i = 0; i < wetlands.Count; i++)
        {
            ValidateNormalized(wetlands[i].x, $"landmarks.wetlands[{i}].x", errors);
            ValidateNormalized(wetlands[i].y, $"landmarks.wetlands[{i}].y", errors);
        }

        var presence = spec.water?.seasonal?.presenceByMonth;
        if (presence == null)
        {
            errors.Add("water.seasonal.presenceByMonth is required.");
        }
        else
        {
            for (var month = 1; month <= 12; month++)
            {
                if (!presence.ContainsKey(month.ToString()))
                {
                    errors.Add($"water.seasonal.presenceByMonth missing key '{month}'.");
                }
            }
        }

        var species = spec.legend?.species;
        if (species == null)
        {
            errors.Add("legend.species is required.");
        }
        else
        {
            var missing = RequiredLegendSpecies.Where(required => !species.ContainsKey(required)).ToList();
            if (missing.Count > 0)
            {
                errors.Add($"legend.species missing required keys: {string.Join(", ", missing)}.");
            }
        }

        return errors;
    }

    private static void ValidateRiver(RiverSpec river, string path, List<string> errors)
    {
        if (river == null)
        {
            errors.Add($"{path} is required.");
            return;
        }

        var centerline = river.centerline ?? new List<PointSpec>();
        if (centerline.Count < 2)
        {
            errors.Add($"{path}.centerline must contain at least 2 points.");
        }

        for (var i = 0; i < centerline.Count; i++)
        {
            ValidateNormalized(centerline[i].x, $"{path}.centerline[{i}].x", errors);
            ValidateNormalized(centerline[i].y, $"{path}.centerline[{i}].y", errors);
        }

        var crossings = river.crossings ?? new List<CrossingSpec>();
        for (var i = 0; i < crossings.Count; i++)
        {
            ValidateNormalized(crossings[i].x, $"{path}.crossings[{i}].x", errors);
            ValidateNormalized(crossings[i].y, $"{path}.crossings[{i}].y", errors);
        }
    }

    private static void ValidateNormalized(float value, string path, List<string> errors)
    {
        if (value < 0f || value > 1f)
        {
            errors.Add($"{path} must be in [0..1], found {value}.");
        }
    }
}
