using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Presentation/Art Pipelines/Iso L2", fileName = "IsoL2Pipeline")]
public class IsoL2Pipeline : ArtPipelineBase
{
    private static readonly List<string> Required = new() { "iso2.world.tileset", "iso2.agent.*" };

    public override ArtMode Mode => ArtMode.IsoL2;
    public override string DisplayName => "Iso L2";

    public override bool IsAvailable(ArtManifest manifest)
    {
        return false;
    }

    public override List<string> MissingRequirements(ArtManifest manifest)
    {
        return new List<string>(Required);
    }

    public override GameObject CreateRenderer(VisualKey key, Transform parent)
    {
        GameObject rendererObject = new($"Renderer_{key.entityId}");
        rendererObject.transform.SetParent(parent, false);
        rendererObject.AddComponent<SpriteRenderer>();
        return rendererObject;
    }

    public override void ApplyVisual(GameObject renderer, VisualKey key, Vector2 velocity, float deltaTime)
    {
    }
}
