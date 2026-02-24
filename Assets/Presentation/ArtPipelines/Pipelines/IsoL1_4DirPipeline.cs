using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Presentation/Art Pipelines/Iso L1 4Dir", fileName = "IsoL1_4DirPipeline")]
public class IsoL1_4DirPipeline : ArtPipelineBase
{
    private const string Requirement = "iso4.agents";

    public override ArtMode Mode => ArtMode.IsoL1_4Dir;
    public override string DisplayName => "Iso L1 4Dir";

    public override bool IsAvailable(ArtManifest manifest)
    {
        return manifest != null && manifest.Has(Requirement);
    }

    public override List<string> MissingRequirements(ArtManifest manifest)
    {
        return IsAvailable(manifest) ? new List<string>() : new List<string> { Requirement };
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
