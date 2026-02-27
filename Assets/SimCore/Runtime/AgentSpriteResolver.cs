using System;
using UnityEngine;

public static class AgentSpriteResolver
{
    public static bool TryResolve(string spriteId, out Sprite fill, out Sprite outline)
    {
        fill = null;
        outline = null;

        if (string.IsNullOrWhiteSpace(spriteId) || !spriteId.StartsWith("agent:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var visual = SimVisualSettingsService.CurrentForActiveSim();
        var preferredPack = visual != null ? visual.preferredAgentPack : null;

        if (preferredPack != null && preferredPack.TryGetSprite(spriteId, out var packSprite) && packSprite != null)
        {
            fill = packSprite;
            return true;
        }

        var shouldUsePrimitive = visual == null || visual.usePrimitiveBaseline;

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
