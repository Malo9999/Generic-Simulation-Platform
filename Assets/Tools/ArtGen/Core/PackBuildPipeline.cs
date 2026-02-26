using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
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
        public Color32 outlineColor;
        public Color32 baseColor;
        public Color32 shadowColor;
        public Color32 highlightColor;
        public Color32? stripeColor;
    }

    private readonly struct CompileSheetOptions
    {
        public readonly bool renderAntStripeOverlay;

        public CompileSheetOptions(bool renderAntStripeOverlay)
        {
            this.renderAntStripeOverlay = renderAntStripeOverlay;
        }
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
        var antsCompatForExport = new List<ContentPack.SpriteEntry>();
        Texture2D antsCompatTextureForExport = null;
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
            var cells = useOutline ? BuildOutlineCells(recipe, entity, displaySpecies) : BuildProceduralCells(recipe, report, entity, resolvedSpeciesIds, module);

            var clipStates = string.Equals(entity.entityId, "ant", StringComparison.OrdinalIgnoreCase) ? AntContractStates() : entity.states;
            foreach (var role in entity.roles)
            foreach (var stage in entity.lifeStages)
            foreach (var state in clipStates)
            {
                var frameCount = string.Equals(entity.entityId, "ant", StringComparison.OrdinalIgnoreCase)
                    ? AntContractLocalFrameCount(state)
                    : Mathf.Max(1, entity.animationPolicy.FramesForState(state));
                clipMetadata.Add(new ContentPack.ClipMetadataEntry
                {
                    keyPrefix = $"agent:{entity.entityId}:{role}:{stage}:{state}",
                    fps = Mathf.Max(1, entity.animationPolicy.defaultFps),
                    frameCount = frameCount
                });
            }

            if (recipe.generationPolicy.compileSpritesheets)
            {
                var sheetName = entity.entityId == "ant" ? "ants_anim" : entity.entityId + "_anim";
                var texPath = $"{recipe.outputFolder}/Generated/{sheetName}.png";
                var generatedSprites = CompileSheet(
                    texPath,
                    cells,
                    recipe.agentSpriteSize,
                    overwrite,
                    recipe.simulationId,
                    new CompileSheetOptions(recipe.generationPolicy.renderAntStripeOverlay));
                if (generatedSprites.Count == 0)
                {
                    Debug.LogWarning($"[PackBuildPipeline] Pipeline generated zero sprites for '{entity.entityId}'. Falling back to placeholder sprites.");
                    var expectedIds = BuildExpectedSpriteIds(entity, resolvedSpeciesIds);
                    generatedSprites = BuildMissingPipelinePlaceholderSprites(
                        texPath,
                        expectedIds,
                        recipe.agentSpriteSize,
                        overwrite);
                }
                textureEntries.Add(new ContentPack.TextureEntry { id = "sheet:" + sheetName, texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) });
                foreach (var sp in generatedSprites) spriteEntries.Add(new ContentPack.SpriteEntry { id = sp.name, category = "agent", sprite = sp });
                report.spriteCount += generatedSprites.Count;

                if (entity.entityId == "ant")
                {
                    var generatedIds = generatedSprites.Select(s => s.name).ToList();
                    antSpeciesLogged = new List<string>(resolvedSpeciesIds);
                    if (resolvedSpeciesIds.Count > 0)
                    {
                        var firstSpecies = resolvedSpeciesIds[0];
                        antSpriteSamples = BuildAntContractSamplesForSpecies(generatedIds, firstSpecies).Take(12).ToList();
                    }
                    else
                    {
                        antSpriteSamples = generatedIds.Take(12).ToList();
                    }

                    ValidateAntContractSprites(report, generatedIds, resolvedSpeciesIds, recipe.generationPolicy.includeAntMaskSpritesInMainPack);
                }

                if (entity.entityId == "ant" && recipe.generationPolicy.exportCompatibilityAntContentPack)
                {
                    var compatPath = $"{recipe.outputFolder}/Generated/ants.png";
                    var compatCells = BuildAntCompatibilityCells(cells);
                    var compatSprites = CompileSheet(
                        compatPath,
                        compatCells,
                        recipe.agentSpriteSize,
                        overwrite,
                        recipe.simulationId,
                        new CompileSheetOptions(renderAntStripeOverlay: true));
                    antsCompatTextureForExport = AssetDatabase.LoadAssetAtPath<Texture2D>(compatPath);
                    foreach (var sp in compatSprites) antsCompatForExport.Add(new ContentPack.SpriteEntry { id = sp.name.Replace("agent:ant:", "ant_").Replace(":", "_"), category = "agent", sprite = sp });
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
        pack.SetEntries(textureEntries, spriteEntries);
        pack.SetSelections(speciesSelections);
        pack.SetClipMetadata(clipMetadata);
        EditorUtility.SetDirty(pack);
        report.contentPackVersion = pack.Version;

        ValidateBuildOutputs(recipe, pack, report);

        if (recipe.generationPolicy.exportCompatibilityAntContentPack) ExportAntCompatibility(recipe, spriteEntries, textureEntries, antsCompatTextureForExport, antsCompatForExport);

        if (antSpeciesLogged.Count > 0)
        {
            Debug.Log($"[PackBuildPipeline] Ant speciesIds in pack: {string.Join(", ", antSpeciesLogged)}");
            Debug.Log($"[PackBuildPipeline] Ant contract sample IDs: {string.Join(", ", antSpriteSamples)}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return report;
    }

    private static void ValidateBuildOutputs(PackRecipe recipe, ContentPack pack, BuildReport report)
    {
        var ids = pack.GetAllSpriteIds().OrderBy(id => id, StringComparer.Ordinal).ToList();
        Debug.Log($"[PackBuildPipeline] Generated sprite ids: {ids.Count}");
        Debug.Log($"[PackBuildPipeline] Sprite id samples: {string.Join(", ", ids.Take(10))}");

        foreach (var entity in recipe.entities)
        {
            var prefix = $"agent:{entity.entityId}:";
            if (ids.Any(id => id.StartsWith(prefix, StringComparison.Ordinal)))
            {
                continue;
            }

            var message = $"No generated sprites found for entity '{entity.entityId}' (expected prefix '{prefix}').";
            report.warnings.Add(message);
            Debug.LogError("[PackBuildPipeline] " + message);
        }
    }

    private static List<SheetCell> BuildProceduralCells(PackRecipe recipe, BuildReport report, PackRecipe.EntityRequirement entity, List<string> speciesIds, IArchetypeModule module)
    {
        var cells = new List<SheetCell>();
        var speciesDisplayIds = BuildDisplaySpeciesIds(recipe.referenceAssets.Where(a => a.entityId == entity.entityId).ToList(), speciesIds.Count);
        var isAntEntity = string.Equals(entity.entityId, "ant", StringComparison.OrdinalIgnoreCase);
        var states = isAntEntity ? AntContractStates() : entity.states;
        for (var s = 0; s < speciesIds.Count; s++)
        foreach (var role in entity.roles)
        foreach (var stage in entity.lifeStages)
        foreach (var state in states)
        {
            var frameCount = isAntEntity ? AntContractLocalFrameCount(state) : entity.animationPolicy.FramesForState(state);
            var lastAntFrameMask = default(byte[]);
            for (var frame = 0; frame < frameCount; frame++)
            {
                var speciesId = speciesIds[s];
                var contractFrame = isAntEntity ? ContractFrameIndex(state, frame) : frame;
                var frameFolder = $"{recipe.outputFolder}/Blueprints/Generated/{entity.entityId}/{speciesId}/{role}/{stage}/{state}";
                ImportSettingsUtil.EnsureFolder(frameFolder);
                var bpPath = $"{frameFolder}/{contractFrame:00}.asset";
                var req = new ArchetypeSynthesisRequest { recipe = recipe, entity = entity, speciesId = speciesId, role = role, stage = stage, state = state, frameIndex = frame, blueprintPath = bpPath, seed = Deterministic.DeriveSeed(recipe.seed, $"{entity.entityId}:{speciesId}:{role}:{stage}:{state}:{frame}") };
                var synth = module.Synthesize(req);
                foreach (var sf in synth.frames)
                {
                    var spriteId = isAntEntity
                        ? BuildAntSpriteId(speciesId, role, stage, state, contractFrame)
                        : RewriteSpriteId(sf.spriteId, entity.entityId, speciesId, speciesDisplayIds[s]);
                    cells.Add(new SheetCell
                    {
                        id = spriteId,
                        body = sf.bodyBlueprint,
                        mask = sf.maskBlueprint,
                        outlineColor = sf.outlineColor,
                        baseColor = sf.baseColor,
                        shadowColor = sf.shadowColor,
                        highlightColor = sf.highlightColor,
                        stripeColor = sf.stripeColor
                    });
                    if (!isAntEntity || recipe.generationPolicy.includeAntMaskSpritesInMainPack)
                    {
                        var maskBlueprint = sf.maskBlueprint ?? BuildMaskBlueprintFromBody(sf.bodyBlueprint);
                        cells.Add(new SheetCell { id = spriteId + "_mask", body = maskBlueprint, mask = null });
                    }

                    if (isAntEntity && (string.Equals(state, "walk", StringComparison.OrdinalIgnoreCase) || string.Equals(state, "run", StringComparison.OrdinalIgnoreCase)))
                    {
                        var currentMask = SnapshotLayer(sf.bodyBlueprint, "body");
                        if (lastAntFrameMask != null)
                        {
                            var pixelDiff = PixelDiff(lastAntFrameMask, currentMask);
                            if (pixelDiff < 12)
                            {
                                var prefix = $"agent:{entity.entityId}:{speciesId}:{role}:{stage}:{state}";
                                var warning = $"[PackBuildPipeline] Similar ant frames detected for '{prefix}' around frame {contractFrame:00} (pixel diff={pixelDiff}).";
                                report.warnings.Add(warning);
                                Debug.LogWarning(warning);
                            }
                        }

                        lastAntFrameMask = currentMask;
                    }

                    report.blueprintCount += sf.bodyBlueprint != null ? 1 : 0;
                }
            }
        }

        return cells;
    }

    private static int ContractFrameIndex(string state, int localFrame)
    {
        switch (state.ToLowerInvariant())
        {
            case "idle": return localFrame;
            case "walk": return 2 + localFrame;
            case "run": return 5 + localFrame;
            case "fight": return 9;
            default: return localFrame;
        }
    }

    private static int AntContractLocalFrameCount(string state)
    {
        switch (state.ToLowerInvariant())
        {
            case "idle": return 2;
            case "walk": return 3;
            case "run": return 4;
            case "fight": return 1;
            default: return 0;
        }
    }

    private static string BuildAntSpriteId(string speciesId, string role, string stage, string state, int contractFrame)
        => $"agent:ant:{speciesId}:{role}:{stage}:{state}:{contractFrame:00}";

    private static IReadOnlyList<string> AntContractStates()
        => new[] { "idle", "walk", "run", "fight" };

    private static List<string> BuildAntContractSamplesForSpecies(IEnumerable<string> antSpriteIds, string speciesId)
    {
        var all = antSpriteIds
            .Where(id => !id.EndsWith("_mask", StringComparison.Ordinal))
            .Where(id => id.Contains($"agent:ant:{speciesId}:", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var speciesIds = all.ToHashSet(StringComparer.Ordinal);
        var samples = new List<string>();
        foreach (var expected in ExpectedAntContractSpriteIds(speciesId))
        {
            if (speciesIds.Contains(expected))
            {
                samples.Add(expected);
            }
        }

        if (samples.Count > 0)
        {
            return samples;
        }

        return all.OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    private static void ValidateAntContractSprites(BuildReport report, IEnumerable<string> antSpriteIds, List<string> speciesIds, bool requireMaskSprites)
    {
        var actual = new HashSet<string>(antSpriteIds, StringComparer.Ordinal);
        foreach (var speciesId in speciesIds)
        {
            string firstMissing = null;
            foreach (var expected in ExpectedAntContractSpriteIds(speciesId))
            {
                if (!actual.Contains(expected))
                {
                    firstMissing = expected;
                    break;
                }

                if (requireMaskSprites)
                {
                    var maskId = expected + "_mask";
                    if (!actual.Contains(maskId))
                    {
                        firstMissing = maskId;
                        break;
                    }
                }
            }

            if (firstMissing == null)
            {
                continue;
            }

            var message = $"Missing required ant contract sprite for {speciesId}: {firstMissing}";
            report.warnings.Add(message);
            Debug.LogError("[PackBuildPipeline] " + message);
        }
    }

    private static IEnumerable<string> ExpectedAntContractSpriteIds(string speciesId)
    {
        foreach (var frame in Enumerable.Range(0, 2)) yield return BuildAntSpriteId(speciesId, "worker", "adult", "idle", frame);
        foreach (var frame in Enumerable.Range(2, 3)) yield return BuildAntSpriteId(speciesId, "worker", "adult", "walk", frame);
        foreach (var frame in Enumerable.Range(5, 4)) yield return BuildAntSpriteId(speciesId, "worker", "adult", "run", frame);
        yield return BuildAntSpriteId(speciesId, "worker", "adult", "fight", 9);
    }

    private static List<string> BuildExpectedSpriteIds(PackRecipe.EntityRequirement entity, List<string> speciesIds)
    {
        var ids = new List<string>();
        var isAntEntity = string.Equals(entity.entityId, "ant", StringComparison.OrdinalIgnoreCase);
        var states = isAntEntity ? AntContractStates() : entity.states;

        foreach (var speciesId in speciesIds)
        foreach (var role in entity.roles)
        foreach (var stage in entity.lifeStages)
        foreach (var state in states)
        {
            var localFrames = isAntEntity ? AntContractLocalFrameCount(state) : Mathf.Max(1, entity.animationPolicy.FramesForState(state));
            for (var frame = 0; frame < localFrames; frame++)
            {
                var contractFrame = isAntEntity ? ContractFrameIndex(state, frame) : frame;
                var id = isAntEntity
                    ? BuildAntSpriteId(speciesId, role, stage, state, contractFrame)
                    : $"agent:{entity.entityId}:{speciesId}:{role}:{stage}:{state}:{contractFrame:00}";
                ids.Add(id);
            }
        }

        return ids;
    }

    private static List<Sprite> BuildMissingPipelinePlaceholderSprites(string texturePath, List<string> spriteIds, int cellSize, bool overwrite)
    {
        var spritesAssetPath = BuildSpritesAssetPath(texturePath);
        if (spriteIds == null || spriteIds.Count == 0)
        {
            spriteIds = new List<string> { "agent:missing:default:worker:adult:idle:00" };
        }

        if (!overwrite)
        {
            var cached = LoadSpritesFromAssetPath(spritesAssetPath);
            if (cached.Count > 0)
            {
                return cached;
            }
        }

        const int pad = 2;
        const int columns = 5;
        var rows = Mathf.CeilToInt(spriteIds.Count / (float)columns);
        var width = pad + columns * (cellSize + pad);
        var height = pad + rows * (cellSize + pad);
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var fill = Enumerable.Repeat(new Color32(255, 0, 255, 255), width * height).ToArray();
        texture.SetPixels32(fill);

        for (var i = 0; i < spriteIds.Count; i++)
        {
            var col = i % columns;
            var row = i / columns;
            var startX = pad + col * (cellSize + pad);
            var startY = pad + row * (cellSize + pad);
            for (var y = 0; y < cellSize; y++)
            for (var x = 0; x < cellSize; x++)
            {
                var isChecker = ((x / 4) + (y / 4)) % 2 == 0;
                var px = startX + x;
                var py = startY + y;
                fill[py * width + px] = isChecker ? new Color32(255, 0, 255, 255) : new Color32(60, 0, 60, 255);
            }
        }

        texture.SetPixels32(fill);
        texture.Apply(false, false);
        File.WriteAllBytes(texturePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
        SpriteBakeUtility.EnsureTextureImportSettings(texturePath);
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);

        var importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (importedTexture == null)
        {
            return new List<Sprite>();
        }

        var baked = SpriteBakeUtility.BakeSpritesFromGrid(
            importedTexture,
            cellSize,
            cellSize,
            columns,
            rows,
            pad,
            new Vector2(0.5f, 0.5f),
            cellSize,
            index => index < spriteIds.Count ? spriteIds[index] : $"placeholder:{index:000}");
        var container = GetOrCreateSpriteSubAssetContainer(spritesAssetPath);
        SpriteBakeUtility.AddOrReplaceSubAssets(container, baked);
        return LoadSpritesFromAssetPath(spritesAssetPath);
    }

    private static List<SheetCell> BuildOutlineCells(PackRecipe recipe, PackRecipe.EntityRequirement entity, List<string> displaySpecies)
    {
        var cells = new List<SheetCell>();
        var isAntEntity = string.Equals(entity.entityId, "ant", StringComparison.OrdinalIgnoreCase);
        var states = isAntEntity ? AntContractStates() : entity.states;
        var previewSpecies = isAntEntity && displaySpecies.Count > 0 ? displaySpecies[0] : string.Empty;
        var speciesFrameHashes = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);
        for (var i = 0; i < displaySpecies.Count; i++)
        {
            var species = displaySpecies[i];
            var outlinePath = Path.Combine(recipe.outputFolder, "Debug", "Outlines", $"{species}_best.png");
            if (!File.Exists(outlinePath)) continue;
            var fillColor = ReferenceColorSampler.SampleOrFallback(recipe.simulationId, species, new Color32(126, 92, 62, 255));
            var basePixels = LoadAndNormalizeOutline(outlinePath, recipe.agentSpriteSize, fillColor);
            var frameHashesByState = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var state in states)
            {
                var frameCount = isAntEntity ? AntContractLocalFrameCount(state) : entity.animationPolicy.FramesForState(state);
                var frameHashes = new List<string>(frameCount);
                for (var frame = 0; frame < frameCount; frame++)
                {
                    var contractFrame = isAntEntity ? ContractFrameIndex(state, frame) : frame;
                    var warped = WarpFrame(basePixels, recipe.agentSpriteSize, recipe.agentSpriteSize, frame, state);
                    var id = isAntEntity
                        ? BuildAntSpriteId(species, "worker", "adult", state, contractFrame)
                        : $"agent:{entity.entityId}:{species}:worker:adult:{state}:{contractFrame:00}";
                    cells.Add(new SheetCell { id = id, pixels = warped });
                    if (!isAntEntity || recipe.generationPolicy.includeAntMaskSpritesInMainPack)
                    {
                        cells.Add(new SheetCell { id = id + "_mask", pixels = MakeMask(warped) });
                    }
                    frameHashes.Add(HashPixels(warped));

                    if (isAntEntity && string.Equals(species, previewSpecies, StringComparison.Ordinal))
                    {
                        DumpFramePreview(recipe, species, id, warped, recipe.agentSpriteSize);
                    }
                }

                frameHashesByState[state] = frameHashes;
            }

            if (isAntEntity && frameHashesByState.Count > 0)
            {
                speciesFrameHashes[species] = frameHashesByState;
            }
        }

        foreach (var speciesEntry in speciesFrameHashes)
        {
            WarnOnIdenticalFrames(speciesEntry.Key, speciesEntry.Value);
        }

        return cells;
    }

    private static Color32[] MakeMask(Color32[] pixels)
    {
        var mask = new Color32[pixels.Length];
        for (var i = 0; i < pixels.Length; i++)
        {
            var alpha = pixels[i].a;
            mask[i] = alpha > 0 ? new Color32(255, 255, 255, alpha) : new Color32(0, 0, 0, 0);
        }

        return mask;
    }

    private static Color32[] LoadAndNormalizeOutline(string path, int size, Color32 fillColor)
    {
        const byte alphaThreshold = 96;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(path));

        var src = tex.GetPixels32();
        var srcMask = new bool[tex.width * tex.height];
        for (var i = 0; i < src.Length; i++)
        {
            srcMask[i] = src[i].a >= alphaThreshold;
        }

        KeepLargestConnectedComponent(srcMask, tex.width, tex.height);

        var scaledMask = new bool[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var sx = Mathf.Clamp(Mathf.FloorToInt((x / (float)size) * tex.width), 0, tex.width - 1);
            var sy = Mathf.Clamp(Mathf.FloorToInt((y / (float)size) * tex.height), 0, tex.height - 1);
            scaledMask[y * size + x] = srcMask[sy * tex.width + sx];
        }

        var pixels = new Color32[size * size];
        ApplyFlatShading(scaledMask, size, size, fillColor, pixels);
        AddOutline(pixels, size, size, MultiplyColor(fillColor, 0.35f));

        UnityEngine.Object.DestroyImmediate(tex);
        return pixels;
    }

    private static Color32[] WarpFrame(Color32[] src, int w, int h, int frame, string state)
    {
        var fillMask = new bool[w * h];
        var fillColors = new Color32[w * h];
        for (var i = 0; i < src.Length; i++)
        {
            if (src[i].a == 0 || IsDarkOutline(src[i])) continue;
            fillMask[i] = true;
            fillColors[i] = src[i];
        }

        if (!TryGetBounds(fillMask, w, h, out var minX, out var maxX, out var minY, out var maxY))
        {
            return (Color32[])src.Clone();
        }

        var stateKey = state?.ToLowerInvariant() ?? string.Empty;
        var warpedMask = new bool[w * h];
        var warpedColors = new Color32[w * h];

        var bboxWidth = Mathf.Max(1, maxX - minX + 1);
        var bboxHeight = Mathf.Max(1, maxY - minY + 1);
        var headMaxY = minY + Mathf.FloorToInt(bboxHeight * 0.25f);
        var legsMinY = minY + Mathf.FloorToInt(bboxHeight * 0.25f);
        var legsMaxY = minY + Mathf.FloorToInt(bboxHeight * 0.75f);
        var centerX = (minX + maxX) / 2;
        var leftEdgeLimit = minX + Mathf.Max(1, bboxWidth / 5);
        var rightEdgeLimit = maxX - Mathf.Max(1, bboxWidth / 5);

        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var index = y * w + x;
            if (!fillMask[index]) continue;

            var dx = 0;
            var dy = 0;
            switch (stateKey)
            {
                case "idle":
                    if (y <= headMaxY && (x == minX || x == maxX || y == minY))
                    {
                        dx = frame % 2 == 0 ? 1 : -1;
                    }
                    break;
                case "walk":
                    if (y >= legsMinY && y <= legsMaxY)
                    {
                        var phase = frame % 3;
                        if (x <= leftEdgeLimit) dx = phase == 1 ? 1 : (phase == 2 ? -1 : 0);
                        if (x >= rightEdgeLimit) dx = phase == 1 ? -1 : (phase == 2 ? 1 : 0);
                    }
                    break;
                case "run":
                    if (frame % 2 == 1) dy = -1;
                    dx = 1;
                    if (y >= legsMinY && y <= maxY)
                    {
                        var phase = frame % 4;
                        if (x <= leftEdgeLimit) dx += phase < 2 ? 2 : -2;
                        if (x >= rightEdgeLimit) dx += phase < 2 ? -2 : 2;
                    }
                    break;
                case "fight":
                    if (y <= headMaxY && x < centerX) dx = -1;
                    if (y <= headMaxY && x > centerX) dx = 1;
                    if (y >= legsMinY && x <= leftEdgeLimit) dx = 1;
                    break;
            }

            CopyFillPixel(x + dx, y + dy, fillColors[index], warpedMask, warpedColors, w, h);
        }

        if (stateKey == "fight")
        {
            var sourceColor = fillColors[minY * w + centerX];
            var mandibleY = Mathf.Clamp(minY, 0, h - 1);
            CopyFillPixel(centerX - 1, mandibleY, sourceColor, warpedMask, warpedColors, w, h);
            CopyFillPixel(centerX + 1, mandibleY, sourceColor, warpedMask, warpedColors, w, h);
            CopyFillPixel(centerX - 2, Mathf.Clamp(mandibleY + 1, 0, h - 1), sourceColor, warpedMask, warpedColors, w, h);
            CopyFillPixel(centerX + 2, Mathf.Clamp(mandibleY + 1, 0, h - 1), sourceColor, warpedMask, warpedColors, w, h);
        }

        var result = new Color32[w * h];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = warpedMask[i] ? warpedColors[i] : new Color32(0, 0, 0, 0);
        }

        AddOutline(result, w, h, new Color32(24, 18, 12, 255));
        return result;
    }

    private static void ApplyFlatShading(bool[] mask, int w, int h, Color32 fillColor, Color32[] dst)
    {
        var baseColor = fillColor;
        var highlightColor = MultiplyColor(fillColor, 1.15f);
        var shadowColor = MultiplyColor(fillColor, 0.75f);
        var centroid = ComputeCentroid(mask, w, h);

        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var index = y * w + x;
            if (!mask[index])
            {
                dst[index] = new Color32(0, 0, 0, 0);
                continue;
            }

            var relation = (x - centroid.x) + (y - centroid.y);
            dst[index] = relation < 0f ? highlightColor : relation > 0f ? shadowColor : baseColor;
        }
    }

    private static void AddOutline(Color32[] px, int w, int h, Color32 outlineColor)
    {
        var copy = (Color32[])px.Clone();
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var i = y * w + x;
            if (copy[i].a > 0) continue;
            var near = false;
            if (x > 0 && copy[i - 1].a > 0) near = true;
            if (x < w - 1 && copy[i + 1].a > 0) near = true;
            if (y > 0 && copy[i - w].a > 0) near = true;
            if (y < h - 1 && copy[i + w].a > 0) near = true;
            if (near) px[i] = outlineColor;
        }
    }

    private static void KeepLargestConnectedComponent(bool[] mask, int w, int h)
    {
        var visited = new bool[mask.Length];
        var largest = new List<int>();
        var queue = new Queue<int>();
        var component = new List<int>();

        for (var i = 0; i < mask.Length; i++)
        {
            if (!mask[i] || visited[i]) continue;
            component.Clear();
            queue.Clear();
            queue.Enqueue(i);
            visited[i] = true;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                var cx = current % w;
                var cy = current / w;
                VisitNeighbor(cx - 1, cy);
                VisitNeighbor(cx + 1, cy);
                VisitNeighbor(cx, cy - 1);
                VisitNeighbor(cx, cy + 1);
            }

            if (component.Count > largest.Count)
            {
                largest = new List<int>(component);
            }
        }

        for (var i = 0; i < mask.Length; i++) mask[i] = false;
        foreach (var idx in largest) mask[idx] = true;

        void VisitNeighbor(int x, int y)
        {
            if (x < 0 || x >= w || y < 0 || y >= h) return;
            var ni = y * w + x;
            if (!mask[ni] || visited[ni]) return;
            visited[ni] = true;
            queue.Enqueue(ni);
        }
    }

    private static Vector2 ComputeCentroid(bool[] mask, int w, int h)
    {
        var sumX = 0f;
        var sumY = 0f;
        var count = 0;

        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            if (!mask[y * w + x]) continue;
            sumX += x;
            sumY += y;
            count++;
        }

        if (count == 0) return new Vector2(w / 2f, h / 2f);
        return new Vector2(sumX / count, sumY / count);
    }

    private static Color32 MultiplyColor(Color32 color, float factor)
    {
        var r = (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * factor), 0, 255);
        var g = (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * factor), 0, 255);
        var b = (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * factor), 0, 255);
        return new Color32(r, g, b, color.a);
    }

    private static bool IsDarkOutline(Color32 color)
    {
        return color.a > 0 && color.r <= 32 && color.g <= 32 && color.b <= 32;
    }

    private static bool TryGetBounds(bool[] mask, int w, int h, out int minX, out int maxX, out int minY, out int maxY)
    {
        minX = w;
        minY = h;
        maxX = 0;
        maxY = 0;
        var found = false;

        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            if (!mask[y * w + x]) continue;
            found = true;
            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }

        return found;
    }

    private static void CopyFillPixel(int x, int y, Color32 color, bool[] mask, Color32[] colors, int w, int h)
    {
        if (x < 0 || x >= w || y < 0 || y >= h) return;
        var index = y * w + x;
        mask[index] = true;
        colors[index] = color;
    }

    private static string HashPixels(Color32[] pixels)
    {
        using (var sha1 = SHA1.Create())
        {
            var bytes = new byte[pixels.Length * 4];
            for (var i = 0; i < pixels.Length; i++)
            {
                var offset = i * 4;
                bytes[offset] = pixels[i].r;
                bytes[offset + 1] = pixels[i].g;
                bytes[offset + 2] = pixels[i].b;
                bytes[offset + 3] = pixels[i].a;
            }

            return BitConverter.ToString(sha1.ComputeHash(bytes)).Replace("-", string.Empty);
        }
    }

    private static void WarnOnIdenticalFrames(string speciesId, Dictionary<string, List<string>> frameHashesByState)
    {
        var duplicates = new List<string>();
        foreach (var stateEntry in frameHashesByState)
        {
            var groups = stateEntry.Value
                .Select((hash, index) => new { hash, index })
                .GroupBy(x => x.hash)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in groups)
            {
                duplicates.Add($"{stateEntry.Key}[{string.Join(",", group.Select(x => x.index))}]");
            }
        }

        if (duplicates.Count == 0)
        {
            return;
        }

        Debug.LogWarning($"[PackBuildPipeline] Identical outline-driven ant frames for species '{speciesId}': {string.Join("; ", duplicates)}");
    }

    private static void DumpFramePreview(PackRecipe recipe, string species, string id, Color32[] pixels, int size)
    {
        var simSafe = SanitizePathToken(recipe.simulationId);
        var packSafe = SanitizePathToken(Path.GetFileName(recipe.outputFolder));
        var speciesSafe = SanitizePathToken(species);
        var previewDir = $"Assets/Presentation/Packs/{simSafe}/{packSafe}/Debug/FramePreviews/{speciesSafe}";

        ImportSettingsUtil.EnsureFolder("Assets/Presentation");
        ImportSettingsUtil.EnsureFolder("Assets/Presentation/Packs");
        ImportSettingsUtil.EnsureFolder($"Assets/Presentation/Packs/{simSafe}");
        ImportSettingsUtil.EnsureFolder($"Assets/Presentation/Packs/{simSafe}/{packSafe}");
        ImportSettingsUtil.EnsureFolder($"Assets/Presentation/Packs/{simSafe}/{packSafe}/Debug");
        ImportSettingsUtil.EnsureFolder($"Assets/Presentation/Packs/{simSafe}/{packSafe}/Debug/FramePreviews");
        ImportSettingsUtil.EnsureFolder(previewDir);

        var frameToken = id.Split(':').LastOrDefault() ?? "00";
        var filePath = $"{previewDir}/{frameToken}.png";
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(filePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceSynchronousImport);
    }

    private static string SanitizePathToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return "default";
        var sb = new StringBuilder(token.Length);
        foreach (var ch in token)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_');
        }

        return sb.ToString();
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

    private static PixelBlueprint2D BuildMaskBlueprintFromBody(PixelBlueprint2D bodyBlueprint)
    {
        if (bodyBlueprint == null)
        {
            return null;
        }

        var maskBlueprint = ScriptableObject.CreateInstance<PixelBlueprint2D>();
        maskBlueprint.width = Mathf.Max(1, bodyBlueprint.width);
        maskBlueprint.height = Mathf.Max(1, bodyBlueprint.height);
        var maskLayer = maskBlueprint.EnsureLayer("body");
        var maskPixels = maskLayer.pixels;

        var bodyLayer = bodyBlueprint.EnsureLayer("body");
        var copyLength = Mathf.Min(maskPixels.Length, bodyLayer.pixels.Length);
        Array.Copy(bodyLayer.pixels, maskPixels, copyLength);

        var hasFilled = false;
        for (var i = 0; i < copyLength; i++)
        {
            if (maskPixels[i] > 0)
            {
                hasFilled = true;
                break;
            }
        }

        if (!hasFilled)
        {
            foreach (var layer in bodyBlueprint.layers)
            {
                if (layer?.pixels == null)
                {
                    continue;
                }

                var length = Mathf.Min(maskPixels.Length, layer.pixels.Length);
                for (var i = 0; i < length; i++)
                {
                    if (layer.pixels[i] > 0)
                    {
                        maskPixels[i] = 1;
                        hasFilled = true;
                    }
                }

                if (hasFilled)
                {
                    // Keep processing all layers to produce a full silhouette union.
                    continue;
                }
            }
        }

        return maskBlueprint;
    }

    private static List<Sprite> CompileSheet(string path, List<SheetCell> cells, int cellSize, bool overwrite, string simulationId, CompileSheetOptions options)
    {
        var spritesAssetPath = BuildSpritesAssetPath(path);
        if (!overwrite)
        {
            var cached = LoadSpritesFromAssetPath(spritesAssetPath);
            if (cached.Count > 0)
            {
                Debug.Log($"[PackBuildPipeline] Reusing existing sprite container at '{spritesAssetPath}' with {cached.Count} sprites.");
                return cached;
            }
        }
        if (cells == null || cells.Count == 0)
        {
            Debug.LogWarning($"[PackBuildPipeline] CompileSheet called with 0 cells for path '{path}'. Returning empty list so fallback can generate placeholders.");
            return new List<Sprite>();
        }
        if (cellSize <= 0) return new List<Sprite>();
        var columns = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, cells.Count))), 1, 64);
        var rows = Mathf.Max(1, Mathf.CeilToInt(cells.Count / (float)columns));
        var width = columns * cellSize;
        var height = rows * cellSize;
        Debug.Log($"[PackBuildPipeline] CompileSheet sheetPath='{path}', spritesAssetPath='{spritesAssetPath}', cellCount={cells.Count}, columns={columns}, rows={rows}, width={width}, height={height}");
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

            var isMaskCell = cells[i].id.EndsWith("_mask", StringComparison.Ordinal);
            var isAntCell = TryParseAntSpeciesFromSpriteId(cells[i].id, out var speciesId);
            var fallbackBaseColor = new Color32(56, 44, 31, 255);
            if (isAntCell)
            {
                fallbackBaseColor = ReferenceColorSampler.SampleOrFallback(simulationId, speciesId, fallbackBaseColor);
            }

            if (isMaskCell)
            {
                RenderSolidMask(cells[i].body, cellSize, (int)rect.x, (int)rect.y, pixels, width);
                continue;
            }

            if (isAntCell)
            {
                RenderAntLayers(cells[i], cellSize, (int)rect.x, (int)rect.y, pixels, width, fallbackBaseColor, options.renderAntStripeOverlay);
                continue;
            }

            BlueprintRasterizer.Render(cells[i].body, "body", cellSize, (int)rect.x, (int)rect.y, fallbackBaseColor, pixels, width);
            BlueprintRasterizer.Render(cells[i].mask, "stripe", cellSize, (int)rect.x, (int)rect.y, Color.white, pixels, width);
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        SpriteBakeUtility.EnsureTextureImportSettings(path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (sheet == null)
        {
            Debug.LogError($"[PackBuildPipeline] Failed to load imported sprite sheet at '{path}'.");
            return new List<Sprite>();
        }

        Debug.Log($"[PackBuildPipeline] CompileSheet imported sheet '{sheet.name}' ({sheet.width}x{sheet.height}) for path '{path}'.");

        var sprites = SpriteBakeUtility.BakeSpritesFromGrid(
            sheet,
            cellSize,
            cellSize,
            columns,
            rows,
            0,
            new Vector2(0.5f, 0.5f),
            cellSize,
            index => index < cells.Count ? cells[index].id : $"sheet:{index:000}");
        Debug.Log($"[PackBuildPipeline] CompileSheet bakedSprites.Count={sprites.Count} for sheetPath='{path}'");

        if (sprites.Count > 0)
        {
            var firstSprite = sprites[0];
            if (firstSprite.texture == null)
            {
                Debug.LogError($"[PackBuildPipeline] First baked sprite '{firstSprite.name}' has null texture for sheetPath='{path}'. Failing fast.");
                return new List<Sprite>();
            }

            Debug.Log($"[PackBuildPipeline] First baked sprite '{firstSprite.name}' texture valid={firstSprite.texture != null} ({firstSprite.texture.width}x{firstSprite.texture.height}).");
        }

        var container = GetOrCreateSpriteSubAssetContainer(spritesAssetPath);
        SpriteBakeUtility.AddOrReplaceSubAssets(container, sprites);
        return LoadSpritesFromAssetPath(spritesAssetPath);
    }

    private static string BuildSpritesAssetPath(string texturePath)
    {
        return $"{Path.GetDirectoryName(texturePath)}/{Path.GetFileNameWithoutExtension(texturePath)}_sprites.asset".Replace("\\", "/");
    }

    private static SpriteSubAssetContainer GetOrCreateSpriteSubAssetContainer(string spritesAssetPath)
    {
        var container = AssetDatabase.LoadAssetAtPath<SpriteSubAssetContainer>(spritesAssetPath);
        if (container != null)
        {
            return container;
        }

        container = ScriptableObject.CreateInstance<SpriteSubAssetContainer>();
        container.name = Path.GetFileNameWithoutExtension(spritesAssetPath);
        AssetDatabase.CreateAsset(container, spritesAssetPath);
        return container;
    }

    private static List<Sprite> LoadSpritesFromAssetPath(string spritesAssetPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(spritesAssetPath)
            .OfType<Sprite>()
            .OrderBy(s => s.name, StringComparer.Ordinal)
            .ToList();
    }

    private static void RenderAntLayers(SheetCell cell, int cellSize, int ox, int oy, Color32[] pixels, int width, Color32 fallbackBaseColor, bool renderStripeOverlay)
    {
        var outline = cell.outlineColor.a > 0 ? cell.outlineColor : new Color32(20, 16, 12, 255);
        var baseColor = cell.baseColor.a > 0 ? cell.baseColor : fallbackBaseColor;
        var bodyRamp = BuildToneRamp(baseColor, outline, cell.shadowColor, cell.highlightColor);
        var legsRamp = BuildToneRamp(ScaleColor(baseColor, 0.82f), outline);
        var mandibleRamp = BuildToneRamp(ScaleColor(baseColor, 0.78f), outline);
        var eyeRamp = new BlueprintRasterizer.ToneRamp(new Color32(232, 220, 156, 255), new Color32(171, 149, 92, 255), new Color32(255, 236, 176, 255), outline);

        BlueprintRasterizer.RenderLayers(
            cell.body,
            cellSize,
            ox,
            oy,
            pixels,
            width,
            new BlueprintRasterizer.LayerStyle("body", bodyRamp, true, "body"),
            new BlueprintRasterizer.LayerStyle("legs", legsRamp, false),
            new BlueprintRasterizer.LayerStyle("antennae", legsRamp, false),
            new BlueprintRasterizer.LayerStyle("mandibles", mandibleRamp, false),
            new BlueprintRasterizer.LayerStyle("eyes", eyeRamp, false));
        if (renderStripeOverlay)
        {
            var stripeColor = cell.stripeColor ?? new Color32(245, 238, 210, 255);
            BlueprintRasterizer.Render(cell.mask, "stripe", cellSize, ox, oy, stripeColor, pixels, width);
        }
    }

    private static List<SheetCell> BuildAntCompatibilityCells(List<SheetCell> cells)
    {
        var compatCells = new List<SheetCell>();
        var bodyCells = cells.Where(c => !c.id.EndsWith("_mask", StringComparison.Ordinal)).Take(2);
        foreach (var bodyCell in bodyCells)
        {
            compatCells.Add(bodyCell);
            var maskCell = cells.FirstOrDefault(c => string.Equals(c.id, bodyCell.id + "_mask", StringComparison.Ordinal));
            if (maskCell != null)
            {
                compatCells.Add(maskCell);
                continue;
            }

            var generatedMask = bodyCell.body != null ? BuildMaskBlueprintFromBody(bodyCell.body) : null;
            compatCells.Add(new SheetCell { id = bodyCell.id + "_mask", body = generatedMask, mask = null, pixels = bodyCell.pixels != null ? MakeMask(bodyCell.pixels) : null });
        }

        return compatCells;
    }

    private static BlueprintRasterizer.ToneRamp BuildToneRamp(Color32 baseColor, Color32 outline, Color32? shadowOverride = null, Color32? highlightOverride = null)
    {
        var shadow = shadowOverride.HasValue && shadowOverride.Value.a > 0 ? shadowOverride.Value : ScaleColor(baseColor, 0.72f);
        var highlight = highlightOverride.HasValue && highlightOverride.Value.a > 0 ? highlightOverride.Value : ScaleColor(baseColor, 1.18f);
        return new BlueprintRasterizer.ToneRamp(baseColor, shadow, highlight, outline);
    }

    private static Color32 ScaleColor(Color32 color, float factor)
    {
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * factor), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * factor), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * factor), 0, 255),
            255);
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

    private static byte[] SnapshotLayer(PixelBlueprint2D blueprint, string layerName)
    {
        if (blueprint == null)
        {
            return null;
        }

        var layer = blueprint.EnsureLayer(layerName);
        var snapshot = new byte[blueprint.width * blueprint.height];
        var length = Mathf.Min(snapshot.Length, layer.pixels.Length);
        for (var i = 0; i < length; i++)
        {
            snapshot[i] = layer.pixels[i] > 0 ? (byte)1 : (byte)0;
        }

        return snapshot;
    }

    private static int PixelDiff(byte[] a, byte[] b)
    {
        if (a == null || b == null)
        {
            return int.MaxValue;
        }

        var length = Mathf.Min(a.Length, b.Length);
        var diff = 0;
        for (var i = 0; i < length; i++)
        {
            if (a[i] != b[i])
            {
                diff++;
            }
        }

        diff += Mathf.Abs(a.Length - b.Length);
        return diff;
    }

    private static void RenderSolidMask(PixelBlueprint2D blueprint, int targetSize, int ox, int oy, Color32[] outPixels, int outWidth)
    {
        if (blueprint == null)
        {
            return;
        }

        var layer = blueprint.EnsureLayer("body");
        for (var y = 0; y < targetSize; y++)
        for (var x = 0; x < targetSize; x++)
        {
            var sx = Mathf.Clamp(Mathf.FloorToInt((x / (float)targetSize) * blueprint.width), 0, blueprint.width - 1);
            var sy = Mathf.Clamp(Mathf.FloorToInt((y / (float)targetSize) * blueprint.height), 0, blueprint.height - 1);
            if (layer.pixels[(sy * blueprint.width) + sx] > 0)
            {
                outPixels[((oy + y) * outWidth) + ox + x] = Color.white;
            }
        }
    }

    private static void ExportAntCompatibility(PackRecipe recipe, List<ContentPack.SpriteEntry> sprites, List<ContentPack.TextureEntry> textures, Texture2D antsCompatTexture, List<ContentPack.SpriteEntry> antsCompatSprites)
    {
        var antPackPath = $"{recipe.outputFolder}/AntContentPack.asset";
        var antPack = AssetDatabase.LoadAssetAtPath<AntContentPack>(antPackPath) ?? ScriptableObject.CreateInstance<AntContentPack>();
        if (AssetDatabase.LoadAssetAtPath<AntContentPack>(antPackPath) == null) AssetDatabase.CreateAsset(antPack, antPackPath);
        antPack.SetMetadata(recipe.seed, recipe.tileSize, "Generated");
        antPack.SetTextures(textures.FirstOrDefault(t => t.id == "tile:surface").texture, textures.FirstOrDefault(t => t.id == "tile:underground").texture, antsCompatTexture, null, textures.FirstOrDefault(t => t.id == "prop:sheet").texture);
        antPack.SetLookups(sprites.Where(s => s.id.StartsWith("tile:ant:surface:")).Select(s => new AntContentPack.SpriteLookupEntry { id = s.id, sprite = s.sprite }).ToList(), sprites.Where(s => s.id.StartsWith("tile:ant:underground:")).Select(s => new AntContentPack.SpriteLookupEntry { id = s.id, sprite = s.sprite }).ToList(), antsCompatSprites.Select(s => new AntContentPack.SpriteLookupEntry { id = s.id, sprite = s.sprite }).ToList(), null, sprites.Where(s => s.id.StartsWith("prop:ant:")).Select(s => new AntContentPack.SpriteLookupEntry { id = s.id, sprite = s.sprite }).ToList());
        EditorUtility.SetDirty(antPack);
    }

    private static void Validate(PackRecipe recipe)
    {
        if (recipe == null) throw new ArgumentNullException(nameof(recipe));
        if (string.IsNullOrWhiteSpace(recipe.outputFolder) || !recipe.outputFolder.StartsWith("Assets/", StringComparison.Ordinal)) throw new ArgumentException("outputFolder must be under Assets/");
    }
}
