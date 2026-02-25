using UnityEngine;

public class SelectionHaloTwinkle : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private float twinkleInterval = 0.25f;
    [SerializeField] private float brightAlpha = 1f;
    [SerializeField] private float dimAlpha = 0.4f;

    private float elapsed;
    private bool dimmed;

    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        ApplyAlpha(brightAlpha);
    }

    private void Update()
    {
        if (targetRenderer == null)
        {
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed < twinkleInterval)
        {
            return;
        }

        elapsed -= twinkleInterval;
        dimmed = !dimmed;
        ApplyAlpha(dimmed ? dimAlpha : brightAlpha);
    }

    private void ApplyAlpha(float alpha)
    {
        var color = targetRenderer.color;
        color.a = alpha;
        targetRenderer.color = color;
    }
}
