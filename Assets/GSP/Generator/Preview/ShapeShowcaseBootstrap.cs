using UnityEngine;

public sealed class ShapeShowcaseBootstrap : MonoBehaviour
{
    [SerializeField] private int columns = 5;
    [SerializeField] private float spacing = 2f;
    [SerializeField] private float spriteScale = 1f;

    private static readonly string[] ShowcaseShapeIds =
    {
        ShapeId.DotCore,
        ShapeId.DotGlow,
        ShapeId.DotGlowSmall,
        ShapeId.RingPing,
        ShapeId.PulseRing,
        ShapeId.OrganicMetaball,
        ShapeId.OrganicAmoeba,
        ShapeId.NoiseBlob,
        ShapeId.FieldBlob,
        ShapeId.TriangleAgent,
        ShapeId.DiamondAgent,
        ShapeId.ArrowAgent,
        ShapeId.LineSegment,
        ShapeId.StrokeScribble,
        ShapeId.Filament,
        ShapeId.CrossMarker,
        ShapeId.ArcSector
    };

    private void Start()
    {
        SpawnGrid();
    }

    private void SpawnGrid()
    {
        var total = ShowcaseShapeIds.Length;
        var rows = Mathf.CeilToInt(total / (float)Mathf.Max(1, columns));
        var xOffset = (Mathf.Max(1, columns) - 1) * spacing * 0.5f;
        var yOffset = (rows - 1) * spacing * 0.5f;

        for (var i = 0; i < total; i++)
        {
            var id = ShowcaseShapeIds[i];
            if (!ShapeLibraryProvider.TryGetSprite(id, out var sprite))
            {
                continue;
            }

            var col = i % columns;
            var row = i / columns;
            var position = new Vector3((col * spacing) - xOffset, yOffset - (row * spacing), 0f);

            var go = new GameObject(id);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * spriteScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = GetTint(id);

            var profile = AnimatedShapeProfile.CreateForShapeId(id);
            if (profile.animType != ShapeAnimType.None)
            {
                var driver = go.AddComponent<AnimatedShapeDriver>();
                driver.Configure(sr, profile, i * 97);
            }
        }
    }

    private static Color GetTint(string shapeId)
    {
        if (shapeId == ShapeId.FieldBlob)
        {
            return new Color(0.45f, 0.9f, 1f, 0.95f);
        }

        if (shapeId == ShapeId.DotGlow || shapeId == ShapeId.DotGlowSmall || shapeId == ShapeId.PulseRing || shapeId == ShapeId.RingPing)
        {
            return new Color(0.55f, 1f, 1f, 1f);
        }

        if (shapeId == ShapeId.TriangleAgent || shapeId == ShapeId.DiamondAgent || shapeId == ShapeId.ArrowAgent || shapeId == ShapeId.CrossMarker)
        {
            return new Color(1f, 0.95f, 0.65f, 1f);
        }

        if (shapeId == ShapeId.LineSegment || shapeId == ShapeId.StrokeScribble || shapeId == ShapeId.Filament || shapeId == ShapeId.ArcSector)
        {
            return new Color(0.75f, 1f, 0.8f, 1f);
        }

        return new Color(0.8f, 1f, 0.9f, 1f);
    }
}
