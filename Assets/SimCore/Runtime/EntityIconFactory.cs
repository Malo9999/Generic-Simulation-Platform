using UnityEngine;

public static class EntityIconFactory
{
    public static Transform CreateAntIcon(Transform parent, AntRole role, Color bodyTint, int sizePx = 64)
    {
        var root = CreateRoot(parent, "AntIcon");

        var thoraxScale = new Vector3(0.32f, 0.26f, 1f);
        var headScale = role == AntRole.Warrior ? new Vector3(0.34f, 0.3f, 1f) : new Vector3(0.25f, 0.22f, 1f);
        var abdomenScale = role == AntRole.Queen ? new Vector3(0.54f, 0.36f, 1f) : new Vector3(0.42f, 0.3f, 1f);

        CreateOutlinedPart(root, "Abdomen", PrimitiveSpriteLibrary.CircleFill(sizePx), PrimitiveSpriteLibrary.CircleOutline(sizePx), bodyTint, new Vector3(-0.28f, 0f, 0f), abdomenScale);
        CreateOutlinedPart(root, "Thorax", PrimitiveSpriteLibrary.CircleFill(sizePx), PrimitiveSpriteLibrary.CircleOutline(sizePx), bodyTint, new Vector3(0f, 0f, 0f), thoraxScale);
        var head = CreateOutlinedPart(root, "Head", PrimitiveSpriteLibrary.CircleFill(sizePx), PrimitiveSpriteLibrary.CircleOutline(sizePx), bodyTint, new Vector3(0.28f, 0f, 0f), headScale);

        var legColor = bodyTint * 0.55f;
        for (var i = 0; i < 3; i++)
        {
            var y = (i - 1) * 0.13f;
            CreateSimplePart(root, $"LegTop_{i}", PrimitiveSpriteLibrary.CapsuleFill(sizePx), legColor, new Vector3(-0.02f, y + 0.05f, 0f), new Vector3(0.42f, 0.05f, 1f), 2, 22f + (i * 6f));
            CreateSimplePart(root, $"LegBottom_{i}", PrimitiveSpriteLibrary.CapsuleFill(sizePx), legColor, new Vector3(-0.02f, y - 0.05f, 0f), new Vector3(0.42f, 0.05f, 1f), 2, -22f - (i * 6f));
        }

        CreateSimplePart(root, "AntennaTop", PrimitiveSpriteLibrary.CapsuleFill(sizePx), legColor, new Vector3(0.43f, 0.08f, 0f), new Vector3(0.25f, 0.04f, 1f), 2, 35f);
        CreateSimplePart(root, "AntennaBottom", PrimitiveSpriteLibrary.CapsuleFill(sizePx), legColor, new Vector3(0.43f, -0.08f, 0f), new Vector3(0.25f, 0.04f, 1f), 2, -35f);

        if (role == AntRole.Warrior)
        {
            var mandibleColor = bodyTint * 0.45f;
            CreateSimplePart(head, "MandibleTop", PrimitiveSpriteLibrary.RoundedRectFill(sizePx), mandibleColor, new Vector3(0.22f, 0.08f, 0f), new Vector3(0.12f, 0.05f, 1f), 2, 20f);
            CreateSimplePart(head, "MandibleBottom", PrimitiveSpriteLibrary.RoundedRectFill(sizePx), mandibleColor, new Vector3(0.22f, -0.08f, 0f), new Vector3(0.12f, 0.05f, 1f), 2, -20f);
            SetOutlineTint(root, bodyTint * 0.25f);
        }

        if (role == AntRole.Queen)
        {
            var wingColor = new Color(1f, 1f, 1f, 0.35f);
            CreateSimplePart(root, "WingTop", PrimitiveSpriteLibrary.CircleFill(sizePx), wingColor, new Vector3(-0.04f, 0.2f, 0f), new Vector3(0.5f, 0.22f, 1f), 0, 20f);
            CreateSimplePart(root, "WingBottom", PrimitiveSpriteLibrary.CircleFill(sizePx), wingColor, new Vector3(-0.04f, -0.2f, 0f), new Vector3(0.5f, 0.22f, 1f), 0, -20f);
        }

        return root;
    }

