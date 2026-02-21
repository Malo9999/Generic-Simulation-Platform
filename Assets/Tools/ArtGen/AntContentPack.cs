using System;
using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine;

[CreateAssetMenu(fileName = "AntContentPack", menuName = "GSP/Art/Ant Content Pack")]
public sealed class AntContentPack : ScriptableObject
{
    public const int CurrentVersion = 1;

    [Serializable]
    public struct SpriteLookupEntry
    {
        public string id;
        public Sprite sprite;
    }

    [SerializeField] private int version = CurrentVersion;
    [SerializeField] private int seed;
    [SerializeField] private int tileSize;
    [SerializeField] private string paletteId = string.Empty;
    [FormerlySerializedAs("tilesetTexture")]
    [SerializeField] private Texture2D tilesetTexture;
    [SerializeField] private Texture2D tilesetSurfaceTexture;
    [SerializeField] private Texture2D tilesetUndergroundTexture;
    [SerializeField] private Texture2D antsTexture;
    [SerializeField] private Texture2D propsTexture;
    [FormerlySerializedAs("tileSprites")]
    [SerializeField] private List<SpriteLookupEntry> tileSprites = new();
    [SerializeField] private List<SpriteLookupEntry> surfaceTileSprites = new();
    [SerializeField] private List<SpriteLookupEntry> undergroundTileSprites = new();
    [SerializeField] private List<SpriteLookupEntry> antRoleSprites = new();
    [SerializeField] private List<SpriteLookupEntry> propSprites = new();

    public int Version => version;
    public int Seed => seed;
    public int TileSize => tileSize;
    public string PaletteId => paletteId;
    public Texture2D TilesetTexture => tilesetTexture;
    public Texture2D TilesetSurfaceTexture => tilesetSurfaceTexture != null ? tilesetSurfaceTexture : tilesetTexture;
    public Texture2D TilesetUndergroundTexture => tilesetUndergroundTexture;
    public Texture2D AntsTexture => antsTexture;
    public Texture2D PropsTexture => propsTexture;
    public IReadOnlyList<SpriteLookupEntry> TileSprites => tileSprites;
    public IReadOnlyList<SpriteLookupEntry> SurfaceTileSprites => surfaceTileSprites;
    public IReadOnlyList<SpriteLookupEntry> UndergroundTileSprites => undergroundTileSprites;
    public IReadOnlyList<SpriteLookupEntry> AntRoleSprites => antRoleSprites;
    public IReadOnlyList<SpriteLookupEntry> PropSprites => propSprites;

    public void SetMetadata(int generatedSeed, int generatedTileSize, string generatedPaletteId)
    {
        version = CurrentVersion;
        seed = generatedSeed;
        tileSize = generatedTileSize;
        paletteId = generatedPaletteId;
    }

    public void SetTextures(Texture2D surfaceTiles, Texture2D undergroundTiles, Texture2D ants, Texture2D props)
    {
        tilesetTexture = surfaceTiles;
        tilesetSurfaceTexture = surfaceTiles;
        tilesetUndergroundTexture = undergroundTiles;
        antsTexture = ants;
        propsTexture = props;
    }

    public void SetLookups(List<SpriteLookupEntry> surfaceTiles, List<SpriteLookupEntry> undergroundTiles, List<SpriteLookupEntry> ants, List<SpriteLookupEntry> props)
    {
        surfaceTileSprites = surfaceTiles ?? new List<SpriteLookupEntry>();
        undergroundTileSprites = undergroundTiles ?? new List<SpriteLookupEntry>();
        tileSprites = surfaceTileSprites;
        antRoleSprites = ants ?? new List<SpriteLookupEntry>();
        propSprites = props ?? new List<SpriteLookupEntry>();
    }
}
