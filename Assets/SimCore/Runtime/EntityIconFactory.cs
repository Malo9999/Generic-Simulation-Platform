using UnityEngine;

public static class EntityIconFactory
{
    private const int DefaultSizePx = 64;
    private const int AntSegmentSizePx = 256;
    private const int AntAppendageSizePx = 128;

    public static void BuildMarble(Transform root, EntityIdentity identity)
    {
        var stripe = (MarbleStripe)Mathf.Abs(identity.variant % 4);
        var baseTint = ColorFromIdentity(identity, 0.2f, 0.95f, 0x9E3779B9u);
        var stripeTint = ColorFromIdentity(identity, 0.7f, 1f, 0x7F4A7C15u);
        BuildMarble(root, stripe, baseTint, stripeTint);
    }

    public static void BuildCar(Transform root, EntityIdentity identity)
    {
        var livery = (CarLivery)Mathf.Abs(identity.variant % 4);
        var bodyTint = ColorFromIdentity(identity, 0.2f, 0.95f, 0xA24BAEDCu);
        var liveryTint = ColorFromIdentity(identity, 0.8f, 1f, 0x165667B1u);
        BuildCar(root, livery, bodyTint, liveryTint);
    }

    public static void BuildAthlete(Transform root, EntityIdentity identity)
    {
        var kit = (AthleteKit)Mathf.Abs(identity.variant % 3);
        var jerseyTint = ColorFromIdentity(identity, 0.15f, 0.95f, 0x27D4EB2Fu);
        var padsTint = ColorFromIdentity(identity, 0.8f, 1f, 0xB5297A4Du);
        BuildAthlete(root, kit, jerseyTint, padsTint);
    }

