using UnityEngine;

public class MarbleRaceRunner : MonoBehaviour, ITickableSimulationRunner
{
    private const int MarbleCount = 12;

    private Transform[] marbles;
    private Vector2[] positions;
    private Vector2[] velocities;
    private float halfWidth = 32f;
    private float halfHeight = 32f;

    public void Initialize(ScenarioConfig config)
    {
        EnsureMainCamera();
        BuildMarbles(config);
        Debug.Log($"{nameof(MarbleRaceRunner)} Initialize seed={config.seed}, scenario={config.scenarioName}");
    }

    public void Tick(int tickIndex, float dt)
    {
        if (marbles == null)
        {
            return;
        }

        for (var i = 0; i < marbles.Length; i++)
        {
            positions[i] += velocities[i] * dt;

            if (positions[i].x < -halfWidth || positions[i].x > halfWidth)
            {
                positions[i].x = Mathf.Clamp(positions[i].x, -halfWidth, halfWidth);
                velocities[i].x *= -1f;
            }

            if (positions[i].y < -halfHeight || positions[i].y > halfHeight)
            {
                positions[i].y = Mathf.Clamp(positions[i].y, -halfHeight, halfHeight);
                velocities[i].y *= -1f;
            }

            marbles[i].localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
        }
    }

    public void Shutdown()
    {
        if (marbles != null)
        {
            for (var i = 0; i < marbles.Length; i++)
            {
                if (marbles[i] != null)
                {
                    Destroy(marbles[i].gameObject);
                }
            }
        }

        marbles = null;
        positions = null;
        velocities = null;
        Debug.Log("MarbleRaceRunner Shutdown");
    }

    private void BuildMarbles(ScenarioConfig config)
    {
        Shutdown();

        halfWidth = Mathf.Max(1f, (config?.world?.arenaWidth ?? 64) * 0.5f);
        halfHeight = Mathf.Max(1f, (config?.world?.arenaHeight ?? 64) * 0.5f);

        marbles = new Transform[MarbleCount];
        positions = new Vector2[MarbleCount];
        velocities = new Vector2[MarbleCount];

        var baseSprite = ProceduralSpriteLibrary.GetMarbleBase(64);

        for (var i = 0; i < MarbleCount; i++)
        {
            var marble = new GameObject($"Marble_{i}");
            marble.transform.SetParent(transform, false);

            var baseRenderer = marble.AddComponent<SpriteRenderer>();
            baseRenderer.sprite = baseSprite;
            baseRenderer.color = RandomBrightColor();

            var stripeGo = new GameObject("Stripe");
            stripeGo.transform.SetParent(marble.transform, false);

            var stripeRenderer = stripeGo.AddComponent<SpriteRenderer>();
            stripeRenderer.sprite = ProceduralSpriteLibrary.GetMarbleStripe((MarbleStripe)RngService.Global.Range(0, 4), 64);
            stripeRenderer.color = RandomContrastingColor(baseRenderer.color);
            stripeRenderer.sortingOrder = baseRenderer.sortingOrder + 1;

            var startX = RngService.Global.Range(-halfWidth, halfWidth);
            var startY = RngService.Global.Range(-halfHeight, halfHeight);
            var speed = RngService.Global.Range(5f, 14f);
            var angle = RngService.Global.Range(0f, Mathf.PI * 2f);

            positions[i] = new Vector2(startX, startY);
            velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            marble.transform.localPosition = new Vector3(startX, startY, 0f);
            marble.transform.localScale = Vector3.one;
            marbles[i] = marble.transform;
        }
    }

    private static Color RandomBrightColor()
    {
        return new Color(
            RngService.Global.Range(0.2f, 1f),
            RngService.Global.Range(0.2f, 1f),
            RngService.Global.Range(0.2f, 1f),
            1f);
    }

    private static Color RandomContrastingColor(Color baseColor)
    {
        var inv = new Color(1f - baseColor.r, 1f - baseColor.g, 1f - baseColor.b, 1f);
        var jitter = new Color(
            RngService.Global.Range(0f, 0.2f),
            RngService.Global.Range(0f, 0.2f),
            RngService.Global.Range(0f, 0.2f),
            0f);
        return (inv * 0.8f) + jitter;
    }

    private void EnsureMainCamera()
    {
        if (Camera.main != null)
        {
            return;
        }

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";

        var cameraComponent = cameraObject.AddComponent<Camera>();
        cameraComponent.orthographic = true;
        cameraComponent.orthographicSize = Mathf.Max(halfHeight + 2f, 10f);
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
    }
}
