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

    [Header("Colony Hub")]
    [SerializeField] private bool useColonyHub = true;
    [SerializeField] private Vector2 colonyHub = Vector2.zero;
    [SerializeField, Min(0.25f)] private float colonyHubRadius = 4.5f;
    [SerializeField, Min(0f)] private float returnToHubWeight = 1.35f;
    [SerializeField, Range(0f, 1f)] private float returnTrailBlend = 0.82f;
    [SerializeField, Min(0f)] private float returnDepositBoost = 2.8f;
    [SerializeField, Min(0f)] private float successfulReturnDepositBurst = 6.0f;
    [SerializeField, Min(0f)] private float hubInfluenceRadius = 9f;

    [Header("Loop Pruning")]
    [SerializeField, Min(0f)] private float nonUsefulLoopPruneStrength = 0.14f;
    [SerializeField, Min(0f)] private float nonUsefulLoopTrailThreshold = 0.13f;
    [SerializeField, Min(0f)] private float nonUsefulLoopCurvatureThreshold = 0.085f;

    [Header("Palette")]
    [SerializeField] private bool useGlowAgentShape = true;
    [SerializeField] private bool useFieldBlobOverlay = true;
    [SerializeField] private Color backgroundColor = new(0.10f, 0.09f, 0.07f, 1f);

    [Header("Rendering")]
    [SerializeField] private bool showFoodMarkers = true;

    [Header("Camera Framing")]
    [SerializeField] private bool autoFrameCamera = true;
    [SerializeField] private bool adaptiveCameraFraming = true;
    [SerializeField, Min(0.1f)] private float cameraPadding = 1.1f;
    [SerializeField, Min(0.01f)] private float cameraFollowSmooth = 3.5f;
    [SerializeField, Min(0.01f)] private float cameraZoomSmooth = 2.8f;
    [SerializeField, Min(1f)] private float minimumCameraSize = 8f;
    [SerializeField, Range(0f, 1f)] private float cameraLookAheadToActivity = 0.12f;
    [SerializeField, Min(0f)] private float cameraDeadZoneRadius = 0.75f;

    private NeuralSlimeMoldRunner runner;
    private NeuralSlimeMoldRenderer rendererComponent;
    private bool hasStarted;

    private static readonly Color MoldBackgroundPreset = new(0.10f, 0.09f, 0.07f, 1f);

    private void Awake()
    {
        runner = new NeuralSlimeMoldRunner();
        rendererComponent = GetComponent<NeuralSlimeMoldRenderer>();
        if (rendererComponent == null)
        {
            rendererComponent = gameObject.AddComponent<NeuralSlimeMoldRenderer>();
        }

        ApplyRendererOverrides();
        ApplyCameraBackground();
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

        if (autoFrameCamera && adaptiveCameraFraming)
        {
            UpdateAdaptiveCamera(Time.deltaTime);
        }
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
            manualFoodConfigs,
            useColonyHub,
            colonyHub,
            colonyHubRadius,
            returnToHubWeight,
            returnTrailBlend,
            returnDepositBoost,
            successfulReturnDepositBurst,
            hubInfluenceRadius,
            nonUsefulLoopPruneStrength,
            nonUsefulLoopTrailThreshold,
            nonUsefulLoopCurvatureThreshold);

        ApplyRendererOverrides();
        ApplyCameraBackground();
        rendererComponent.Build(runner);

        if (autoFrameCamera)
        {
            FrameCameraImmediate();
        }

        hasStarted = true;
    }

    [ContextMenu("Reseed")]
    public void Reseed()
    {
        seed = StableHashUtility.CombineSeed(seed, "neural-slime-next");
        StartSimulation();
    }

    [ContextMenu("Apply Mold Palette Preset")]
    public void ApplyMoldPalettePreset()
    {
        backgroundColor = MoldBackgroundPreset;
        ApplyRendererOverrides();
        ApplyCameraBackground();
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

    private void ApplyCameraBackground()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        cam.backgroundColor = backgroundColor;
    }

    private void FrameCameraImmediate()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            return;
        }

        var worldTargetSize = ComputeFullMapCameraSize(cam);
        var activityTargetSize = ComputeActivityCameraSize(cam);
        var targetSize = adaptiveCameraFraming
            ? Mathf.Max(minimumCameraSize, Mathf.Min(worldTargetSize, activityTargetSize))
            : Mathf.Max(minimumCameraSize, worldTargetSize);

        cam.orthographicSize = targetSize;

        var targetCenter = adaptiveCameraFraming ? runner.ActivityCenter : Vector2.zero;
        cam.transform.position = new Vector3(targetCenter.x, targetCenter.y, cam.transform.position.z);
    }

    private void UpdateAdaptiveCamera(float dt)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic)
        {
            return;
        }

        var targetCenter = runner.ActivityCenter;
        var current2D = new Vector2(cam.transform.position.x, cam.transform.position.y);
        var delta = targetCenter - current2D;

        if (delta.magnitude > cameraDeadZoneRadius)
        {
            var lookAheadCenter = Vector2.Lerp(current2D, targetCenter, 1f + cameraLookAheadToActivity);
            var smoothed = Vector2.Lerp(
                current2D,
                lookAheadCenter,
                1f - Mathf.Exp(-cameraFollowSmooth * Mathf.Max(0f, dt)));
            cam.transform.position = new Vector3(smoothed.x, smoothed.y, cam.transform.position.z);
        }

        var worldTargetSize = ComputeFullMapCameraSize(cam);
        var activityTargetSize = ComputeActivityCameraSize(cam);
        var targetSize = Mathf.Max(minimumCameraSize, Mathf.Min(worldTargetSize, activityTargetSize));
        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            targetSize,
            1f - Mathf.Exp(-cameraZoomSmooth * Mathf.Max(0f, dt)));
    }

    private float ComputeFullMapCameraSize(Camera cam)
    {
        var halfHeight = mapSize.y * 0.5f;
        var halfWidth = mapSize.x * 0.5f;
        var aspect = Mathf.Max(0.1f, cam.aspect);
        var orthoFromWidth = halfWidth / aspect;
        return Mathf.Max(halfHeight, orthoFromWidth) * Mathf.Max(0.1f, cameraPadding);
    }

    private float ComputeActivityCameraSize(Camera cam)
    {
        var aspect = Mathf.Max(0.1f, cam.aspect);
        var radius = Mathf.Max(1f, runner != null ? runner.ActivityRadius : 1f);
        var paddedRadius = radius * Mathf.Max(0.1f, cameraPadding);
        var vertical = paddedRadius;
        var horizontal = paddedRadius / aspect;
        return Mathf.Max(vertical, horizontal);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.8f, 0.4f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(mapSize.x, mapSize.y, 0f));

        if (manualFoodConfigs != null)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.25f, 0.45f);
            for (var i = 0; i < manualFoodConfigs.Length; i++)
            {
                var node = manualFoodConfigs[i];
                Gizmos.DrawWireSphere(
                    new Vector3(node.position.x, node.position.y, 0f),
                    Mathf.Max(0.01f, node.consumeRadius));
            }
        }

        if (useColonyHub)
        {
            Gizmos.color = new Color(0.35f, 0.95f, 1f, 0.5f);
            Gizmos.DrawWireSphere(new Vector3(colonyHub.x, colonyHub.y, 0f), colonyHubRadius);

            Gizmos.color = new Color(0.35f, 0.95f, 1f, 0.15f);
            Gizmos.DrawWireSphere(new Vector3(colonyHub.x, colonyHub.y, 0f), Mathf.Max(colonyHubRadius, hubInfluenceRadius));

            Gizmos.color = new Color(0.75f, 1f, 1f, 0.8f);
            Gizmos.DrawSphere(new Vector3(colonyHub.x, colonyHub.y, 0f), 0.25f);
        }

        if (UnityEngine.Application.isPlaying && runner != null)
        {
            Gizmos.color = new Color(0.4f, 1f, 0.9f, 0.35f);
            Gizmos.DrawWireSphere(new Vector3(runner.ActivityCenter.x, runner.ActivityCenter.y, 0f), runner.ActivityRadius);

            Gizmos.color = new Color(0.8f, 1f, 1f, 0.7f);
            Gizmos.DrawSphere(new Vector3(runner.ActivityCenter.x, runner.ActivityCenter.y, 0f), 0.35f);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        mapSize.x = Mathf.Max(8f, mapSize.x);
        mapSize.y = Mathf.Max(8f, mapSize.y);

        trailResolution.x = Mathf.Max(16, trailResolution.x);
        trailResolution.y = Mathf.Max(16, trailResolution.y);

        agentCount = Mathf.Max(1, agentCount);
        foodNodeCount = Mathf.Max(1, foodNodeCount);

        sensorDistance = Mathf.Max(0f, sensorDistance);
        speed = Mathf.Max(0f, speed);
        depositAmount = Mathf.Max(0f, depositAmount);
        explorationTurnNoise = Mathf.Max(0f, explorationTurnNoise);

        foodStrength = Mathf.Max(0f, foodStrength);
        foodCapacity = Mathf.Max(0f, foodCapacity);
        consumeRadius = Mathf.Max(0f, consumeRadius);
        consumeRate = Mathf.Max(0f, consumeRate);
        foodReactivationDelay = Mathf.Max(0f, foodReactivationDelay);
        regrowRate = Mathf.Max(0f, regrowRate);

        colonyHubRadius = Mathf.Max(0.25f, colonyHubRadius);
        returnToHubWeight = Mathf.Max(0f, returnToHubWeight);
        returnTrailBlend = Mathf.Clamp01(returnTrailBlend);
        returnDepositBoost = Mathf.Max(0f, returnDepositBoost);
        successfulReturnDepositBurst = Mathf.Max(0f, successfulReturnDepositBurst);
        hubInfluenceRadius = Mathf.Max(colonyHubRadius, hubInfluenceRadius);

        nonUsefulLoopPruneStrength = Mathf.Max(0f, nonUsefulLoopPruneStrength);
        nonUsefulLoopTrailThreshold = Mathf.Max(0f, nonUsefulLoopTrailThreshold);
        nonUsefulLoopCurvatureThreshold = Mathf.Max(0f, nonUsefulLoopCurvatureThreshold);

        cameraPadding = Mathf.Max(0.1f, cameraPadding);
        cameraFollowSmooth = Mathf.Max(0.01f, cameraFollowSmooth);
        cameraZoomSmooth = Mathf.Max(0.01f, cameraZoomSmooth);
        minimumCameraSize = Mathf.Max(1f, minimumCameraSize);
        cameraDeadZoneRadius = Mathf.Max(0f, cameraDeadZoneRadius);

        if (rendererComponent == null)
        {
            rendererComponent = GetComponent<NeuralSlimeMoldRenderer>();
        }

        ApplyRendererOverrides();
        ApplyCameraBackground();
    }
#endif
}