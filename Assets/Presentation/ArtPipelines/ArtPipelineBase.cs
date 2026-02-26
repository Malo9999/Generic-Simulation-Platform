using System.Collections.Generic;
using UnityEngine;

public enum DebugPlaceholderMode
{
    Overlay,
    Replace
}

public abstract class ArtPipelineBase : ScriptableObject, IArtPipeline
{
    public abstract ArtMode Mode { get; }
    public abstract string DisplayName { get; }

    public abstract bool IsAvailable(ArtManifest manifest);
    public abstract List<string> MissingRequirements(ArtManifest manifest);
    public abstract GameObject CreateRenderer(VisualKey key, Transform parent);
    public virtual void ConfigureDebug(bool enabled, DebugPlaceholderMode mode) { }
    public abstract void ApplyVisual(GameObject renderer, VisualKey key, Vector2 velocity, float deltaTime);
}
