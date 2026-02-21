using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public enum AntPalettePreset
{
    Classic,
    Desert,
    Twilight
}

public static class AntPackGenerator
{
    public const string DefaultBaseFolder = "Assets/Presentation/Packs/Ants";

    public sealed class Request
    {
        public string PackName;
        public int Seed;
        public int TileSize;
        public int AntSpriteSize;
        public AntPalettePreset Palette;
        public string OutputFolder;
        public bool GenerateTiles;
        public bool GenerateAnts;
        public bool GenerateProps;
        public bool Overwrite;
    }

    public static string Generate(Request request)
    {
        Validate(request);
        ImportSettingsUtil.EnsureFolder(DefaultBaseFolder);
        ImportSettingsUtil.EnsureFolder(request.OutputFolder);

        var surfaceTileLookups = new List<AntContentPack.SpriteLookupEntry>();
        var undergroundTileLookups = new List<AntContentPack.SpriteLookupEntry>();
        var antLookups = new List<AntContentPack.SpriteLookupEntry>();
        var propLookups = new List<AntContentPack.SpriteLookupEntry>();

        Texture2D surfaceTilesTexture = null;
        Texture2D undergroundTilesTexture = null;
        Texture2D antsTexture = null;
        Texture2D propsTexture = null;

        if (request.GenerateTiles)
        {
            var tileResult = AntTilesetSheetGenerator.Generate(request.OutputFolder, request.Seed, request.TileSize, request.Palette, request.Overwrite);
            surfaceTilesTexture = tileResult.SurfaceTexture;
            undergroundTilesTexture = tileResult.UndergroundTexture;
            surfaceTileLookups = tileResult.SurfaceSprites.Select(s => new AntContentPack.SpriteLookupEntry { id = s.name, sprite = s }).ToList();
            undergroundTileLookups = tileResult.UndergroundSprites.Select(s => new AntContentPack.SpriteLookupEntry { id = s.name, sprite = s }).ToList();

        }

        if (request.GenerateAnts)
        {
            var antResult = AntSpriteSheetGenerator.Generate(request.OutputFolder, request.Seed, request.AntSpriteSize, request.Palette, request.Overwrite);
            antsTexture = antResult.Texture;
            antLookups = antResult.Sprites.Select(s => new AntContentPack.SpriteLookupEntry { id = s.name, sprite = s }).ToList();
        }

        if (request.GenerateProps)
        {
            var propResult = AntPropsGenerator.Generate(request.OutputFolder, request.Seed, request.TileSize, request.Palette, request.Overwrite);
            propsTexture = propResult.Texture;
            propLookups = propResult.Sprites.Select(s => new AntContentPack.SpriteLookupEntry { id = s.name, sprite = s }).ToList();
        }

        var assetPath = Path.Combine(request.OutputFolder, "AntContentPack.asset").Replace('\\', '/');
        var pack = AssetDatabase.LoadAssetAtPath<AntContentPack>(assetPath);
        if (pack == null)
        {
            pack = ScriptableObject.CreateInstance<AntContentPack>();
            AssetDatabase.CreateAsset(pack, assetPath);
        }

        pack.SetMetadata(request.Seed, request.TileSize, request.Palette.ToString());
        pack.SetTextures(surfaceTilesTexture, undergroundTilesTexture, antsTexture, propsTexture);
        pack.SetLookups(surfaceTileLookups, undergroundTileLookups, antLookups, propLookups);
        EditorUtility.SetDirty(pack);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(pack);

        Debug.Log($"Ant pack generated: '{request.PackName}'\nSurface tiles: {(surfaceTilesTexture != null ? AssetDatabase.GetAssetPath(surfaceTilesTexture) : "n/a")} ({surfaceTileLookups.Count})\nUnderground tiles: {(undergroundTilesTexture != null ? AssetDatabase.GetAssetPath(undergroundTilesTexture) : "n/a")} ({undergroundTileLookups.Count})\nAnts: {antLookups.Count}\nProps: {propLookups.Count}");

        return $"Generated Ant pack '{request.PackName}' at {request.OutputFolder}\nSurface Tiles={surfaceTileLookups.Count}, Underground Tiles={undergroundTileLookups.Count}, Ants={antLookups.Count}, Props={propLookups.Count}";
    }

    public static void Validate(Request request)
    {
        if (string.IsNullOrWhiteSpace(request.PackName))
        {
            throw new ArgumentException("PackName is required.");
        }

        if (request.TileSize < 8)
        {
            throw new ArgumentException("TileSize must be >= 8.");
        }

        if (request.AntSpriteSize < 32)
        {
            throw new ArgumentException("AntSpriteSize must be >= 32.");
        }

        if (request.AntSpriteSize % 8 != 0)
        {
            throw new ArgumentException("AntSpriteSize must be a multiple of 8.");
        }

        if (!request.GenerateTiles && !request.GenerateAnts && !request.GenerateProps)
        {
            throw new ArgumentException("Select at least one generator toggle.");
        }

        if (!request.OutputFolder.StartsWith("Assets/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Output folder must be under Assets/.");
        }
    }

    public static List<SpriteRect> BuildGridRects(IReadOnlyList<string> names, int tileSize, int columns)
    {
        var rows = Mathf.CeilToInt(names.Count / (float)columns);
        var rects = new List<SpriteRect>(names.Count);
        for (var index = 0; index < names.Count; index++)
        {
            var col = index % columns;
            var row = rows - 1 - index / columns;
            rects.Add(new SpriteRect
            {
                name = names[index],
                rect = new Rect(col * tileSize, row * tileSize, tileSize, tileSize),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                spriteID = GUID.Generate()
            });
        }

        return rects;
    }

    public readonly struct TextureResult
    {
        public readonly Texture2D Texture;
        public readonly List<Sprite> Sprites;

        public TextureResult(Texture2D texture, List<Sprite> sprites)
        {
            Texture = texture;
            Sprites = sprites;
        }
    }
}
