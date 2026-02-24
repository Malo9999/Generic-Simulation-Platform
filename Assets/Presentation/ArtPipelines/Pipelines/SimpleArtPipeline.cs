using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Presentation/Art Pipelines/Simple", fileName = "SimpleArtPipeline")]
public class SimpleArtPipeline : ArtPipelineBase
{
    public override ArtMode Mode => ArtMode.Simple;
    public override string DisplayName => "Simple";

    public override bool IsAvailable(ArtManifest manifest)
    {
        return true;
    }

    public override List<string> MissingRequirements(ArtManifest manifest)
    {
        return new List<string>();
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
