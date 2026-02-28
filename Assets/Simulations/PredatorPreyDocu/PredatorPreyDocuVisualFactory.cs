using UnityEngine;

public static class PredatorPreyDocuVisualFactory
{
    private const string SortingLayerDefault = "Default";

    private static readonly Color[] HerdPalette =
    {
        new(0.88f, 0.95f, 0.34f, 1f),
        new(0.62f, 0.9f, 0.3f, 1f),
        new(0.98f, 0.83f, 0.34f, 1f),
        new(0.72f, 0.92f, 0.55f, 1f),
        new(0.95f, 0.92f, 0.5f, 1f)
    };

    private static readonly Color[] PridePalette =
    {
        new(0.35f, 0.9f, 0.95f, 1f),
        new(0.85f, 0.6f, 0.98f, 1f),
        new(1f, 0.67f, 0.32f, 1f),
        new(0.45f, 0.75f, 1f, 1f),
        new(0.9f, 0.45f, 0.75f, 1f)
    };

    public static Color HerdAccentColor(int herdId)
    {
        return HerdPalette[Mathf.Abs(herdId) % HerdPalette.Length];
    }

    public static Color PrideAccentColor(int prideId)
    {
        return PridePalette[Mathf.Abs(prideId) % PridePalette.Length];
    }

    public static GameObject BuildPrey(Transform parent, Color herdAccent, float scale, bool showAccent)
    {
        var root = new GameObject("PreyVisual");
        root.transform.SetParent(parent, false);
        root.transform.localScale = Vector3.one * scale;

        AddSprite(root.transform, "Body", PrimitiveSpriteLibrary.CapsuleFill(), new Color(0.28f, 0.74f, 0.22f, 1f), Vector3.zero, new Vector3(1f, 0.62f, 1f), 0);
        AddSprite(root.transform, "Outline", PrimitiveSpriteLibrary.CapsuleOutline(), new Color(0.12f, 0.28f, 0.1f, 1f), Vector3.zero, new Vector3(1.06f, 0.68f, 1f), 1);

        if (showAccent)
        {
            AddSprite(root.transform, "Accent", PrimitiveSpriteLibrary.CircleOutline(), herdAccent, new Vector3(0.28f, 0.14f, 0f), new Vector3(0.28f, 0.28f, 1f), 2);
        }

        return root;
    }

    public static GameObject BuildLion(Transform parent, Color prideAccent, float scale, bool isMale, bool isYoung, bool isLeader, bool showAccent, bool showMaleRing, bool isRoaming)
    {
        var root = new GameObject("LionVisual");
        root.transform.SetParent(parent, false);

        var ageScale = isYoung ? 0.85f : 1f;
        root.transform.localScale = Vector3.one * (scale * ageScale);

        var bodyColor = isRoaming ? new Color(0.82f, 0.2f, 0.16f, 1f) : new Color(0.9f, 0.28f, 0.2f, 1f);
        AddSprite(root.transform, "Body", PrimitiveSpriteLibrary.CircleFill(), bodyColor, Vector3.zero, Vector3.one, 0);
        AddSprite(root.transform, "Outline", PrimitiveSpriteLibrary.CircleOutline(), new Color(0.34f, 0.08f, 0.08f, 1f), Vector3.zero, new Vector3(1.06f, 1.06f, 1f), 1);

        if (showAccent)
        {
            AddSprite(root.transform, "AccentRing", PrimitiveSpriteLibrary.CircleOutline(), prideAccent, Vector3.zero, new Vector3(1.18f, 1.18f, 1f), 2);
        }

        if (isMale && showMaleRing)
        {
            AddSprite(root.transform, "ManeRing", PrimitiveSpriteLibrary.CircleOutline(), new Color(0.58f, 0.2f, 0.08f, 1f), Vector3.zero, new Vector3(1.3f, 1.3f, 1f), 3);
        }

        if (isLeader)
        {
            AddSprite(root.transform, "LeaderRing", PrimitiveSpriteLibrary.CircleOutline(), new Color(1f, 0.92f, 0.5f, 1f), Vector3.zero, new Vector3(1.25f, 1.25f, 1f), 3);
        }

        return root;
    }

    private static void AddSprite(Transform parent, string name, Sprite sprite, Color color, Vector3 localPos, Vector3 localScale, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = SortingLayerDefault;
        renderer.sortingOrder = sortingOrder;
        renderer.enabled = true;
    }
}
