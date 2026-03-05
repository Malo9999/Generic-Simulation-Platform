using System;

public abstract class WorldRecipeBase<TSettings> : IWorldRecipe where TSettings : WorldRecipeSettingsSO
{
    public abstract string RecipeId { get; }
    public abstract int Version { get; }
    public Type SettingsType => typeof(TSettings);

    public WorldMap Generate(object settings, int seed, WorldGridSpec grid, NoiseDescriptorSet noise, IWorldGenLogger log)
    {
        var typed = settings as TSettings;
        if (typed == null)
        {
            throw new ArgumentException($"Expected settings type {typeof(TSettings).Name}");
        }

        return GenerateTyped(typed, seed, grid, noise, log);
    }

    protected abstract WorldMap GenerateTyped(TSettings settings, int seed, WorldGridSpec grid, NoiseDescriptorSet noise, IWorldGenLogger log);
}
