using System.Collections.Generic;

public sealed class AntEnvironmentModule : IEnvironmentModule
{
    public string EnvironmentId => "env.ant.v1";

    public EnvironmentGenResult Generate(EnvironmentGenRequest req)
    {
        var outp = new EnvironmentGenResult();
        var tiles = AntTilesetSheetGenerator.Generate(req.outputFolder, req.recipe.seed, req.recipe.tileSize, AntPalettePreset.Classic, req.overwrite);
        outp.textures.Add(new ContentPack.TextureEntry { id = "tile:surface", texture = tiles.SurfaceTexture });
        outp.textures.Add(new ContentPack.TextureEntry { id = "tile:underground", texture = tiles.UndergroundTexture });
        foreach (var s in tiles.SurfaceSprites)
            outp.sprites.Add(new ContentPack.SpriteEntry { id = $"tile:ant:surface:{s.name}", category = "tile", sprite = s });
        foreach (var s in tiles.UndergroundSprites)
            outp.sprites.Add(new ContentPack.SpriteEntry { id = $"tile:ant:underground:{s.name}", category = "tile", sprite = s });

        var props = AntPropsGenerator.Generate(req.outputFolder, req.recipe.seed, req.recipe.tileSize, AntPalettePreset.Classic, req.overwrite);
        outp.textures.Add(new ContentPack.TextureEntry { id = "prop:sheet", texture = props.Texture });
        foreach (var s in props.Sprites)
            outp.sprites.Add(new ContentPack.SpriteEntry { id = $"prop:ant:{s.name}:default:na:na:00", category = "prop", sprite = s });
        return outp;
    }
}
