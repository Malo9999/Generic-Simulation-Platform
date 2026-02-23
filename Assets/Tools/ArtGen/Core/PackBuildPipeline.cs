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
        public Color32[] pixels;
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
        var speciesSelections = new List<ContentPack.SpeciesSelection>();
        var clipMetadata = new List<ContentPack.ClipMetadataEntry>();
        var antSpeciesLogged = new List<string>();
        var antSpriteSamples = new List<string>();

        foreach (var entity in recipe.entities)
        {
            var module = ModuleRegistry.GetArchetype(entity.archetypeId) ?? throw new InvalidOperationException($"Missing archetype module '{entity.archetypeId}'");
            module.EnsureLibrariesExist();
            var assetNeeds = (recipe.referenceAssets ?? new List<PackRecipe.ReferenceAssetNeed>()).Where(a => string.Equals(a.entityId, entity.entityId, StringComparison.OrdinalIgnoreCase)).ToList();
            var useOutline = assetNeeds.Any(a => a.generationMode == PackRecipe.GenerationMode.OutlineDriven);

            var speciesIds = module.PickSpeciesIds(Deterministic.DeriveSeed(recipe.seed, "species:" + entity.entityId), entity.speciesCount);
            var recipeSpeciesIds = BuildRecipeSpeciesIds(entity.entityId, assetNeeds, entity.speciesCount);
            var displaySpecies = recipeSpeciesIds.Count > 0 ? recipeSpeciesIds : BuildDisplaySpeciesIds(assetNeeds, entity.speciesCount);
            var resolvedSpeciesIds = recipeSpeciesIds.Count > 0 ? recipeSpeciesIds : speciesIds;

            speciesSelections.Add(new ContentPack.SpeciesSelection { entityId = entity.entityId, speciesIds = new List<string>(resolvedSpeciesIds) });
            var cells = useOutline ? BuildOutlineCells(recipe, entity, displaySpecies) : BuildProceduralCells(recipe, report, entity, speciesIds, module);

            foreach (var role in entity.roles)
            foreach (var stage in entity.lifeStages)
            foreach (var state in entity.states)
            {
                clipMetadata.Add(new ContentPack.ClipMetadataEntry
                {
                    keyPrefix = $"agent:{entity.entityId}:{role}:{stage}:{state}",
                    fps = Mathf.Max(1, entity.animationPolicy.defaultFps),
                    frameCount = Mathf.Max(1, entity.animationPolicy.FramesForState(state))
                });
            }

            if (recipe.generationPolicy.compileSpritesheets)
            {
                var sheetName = entity.entityId == "ant" ? "ants_anim" : entity.entityId + "_anim";
                var texPath = $"{recipe.outputFolder}/Generated/{sheetName}.png";
                var generatedSprites = CompileSheet(texPath, cells, recipe.agentSpriteSize, overwrite, recipe.simulationId);
                textureEntries.Add(new ContentPack.TextureEntry { id = "sheet:" + sheetName, texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) });
                foreach (var sp in generatedSprites) spriteEntries.Add(new ContentPack.SpriteEntry { id = sp.name, category = "agent", sprite = sp });
                report.spriteCount += generatedSprites.Count;

                if (entity.entityId == "ant")
                {
                    antSpeciesLogged = new List<string>(resolvedSpeciesIds);
                    antSpriteSamples = generatedSprites.Select(s => s.name).Take(5).ToList();
                }

                if (entity.entityId == "ant")
                {
                    var compatPath = $"{recipe.outputFolder}/Generated/ants.png";
                    var compatSprites = CompileSheet(compatPath, cells.Take(4).ToList(), recipe.agentSpriteSize, overwrite, recipe.simulationId);
                    textureEntries.Add(new ContentPack.TextureEntry { id = "sheet:ants", texture = AssetDatabase.LoadAssetAtPath<Texture2D>(compatPath) });
                    foreach (var sp in compatSprites) antsCompat.Add(new ContentPack.SpriteEntry { id = sp.name.Replace("agent:ant:", "ant_").Replace(":", "_"), category = "agent", sprite = sp });
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
        pack.SetSelections(speciesSelections);
        pack.SetClipMetadata(clipMetadata);
        EditorUtility.SetDirty(pack);
        report.contentPackVersion = pack.Version;

        if (recipe.generationPolicy.exportCompatibilityAntContentPack) ExportAntCompatibility(recipe, spriteEntries, textureEntries);

        if (antSpeciesLogged.Count > 0)
        {
            Debug.Log($"[PackBuildPipeline] Ant speciesIds in pack: {string.Join(", ", antSpeciesLogged)}");
            Debug.Log($"[PackBuildPipeline] Ant sprite sample IDs: {string.Join(", ", antSpriteSamples)}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return report;
    }

    private static List<SheetCell> BuildProceduralCells(PackRecipe recipe, BuildReport report, PackRecipe.EntityRequirement entity, List<string> speciesIds, IArchetypeModule module)
    {
        var cells = new List<SheetCell>();
        var speciesDisplayIds = BuildDisplaySpeciesIds(recipe.referenceAssets.Where(a => a.entityId == entity.entityId).ToList(), speciesIds.Count);
        for (var s = 0; s < speciesIds.Count; s++)
        foreach (var role in entity.roles)
        foreach (var stage in entity.lifeStages)
        foreach (var state in entity.states)
        {
            var frameCount = entity.animationPolicy.FramesForState(state);
            for (var frame = 0; frame < frameCount; frame++)
            {
                var speciesId = speciesIds[s];
                var frameFolder = $"{recipe.outputFolder}/Blueprints/Generated/{entity.entityId}/{speciesId}/{role}/{stage}/{state}";
                ImportSettingsUtil.EnsureFolder(frameFolder);
                var bpPath = $"{frameFolder}/{frame:00}.asset";
                var req = new ArchetypeSynthesisRequest { recipe = recipe, entity = entity, speciesId = speciesId, role = role, stage = stage, state = state, frameIndex = frame, blueprintPath = bpPath, seed = Deterministic.DeriveSeed(recipe.seed, $"{entity.entityId}:{speciesId}:{role}:{stage}:{state}:{frame}") };
                var synth = module.Synthesize(req);
                foreach (var sf in synth.frames)
                {
                    cells.Add(new SheetCell { id = RewriteSpriteId(sf.spriteId, entity.entityId, speciesId, speciesDisplayIds[s]), body = sf.bodyBlueprint, mask = sf.maskBlueprint });
                    report.blueprintCount += sf.bodyBlueprint != null ? 1 : 0;
                }
            }
        }

        return cells;
    }

    private static List<SheetCell> BuildOutlineCells(PackRecipe recipe, PackRecipe.EntityRequirement entity, List<string> displaySpecies)
    {
        var cells = new List<SheetCell>();
        var total = 10;
        for (var i = 0; i < displaySpecies.Count; i++)
        {
            var species = displaySpecies[i];
            var outlinePath = Path.Combine(recipe.outputFolder, "Debug", "Outlines", $"{species}_best.png");
            if (!File.Exists(outlinePath)) continue;
            var fillColor = ReferenceColorSampler.SampleOrFallback(recipe.simulationId, species, new Color32(126, 92, 62, 255));
            var basePixels = LoadAndNormalizeOutline(outlinePath, recipe.agentSpriteSize, fillColor);
            for (var frame = 0; frame < total; frame++)
            {
                var warped = WarpFrame(basePixels, recipe.agentSpriteSize, recipe.agentSpriteSize, frame, "idle");
                var id = $"agent:{entity.entityId}:{species}:worker:adult:idle:{frame:00}";
                cells.Add(new SheetCell { id = id, pixels = warped });
            }
        }

        return cells;
    }

    private static Color32[] LoadAndNormalizeOutline(string path, int size, Color32 fillColor)
    {
        var tex = new Texture2D(2,2,TextureFormat.RGBA32,false);
        tex.LoadImage(File.ReadAllBytes(path));
        var src = tex.GetPixels32();
        var dst = new Color32[size*size];
        for (var y=0;y<size;y++) for (var x=0;x<size;x++)
        {
            var sx = Mathf.Clamp(Mathf.FloorToInt((x/(float)size)*tex.width),0,tex.width-1);
            var sy = Mathf.Clamp(Mathf.FloorToInt((y/(float)size)*tex.height),0,tex.height-1);
            var v = src[sy*tex.width+sx].a > 127;
            dst[y*size+x] = v ? fillColor : new Color32(0,0,0,0);
        }
        UnityEngine.Object.DestroyImmediate(tex);
        AddOutline(dst, size, size);
        return dst;
    }

    private static Color32[] WarpFrame(Color32[] src, int w, int h, int frame, string state)
    {
        var dst = new Color32[src.Length];
        Array.Copy(src, dst, src.Length);
        var shift = (frame % 3) - 1;
        if (state == "idle") shift = frame % 2;
        for (var y = h - 1; y >= 0; y--) for (var x = w - 1; x >= 0; x--)
        {
            var nx = Mathf.Clamp(x + shift, 0, w - 1);
            dst[y*w+nx] = src[y*w+x];
        }
        return dst;
    }

    private static void AddOutline(Color32[] px, int w, int h)
    {
        var copy = (Color32[])px.Clone();
        for (var y=1;y<h-1;y++) for (var x=1;x<w-1;x++)
        {
            var i = y*w+x;
            if (copy[i].a > 0) continue;
            var near = false;
            for (var oy=-1;oy<=1;oy++) for (var ox=-1;ox<=1;ox++) if (copy[(y+oy)*w+(x+ox)].a>0) near = true;
            if (near) px[i] = new Color32(24, 18, 12, 255);
        }
    }

    private static List<string> BuildDisplaySpeciesIds(List<PackRecipe.ReferenceAssetNeed> assets, int speciesCount)
    {
        var ids = new List<string>();
        foreach (var asset in assets)
        {
            var variants = Mathf.Max(1, asset.variantCount);
            if (variants == 1) ids.Add(asset.assetId);
            else for (var i = 0; i < variants; i++) ids.Add($"{asset.assetId}_v{i}");
        }
        if (ids.Count == 0) for (var i = 0; i < speciesCount; i++) ids.Add($"species_v{i}");
        while (ids.Count < speciesCount) ids.Add($"{ids[0]}_v{ids.Count}");
        return ids.Take(speciesCount).ToList();
    }

    private static List<string> BuildRecipeSpeciesIds(string entityId, List<PackRecipe.ReferenceAssetNeed> assets, int speciesCount)
    {
        if (!string.Equals(entityId, "ant", StringComparison.OrdinalIgnoreCase))
        {
            return new List<string>();
        }

        var ids = assets
            .Where(a => a.generationMode == PackRecipe.GenerationMode.OutlineDriven)
            .Select(a => a.assetId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Take(Mathf.Max(0, speciesCount))
            .ToList();

        return ids;
    }

    private static string RewriteSpriteId(string original, string entityId, string originalSpeciesId, string displaySpeciesId)
        => original.Replace($"agent:{entityId}:{originalSpeciesId}:", $"agent:{entityId}:{displaySpeciesId}:");

    private static List<Sprite> CompileSheet(string path, List<SheetCell> cells, int cellSize, bool overwrite, string simulationId)
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
            if (cells[i].pixels != null)
            {
                for (var y = 0; y < cellSize; y++)
                for (var x = 0; x < cellSize; x++)
                {
                    pixels[((int)rect.y + y) * width + (int)rect.x + x] = cells[i].pixels[y * cellSize + x];
                }
                continue;
            }

            var bodyColor = new Color32(56, 44, 31, 255);
            if (TryParseAntSpeciesFromSpriteId(cells[i].id, out var speciesId))
            {
                bodyColor = ReferenceColorSampler.SampleOrFallback(simulationId, speciesId, bodyColor);
            }

            BlueprintRasterizer.Render(cells[i].body, "body", cellSize, (int)rect.x, (int)rect.y, bodyColor, pixels, width);
            BlueprintRasterizer.Render(cells[i].mask, "stripe", cellSize, (int)rect.x, (int)rect.y, Color.white, pixels, width);
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var rects = new List<SpriteRect>(cells.Count);
        for (var i = 0; i < cells.Count; i++) rects.Add(new SpriteRect { name = cells[i].id, rect = SheetLayout.CellRect(i, columns, cellSize, cells.Count), alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f), spriteID = GUID.Generate() });
        return ImportSettingsUtil.ConfigureAsPixelArtMultiple(path, cellSize, rects);
    }

    private static bool TryParseAntSpeciesFromSpriteId(string spriteId, out string speciesId)
    {
        speciesId = string.Empty;
        if (string.IsNullOrWhiteSpace(spriteId))
        {
            return false;
        }

        var tokens = spriteId.Split(':');
        if (tokens.Length < 4 || !string.Equals(tokens[0], "agent", StringComparison.OrdinalIgnoreCase) || !string.Equals(tokens[1], "ant", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        speciesId = tokens[2];
        return !string.IsNullOrWhiteSpace(speciesId);
    }

    private static void ExportAntCompatibility(PackRecipe recipe, List<ContentPack.SpriteEntry> sprites, List<ContentPack.TextureEntry> textures)
    {
        var antPackPath = $"{recipe.outputFolder}/AntContentPack.asset";
        var antPack = AssetDatabase.LoadAssetAtPath<AntContentPack>(antPackPath) ?? ScriptableObject.CreateInstance<AntContentPack>();
        if (AssetDatabase.LoadAssetAtPath<AntContentPack>(antPackPath) == null) AssetDatabase.CreateAsset(antPack, antPackPath);
        antPack.SetMetadata(recipe.seed, recipe.tileSize, "Generated");
        antPack.SetTextures(textures.FirstOrDefault(t => t.id == "tile:surface").texture, textures.FirstOrDefault(t => t.id == "tile:underground").texture, textures.FirstOrDefault(t => t.id == "sheet:ants").texture, null, textures.FirstOrDefault(t => t.id == "prop:sheet").texture);
        antPack.SetLookups(sprites.Where(s => s.id.StartsWith("tile:ant:surface:")).Select(s => new AntContentPack.SpriteLookupEntry { id = s.id, sprite = s.sprite }).ToList(), sprites.Where(s => s.id.StartsWith("tile:ant:underground:")).Select(s => new AntContentPack.SpriteLookupEntry { id = s.id, sprite = s.sprite }).ToList(), sprites.Where(s => s.id.StartsWith("ant_")).Select(s => new AntContentPack.SpriteLookupEntry { id = s.id, sprite = s.sprite }).ToList(), null, sprites.Where(s => s.id.StartsWith("prop:ant:")).Select(s => new AntContentPack.SpriteLookupEntry { id = s.id, sprite = s.sprite }).ToList());
        EditorUtility.SetDirty(antPack);
    }

    private static void Validate(PackRecipe recipe)
    {
        if (recipe == null) throw new ArgumentNullException(nameof(recipe));
        if (string.IsNullOrWhiteSpace(recipe.outputFolder) || !recipe.outputFolder.StartsWith("Assets/", StringComparison.Ordinal)) throw new ArgumentException("outputFolder must be under Assets/");
    }
}
