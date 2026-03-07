using System;

public interface IWorldRecipe
{
    string RecipeId { get; }
    int Version { get; }
    Type SettingsType { get; }
    WorldMap Generate(object settings, int seed, WorldGridSpec grid, NoiseSet noise, IWorldGenLogger log);
}