    public static void BuildAnt(Transform root, AntRole role, Color bodyTint)
    {
        var darkOutline = new Color(0.06f, 0.06f, 0.06f, 1f);
        var thoraxScale = new Vector3(0.28f, 0.28f, 1f);
        var abdomenScale = role == AntRole.Queen ? new Vector3(0.42f, 0.42f, 1f) : new Vector3(0.36f, 0.36f, 1f);
        var headScale = role == AntRole.Warrior ? new Vector3(0.30f, 0.30f, 1f) : new Vector3(0.24f, 0.24f, 1f);

        AddOutlinedPart(root, "Abdomen", PrimitiveSpriteLibrary.CircleOutline(AntSegmentSizePx), PrimitiveSpriteLibrary.CircleFill(AntSegmentSizePx), bodyTint, new Vector3(-0.50f, 0f, 0f), abdomenScale, 0f, darkOutline);
        AddOutlinedPart(root, "Thorax", PrimitiveSpriteLibrary.CircleOutline(AntSegmentSizePx), PrimitiveSpriteLibrary.CircleFill(AntSegmentSizePx), bodyTint, Vector3.zero, thoraxScale, 0f, darkOutline);
        AddOutlinedPart(root, "Head", PrimitiveSpriteLibrary.CircleOutline(AntSegmentSizePx), PrimitiveSpriteLibrary.CircleFill(AntSegmentSizePx), bodyTint, new Vector3(0.48f, 0f, 0f), headScale, 0f, darkOutline);

        var connectorTint = bodyTint * 0.85f;
        AddOutlinedPart(root, "WaistRear", PrimitiveSpriteLibrary.CapsuleOutline(AntAppendageSizePx), PrimitiveSpriteLibrary.CapsuleFill(AntAppendageSizePx), connectorTint, new Vector3(-0.23f, 0f, 0f), new Vector3(0.11f, 0.06f, 1f), 0f, darkOutline);
        AddOutlinedPart(root, "WaistFront", PrimitiveSpriteLibrary.CapsuleOutline(AntAppendageSizePx), PrimitiveSpriteLibrary.CapsuleFill(AntAppendageSizePx), connectorTint, new Vector3(0.20f, 0f, 0f), new Vector3(0.1f, 0.055f, 1f), 0f, darkOutline);

        var appendageFill = new Color(0.12f, 0.10f, 0.10f, 1f);
        var legXOffsets = new[] { -0.15f, 0f, 0.15f };
        var legAngles = new[] { 72f, 90f, 108f };
        for (var i = 0; i < legXOffsets.Length; i++)
        {
            var x = legXOffsets[i];
            AddOutlinedPart(root, $"LegTop_{i}", PrimitiveSpriteLibrary.CapsuleOutline(AntAppendageSizePx), PrimitiveSpriteLibrary.CapsuleFill(AntAppendageSizePx), appendageFill, new Vector3(x, 0.24f, 0f), new Vector3(0.28f, 0.05f, 1f), legAngles[i], darkOutline);
            AddOutlinedPart(root, $"LegBottom_{i}", PrimitiveSpriteLibrary.CapsuleOutline(AntAppendageSizePx), PrimitiveSpriteLibrary.CapsuleFill(AntAppendageSizePx), appendageFill, new Vector3(x, -0.24f, 0f), new Vector3(0.28f, 0.05f, 1f), -legAngles[i], darkOutline);
        }

        AddOutlinedPart(root, "AntennaTop", PrimitiveSpriteLibrary.CapsuleOutline(AntAppendageSizePx), PrimitiveSpriteLibrary.CapsuleFill(AntAppendageSizePx), appendageFill, new Vector3(0.55f, 0.09f, 0f), new Vector3(0.20f, 0.024f, 1f), 35f, darkOutline);
        AddOutlinedPart(root, "AntennaBottom", PrimitiveSpriteLibrary.CapsuleOutline(AntAppendageSizePx), PrimitiveSpriteLibrary.CapsuleFill(AntAppendageSizePx), appendageFill, new Vector3(0.55f, -0.09f, 0f), new Vector3(0.20f, 0.024f, 1f), -35f, darkOutline);

        if (role == AntRole.Queen)
        {
            var wingTint = new Color(0.95f, 0.97f, 1f, 0.15f);
            AddPart(root, "WingTop", PrimitiveSpriteLibrary.CircleFill(AntSegmentSizePx), wingTint, new Vector3(-0.02f, 0.22f, 0f), new Vector3(0.52f, 0.24f, 1f), -1, 18f);
            AddPart(root, "WingBottom", PrimitiveSpriteLibrary.CircleFill(AntSegmentSizePx), wingTint, new Vector3(-0.02f, -0.22f, 0f), new Vector3(0.52f, 0.24f, 1f), -1, -18f);
        }

        if (role == AntRole.Warrior)
        {
            var mandibleTint = bodyTint * 0.4f;
            AddOutlinedPart(root, "MandibleTop", PrimitiveSpriteLibrary.RoundedRectOutline(AntAppendageSizePx), PrimitiveSpriteLibrary.RoundedRectFill(AntAppendageSizePx), mandibleTint, new Vector3(0.64f, 0.07f, 0f), new Vector3(0.08f, 0.04f, 1f), 22f, darkOutline);
            AddOutlinedPart(root, "MandibleBottom", PrimitiveSpriteLibrary.RoundedRectOutline(AntAppendageSizePx), PrimitiveSpriteLibrary.RoundedRectFill(AntAppendageSizePx), mandibleTint, new Vector3(0.64f, -0.07f, 0f), new Vector3(0.08f, 0.04f, 1f), -22f, darkOutline);
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
        RenderOrder.Apply(renderer, RenderOrder.EntityBody + sortingOrder);
    }

    private static Color ColorFromIdentity(EntityIdentity identity, float minChannel, float maxChannel, uint salt)
    {
        var hash = HashIdentity(identity, salt);
        return new Color(
            Mathf.Lerp(minChannel, maxChannel, ((hash >> 0) & 0xFF) / 255f),
            Mathf.Lerp(minChannel, maxChannel, ((hash >> 8) & 0xFF) / 255f),
            Mathf.Lerp(minChannel, maxChannel, ((hash >> 16) & 0xFF) / 255f),
            1f);
    }

    private static uint HashIdentity(EntityIdentity identity, uint salt)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)identity.entityId) * 16777619u;
            hash = (hash ^ (uint)identity.teamId) * 16777619u;
            hash = (hash ^ (uint)identity.variant) * 16777619u;
            hash = (hash ^ (uint)identity.appearanceSeed) * 16777619u;
            hash = (hash ^ (uint)identity.status) * 16777619u;
            hash = (hash ^ salt) * 16777619u;
            return hash;
        }
    }
}
