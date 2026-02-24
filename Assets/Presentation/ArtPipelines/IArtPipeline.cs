using System.Collections.Generic;
using UnityEngine;

public interface IArtPipeline
{
    ArtMode Mode { get; }
    string DisplayName { get; }
    bool IsAvailable(ArtManifest manifest);
    List<string> MissingRequirements(ArtManifest manifest);
    GameObject CreateRenderer(VisualKey key, Transform parent);
    void ApplyVisual(GameObject renderer, VisualKey key, Vector2 velocity, float deltaTime);
}
