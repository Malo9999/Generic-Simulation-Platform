using UnityEditor;

public static class AntTilesetsV1Generator
{
    private const int DefaultSeed = 137531;

    [MenuItem("Tools/GSP/Generate Ant Tilesets")]
    public static void GenerateFromMenu()
    {
        AntPackGeneratorWindow.OpenTilesOnly("LegacyTileset", DefaultSeed);
    }

    public static void Generate(int seed)
    {
        AntPackGeneratorWindow.OpenTilesOnly("LegacyTileset", seed);
    }
}
