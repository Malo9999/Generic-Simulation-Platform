using System;
using UnityEngine;

public static class AgentSpriteResolver
{
    public static bool TryResolve(string spriteId, out Sprite fill, out Sprite outline)
    {
        fill = null;
        outline = null;

        if (string.IsNullOrWhiteSpace(spriteId) || !spriteId.StartsWith("agent:", StringComparison.Ordinal))
        {
            return false;
        }

        var bootstrapper = UnityEngine.Object.FindFirstObjectByType<Bootstrapper>()
            ?? UnityEngine.Object.FindAnyObjectByType<Bootstrapper>();
        var visual = bootstrapper != null ? bootstrapper.GetCurrentVisualSettings() : null;
        var preferredPack = bootstrapper != null ? bootstrapper.CurrentPreferredAgentPack : null;

        if (preferredPack != null && preferredPack.TryGetSprite(spriteId, out var packSprite) && packSprite != null)
        {
            fill = packSprite;
            return true;
        }

        var shouldUsePrimitive = bootstrapper == null
            || bootstrapper.CurrentArtMode == ArtMode.Simple
            || bootstrapper.CurrentUsePlaceholders
            || visual == null
            || visual.usePrimitiveBaseline;

        if (!shouldUsePrimitive)
        {
            return false;
        }

        var sizePx = visual != null ? visual.agentSizePx : 64;
        var shape = visual != null ? visual.agentShape : BasicShapeKind.Circle;
        var useOutline = visual == null || visual.agentOutline;

        switch (shape)
        {
            case BasicShapeKind.Capsule:
                fill = PrimitiveSpriteLibrary.CapsuleFill(sizePx);
                outline = useOutline ? PrimitiveSpriteLibrary.CapsuleOutline(sizePx) : null;
                break;
            case BasicShapeKind.RoundedRect:
                fill = PrimitiveSpriteLibrary.RoundedRectFill(sizePx);
                outline = useOutline ? PrimitiveSpriteLibrary.RoundedRectOutline(sizePx) : null;
                break;
            default:
                fill = PrimitiveSpriteLibrary.CircleFill(sizePx);
                outline = useOutline ? PrimitiveSpriteLibrary.CircleOutline(sizePx) : null;
                break;
        }

        return fill != null;
    }
}
