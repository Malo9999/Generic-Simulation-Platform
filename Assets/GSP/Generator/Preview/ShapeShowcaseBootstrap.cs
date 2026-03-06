using System.Collections.Generic;
using UnityEngine;

public sealed class ShapeShowcaseBootstrap : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private float horizontalSpacing = 4.15f;
    [SerializeField] private float verticalSpacing = 3.5f;
    [SerializeField] private float categoryGap = 1.8f;
    [SerializeField] private float spriteScale = 1f;
    [SerializeField] private float headerOffset = 1.7f;
    [SerializeField] private float labelOffset = 1.45f;

    [Header("Visuals")]
    [SerializeField] private Color headerColor = new(0.95f, 0.98f, 1f, 1f);
    [SerializeField] private Color labelColor = new(0.82f, 0.95f, 1f, 1f);
    [SerializeField] private Color cameraBackground = Color.black;
    [SerializeField] private ShapeCategoryPalette shapeCategoryPalette;
    [SerializeField] private ShapeMaterialPalette shapeMaterialPalette;

    [Header("Trails")]
    [SerializeField] private bool enableTrailDemo = true;
    [SerializeField] private bool enableSlimeMoldMiniDemo = true;
    [SerializeField] private TrailBufferSettings trailSettings = new();
    [SerializeField] private SlimeMoldTrailPreset slimeMoldPreset;

    [SerializeField] private int headerFontSize = 58;
    [SerializeField] private int labelFontSize = 32;

    private static readonly ShowcaseCategory[] Categories =
    {
        new("CORE", ShapePaletteCategory.Core, new[]
        {
            ShapeId.DotCore,
            ShapeId.DotGlow,
            ShapeId.DotGlowSmall,
            ShapeId.RingPing,
            ShapeId.PulseRing
        }),
        new("ORGANIC", ShapePaletteCategory.Organic, new[]
        {
            ShapeId.OrganicMetaball,
            ShapeId.OrganicAmoeba,
            ShapeId.NoiseBlob,
            ShapeId.FieldBlob
        }),
        new("AGENTS", ShapePaletteCategory.Agents, new[]
        {
            ShapeId.TriangleAgent,
            ShapeId.DiamondAgent,
            ShapeId.ArrowAgent
        }),
        new("MARKERS", ShapePaletteCategory.Markers, new[]
        {
            ShapeId.CrossMarker,
            ShapeId.ArcSector
        }),
        new("LINES", ShapePaletteCategory.Lines, new[]
        {
            ShapeId.LineSegment,
            ShapeId.StrokeScribble,
            ShapeId.Filament
        })
    };

    private readonly HashSet<string> missingLogged = new();
    private ShapeCategoryPalette runtimeFallbackPalette;
    private ShapeMaterialPalette runtimeMaterialPalette;
    private TrailBufferController trailBufferController;

    private void Start()
    {
        ApplySceneBackground();
        if (enableTrailDemo)
        {
            SetupTrailSystem();
        }

        SpawnShowcase();

        if (enableTrailDemo && enableSlimeMoldMiniDemo)
        {
            SpawnSlimeMoldMiniDemo();
        }
    }

    private void SetupTrailSystem()
    {
        var trailRoot = new GameObject("TrailBuffer");
        trailRoot.transform.SetParent(transform, false);

        trailBufferController = trailRoot.AddComponent<TrailBufferController>();
        trailBufferController.Settings.textureWidth = trailSettings.textureWidth;
        trailBufferController.Settings.textureHeight = trailSettings.textureHeight;
        trailBufferController.Settings.pixelsPerUnit = trailSettings.pixelsPerUnit;
        trailBufferController.Settings.worldBounds = trailSettings.worldBounds;
        trailBufferController.Settings.useWorldBounds = trailSettings.useWorldBounds;
        trailBufferController.Settings.decayPerSecond = trailSettings.decayPerSecond;
        trailBufferController.Settings.diffuseStrength = trailSettings.diffuseStrength;
        trailBufferController.Settings.depositStrength = trailSettings.depositStrength;
        trailBufferController.Settings.depositRadiusPx = trailSettings.depositRadiusPx;
        trailBufferController.Settings.useAdditiveComposite = trailSettings.useAdditiveComposite;
        trailBufferController.Settings.tintColor = trailSettings.tintColor;

        var renderer = trailRoot.AddComponent<TrailBufferRenderer>();
        renderer.Configure(trailBufferController);
    }

    private void ApplySceneBackground()
    {
        var cameraRef = Camera.main;
        if (cameraRef == null)
        {
            return;
        }

        cameraRef.backgroundColor = cameraBackground;
        cameraRef.clearFlags = CameraClearFlags.SolidColor;
    }

    private void SpawnShowcase()
    {
        var maxColumns = 0;
        foreach (var category in Categories)
        {
            maxColumns = Mathf.Max(maxColumns, category.ShapeIds.Length);
        }

        var totalRows = Categories.Length;
        var originX = -((Mathf.Max(1, maxColumns) - 1) * horizontalSpacing * 0.5f);
        var originY = ((Mathf.Max(1, totalRows) - 1) * (verticalSpacing + categoryGap) * 0.5f);

        var spawnedCount = 0;
        for (var row = 0; row < Categories.Length; row++)
        {
            var category = Categories[row];
            var y = originY - (row * (verticalSpacing + categoryGap));
            SpawnHeader(category.Name, new Vector3(originX - (horizontalSpacing * 1.05f), y + headerOffset, 0f), category.Category);

            var width = (category.ShapeIds.Length - 1) * horizontalSpacing;
            var startX = -width * 0.5f;

            for (var col = 0; col < category.ShapeIds.Length; col++)
            {
                var id = category.ShapeIds[col];
                if (!ShapeLibraryProvider.TryGetSprite(id, out var sprite))
                {
                    if (missingLogged.Add(id))
                    {
                        Debug.LogWarning($"Shape showcase missing sprite id: {id}");
                    }

                    continue;
                }

                var position = new Vector3(startX + (col * horizontalSpacing), y, 0f);
                SpawnShape(id, category.Category, sprite, position, spawnedCount);
                spawnedCount++;
            }
        }

        if (spawnedCount == 0)
        {
            Debug.LogError("Shape showcase could not resolve any sprites from ShapeLibraryProvider.");
        }
    }

    private void SpawnShape(string shapeId, ShapePaletteCategory category, Sprite sprite, Vector3 localPosition, int sequence)
    {
        var go = new GameObject(shapeId);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = Vector3.one * spriteScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = GetTint(shapeId);

        var palette = ResolveMaterialPalette();
        if (palette != null)
        {
            var mat = palette.GetMaterialForShape(shapeId);
            if (mat != null)
            {
                sr.sharedMaterial = mat;
            }
        }

        var profile = AnimatedShapeProfile.CreateForShapeId(shapeId);
        if (profile.animType != ShapeAnimType.None)
        {
            var driver = go.AddComponent<AnimatedShapeDriver>();
            driver.Configure(sr, profile, sequence * 97);
        }

        ConfigureTrailDemo(go, shapeId, category, sequence);
        SpawnLabel(shapeId, go.transform, labelOffset);
    }

    private void ConfigureTrailDemo(GameObject go, string shapeId, ShapePaletteCategory category, int sequence)
    {
        if (!enableTrailDemo || trailBufferController == null)
        {
            return;
        }

        var motion = go.AddComponent<ShowcaseTrailMotion>();
        motion.Configure(sequence + 17, category == ShapePaletteCategory.Organic ? 0.9f : 1f);

        if (category == ShapePaletteCategory.Markers || category == ShapePaletteCategory.Agents)
        {
            return;
        }

        var emitter = go.AddComponent<TrailEmitter>();
        var strength = 1f;
        var radiusScale = 1f;

        if (category == ShapePaletteCategory.Organic)
        {
            strength = 0.65f;
            radiusScale = 1.25f;
        }
        else if (shapeId == ShapeId.Filament)
        {
            strength = 1.1f;
            radiusScale = 0.85f;
        }

        emitter.Configure(trailBufferController, strength, radiusScale);
    }

    private void SpawnSlimeMoldMiniDemo()
    {
        if (!ShapeLibraryProvider.TryGetSprite(ShapeId.DotCore, out var dotSprite))
        {
            return;
        }

        var preset = slimeMoldPreset != null ? slimeMoldPreset : ScriptableObject.CreateInstance<SlimeMoldTrailPreset>();
        preset.ApplyTo(trailBufferController.Settings);

        var parent = new GameObject("SlimeMoldMiniDemo");
        parent.transform.SetParent(transform, false);

        var center = trailBufferController.WorldBounds.center;
        var radius = Mathf.Min(trailBufferController.WorldBounds.width, trailBufferController.WorldBounds.height) * 0.25f;

        for (var i = 0; i < 10; i++)
        {
            var angle = (Mathf.PI * 2f * i) / 10f;
            var pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            var agentGo = new GameObject($"slime_agent_{i:00}");
            agentGo.transform.SetParent(parent.transform, false);
            agentGo.transform.position = new Vector3(pos.x, pos.y, 0f);
            agentGo.transform.localScale = Vector3.one * 0.8f;

            var sr = agentGo.AddComponent<SpriteRenderer>();
            sr.sprite = dotSprite;
            sr.color = new Color(0.6f, 1f, 0.85f, 0.85f);

            var emitter = agentGo.AddComponent<TrailEmitter>();
            emitter.Configure(trailBufferController, preset.depositStrength, 0.8f);

            var agent = agentGo.AddComponent<SlimeMoldDemoAgent>();
            agent.Configure(trailBufferController, preset, new Vector2(Mathf.Cos(angle + 1.2f), Mathf.Sin(angle + 1.2f)));
        }
    }

    private void SpawnHeader(string text, Vector3 localPosition, ShapePaletteCategory category)
    {
        var header = new GameObject($"Header_{text}");
        header.transform.SetParent(transform, false);
        header.transform.localPosition = localPosition;

        var mesh = header.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.anchor = TextAnchor.MiddleLeft;
        mesh.alignment = TextAlignment.Left;
        mesh.characterSize = 0.08f;
        mesh.fontSize = headerFontSize;
        mesh.color = Color.Lerp(headerColor, ResolvePalette().GetColor(category), 0.45f);
    }

    private void SpawnLabel(string id, Transform parent, float yOffset)
    {
        var label = new GameObject($"Label_{id}");
        label.transform.SetParent(parent, false);
        label.transform.localPosition = new Vector3(0f, -yOffset, 0f);

        var mesh = label.AddComponent<TextMesh>();
        mesh.text = id;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.characterSize = 0.055f;
        mesh.fontSize = labelFontSize;
        mesh.color = labelColor;
    }

    private Color GetTint(string shapeId)
    {
        return ResolvePalette().GetColor(ShapeMaterialPalette.ShapeIdToCategory(shapeId));
    }

    private ShapeCategoryPalette ResolvePalette()
    {
        if (shapeCategoryPalette != null)
        {
            return shapeCategoryPalette;
        }

        if (runtimeFallbackPalette == null)
        {
            runtimeFallbackPalette = ScriptableObject.CreateInstance<ShapeCategoryPalette>();
        }

        return runtimeFallbackPalette;
    }

    private ShapeMaterialPalette ResolveMaterialPalette()
    {
        if (shapeMaterialPalette != null)
        {
            return shapeMaterialPalette;
        }

        if (runtimeMaterialPalette == null)
        {
            runtimeMaterialPalette = ShapeMaterialPaletteLoader.Load();
        }

        return runtimeMaterialPalette;
    }

    private readonly struct ShowcaseCategory
    {
        public ShowcaseCategory(string name, ShapePaletteCategory category, string[] shapeIds)
        {
            Name = name;
            Category = category;
            ShapeIds = shapeIds;
        }

        public string Name { get; }
        public ShapePaletteCategory Category { get; }
        public string[] ShapeIds { get; }
    }
}
