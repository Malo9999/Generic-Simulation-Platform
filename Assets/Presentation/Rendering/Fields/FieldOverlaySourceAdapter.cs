using UnityEngine;

public sealed class FieldOverlaySourceAdapter : MonoBehaviour
{
    [SerializeField] private FieldBufferController target;
    [SerializeField] private MonoBehaviour sourceBehaviour;
    [SerializeField] private bool clearBeforeWrite = true;
    [SerializeField, Min(0.02f)] private float pollInterval = 0.15f;

    private IFieldOverlaySource source;
    private float elapsed;

    private void Awake()
    {
        source = sourceBehaviour as IFieldOverlaySource;
    }

    private void OnValidate()
    {
        source = sourceBehaviour as IFieldOverlaySource;
    }

    private void Update()
    {
        if (target == null || source == null)
        {
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed < pollInterval)
        {
            return;
        }

        elapsed = 0f;

        if (source.WorldBounds.size.sqrMagnitude > 0.001f)
        {
            var worldBounds = source.WorldBounds;
            target.SetWorldBounds(new Rect(worldBounds.min.x, worldBounds.min.y, worldBounds.size.x, worldBounds.size.y));
        }

        if (clearBeforeWrite)
        {
            target.Clear();
        }

        source.WriteTo(target);
    }
}
