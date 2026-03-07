using UnityEngine;

public interface IFieldOverlaySource
{
    Bounds WorldBounds { get; }
    void WriteTo(FieldBufferController target);
}
