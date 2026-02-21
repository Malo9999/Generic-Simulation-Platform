using UnityEngine;

public class RaceCarRunner : MonoBehaviour, ITickableSimulationRunner
{
    private const int CarCount = 10;
    private const int SpawnDebugCount = 5;

    [SerializeField] private bool logSpawnIdentity = true;

    private Transform[] cars;
    private EntityIdentity[] identities;
    private Vector2[] positions;
    private Vector2[] velocities;
    private float[] laneTargets;
    private int nextEntityId;
    private float halfWidth = 32f;
    private float halfHeight = 32f;

    public void Initialize(ScenarioConfig config)
    {
        EnsureMainCamera();
        BuildCars(config);
        Debug.Log($"{nameof(RaceCarRunner)} Initialize seed={config.seed}, scenario={config.scenarioName}");
    }

    public void Tick(int tickIndex, float dt)
    {
        if (cars == null)
        {
            return;
        }

        for (var i = 0; i < cars.Length; i++)
        {
            positions[i].x += velocities[i].x * dt;

            if (positions[i].x < -halfWidth || positions[i].x > halfWidth)
            {
                positions[i].x = Mathf.Clamp(positions[i].x, -halfWidth, halfWidth);
                velocities[i].x *= -1f;
            }

            var targetY = laneTargets[i] + Mathf.Sin((tickIndex * 0.08f) + i) * 0.25f;
            positions[i].y = Mathf.MoveTowards(positions[i].y, targetY, dt * 2.5f);
            positions[i].y = Mathf.Clamp(positions[i].y, -halfHeight, halfHeight);

            cars[i].localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
            if (Mathf.Abs(velocities[i].x) > 0.0001f)
            {
                cars[i].right = new Vector2(Mathf.Sign(velocities[i].x), 0f);
            }
        }
    }

    public void Shutdown()
    {
        if (cars != null)
        {
            for (var i = 0; i < cars.Length; i++)
            {
                if (cars[i] != null)
                {
                    Destroy(cars[i].gameObject);
                }
            }
        }

        cars = null;
        identities = null;
        positions = null;
        velocities = null;
        laneTargets = null;
        Debug.Log("RaceCarRunner Shutdown");
    }

    private void BuildCars(ScenarioConfig config)
    {
        Shutdown();
        nextEntityId = 0;

        halfWidth = Mathf.Max(1f, (config?.world?.arenaWidth ?? 64) * 0.5f);
        halfHeight = Mathf.Max(1f, (config?.world?.arenaHeight ?? 64) * 0.5f);

        cars = new Transform[CarCount];
        identities = new EntityIdentity[CarCount];
        positions = new Vector2[CarCount];
        velocities = new Vector2[CarCount];
        laneTargets = new float[CarCount];

        for (var i = 0; i < CarCount; i++)
        {
            var car = new GameObject($"Car_{i}");
            car.transform.SetParent(transform, false);

            var identity = IdentityService.Create(
                entityId: nextEntityId++,
                teamId: i % 2,
                role: "car",
                variantCount: 4,
                scenarioSeed: config?.seed ?? 0,
                simIdOrSalt: "RaceCar");

            var iconRoot = new GameObject("IconRoot");
            iconRoot.transform.SetParent(car.transform, false);
            EntityIconFactory.BuildCar(iconRoot.transform, identity);

            var startX = RngService.Global.Range(-halfWidth, halfWidth);
            var lane = Mathf.Lerp(-halfHeight * 0.8f, halfHeight * 0.8f, (i + 0.5f) / CarCount);
            var jitterY = RngService.Global.Range(-0.35f, 0.35f);
            var speed = RngService.Global.Range(10f, 17f);
            if (RngService.Global.Value() < 0.5f)
            {
                speed *= -1f;
            }

            positions[i] = new Vector2(startX, lane + jitterY);
            velocities[i] = new Vector2(speed, 0f);
            laneTargets[i] = lane;

            car.transform.localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
            if (Mathf.Abs(speed) > 0.001f)
            {
                car.transform.right = new Vector2(Mathf.Sign(speed), 0f);
            }
            cars[i] = car.transform;
            identities[i] = identity;

            if (logSpawnIdentity && i < SpawnDebugCount)
            {
                Debug.Log($"{nameof(RaceCarRunner)} spawn[{i}] {identity}");
            }
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
