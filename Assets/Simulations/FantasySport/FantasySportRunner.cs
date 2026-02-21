using UnityEngine;

public class FantasySportRunner : MonoBehaviour, ITickableSimulationRunner
{
    private const int AthleteCount = 10;
    private const int SpawnDebugCount = 5;

    [SerializeField] private bool logSpawnIdentity = true;

    private Transform[] athletes;
    private EntityIdentity[] identities;
    private Vector2[] positions;
    private Vector2[] velocities;
    private float halfWidth = 32f;
    private float halfHeight = 32f;
    private int nextEntityId;

    public void Initialize(ScenarioConfig config)
    {
        EnsureMainCamera();
        BuildAthletes(config);
        Debug.Log($"{nameof(FantasySportRunner)} Initialize seed={config.seed}, scenario={config.scenarioName}");
    }

    public void Tick(int tickIndex, float dt)
    {
        if (athletes == null)
        {
            return;
        }

        for (var i = 0; i < athletes.Length; i++)
        {
            if (tickIndex % 60 == 0)
            {
                var turn = RngService.Global.Range(-0.45f, 0.45f);
                var cos = Mathf.Cos(turn);
                var sin = Mathf.Sin(turn);
                var vx = velocities[i].x;
                var vy = velocities[i].y;
                velocities[i] = new Vector2((vx * cos) - (vy * sin), (vx * sin) + (vy * cos));
            }

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

            athletes[i].localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
            FaceVelocity(athletes[i], velocities[i]);
        }
    }

    public void Shutdown()
    {
        if (athletes != null)
        {
            for (var i = 0; i < athletes.Length; i++)
            {
                if (athletes[i] != null)
                {
                    Destroy(athletes[i].gameObject);
                }
            }
        }

        athletes = null;
        identities = null;
        positions = null;
        velocities = null;
        Debug.Log("FantasySportRunner Shutdown");
    }

    private void BuildAthletes(ScenarioConfig config)
    {
        Shutdown();
        nextEntityId = 0;

        halfWidth = Mathf.Max(1f, (config?.world?.arenaWidth ?? 64) * 0.5f);
        halfHeight = Mathf.Max(1f, (config?.world?.arenaHeight ?? 64) * 0.5f);

        athletes = new Transform[AthleteCount];
        identities = new EntityIdentity[AthleteCount];
        positions = new Vector2[AthleteCount];
        velocities = new Vector2[AthleteCount];

        for (var i = 0; i < AthleteCount; i++)
        {
            var athlete = new GameObject($"Athlete_{i}");
            athlete.transform.SetParent(transform, false);

            var identity = IdentityService.Create(
                entityId: nextEntityId++,
                teamId: i % 2,
                role: i < AthleteCount / 2 ? "offense" : "defense",
                variantCount: 3,
                scenarioSeed: config?.seed ?? 0,
                simIdOrSalt: "FantasySport");

            var iconRoot = new GameObject("IconRoot");
            iconRoot.transform.SetParent(athlete.transform, false);
            EntityIconFactory.BuildAthlete(iconRoot.transform, identity);

            var startX = RngService.Global.Range(-halfWidth, halfWidth);
            var startY = RngService.Global.Range(-halfHeight, halfHeight);
            var speed = RngService.Global.Range(3f, 7f);
            var angle = RngService.Global.Range(0f, Mathf.PI * 2f);

            positions[i] = new Vector2(startX, startY);
            velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            athlete.transform.localPosition = new Vector3(startX, startY, 0f);
            athlete.transform.localScale = Vector3.one * RngService.Global.Range(0.95f, 1.1f);
            FaceVelocity(athlete.transform, velocities[i]);
            athletes[i] = athlete.transform;
            identities[i] = identity;

            if (logSpawnIdentity && i < SpawnDebugCount)
            {
                Debug.Log($"{nameof(FantasySportRunner)} spawn[{i}] {identity}");
            }
        }
    }

    private static void FaceVelocity(Transform target, Vector2 velocity)
    {
        if (velocity.sqrMagnitude > 0.0001f)
        {
            var angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            target.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
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
