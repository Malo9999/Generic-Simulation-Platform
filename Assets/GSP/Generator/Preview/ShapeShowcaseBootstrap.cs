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

    [Header("Field Overlay")]
    [SerializeField] private bool enableFieldOverlayDemo = true;
    [SerializeField] private FieldOverlayDemoMode overlayMode = FieldOverlayDemoMode.Pheromone;
    [SerializeField] private bool enableSlimeMoldMiniDemo = true;
    [SerializeField] private TrailShowcasePreset trailPreset;
    [SerializeField] private TrailVisualSettings trailSettings = new();
    [SerializeField] private SlimeMoldSteeringSettings slimeSteeringSettings = new();

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
    private FieldBufferController fieldBufferController;

    private void Start()
    {
        ApplyTrailPreset();
        ApplySceneBackground();
        if (enableTrailDemo)
        {
            SetupTrailSystem();
        }

        if (enableFieldOverlayDemo && overlayMode != FieldOverlayDemoMode.Off)
        {
            SetupFieldOverlaySystem();
        }

        SpawnShowcase();

        if (enableTrailDemo && enableSlimeMoldMiniDemo)
        {
            SpawnSlimeMoldMiniDemo();
        }

        FitCameraToSpawnedContent();
    }

    private void FitCameraToSpawnedContent()
    {
        if (!TryCalculateShowcaseBounds(out var bounds))
        {
            return;
        }

        FrameMainCameraToBounds(bounds);
    }

    private void FrameMainCameraToBounds(Bounds bounds)
    {
        var cameraRef = Camera.main;
        if (cameraRef == null || !cameraRef.orthographic)
        {
            return;
        }

        var center = bounds.center;
        var halfHeight = bounds.extents.y;
        var halfWidth = bounds.extents.x;

        var sizeFromHeight = halfHeight;
        var sizeFromWidth = halfWidth / Mathf.Max(0.01f, cameraRef.aspect);
        var targetSize = Mathf.Max(sizeFromHeight, sizeFromWidth, 1f) * 1.15f;

        cameraRef.transform.position = new Vector3(center.x, center.y, cameraRef.transform.position.z);
        cameraRef.orthographicSize = targetSize;

        var followController = cameraRef.GetComponent<CameraFollowController>();
        if (followController != null)
        {
            followController.enabled = false;
        }
    }

    private bool TryCalculateShowcaseBounds(out Bounds bounds)
    {
        bounds = default;
        var hasBounds = false;

        var spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        for (var i = 0; i < spriteRenderers.Length; i++)
        {
            if (!spriteRenderers[i].enabled || spriteRenderers[i].sprite == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = spriteRenderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(spriteRenderers[i].bounds);
            }
        }

        var textMeshes = GetComponentsInChildren<TextMesh>();
        for (var i = 0; i < textMeshes.Length; i++)
        {
            var renderer = textMeshes[i].GetComponent<Renderer>();
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void SetupTrailSystem()
    {
        var trailRoot = new GameObject("TrailBuffer");
        trailRoot.transform.SetParent(transform, false);

        trailBufferController = trailRoot.AddComponent<TrailBufferController>();
        trailSettings.ApplyTo(trailBufferController.Settings);

        var renderer = trailRoot.AddComponent<TrailBufferRenderer>();
        renderer.Configure(trailBufferController);
    }


    private void SetupFieldOverlaySystem()
    {
        var fieldRoot = new GameObject("FieldOverlay");
        fieldRoot.transform.SetParent(transform, false);

        fieldBufferController = fieldRoot.AddComponent<FieldBufferController>();

        if (overlayMode == FieldOverlayDemoMode.Pheromone)
        {
            PheromoneOverlayProfile.Apply(fieldBufferController.Settings);
        }
        else
        {
            ApplyHeatmapProfile(fieldBufferController.Settings);
        }

        ApplyKnownGoodFieldOverlayDefaults(fieldBufferController.Settings);

        var renderer = fieldRoot.AddComponent<FieldOverlayRenderer>();
        renderer.Configure(fieldBufferController);
    }

    private static void ApplyHeatmapProfile(FieldOverlaySettings settings)
    {
        settings.width = 256;
        settings.height = 144;
        settings.decayPerSecond = 0.80f;
        settings.diffuseStrength = 0.05f;
        settings.intensity = 1f;
        settings.alphaMultiplier = 0.40f;
        settings.blendMode = FieldOverlayBlendMode.Alpha;
        settings.tintLow = FieldOverlayPalette.DefaultLow;
        settings.tintHigh = new Color(0.95f, 0.72f, 0.30f, 1f);
    }

    private void ApplyKnownGoodFieldOverlayDefaults(FieldOverlaySettings settings)
    {
        if (settings == null)
        {
            return;
        }

        settings.tintLow = new Color(0f, 0f, 0f, 0f);
        settings.blendMode = FieldOverlayBlendMode.Alpha;
        settings.alphaMultiplier = 0.35f;
        settings.intensity = 1f;
        settings.decayPerSecond = 0.80f;
        settings.diffuseStrength = 0.05f;

        if (overlayMode == FieldOverlayDemoMode.Heatmap)
        {
            settings.tintHigh = new Color(0.95f, 0.72f, 0.30f, 1f);
        }
        else
        {
            settings.tintHigh = new Color(0.3f, 0.92f, 1f, 1f);
        }

        if (settings.tintHigh.a <= 0f)
        {
            settings.tintHigh = new Color(settings.tintHigh.r, settings.tintHigh.g, settings.tintHigh.b, 1f);
        }

        var looksMagenta = settings.tintHigh.r >= 0.85f && settings.tintHigh.g <= 0.2f && settings.tintHigh.b >= 0.85f;
        if (looksMagenta)
        {
            settings.tintHigh = overlayMode == FieldOverlayDemoMode.Heatmap
                ? new Color(0.95f, 0.72f, 0.30f, 1f)
                : new Color(0.3f, 0.92f, 1f, 1f);
        }
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
        if (!enableTrailDemo || (trailBufferController == null && fieldBufferController == null))
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

        if (fieldBufferController != null)
        {
            emitter.Configure(fieldBufferController, strength, radiusScale);
        }
        else
        {
            emitter.Configure(trailBufferController, strength, radiusScale);
        }
    }

    private void SpawnSlimeMoldMiniDemo()
    {
        if (!ShapeLibraryProvider.TryGetSprite(ShapeId.DotCore, out var dotSprite))
        {
            return;
        }

        var parent = new GameObject("SlimeMoldMiniDemo");
        parent.transform.SetParent(transform, false);

        var activeBuffer = ResolveActiveFieldBuffer();
        if (activeBuffer == null)
        {
            return;
        }

        var center = activeBuffer.WorldBounds.center;
        var radius = Mathf.Min(activeBuffer.WorldBounds.width, activeBuffer.WorldBounds.height) * 0.25f;

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
            if (fieldBufferController != null)
            {
                emitter.Configure(fieldBufferController, trailSettings.depositStrength, 0.8f);
            }
            else
            {
                emitter.Configure(trailBufferController, trailSettings.depositStrength, 0.8f);
            }

            var agent = agentGo.AddComponent<SlimeMoldDemoAgent>();
            if (fieldBufferController != null)
            {
                agent.Configure(fieldBufferController, slimeSteeringSettings, new Vector2(Mathf.Cos(angle + 1.2f), Mathf.Sin(angle + 1.2f)));
            }
            else
            {
                agent.Configure(trailBufferController, slimeSteeringSettings, new Vector2(Mathf.Cos(angle + 1.2f), Mathf.Sin(angle + 1.2f)));
            }
        }
    }


    private IFieldDepositBuffer ResolveActiveFieldBuffer()
    {
        if (fieldBufferController != null)
        {
            return fieldBufferController;
        }

        return trailBufferController;
    }

    private void ApplyTrailPreset()
    {
        if (trailPreset == null)
        {
            return;
        }

        trailPreset.ApplyTo(trailSettings, slimeSteeringSettings);
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
