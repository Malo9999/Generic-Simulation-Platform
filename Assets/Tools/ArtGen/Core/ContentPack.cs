using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ContentPack", menuName = "GSP/Art/Content Pack")]
public sealed class ContentPack : ScriptableObject
{
    [Serializable]
    public struct TextureEntry
    {
        public string id;
        public Texture2D texture;
    }

    [Serializable]
    public struct SpriteEntry
    {
        public string id;
        public string category;
        public Sprite sprite;
    }

    [SerializeField] private int version = 1;
    [SerializeField] private int seed;
    [SerializeField] private int tileSize;
    [SerializeField] private int spriteSize;
    [SerializeField] private string simulationId = string.Empty;
    [SerializeField] private string packId = string.Empty;
    [SerializeField] private List<TextureEntry> textures = new();
    [SerializeField] private List<SpriteEntry> sprites = new();

    public IReadOnlyList<TextureEntry> Textures => textures;
    public IReadOnlyList<SpriteEntry> Sprites => sprites;

    public void SetMetadata(PackRecipe recipe)
    {
        version = 1;
        seed = recipe.seed;
        tileSize = recipe.tileSize;
        spriteSize = recipe.agentSpriteSize;
        simulationId = recipe.simulationId;
        packId = recipe.packId;
    }

    public void SetEntries(List<TextureEntry> textureEntries, List<SpriteEntry> spriteEntries)
    {
        textures = textureEntries ?? new List<TextureEntry>();
        sprites = spriteEntries ?? new List<SpriteEntry>();
    }

    public Sprite FindSpriteById(string id) => sprites.FirstOrDefault(s => s.id == id).sprite;

    public List<Sprite> FindByCategory(string category) => sprites.Where(s => s.category == category).Select(s => s.sprite).Where(s => s != null).ToList();

    public List<SpriteEntry> FindByPrefix(string idPrefix) => sprites.Where(s => s.id.StartsWith(idPrefix, StringComparison.Ordinal)).ToList();
}
