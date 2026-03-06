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

    private void Start()
    {
        ApplySceneBackground();
        SpawnShowcase();
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
                SpawnShape(id, sprite, position, spawnedCount);
                spawnedCount++;
            }
        }

        if (spawnedCount == 0)
        {
            Debug.LogError("Shape showcase could not resolve any sprites from ShapeLibraryProvider.");
        }
    }

    private void SpawnShape(string shapeId, Sprite sprite, Vector3 localPosition, int sequence)
    {
        var go = new GameObject(shapeId);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = Vector3.one * spriteScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = GetTint(shapeId);

        var profile = AnimatedShapeProfile.CreateForShapeId(shapeId);
        if (profile.animType != ShapeAnimType.None)
        {
            var driver = go.AddComponent<AnimatedShapeDriver>();
            driver.Configure(sr, profile, sequence * 97);
        }

        SpawnLabel(shapeId, go.transform, labelOffset);
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
        return ResolvePalette().GetColor(ShapeIdToCategory(shapeId));
    }

    private static ShapePaletteCategory ShapeIdToCategory(string shapeId)
    {
        return shapeId switch
        {
            ShapeId.DotCore => ShapePaletteCategory.Core,
            ShapeId.DotGlow => ShapePaletteCategory.Core,
            ShapeId.DotGlowSmall => ShapePaletteCategory.Core,
            ShapeId.RingPing => ShapePaletteCategory.Core,
            ShapeId.PulseRing => ShapePaletteCategory.Core,
            ShapeId.OrganicMetaball => ShapePaletteCategory.Organic,
            ShapeId.OrganicAmoeba => ShapePaletteCategory.Organic,
            ShapeId.NoiseBlob => ShapePaletteCategory.Organic,
            ShapeId.FieldBlob => ShapePaletteCategory.Organic,
            ShapeId.TriangleAgent => ShapePaletteCategory.Agents,
            ShapeId.DiamondAgent => ShapePaletteCategory.Agents,
            ShapeId.ArrowAgent => ShapePaletteCategory.Agents,
            ShapeId.CrossMarker => ShapePaletteCategory.Markers,
            ShapeId.ArcSector => ShapePaletteCategory.Markers,
            ShapeId.LineSegment => ShapePaletteCategory.Lines,
            ShapeId.StrokeScribble => ShapePaletteCategory.Lines,
            ShapeId.Filament => ShapePaletteCategory.Lines,
            _ => ShapePaletteCategory.Core
        };
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
