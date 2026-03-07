using System;
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
    [SerializeField] private bool useMaterialPaletteInShowcase = false;
    [SerializeField] private Material showcaseDefaultSpriteMaterial;
    [SerializeField] private ShapeShowcaseProceduralMaterialConfig proceduralMaterials = new();

    [Header("Theme Verification")]
    [SerializeField] private List<ShowcaseThemeEntry> themes = new();
    [SerializeField, Min(0)] private int selectedThemeIndex;

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
    private static Material runtimeShowcaseFallbackMaterial;

    // ShapeShowcase is a stable tint-only preview scene; advanced demos belong elsewhere.
    private static readonly Color CoreColor = new(70f / 255f, 242f / 255f, 1f, 1f);      // #46F2FF
    private static readonly Color OrganicColor = new(47f / 255f, 175f / 255f, 142f / 255f, 1f); // #2FAF8E
    private static readonly Color AgentsColor = new(143f / 255f, 227f / 255f, 179f / 255f, 1f); // #8FE3B3
    private static readonly Color MarkersColor = new(127f / 255f, 184f / 255f, 216f / 255f, 1f); // #7FB8D8
    private static readonly Color LinesColor = new(62f / 255f, 214f / 255f, 224f / 255f, 1f);   // #3ED6E0

    private void Start()
    {
        ApplySceneBackground();
        ClearStaleShowcaseSpriteMaterials();
        SpawnShowcase();
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

    private void ClearStaleShowcaseSpriteMaterials()
    {
        var sprites = GetComponentsInChildren<SpriteRenderer>(true);
        for (var i = 0; i < sprites.Length; i++)
        {
            sprites[i].sharedMaterial = ResolveShowcaseDefaultSpriteMaterial();
        }
    }

    private void SpawnShowcase()
    {
        var runtimeProceduralConfig = ResolveRuntimeProceduralConfig();

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
                SpawnShape(id, category.Category, sprite, position, spawnedCount, runtimeProceduralConfig);
                spawnedCount++;
            }
        }

        if (spawnedCount == 0)
        {
            Debug.LogError("Shape showcase could not resolve any sprites from ShapeLibraryProvider.");
        }
    }

    private void SpawnShape(string shapeId, ShapePaletteCategory category, Sprite sprite, Vector3 localPosition, int sequence, ShapeShowcaseProceduralMaterialConfig runtimeProceduralConfig)
    {
        var go = new GameObject(shapeId);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = Vector3.one * spriteScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = ResolveCategoryColor(category);
        sr.sharedMaterial = ResolveShowcaseDefaultSpriteMaterial();
        if (useMaterialPaletteInShowcase)
        {
            TryAssignPaletteMaterial(sr, category, shapeId);
        }

        var applier = EnsureProceduralMaterialApplier(go);
        var applyStatus = applier.TryApply(sr, shapeId, category, runtimeProceduralConfig, sequence * 97);
        var proceduralApplied = applyStatus == ProceduralMaterialApplyStatus.Applied;

        var profile = AnimatedShapeProfile.CreateForShapeId(shapeId);
        if (profile.animType != ShapeAnimType.None)
        {
            var driver = go.AddComponent<AnimatedShapeDriver>();
            driver.Configure(sr, profile, sequence * 97);
            driver.SetProceduralMaterialApplier(applier, proceduralApplied);
        }

        var visualKey = BuildShowcaseVisualKey(shapeId, sequence);
        MotionShaderAutoWiring.TryAttachAndConfigure(sr, visualKey, shapeId, MotionShaderProfile.LoadRuntimeProfile());

        SpawnLabel(shapeId, go.transform, labelOffset);
    }


    private static VisualKey BuildShowcaseVisualKey(string shapeId, int sequence)
    {
        return VisualKeyBuilder.Create(
            "ShapeShowcase",
            shapeId,
            sequence,
            shapeId,
            "showcase",
            FacingMode.Auto,
            -1,
            sequence);
    }

    private ShapeShowcaseProceduralMaterialConfig ResolveRuntimeProceduralConfig()
    {
        if (proceduralMaterials == null)
        {
            return null;
        }

        if (!TryGetSelectedTheme(out var selectedTheme) || selectedTheme.materialPalette == null)
        {
            return proceduralMaterials;
        }

        return proceduralMaterials.CreateRuntimeCopy(selectedTheme.materialPalette, selectedTheme.overrideDefaultIntensity, selectedTheme.defaultIntensity);
    }

    private bool TryGetSelectedTheme(out ShowcaseThemeEntry theme)
    {
        theme = default;
        if (themes == null || themes.Count == 0)
        {
            return false;
        }

        var clampedIndex = Mathf.Clamp(selectedThemeIndex, 0, themes.Count - 1);
        theme = themes[clampedIndex];

        if (string.IsNullOrWhiteSpace(theme.themeName))
        {
            return false;
        }

        return true;
    }

    private void TryAssignPaletteMaterial(SpriteRenderer renderer, ShapePaletteCategory category, string shapeId)
    {
        if (renderer == null)
        {
            return;
        }

        var palette = ShapeMaterialPaletteLoader.Load();
        if (palette == null)
        {
            return;
        }

        var material = palette.GetMaterial(category);
        if (material == null)
        {
            if (missingLogged.Add($"mat:{shapeId}"))
            {
                Debug.LogWarning($"Shape showcase missing palette material for shape id: {shapeId}");
            }

            return;
        }

        renderer.sharedMaterial = material;
    }

    private static SpriteProceduralMaterialApplier EnsureProceduralMaterialApplier(GameObject go)
    {
        var applier = go.GetComponent<SpriteProceduralMaterialApplier>();
        if (applier == null)
        {
            applier = go.AddComponent<SpriteProceduralMaterialApplier>();
        }

        return applier;
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
        mesh.color = Color.Lerp(headerColor, ResolveCategoryColor(category), 0.45f);
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


    private Material ResolveShowcaseDefaultSpriteMaterial()
    {
        if (showcaseDefaultSpriteMaterial != null && showcaseDefaultSpriteMaterial.shader != null && showcaseDefaultSpriteMaterial.shader.isSupported)
        {
            return showcaseDefaultSpriteMaterial;
        }

        runtimeShowcaseFallbackMaterial ??= CreateRuntimeSpriteFallbackMaterial();
        return runtimeShowcaseFallbackMaterial;
    }

    private static Material CreateRuntimeSpriteFallbackMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null || !shader.isSupported)
        {
            return null;
        }

        var mat = new Material(shader);
        mat.name = "Runtime_ShowcaseSpriteFallback";
        return mat;
    }

    private static Color ResolveCategoryColor(ShapePaletteCategory category)
    {
        return category switch
        {
            ShapePaletteCategory.Core => CoreColor,
            ShapePaletteCategory.Organic => OrganicColor,
            ShapePaletteCategory.Agents => AgentsColor,
            ShapePaletteCategory.Markers => MarkersColor,
            ShapePaletteCategory.Lines => LinesColor,
            _ => CoreColor
        };
    }

    [Serializable]
    private struct ShowcaseThemeEntry
    {
        public string themeName;
        public ShapeMaterialPalette materialPalette;
        public bool overrideDefaultIntensity;
        [Min(0f)] public float defaultIntensity;
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
