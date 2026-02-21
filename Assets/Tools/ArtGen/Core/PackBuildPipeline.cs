using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class PackBuildPipeline
{
    public sealed class BuildReport
    {
        public string outputFolder;
        public string contentPackVersion;
        public int blueprintCount;
        public int spriteCount;
        public readonly List<string> warnings = new();
        public string Summary => $"Build completed at {outputFolder} ({contentPackVersion}). Blueprints={blueprintCount}, Sprites={spriteCount}, Warnings={warnings.Count}";
    }

    private sealed class SheetCell
    {
        public string id;
        public PixelBlueprint2D body;
        public PixelBlueprint2D mask;
    }

    public static BuildReport Build(PackRecipe recipe, bool overwrite)
    {
        Validate(recipe);
        ImportSettingsUtil.EnsureFolder(recipe.outputFolder);
        ImportSettingsUtil.EnsureFolder($"{recipe.outputFolder}/Generated");
        ImportSettingsUtil.EnsureFolder($"{recipe.outputFolder}/Blueprints");
        ImportSettingsUtil.EnsureFolder($"{recipe.outputFolder}/Blueprints/Generated");

        var report = new BuildReport { outputFolder = recipe.outputFolder };
        var textureEntries = new List<ContentPack.TextureEntry>();
        var spriteEntries = new List<ContentPack.SpriteEntry>();
        var antsCompat = new List<ContentPack.SpriteEntry>();

        foreach (var entity in recipe.entities)
        {
            var module = ModuleRegistry.GetArchetype(entity.archetypeId) ?? throw new InvalidOperationException($"Missing archetype module '{entity.archetypeId}'");
            module.EnsureLibrariesExist();

            var speciesIds = module.PickSpeciesIds(Deterministic.DeriveSeed(recipe.seed, "species:" + entity.entityId), entity.speciesCount);
            var cells = new List<SheetCell>();

            foreach (var speciesId in speciesIds)
            foreach (var role in entity.roles)
            foreach (var stage in entity.lifeStages)
            foreach (var state in entity.states)
            {
                var frameCount = entity.animationPolicy.FramesForState(state);
                for (var frame = 0; frame < frameCount; frame++)
                {
                    var frameFolder = $"{recipe.outputFolder}/Blueprints/Generated/{entity.entityId}/{speciesId}/{role}/{stage}/{state}";
                    ImportSettingsUtil.EnsureFolder(frameFolder);
                    var bpPath = $"{frameFolder}/{frame:00}.asset";

                    var req = new ArchetypeSynthesisRequest
                    {
                        recipe = recipe,
                        entity = entity,
                        speciesId = speciesId,
                        role = role,
                        stage = stage,
                        state = state,
                        frameIndex = frame,
                        blueprintPath = bpPath,
                        seed = Deterministic.DeriveSeed(recipe.seed, $"{entity.entityId}:{speciesId}:{role}:{stage}:{state}:{frame}")
                    };

                    var synth = module.Synthesize(req);
                    foreach (var s in synth.frames)
                    {
                        cells.Add(new SheetCell { id = s.spriteId, body = s.bodyBlueprint, mask = s.maskBlueprint });
                        report.blueprintCount += s.bodyBlueprint != null ? 1 : 0;
                    }
                }
            }

            if (recipe.generationPolicy.compileSpritesheets)
            {
                var sheetName = entity.entityId == "ant" ? "ants_anim" : entity.entityId + "_anim";
                var texPath = $"{recipe.outputFolder}/Generated/{sheetName}.png";
                var generatedSprites = CompileSheet(texPath, cells, recipe.agentSpriteSize, overwrite);
                textureEntries.Add(new ContentPack.TextureEntry { id = "sheet:" + sheetName, texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) });
                foreach (var sp in generatedSprites)
                {
                    spriteEntries.Add(new ContentPack.SpriteEntry { id = sp.name, category = "agent", sprite = sp });
                }
                report.spriteCount += generatedSprites.Count;

                if (entity.entityId == "ant")
                {
                    var compatCells = cells.Where(c => c.id.Contains(":worker:adult:idle:00") || c.id.Contains(":soldier:adult:idle:00")).Take(2).ToList();
                    var compatList = new List<SheetCell>();
                    foreach (var cell in compatCells)
                    {
                        compatList.Add(new SheetCell { id = cell.id.Replace("agent:ant:", "ant_").Replace(":", "_") , body = cell.body, mask = null});
                        compatList.Add(new SheetCell { id = cell.id.Replace("agent:ant:", "ant_").Replace(":", "_") + "_mask", body = cell.mask, mask = null});
                    }
                    var compatPath = $"{recipe.outputFolder}/Generated/ants.png";
                    var compatSprites = CompileSheet(compatPath, compatList, recipe.agentSpriteSize, overwrite);
                    textureEntries.Add(new ContentPack.TextureEntry { id = "sheet:ants", texture = AssetDatabase.LoadAssetAtPath<Texture2D>(compatPath) });
                    foreach (var sp in compatSprites) antsCompat.Add(new ContentPack.SpriteEntry { id = sp.name, category = "agent", sprite = sp });
                }
            }
        }

        var env = ModuleRegistry.GetEnvironment(recipe.environmentId) ?? throw new InvalidOperationException($"Missing environment module '{recipe.environmentId}'");
        var envResult = env.Generate(new EnvironmentGenRequest { recipe = recipe, overwrite = overwrite, outputFolder = recipe.outputFolder + "/Generated" });
        textureEntries.AddRange(envResult.textures);
        spriteEntries.AddRange(envResult.sprites);

        var contentPath = $"{recipe.outputFolder}/ContentPack.asset";
        var pack = AssetDatabase.LoadAssetAtPath<ContentPack>(contentPath);
        if (pack == null)
        {
            pack = ScriptableObject.CreateInstance<ContentPack>();
            AssetDatabase.CreateAsset(pack, contentPath);
        }

        pack.SetMetadata(recipe);
        spriteEntries.AddRange(antsCompat);
        pack.SetEntries(textureEntries, spriteEntries);
        EditorUtility.SetDirty(pack);
        report.contentPackVersion = pack.Version;

        if (recipe.generationPolicy.exportCompatibilityAntContentPack)
        {
            ExportAntCompatibility(recipe, spriteEntries, textureEntries);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return report;
    }

    private static List<Sprite> CompileSheet(string path, List<SheetCell> cells, int cellSize, bool overwrite)
    {
        if (!overwrite && File.Exists(path)) return AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToList();

        var columns = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, cells.Count))), 1, 64);
        var rows = Mathf.CeilToInt(cells.Count / (float)columns);
        var width = columns * cellSize;
        var height = rows * cellSize;
        var pixels = new Color32[width * height];

        for (var i = 0; i < cells.Count; i++)
        {
            var rect = SheetLayout.CellRect(i, columns, cellSize, cells.Count);
            var color = new Color32(56, 44, 31, 255);
            BlueprintRasterizer.Render(cells[i].body, "body", cellSize, (int)rect.x, (int)rect.y, color, pixels, width);
            BlueprintRasterizer.Render(cells[i].mask, "stripe", cellSize, (int)rect.x, (int)rect.y, Color.white, pixels, width);
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var rects = new List<SpriteRect>(cells.Count);
        for (var i = 0; i < cells.Count; i++)
        {
            rects.Add(new SpriteRect
            {
                name = cells[i].id,
                rect = SheetLayout.CellRect(i, columns, cellSize, cells.Count),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                spriteID = GUID.Generate()
            });
        }

        return ImportSettingsUtil.ConfigureAsPixelArtMultiple(path, cellSize, rects);
    }

    private static void ExportAntCompatibility(PackRecipe recipe, List<ContentPack.SpriteEntry> sprites, List<ContentPack.TextureEntry> textures)
    {
        var antPackPath = $"{recipe.outputFolder}/AntContentPack.asset";
        var antPack = AssetDatabase.LoadAssetAtPath<AntContentPack>(antPackPath);
        if (antPack == null)
        {
            antPack = ScriptableObject.CreateInstance<AntContentPack>();
            AssetDatabase.CreateAsset(antPack, antPackPath);
        }

        antPack.SetMetadata(recipe.seed, recipe.tileSize, "Generated");
        var surface = textures.FirstOrDefault(t => t.id == "tile:surface").texture;
        var underground = textures.FirstOrDefault(t => t.id == "tile:underground").texture;
        var ants = textures.FirstOrDefault(t => t.id == "sheet:ants").texture;
        var props = textures.FirstOrDefault(t => t.id == "prop:sheet").texture;
        antPack.SetTextures(surface, underground, ants, null, props);

        var surfaceSprites = sprites.Where(s => s.id.StartsWith("tile:ant:surface:")).Select(s => new AntContentPack.SpriteLookupEntry { id = s.id, sprite = s.sprite }).ToList();
        var undergroundSprites = sprites.Where(s => s.id.StartsWith("tile:ant:underground:")).Select(s => new AntContentPack.SpriteLookupEntry { id = s.id, sprite = s.sprite }).ToList();
        var antSprites = sprites.Where(s => s.id.StartsWith("ant_")).Select(s => new AntContentPack.SpriteLookupEntry { id = s.id, sprite = s.sprite }).ToList();
        var propSprites = sprites.Where(s => s.id.StartsWith("prop:ant:")).Select(s => new AntContentPack.SpriteLookupEntry { id = s.id, sprite = s.sprite }).ToList();

        antPack.SetLookups(surfaceSprites, undergroundSprites, antSprites, null, propSprites);
        EditorUtility.SetDirty(antPack);
    }

    private static void Validate(PackRecipe recipe)
    {
        if (recipe == null) throw new ArgumentNullException(nameof(recipe));
        if (string.IsNullOrWhiteSpace(recipe.outputFolder) || !recipe.outputFolder.StartsWith("Assets/", StringComparison.Ordinal)) throw new ArgumentException("outputFolder must be under Assets/");
    }
}
