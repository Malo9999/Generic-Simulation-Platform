using UnityEngine;

public sealed class ScalarFieldOverlaySource : MonoBehaviour, IFieldOverlaySource
{
    [SerializeField] private Rect worldRect = new(-12f, -6.75f, 24f, 13.5f);
    [SerializeField] private bool normalize = true;

    private float[] values;
    private int width;
    private int height;

    public Bounds WorldBounds => new(worldRect.center, new Vector3(worldRect.width, worldRect.height, 0f));

    public void SetFieldSnapshot(float[] sourceValues, int sourceWidth, int sourceHeight)
    {
        values = sourceValues;
        width = Mathf.Max(1, sourceWidth);
        height = Mathf.Max(1, sourceHeight);
    }

    public void SetWorldRect(Rect rect)
    {
        if (rect.width <= 0.01f || rect.height <= 0.01f)
        {
            return;
        }

        worldRect = rect;
    }

    public void WriteTo(FieldBufferController target)
    {
        if (target == null || values == null || values.Length == 0)
        {
            return;
        }

        target.UploadFromScalarField(values, width, height, normalize);
    }
}