    public static Transform CreateCarIcon(Transform parent, CarLivery livery, Color bodyTint, Color liveryTint, int sizePx = 64)
    {
        var root = CreateRoot(parent, "CarIcon");

        CreateOutlinedPart(root, "Body", PrimitiveSpriteLibrary.RoundedRectFill(sizePx), PrimitiveSpriteLibrary.RoundedRectOutline(sizePx), bodyTint, Vector3.zero, new Vector3(0.86f, 0.42f, 1f));
        CreateOutlinedPart(root, "Cabin", PrimitiveSpriteLibrary.RoundedRectFill(sizePx), PrimitiveSpriteLibrary.RoundedRectOutline(sizePx), bodyTint * 0.9f, new Vector3(0.08f, 0f, 0f), new Vector3(0.36f, 0.25f, 1f));
        CreateSimplePart(root, "Windshield", PrimitiveSpriteLibrary.RoundedRectFill(sizePx), new Color(0.12f, 0.15f, 0.2f, 1f), new Vector3(0.16f, 0f, 0f), new Vector3(0.16f, 0.18f, 1f), 2);

        CreateSimplePart(root, "WheelTop", PrimitiveSpriteLibrary.CircleFill(sizePx), new Color(0.1f, 0.1f, 0.1f, 1f), new Vector3(-0.16f, 0.27f, 0f), new Vector3(0.2f, 0.2f, 1f), 2);
        CreateSimplePart(root, "WheelBottom", PrimitiveSpriteLibrary.CircleFill(sizePx), new Color(0.1f, 0.1f, 0.1f, 1f), new Vector3(-0.16f, -0.27f, 0f), new Vector3(0.2f, 0.2f, 1f), 2);

        switch (livery)
        {
            case CarLivery.CenterStripe:
                CreateSimplePart(root, "LiveryCenter", PrimitiveSpriteLibrary.CapsuleFill(sizePx), liveryTint, new Vector3(0f, 0f, 0f), new Vector3(0.78f, 0.09f, 1f), 2);
                break;
            case CarLivery.SideStripes:
                CreateSimplePart(root, "LiveryTop", PrimitiveSpriteLibrary.CapsuleFill(sizePx), liveryTint, new Vector3(0f, 0.12f, 0f), new Vector3(0.75f, 0.06f, 1f), 2);
                CreateSimplePart(root, "LiveryBottom", PrimitiveSpriteLibrary.CapsuleFill(sizePx), liveryTint, new Vector3(0f, -0.12f, 0f), new Vector3(0.75f, 0.06f, 1f), 2);
                break;
            case CarLivery.DiagonalStripe:
                CreateSimplePart(root, "LiveryDiagonal", PrimitiveSpriteLibrary.CapsuleFill(sizePx), liveryTint, new Vector3(0f, 0f, 0f), new Vector3(0.78f, 0.08f, 1f), 2, 18f);
                break;
            default:
                CreateSimplePart(root, "LiverySolid", PrimitiveSpriteLibrary.RoundedRectFill(sizePx), liveryTint * 0.3f, Vector3.zero, new Vector3(0.8f, 0.35f, 1f), 2);
                break;
        }

        return root;
    }

    public static Transform CreateMarbleIcon(Transform parent, MarbleStripe stripe, Color baseTint, Color stripeTint, int sizePx = 64)
    {
        var root = CreateRoot(parent, "MarbleIcon");

        CreateOutlinedPart(root, "Base", PrimitiveSpriteLibrary.CircleFill(sizePx), PrimitiveSpriteLibrary.CircleOutline(sizePx), baseTint, Vector3.zero, new Vector3(0.6f, 0.6f, 1f));
        CreateSimplePart(root, "Highlight", PrimitiveSpriteLibrary.CircleFill(sizePx), new Color(1f, 1f, 1f, 0.35f), new Vector3(-0.14f, 0.14f, 0f), new Vector3(0.2f, 0.2f, 1f), 2);

        var maskObject = new GameObject("StripeMask");
        maskObject.transform.SetParent(root, false);
        var maskRenderer = maskObject.AddComponent<SpriteRenderer>();
        maskRenderer.sprite = PrimitiveSpriteLibrary.CircleFill(sizePx);
        maskRenderer.enabled = false;
        var spriteMask = maskObject.AddComponent<SpriteMask>();
        spriteMask.sprite = PrimitiveSpriteLibrary.CircleFill(sizePx);

        if (stripe != MarbleStripe.None)
        {
            if (stripe == MarbleStripe.Single)
            {
                CreateMaskedStripe(root, spriteMask, sizePx, "StripeSingle", stripeTint, new Vector3(0f, 0f, 0f), new Vector3(0.14f, 0.58f, 1f), 0f);
            }
            else if (stripe == MarbleStripe.Double)
            {
                CreateMaskedStripe(root, spriteMask, sizePx, "StripeLeft", stripeTint, new Vector3(-0.1f, 0f, 0f), new Vector3(0.1f, 0.58f, 1f), 0f);
                CreateMaskedStripe(root, spriteMask, sizePx, "StripeRight", stripeTint, new Vector3(0.1f, 0f, 0f), new Vector3(0.1f, 0.58f, 1f), 0f);
            }
            else
            {
                CreateMaskedStripe(root, spriteMask, sizePx, "StripeDiagonal", stripeTint, Vector3.zero, new Vector3(0.14f, 0.72f, 1f), 38f);
            }
        }

        return root;
    }

