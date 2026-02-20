using UnityEngine;

public class RaceCarRunner : MonoBehaviour, ITickableSimulationRunner
{
    private const int DotCount = 12;

    private Transform[] dots;
    private Vector2[] positions;
    private Vector2[] velocities;
    private float halfWidth = 32f;
    private float halfHeight = 32f;

    public void Initialize(ScenarioConfig config)
    {
        EnsureMainCamera();
        BuildDots(config);
        Debug.Log($"{nameof(RaceCarRunner)} Initialize seed={config.seed}, scenario={config.scenarioName}");
    }

    public void Tick(int tickIndex, float dt)
    {
        if (dots == null)
        {
            return;
        }

        for (var i = 0; i < dots.Length; i++)
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

            dots[i].localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
        }
    }

    public void Shutdown()
    {
        if (dots != null)
        {
            for (var i = 0; i < dots.Length; i++)
            {
                if (dots[i] != null)
                {
                    Destroy(dots[i].gameObject);
                }
            }
        }

        dots = null;
        positions = null;
        velocities = null;
        Debug.Log("RaceCarRunner Shutdown");
    }

    private void BuildDots(ScenarioConfig config)
    {
        Shutdown();

        halfWidth = Mathf.Max(1f, (config?.world?.arenaWidth ?? 64) * 0.5f);
        halfHeight = Mathf.Max(1f, (config?.world?.arenaHeight ?? 64) * 0.5f);

        dots = new Transform[DotCount];
        positions = new Vector2[DotCount];
        velocities = new Vector2[DotCount];

        var sprite = CreateDotSprite();

        for (var i = 0; i < DotCount; i++)
        {
            var dot = new GameObject($"Dot_{i}");
            dot.transform.SetParent(transform, false);

            var renderer = dot.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(RngService.Global.Value(), RngService.Global.Value(), RngService.Global.Value());

            var startX = RngService.Global.Range(-halfWidth, halfWidth);
            var startY = RngService.Global.Range(-halfHeight, halfHeight);
            var speed = RngService.Global.Range(5f, 14f);
            var angle = RngService.Global.Range(0f, Mathf.PI * 2f);

            positions[i] = new Vector2(startX, startY);
            velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            dot.transform.localPosition = new Vector3(startX, startY, 0f);
            dot.transform.localScale = Vector3.one * RngService.Global.Range(0.8f, 1.8f);
            dots[i] = dot.transform;
        }
    }

    private static Sprite CreateDotSprite()
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
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
