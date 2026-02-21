public interface IPackPreset
{
    string PresetId { get; }
    PackRecipe CreateDefaultRecipe(string packId, int seed);
}
