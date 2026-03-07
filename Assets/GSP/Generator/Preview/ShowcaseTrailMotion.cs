using UnityEngine;

public sealed class ShowcaseTrailMotion : MonoBehaviour
{
    [SerializeField] private Vector2 amplitude = new(0.75f, 0.45f);
    [SerializeField] private Vector2 frequency = new(0.9f, 1.5f);
    [SerializeField] private float phaseOffset;

    private Vector3 basePosition;

    private void Awake()
    {
        basePosition = transform.localPosition;
    }

    public void Configure(int seed, float scale)
    {
        var random = new System.Random(seed);
        phaseOffset = (float)random.NextDouble() * Mathf.PI * 2f;
        amplitude *= Mathf.Max(0.25f, scale);
        frequency = new Vector2(
            frequency.x * Mathf.Lerp(0.8f, 1.35f, (float)random.NextDouble()),
            frequency.y * Mathf.Lerp(0.8f, 1.25f, (float)random.NextDouble()));
    }

    private void Update()
    {
        var t = Time.time + phaseOffset;
        var offset = new Vector3(
            Mathf.Sin(t * frequency.x) * amplitude.x,
            Mathf.Sin(t * frequency.y) * amplitude.y,
            0f);
        transform.localPosition = basePosition + offset;
    }
}
