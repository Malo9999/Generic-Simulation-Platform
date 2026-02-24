using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Presentation/Art Pipelines/Iso L1 8Dir", fileName = "IsoL1_8DirPipeline")]
public class IsoL1_8DirPipeline : ArtPipelineBase
{
    private static readonly List<string> Required = new() { "iso8.agent.prey", "iso8.agent.predator" };

    public override ArtMode Mode => ArtMode.IsoL1_8Dir;
    public override string DisplayName => "Iso L1 8Dir";

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
