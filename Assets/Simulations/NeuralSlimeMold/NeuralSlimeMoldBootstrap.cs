using UnityEngine;

public sealed class NeuralSlimeMoldBootstrap : MonoBehaviour
{
    [Header("Simulation")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private int seed = 12345;
    [SerializeField, Min(1)] private int agentCount = 1800;
    [SerializeField] private Vector2 mapSize = new(64f, 64f);
    [SerializeField] private Vector2Int trailResolution = new(256, 256);
    [SerializeField, Min(0f)] private float trailDecayPerSecond = 0.6f;
    [SerializeField, Range(0f, 1f)] private float trailDiffusion = 0.23f;

    [Header("Agent Motion")]
    [SerializeField] private float sensorAngleDegrees = 35f;
    [SerializeField] private float sensorDistance = 1.8f;
    [SerializeField] private float speed = 6.7f;
    [SerializeField] private float turnRateDegrees = 180f;
    [SerializeField, Min(0f)] private float depositAmount = 1.2f;

    [Header("Palette")]
    [SerializeField] private bool useGlowAgentShape = true;
    [SerializeField] private bool useFieldBlobOverlay = true;

    [Header("Boundary")]
    [SerializeField, Tooltip("Requires Start / Reset Simulation to apply.")] private NeuralSlimeMoldRunner.BoundaryMode boundaryMode = NeuralSlimeMoldRunner.BoundaryMode.SoftWall;
    [SerializeField, Min(0f), Tooltip("Requires Start / Reset Simulation to apply.")] private float wallMargin = 6f;

    [Header("Food Nodes")]
    [SerializeField, Tooltip("Requires Start / Reset Simulation to apply.")] private bool enableFoodNodes = true;
    [SerializeField, Tooltip("Can be toggled during play.")] private bool indirectFoodBias = true;
    [SerializeField, Min(1), Tooltip("Requires Start / Reset Simulation to apply when auto-generated nodes are used.")] private int foodNodeCount = 4;
    [SerializeField, Min(0f), Tooltip("Can be adjusted live.")] private float foodStrength = 0.9f;
    [SerializeField, Min(0.1f), Tooltip("Requires Start / Reset Simulation to apply.")] private float foodRadius = 14f;
    [SerializeField, Tooltip("When enabled, food node placement is deterministic from seed.")] private bool spawnFromSeed = true;
    [SerializeField, Tooltip("Optional explicit node positions; leave empty to auto-generate.")] private Vector2[] manualFoodNodes = null;

    [Header("Camera")]
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

        runner.Tick(Time.deltaTime, trailDiffusion, trailDecayPerSecond, indirectFoodBias, foodStrength);
        rendererComponent.Render(runner);
    }

    [ContextMenu("Start / Reset Simulation")]
    public void StartSimulation()
    {
        var normalizedSeed = seed;
        var turnRateRadians = turnRateDegrees * Mathf.Deg2Rad;
        var sensorAngleRadians = sensorAngleDegrees * Mathf.Deg2Rad;

        runner.ResetWithSeed(
            normalizedSeed,
            agentCount,
            trailResolution,
            mapSize,
            speed,
            turnRateRadians,
            sensorAngleRadians,
            sensorDistance,
            depositAmount,
            boundaryMode,
            wallMargin,
            enableFoodNodes,
            foodNodeCount,
            foodRadius,
            spawnFromSeed,
            manualFoodNodes);

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

    private void FrameCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        cam.orthographic = true;
        var halfW = mapSize.x * 0.5f;
        var halfH = mapSize.y * 0.5f;
        var sizeFromHeight = halfH;
        var sizeFromWidth = halfW / Mathf.Max(0.01f, cam.aspect);
        cam.orthographicSize = Mathf.Max(sizeFromHeight, sizeFromWidth, 6f) * Mathf.Max(1f, cameraPadding);
        var pos = cam.transform.position;
        cam.transform.position = new Vector3(0f, 0f, pos.z);

        var arenaCameraPolicy = MainCameraRuntimeSetup.EnsureArenaCameraRig(cam);
        if (arenaCameraPolicy != null)
        {
            arenaCameraPolicy.SetWorldSizeAndRefresh(mapSize);
        }

        var followController = cam.GetComponent<CameraFollowController>();
        if (followController != null)
        {
            followController.enabled = false;
        }
    }

    private void ApplyRendererOverrides()
    {
        if (rendererComponent == null)
        {
            return;
        }

        rendererComponent.SetShapeToggles(useGlowAgentShape, useFieldBlobOverlay);
    }
}
