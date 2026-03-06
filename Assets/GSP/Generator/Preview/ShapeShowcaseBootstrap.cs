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
    [SerializeField] private Color defaultTint = new(0.55f, 0.98f, 1f, 1f);
    [SerializeField] private Color headerColor = new(0.95f, 0.98f, 1f, 1f);
    [SerializeField] private Color labelColor = new(0.82f, 0.95f, 1f, 1f);
    [SerializeField] private Color cameraBackground = Color.black;

    [SerializeField] private int headerFontSize = 58;
    [SerializeField] private int labelFontSize = 32;

    private static readonly ShowcaseCategory[] Categories =
    {
        new("CORE", new[]
        {
            ShapeId.DotCore,
            ShapeId.DotGlow,
            ShapeId.DotGlowSmall,
            ShapeId.RingPing,
            ShapeId.PulseRing
        }),
        new("ORGANIC", new[]
        {
            ShapeId.OrganicMetaball,
            ShapeId.OrganicAmoeba,
            ShapeId.NoiseBlob,
            ShapeId.FieldBlob
        }),
        new("AGENTS / MARKERS", new[]
        {
            ShapeId.TriangleAgent,
            ShapeId.DiamondAgent,
            ShapeId.ArrowAgent,
            ShapeId.CrossMarker,
            ShapeId.ArcSector
        }),
        new("LINES / MOTION", new[]
        {
            ShapeId.LineSegment,
            ShapeId.StrokeScribble,
            ShapeId.Filament
        })
    };

    private readonly HashSet<string> missingLogged = new();

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
            SpawnHeader(category.Name, new Vector3(originX - (horizontalSpacing * 1.05f), y + headerOffset, 0f));

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
                SpawnShape(category, id, sprite, position, spawnedCount);
                spawnedCount++;
            }
        }

        if (spawnedCount == 0)
        {
            Debug.LogError("Shape showcase could not resolve any sprites from ShapeLibraryProvider.");
        }
    }

    private void SpawnShape(ShowcaseCategory category, string shapeId, Sprite sprite, Vector3 localPosition, int sequence)
    {
        var go = new GameObject(shapeId);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = Vector3.one * spriteScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = GetTint(category.Name, shapeId);

        var profile = AnimatedShapeProfile.CreateForShapeId(shapeId);
        if (profile.animType != ShapeAnimType.None)
        {
            var driver = go.AddComponent<AnimatedShapeDriver>();
            driver.Configure(sr, profile, sequence * 97);
        }

        SpawnLabel(shapeId, go.transform, labelOffset);
    }

    private void SpawnHeader(string text, Vector3 localPosition)
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
        mesh.color = headerColor;
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

    private Color GetTint(string categoryName, string shapeId)
    {
        if (shapeId == ShapeId.FieldBlob)
        {
            return new Color(0.52f, 0.88f, 1f, 0.95f);
        }

        if (categoryName == "AGENTS / MARKERS")
        {
            return new Color(0.78f, 1f, 0.92f, 1f);
        }

        return defaultTint;
    }

    private readonly struct ShowcaseCategory
    {
        public ShowcaseCategory(string name, string[] shapeIds)
        {
            Name = name;
            ShapeIds = shapeIds;
        }

        public string Name { get; }
        public string[] ShapeIds { get; }
    }
}
