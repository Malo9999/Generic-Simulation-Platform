using UnityEngine;

public sealed class NeuralSlimeMoldBootstrap : MonoBehaviour
{
    [Header("Simulation")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private int seed = 12345;
    [SerializeField, Min(1)] private int agentCount = 600;
    [SerializeField] private Vector2 mapSize = new(64f, 64f);
    [SerializeField] private Vector2Int trailResolution = new(256, 256);
    [SerializeField, Min(0f)] private float trailDecayPerSecond = 0.6f;
    [SerializeField, Range(0f, 1f)] private float trailDiffusion = 0.23f;

    [Header("Agent Motion")]
    [SerializeField] private float sensorAngleDegrees = 35f;
    [SerializeField] private float sensorDistance = 1.4f;
    [SerializeField] private float speed = 6.7f;
    [SerializeField] private float turnRateDegrees = 180f;
    [SerializeField, Min(0f)] private float depositAmount = 1.2f;
    [SerializeField, Min(0f)] private float explorationTurnNoise = 0.08f;

    [Header("Food")]
    [SerializeField, Min(1)] private int foodNodeCount = 10;
    [SerializeField, Min(0f)] private float foodStrength = 1.1f;
    [SerializeField, Min(0f)] private float foodCapacity = 2000f;
    [SerializeField, Min(0f)] private float consumeRadius = 6f;
    [SerializeField, Min(0f)] private float consumeRate = 0.25f;
    [SerializeField] private bool allowFoodRegrowth = true;
    [SerializeField, Min(0f)] private float foodReactivationDelay = 10f;
    [SerializeField, Min(0f)] private float regrowRate = 0.08f;
    [SerializeField, Range(0f, 1f)] private float foodReactivationThreshold = 0.25f;
    [SerializeField] private bool spawnFromSeed = true;
    [SerializeField] private NeuralFoodNodeConfig[] manualFoodConfigs;

    [Header("Palette")]
    [SerializeField] private bool useGlowAgentShape = true;
    [SerializeField] private bool useFieldBlobOverlay = true;
    [SerializeField] private Color backgroundColor = new(0.01f, 0.02f, 0.04f, 1f);

    [Header("Rendering")]
    [SerializeField] private bool showFoodMarkers = true;
    [SerializeField] private bool autoFrameCamera = true;
    [SerializeField] private float cameraPadding = 1.1f;

    private NeuralSlimeMoldRunner runner;
    private NeuralSlimeMoldRenderer rendererComponent;
    private bool hasStarted;

    private void Awake()
    {
        runner = new NeuralSlimeMoldRunner();
        rendererComponent = GetComponent<NeuralSlimeMoldRenderer>();
        if (rendererComponent == null)
        {
            rendererComponent = gameObject.AddComponent<NeuralSlimeMoldRenderer>();
        }

        ApplyRendererOverrides();
    }

    private void Start()
    {
        if (autoStart)
        {
            StartSimulation();
        }
    }

    private void Update()
    {
        if (!hasStarted)
        {
            return;
        }

        runner.Tick(Time.deltaTime, trailDiffusion, trailDecayPerSecond, foodStrength, explorationTurnNoise);
        rendererComponent.Render(runner);
    }

    [ContextMenu("Start / Reset Simulation")]
    public void StartSimulation()
    {
        var turnRateRadians = turnRateDegrees * Mathf.Deg2Rad;
        var sensorAngleRadians = sensorAngleDegrees * Mathf.Deg2Rad;

        runner.ResetWithSeed(
            seed,
            agentCount,
            trailResolution,
            mapSize,
            speed,
            turnRateRadians,
            sensorAngleRadians,
            sensorDistance,
            depositAmount,
            foodNodeCount,
            foodStrength,
            foodCapacity,
            consumeRadius,
            consumeRate,
            allowFoodRegrowth,
            foodReactivationDelay,
            regrowRate,
            foodReactivationThreshold,
            spawnFromSeed,
            manualFoodConfigs);

        ApplyRendererOverrides();
        rendererComponent.Build(runner);

        if (autoFrameCamera)
        {
            FrameCamera();
        }

        hasStarted = true;
    }

    [ContextMenu("Reseed")]
    public void Reseed()
    {
        seed = StableHashUtility.CombineSeed(seed, "neural-slime-next");
        StartSimulation();
    }

    private void ApplyRendererOverrides()
    {
        if (rendererComponent == null)
        {
            return;
        }

        rendererComponent.SetShapeToggles(useGlowAgentShape, useFieldBlobOverlay);
        rendererComponent.SetFoodDebugVisuals(showFoodMarkers);
        rendererComponent.SetFoodInfluenceDebugVisuals(false);
        rendererComponent.SetBackgroundColor(backgroundColor);
    }

    private void FrameCamera()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            return;
        }

        var halfHeight = mapSize.y * 0.5f;
        var halfWidth = mapSize.x * 0.5f;
        var aspect = Mathf.Max(0.1f, cam.aspect);
        var orthoFromWidth = halfWidth / aspect;
        var target = Mathf.Max(halfHeight, orthoFromWidth) * Mathf.Max(0.1f, cameraPadding);
        cam.orthographicSize = target;
        cam.transform.position = new Vector3(0f, 0f, cam.transform.position.z);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.8f, 0.4f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(mapSize.x, mapSize.y, 0f));

        if (manualFoodConfigs == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.8f, 0.25f, 0.45f);
        for (var i = 0; i < manualFoodConfigs.Length; i++)
        {
            var node = manualFoodConfigs[i];
            Gizmos.DrawWireSphere(
                new Vector3(node.position.x, node.position.y, 0f),
                Mathf.Max(0.01f, node.consumeRadius));
        }
    }
}