    public static Transform CreateAthleteIcon(Transform parent, AthleteKit kit, Color jerseyTint, Color padsTint, int sizePx = 64)
    {
        var root = CreateRoot(parent, "AthleteIcon");

        CreateOutlinedPart(root, "Torso", PrimitiveSpriteLibrary.RoundedRectFill(sizePx), PrimitiveSpriteLibrary.RoundedRectOutline(sizePx), jerseyTint, new Vector3(0f, -0.03f, 0f), new Vector3(0.42f, 0.56f, 1f));
        CreateOutlinedPart(root, "Helmet", PrimitiveSpriteLibrary.CircleFill(sizePx), PrimitiveSpriteLibrary.CircleOutline(sizePx), jerseyTint * 0.9f, new Vector3(0.22f, 0f, 0f), new Vector3(0.24f, 0.24f, 1f));
        CreateSimplePart(root, "Visor", PrimitiveSpriteLibrary.RoundedRectFill(sizePx), new Color(0.08f, 0.08f, 0.1f, 1f), new Vector3(0.27f, -0.01f, 0f), new Vector3(0.08f, 0.09f, 1f), 2);

        var leftPadScale = new Vector3(0.22f, 0.2f, 1f);
        var rightPadScale = new Vector3(0.22f, 0.2f, 1f);
        if (kit == AthleteKit.Away)
        {
            leftPadScale = new Vector3(0.26f, 0.2f, 1f);
            rightPadScale = new Vector3(0.2f, 0.18f, 1f);
        }
        else if (kit == AthleteKit.Alt)
        {
            leftPadScale = new Vector3(0.24f, 0.22f, 1f);
            rightPadScale = new Vector3(0.24f, 0.22f, 1f);
        }

        CreateOutlinedPart(root, "LeftPad", PrimitiveSpriteLibrary.CircleFill(sizePx), PrimitiveSpriteLibrary.CircleOutline(sizePx), padsTint, new Vector3(-0.08f, 0.16f, 0f), leftPadScale);
        CreateOutlinedPart(root, "RightPad", PrimitiveSpriteLibrary.CircleFill(sizePx), PrimitiveSpriteLibrary.CircleOutline(sizePx), padsTint, new Vector3(0.1f, 0.16f, 0f), rightPadScale);

        if (kit != AthleteKit.Home)
        {
            CreateSimplePart(root, "NumberPatch", PrimitiveSpriteLibrary.RoundedRectFill(sizePx), Color.white, new Vector3(0f, -0.03f, 0f), new Vector3(0.08f, 0.1f, 1f), 2);
        }

        return root;
    }

    private static Transform CreateRoot(Transform parent, string name)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        return root;
    }

    private static Transform CreateOutlinedPart(Transform parent, string name, Sprite fillSprite, Sprite outlineSprite, Color tint, Vector3 localPosition, Vector3 localScale)
    {
        var part = new GameObject(name).transform;
        part.SetParent(parent, false);
        part.localPosition = localPosition;
        part.localScale = localScale;

        var outline = new GameObject("Outline");
        outline.transform.SetParent(part, false);
        var outlineRenderer = outline.AddComponent<SpriteRenderer>();
        outlineRenderer.sprite = outlineSprite;
        outlineRenderer.color = Color.white;
        outlineRenderer.sortingOrder = 0;

        var fill = new GameObject("Fill");
        fill.transform.SetParent(part, false);
        var fillRenderer = fill.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = fillSprite;
        fillRenderer.color = tint;
        fillRenderer.sortingOrder = 1;

        return part;
    }

    private static void CreateSimplePart(Transform parent, string name, Sprite sprite, Color tint, Vector3 localPosition, Vector3 localScale, int sortingOrder, float rotationZ = 0f)
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

    private static void CreateMaskedStripe(Transform parent, SpriteMask mask, int sizePx, string name, Color tint, Vector3 localPos, Vector3 localScale, float rotZ)
    {
        var stripe = new GameObject(name);
        stripe.transform.SetParent(parent, false);
        stripe.transform.localPosition = localPos;
        stripe.transform.localScale = localScale;
        stripe.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);

        var renderer = stripe.AddComponent<SpriteRenderer>();
        renderer.sprite = PrimitiveSpriteLibrary.CapsuleFill(sizePx);
        renderer.color = tint;
        renderer.sortingOrder = 2;
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        mask.frontSortingOrder = 3;
        mask.backSortingOrder = -1;
    }

    private static void SetOutlineTint(Transform root, Color outlineTint)
    {
        var renderers = root.GetComponentsInChildren<SpriteRenderer>();
        for (var i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].name == "Outline")
            {
                renderers[i].color = outlineTint;
            }
        }
    }
}
