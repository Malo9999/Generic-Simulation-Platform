using UnityEngine;

public static class EntityIconFactory
{
    private const int DefaultSizePx = 64;

    public static void BuildAnt(Transform root, AntRole role, Color bodyTint)
    {
        var abdomenScale = role == AntRole.Queen ? new Vector3(0.48f, 0.36f, 1f) : new Vector3(0.4f, 0.3f, 1f);
        var headScale = role == AntRole.Warrior ? new Vector3(0.3f, 0.28f, 1f) : new Vector3(0.24f, 0.22f, 1f);

        AddOutlinedPart(root, "Abdomen", PrimitiveSpriteLibrary.CircleOutline(DefaultSizePx), PrimitiveSpriteLibrary.CircleFill(DefaultSizePx), bodyTint, new Vector3(-0.3f, 0f, 0f), abdomenScale);
        AddOutlinedPart(root, "Thorax", PrimitiveSpriteLibrary.CircleOutline(DefaultSizePx), PrimitiveSpriteLibrary.CircleFill(DefaultSizePx), bodyTint, Vector3.zero, new Vector3(0.3f, 0.26f, 1f));
        AddOutlinedPart(root, "Head", PrimitiveSpriteLibrary.CircleOutline(DefaultSizePx), PrimitiveSpriteLibrary.CircleFill(DefaultSizePx), bodyTint, new Vector3(0.28f, 0f, 0f), headScale);

        var legFill = bodyTint * 0.65f;
        var legOutline = bodyTint * 0.35f;
        var legAngles = new[] { 32f, 45f, 58f };
        for (var i = 0; i < 3; i++)
        {
            var y = 0.16f - (i * 0.16f);
            AddOutlinedPart(root, $"LegLeft_{i}", PrimitiveSpriteLibrary.CapsuleOutline(DefaultSizePx), PrimitiveSpriteLibrary.CapsuleFill(DefaultSizePx), legFill, new Vector3(-0.02f, y, 0f), new Vector3(0.35f, 0.05f, 1f), legAngles[i], legOutline);
            AddOutlinedPart(root, $"LegRight_{i}", PrimitiveSpriteLibrary.CapsuleOutline(DefaultSizePx), PrimitiveSpriteLibrary.CapsuleFill(DefaultSizePx), legFill, new Vector3(-0.02f, -y, 0f), new Vector3(0.35f, 0.05f, 1f), -legAngles[i], legOutline);
        }

        var detailFill = bodyTint * 0.5f;
        AddPart(root, "AntennaLeft", PrimitiveSpriteLibrary.CapsuleFill(DefaultSizePx), detailFill, new Vector3(0.43f, 0.09f, 0f), new Vector3(0.22f, 0.04f, 1f), 2, 38f);
        AddPart(root, "AntennaRight", PrimitiveSpriteLibrary.CapsuleFill(DefaultSizePx), detailFill, new Vector3(0.43f, -0.09f, 0f), new Vector3(0.22f, 0.04f, 1f), 2, -38f);

        if (role == AntRole.Queen)
        {
            var wingTint = new Color(1f, 1f, 1f, 0.15f);
            AddPart(root, "WingTop", PrimitiveSpriteLibrary.CircleFill(DefaultSizePx), wingTint, new Vector3(-0.05f, 0.2f, 0f), new Vector3(0.46f, 0.2f, 1f), 2, 20f);
            AddPart(root, "WingBottom", PrimitiveSpriteLibrary.CircleFill(DefaultSizePx), wingTint, new Vector3(-0.05f, -0.2f, 0f), new Vector3(0.46f, 0.2f, 1f), 2, -20f);
        }

        if (role == AntRole.Warrior)
        {
            var mandibleTint = bodyTint * 0.25f;
            AddPart(root, "MandibleTop", PrimitiveSpriteLibrary.RoundedRectFill(DefaultSizePx), mandibleTint, new Vector3(0.46f, 0.07f, 0f), new Vector3(0.08f, 0.04f, 1f), 2, 20f);
            AddPart(root, "MandibleBottom", PrimitiveSpriteLibrary.RoundedRectFill(DefaultSizePx), mandibleTint, new Vector3(0.46f, -0.07f, 0f), new Vector3(0.08f, 0.04f, 1f), 2, -20f);
        }
    }

