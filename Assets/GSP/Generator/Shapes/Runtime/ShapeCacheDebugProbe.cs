using UnityEngine;

public class ShapeCacheDebugProbe : MonoBehaviour
{
    [SerializeField] private string shapeId = ShapeId.DotCore;

    private void Start()
    {
        var sprite = RuntimeShapeCache.Get(shapeId);
        Debug.Log(sprite != null
            ? $"[ShapeCacheDebugProbe] Loaded shape '{shapeId}' ({sprite.texture.width}x{sprite.texture.height})."
            : $"[ShapeCacheDebugProbe] Failed to load shape '{shapeId}'.");
    }
}
