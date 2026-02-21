using System.Collections.Generic;
using UnityEngine;

public sealed class EnvironmentGenRequest
{
    public PackRecipe recipe;
    public bool overwrite;
    public string outputFolder;
}

public sealed class EnvironmentGenResult
{
    public readonly List<ContentPack.TextureEntry> textures = new();
    public readonly List<ContentPack.SpriteEntry> sprites = new();
}

public interface IEnvironmentModule
{
    string EnvironmentId { get; }
    EnvironmentGenResult Generate(EnvironmentGenRequest req);
}