    public static void BuildCar(Transform root, CarLivery livery, Color bodyTint, Color liveryTint)
    {
        AddOutlinedPart(root, "Body", PrimitiveSpriteLibrary.RoundedRectOutline(DefaultSizePx), PrimitiveSpriteLibrary.RoundedRectFill(DefaultSizePx), bodyTint, Vector3.zero, new Vector3(0.9f, 0.46f, 1f));
        AddOutlinedPart(root, "Cabin", PrimitiveSpriteLibrary.RoundedRectOutline(DefaultSizePx), PrimitiveSpriteLibrary.RoundedRectFill(DefaultSizePx), bodyTint * 0.88f, new Vector3(0.12f, 0.1f, 0f), new Vector3(0.42f, 0.24f, 1f));

        AddPart(root, "WheelLeft", PrimitiveSpriteLibrary.CircleFill(DefaultSizePx), new Color(0.1f, 0.1f, 0.1f, 1f), new Vector3(-0.3f, -0.2f, 0f), new Vector3(0.2f, 0.2f, 1f), 2);
        AddPart(root, "WheelRight", PrimitiveSpriteLibrary.CircleFill(DefaultSizePx), new Color(0.1f, 0.1f, 0.1f, 1f), new Vector3(0.34f, -0.2f, 0f), new Vector3(0.2f, 0.2f, 1f), 2);
        AddPart(root, "Windshield", PrimitiveSpriteLibrary.RoundedRectFill(DefaultSizePx), new Color(0.12f, 0.16f, 0.2f, 1f), new Vector3(0.14f, 0.1f, 0f), new Vector3(0.2f, 0.14f, 1f), 2);

        switch (livery)
        {
            case CarLivery.CenterStripe:
                AddPart(root, "LiveryCenter", PrimitiveSpriteLibrary.CapsuleFill(DefaultSizePx), liveryTint, new Vector3(0f, 0f, 0f), new Vector3(0.76f, 0.08f, 1f), 2);
                break;
            case CarLivery.SideStripes:
                AddPart(root, "LiveryTop", PrimitiveSpriteLibrary.CapsuleFill(DefaultSizePx), liveryTint, new Vector3(0f, 0.1f, 0f), new Vector3(0.74f, 0.06f, 1f), 2);
                AddPart(root, "LiveryBottom", PrimitiveSpriteLibrary.CapsuleFill(DefaultSizePx), liveryTint, new Vector3(0f, -0.08f, 0f), new Vector3(0.74f, 0.06f, 1f), 2);
                break;
            case CarLivery.DiagonalStripe:
                AddPart(root, "LiveryDiagonal", PrimitiveSpriteLibrary.CapsuleFill(DefaultSizePx), liveryTint, Vector3.zero, new Vector3(0.78f, 0.08f, 1f), 2, 18f);
                break;
            default:
                AddPart(root, "LiverySolid", PrimitiveSpriteLibrary.RoundedRectFill(DefaultSizePx), liveryTint * 0.35f, Vector3.zero, new Vector3(0.84f, 0.36f, 1f), 2);
                break;
        }
    }

