using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Presentation/Art Pipelines/Flat", fileName = "FlatArtPipeline")]
public class FlatArtPipeline : ArtPipelineBase
{
    public override ArtMode Mode => ArtMode.Flat;
    public override string DisplayName => "Flat";

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
