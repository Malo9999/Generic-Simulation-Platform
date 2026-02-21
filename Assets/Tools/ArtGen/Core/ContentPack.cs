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

    [Serializable]
    public struct SpeciesSelection
    {
        public string entityId;
        public List<string> speciesIds;
    }

    [Serializable]
    public struct ClipMetadataEntry
    {
        public string keyPrefix;
        public int fps;
        public int frameCount;
    }

    [SerializeField] private int version = 1;
    [SerializeField] private int seed;
    [SerializeField] private int tileSize;
    [SerializeField] private int spriteSize;
    [SerializeField] private string simulationId = string.Empty;
    [SerializeField] private string packId = string.Empty;
    [SerializeField] private List<TextureEntry> textures = new();
    [SerializeField] private List<SpriteEntry> sprites = new();
    [SerializeField] private List<SpeciesSelection> selections = new();
    [SerializeField] private List<ClipMetadataEntry> clipMetadata = new();

    [NonSerialized] private Dictionary<string, Sprite> spriteById;
    [NonSerialized] private Dictionary<string, List<string>> speciesByEntityId;
    [NonSerialized] private Dictionary<string, ClipMetadataEntry> clipByPrefix;

    public string Version => $"v{version}";
    public IReadOnlyList<TextureEntry> Textures => textures;
    public IReadOnlyList<SpriteEntry> Sprites => sprites;
    public IReadOnlyList<SpeciesSelection> Selections => selections;
    public IReadOnlyList<ClipMetadataEntry> ClipMetadata => clipMetadata;

    private void OnEnable() => BuildIndex();

    public override string ToString() => $"ContentPack[{packId}] {Version} (seed={seed}, tile={tileSize}, sprite={spriteSize})";

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
        InvalidateIndex();
        BuildIndex();
    }

    public void SetSelections(List<SpeciesSelection> selectionEntries)
    {
        selections = selectionEntries ?? new List<SpeciesSelection>();
        InvalidateIndex();
        BuildIndex();
    }

    public void SetClipMetadata(List<ClipMetadataEntry> entries)
    {
        clipMetadata = entries ?? new List<ClipMetadataEntry>();
        InvalidateIndex();
        BuildIndex();
    }

    public bool TryGetSprite(string id, out Sprite sprite)
    {
        BuildIndex();
        return spriteById.TryGetValue(id ?? string.Empty, out sprite);
    }

    public IEnumerable<string> GetAllSpriteIds()
    {
        BuildIndex();
        return spriteById.Keys;
    }

    public List<string> FindIdsByPrefix(string prefix, int max = 10)
    {
        BuildIndex();
        if (string.IsNullOrWhiteSpace(prefix) || max <= 0)
        {
            return new List<string>();
        }

        return spriteById.Keys
            .Where(id => id.StartsWith(prefix, StringComparison.Ordinal))
            .Take(max)
            .ToList();
    }

    public string InferFirstSpeciesId(string entityId)
    {
        BuildIndex();
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return string.Empty;
        }

        var prefix = $"agent:{entityId}:";
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        string firstSpecies = null;

        foreach (var id in spriteById.Keys)
        {
            if (!id.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var remainder = id.Substring(prefix.Length);
            var separatorIndex = remainder.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var species = remainder.Substring(0, separatorIndex);
            if (string.IsNullOrWhiteSpace(species))
            {
                continue;
            }

            if (firstSpecies == null)
            {
                firstSpecies = species;
            }

            counts[species] = counts.TryGetValue(species, out var current) ? current + 1 : 1;
        }

        if (counts.Count == 0)
        {
            return string.Empty;
        }

        var bestSpecies = firstSpecies;
        var bestCount = -1;
        foreach (var entry in counts)
        {
            if (entry.Value > bestCount)
            {
                bestSpecies = entry.Key;
                bestCount = entry.Value;
            }
        }

        return bestSpecies ?? string.Empty;
    }

    public string GetSpeciesId(string entityId, int variantIndex)
    {
        BuildIndex();
        if (string.IsNullOrWhiteSpace(entityId) || !speciesByEntityId.TryGetValue(entityId, out var speciesIds) || speciesIds.Count == 0)
        {
            return "default";
        }

        var index = Mathf.Abs(variantIndex) % speciesIds.Count;
        return string.IsNullOrWhiteSpace(speciesIds[index]) ? "default" : speciesIds[index];
    }

    public bool TryGetClipMetadata(string keyPrefix, out ClipMetadataEntry entry)
    {
        BuildIndex();
        return clipByPrefix.TryGetValue(keyPrefix ?? string.Empty, out entry);
    }

    public int InferFrameCountByPrefix(string keyPrefix)
    {
        if (string.IsNullOrWhiteSpace(keyPrefix))
        {
            return 0;
        }

        BuildIndex();
        var prefix = keyPrefix + ":";
        var maxFrame = -1;
        foreach (var id in spriteById.Keys)
        {
            if (!id.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var frameToken = id.Substring(prefix.Length);
            var underscoreIndex = frameToken.IndexOf('_');
            if (underscoreIndex >= 0)
            {
                frameToken = frameToken.Substring(0, underscoreIndex);
            }

            if (int.TryParse(frameToken, out var frameIndex))
            {
                maxFrame = Mathf.Max(maxFrame, frameIndex);
            }
        }

        return maxFrame + 1;
    }

    private void BuildIndex()
    {
        if (spriteById != null)
        {
            return;
        }

        spriteById = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        speciesByEntityId = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        clipByPrefix = new Dictionary<string, ClipMetadataEntry>(StringComparer.Ordinal);

        foreach (var entry in sprites)
        {
            if (!string.IsNullOrWhiteSpace(entry.id) && entry.sprite != null)
            {
                spriteById[entry.id] = entry.sprite;
            }
        }

        foreach (var selection in selections)
        {
            if (string.IsNullOrWhiteSpace(selection.entityId) || selection.speciesIds == null || selection.speciesIds.Count == 0)
            {
                continue;
            }

            speciesByEntityId[selection.entityId] = selection.speciesIds;
        }

        foreach (var entry in clipMetadata)
        {
            if (string.IsNullOrWhiteSpace(entry.keyPrefix))
            {
                continue;
            }

            clipByPrefix[entry.keyPrefix] = entry;
        }
    }

    private void InvalidateIndex()
    {
        spriteById = null;
        speciesByEntityId = null;
        clipByPrefix = null;
    }

    public Sprite FindSpriteById(string id) => sprites.FirstOrDefault(s => s.id == id).sprite;

    public List<Sprite> FindByCategory(string category) => sprites.Where(s => s.category == category).Select(s => s.sprite).Where(s => s != null).ToList();

    public List<SpriteEntry> FindByPrefix(string idPrefix) => sprites.Where(s => s.id.StartsWith(idPrefix, StringComparison.Ordinal)).ToList();
}