    public static void BuildMarble(Transform root, MarbleStripe stripe, Color baseTint, Color stripeTint)
    {
        AddOutlinedPart(root, "Base", PrimitiveSpriteLibrary.CircleOutline(DefaultSizePx), PrimitiveSpriteLibrary.CircleFill(DefaultSizePx), baseTint, Vector3.zero, new Vector3(0.62f, 0.62f, 1f));
        AddPart(root, "Highlight", PrimitiveSpriteLibrary.CircleFill(DefaultSizePx), new Color(1f, 1f, 1f, 0.35f), new Vector3(-0.16f, 0.16f, 0f), new Vector3(0.18f, 0.18f, 1f), 2);

        switch (stripe)
        {
            case MarbleStripe.Single:
                AddPart(root, "StripeSingle", PrimitiveSpriteLibrary.CapsuleFill(DefaultSizePx), stripeTint, Vector3.zero, new Vector3(0.62f, 0.11f, 1f), 2);
                break;
            case MarbleStripe.Double:
                AddPart(root, "StripeUpper", PrimitiveSpriteLibrary.CapsuleFill(DefaultSizePx), stripeTint, new Vector3(0f, 0.1f, 0f), new Vector3(0.58f, 0.08f, 1f), 2);
                AddPart(root, "StripeLower", PrimitiveSpriteLibrary.CapsuleFill(DefaultSizePx), stripeTint, new Vector3(0f, -0.1f, 0f), new Vector3(0.58f, 0.08f, 1f), 2);
                break;
            case MarbleStripe.Diagonal:
                AddPart(root, "StripeDiagonal", PrimitiveSpriteLibrary.CapsuleFill(DefaultSizePx), stripeTint, Vector3.zero, new Vector3(0.68f, 0.1f, 1f), 2, 30f);
                break;
        }
    }

    public static void BuildAthlete(Transform root, AthleteKit kit, Color jerseyTint, Color padsTint)
    {
        AddOutlinedPart(root, "Torso", PrimitiveSpriteLibrary.RoundedRectOutline(DefaultSizePx), PrimitiveSpriteLibrary.RoundedRectFill(DefaultSizePx), jerseyTint, new Vector3(0f, -0.02f, 0f), new Vector3(0.42f, 0.54f, 1f));
        AddOutlinedPart(root, "Head", PrimitiveSpriteLibrary.CircleOutline(DefaultSizePx), PrimitiveSpriteLibrary.CircleFill(DefaultSizePx), jerseyTint * 0.92f, new Vector3(0.24f, 0.12f, 0f), new Vector3(0.24f, 0.24f, 1f));

        AddPart(root, "FaceMask", PrimitiveSpriteLibrary.RoundedRectFill(DefaultSizePx), new Color(0.08f, 0.08f, 0.1f, 1f), new Vector3(0.29f, 0.11f, 0f), new Vector3(0.12f, 0.04f, 1f), 2);
        AddPart(root, "PadLeft", PrimitiveSpriteLibrary.CircleFill(DefaultSizePx), padsTint, new Vector3(-0.13f, 0.17f, 0f), new Vector3(0.28f, 0.2f, 1f), 2);
        AddPart(root, "PadRight", PrimitiveSpriteLibrary.CircleFill(DefaultSizePx), padsTint, new Vector3(0.1f, 0.17f, 0f), new Vector3(0.28f, 0.2f, 1f), 2);

        if (kit != AthleteKit.Home)
        {
            AddPart(root, "NumberPatch", PrimitiveSpriteLibrary.RoundedRectFill(DefaultSizePx), Color.white, new Vector3(0f, -0.03f, 0f), new Vector3(0.1f, 0.08f, 1f), 2);
        }
    }

    private static void AddOutlinedPart(Transform parent, string name, Sprite outlineSprite, Sprite fillSprite, Color fillTint, Vector3 localPosition, Vector3 localScale, float rotationZ = 0f, Color? outlineTint = null)
    {
        var node = new GameObject(name).transform;
        node.SetParent(parent, false);
        node.localPosition = localPosition;
        node.localScale = localScale;
        node.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

        AddPart(node, "Outline", outlineSprite, outlineTint ?? Color.black, Vector3.zero, Vector3.one, 0);
        AddPart(node, "Fill", fillSprite, fillTint, Vector3.zero, Vector3.one, 1);
    }

    private static void AddPart(Transform parent, string name, Sprite sprite, Color tint, Vector3 localPosition, Vector3 localScale, int sortingOrder, float rotationZ = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = tint;
        renderer.sortingOrder = sortingOrder;
    }
}
