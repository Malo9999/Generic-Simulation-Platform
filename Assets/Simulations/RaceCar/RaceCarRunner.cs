using UnityEngine;
using System.Text;

using GSP.TrackEditor;
public class RaceCarRunner : MonoBehaviour, ITickableSimulationRunner
{
    private const int SpawnDebugCount = 5;

    [SerializeField] private bool logSpawnIdentity = true;
    [SerializeField] private TrackBakedData track;

    private Transform[] cars;
    private EntityIdentity[] identities;
    private Vector2[] positions;
    private Vector2[] velocities;
    private float[] laneTargets;
    private float[] lapS;
    private float[] speed;
    private float[] laneOffset;
    private ArtModeSelector artSelector;
    private ArtPipelineBase activePipeline;
    private GameObject[] pipelineRenderers;
    private VisualKey[] visualKeys;
    private int nextEntityId;
    private float halfWidth = 32f;
    private float halfHeight = 32f;
    private SimulationSceneGraph sceneGraph;
    private TrackRuntime trackRuntime;
    private Transform trackRoot;
    private bool trackRootAttempted;
    private bool boundsAppliedAfterStart;
    private bool runtimeTrackBannerLogged;
    [SerializeField] private bool ReverseDirection;
    private TrackPathSampler pathSampler;

    public void Initialize(ScenarioConfig config)
    {
        sceneGraph = SceneGraphUtil.PrepareRunner(transform, "RaceCar");
        trackRootAttempted = false;
        boundsAppliedAfterStart = false;
        runtimeTrackBannerLogged = false;
        SetupTrackRoot();
        EnsureMainCamera();
        BuildCars(config);
        Debug.Log($"{nameof(RaceCarRunner)} Initialize seed={config.seed}, scenario={config.scenarioName}");
    }

    public void Tick(int tickIndex, float dt)
    {
        if (track != null && trackRoot == null && !trackRootAttempted)
        {
            trackRootAttempted = true;
            SetupTrackRoot();
        }

        if (track != null && trackRoot != null && !boundsAppliedAfterStart)
        {
            boundsAppliedAfterStart = true;
            var bounds = ComputePaddedTrackBounds(track);
            PresentationBoundsSync.Apply(bounds);
        }

        if (cars == null)
        {
            return;
        }

        for (var i = 0; i < cars.Length; i++)
        {
            var car = cars[i];
            if (!car)
            {
                continue;
            }

            var oldPosition = positions[i];

            if (pathSampler != null)
            {
                var directionMultiplier = ReverseDirection ? -1f : 1f;
                lapS[i] += speed[i] * dt * directionMultiplier;

                var (pathPos, tangent) = pathSampler.SampleByDistance(lapS[i]);
                var travelTangent = directionMultiplier > 0f ? tangent : -tangent;
                var normal = new Vector2(-travelTangent.y, travelTangent.x);
                var desiredPos = pathPos + normal * laneOffset[i];

                positions[i] = desiredPos;
                velocities[i] = travelTangent * speed[i];
                car.localPosition = new Vector3(desiredPos.x, desiredPos.y, 0f);
                if (travelTangent.sqrMagnitude > 0.0001f)
                {
                    car.right = travelTangent;
                }
            }
            else
            {
                positions[i].x += velocities[i].x * dt;

                if (trackRuntime == null && (positions[i].x < -halfWidth || positions[i].x > halfWidth))
                {
                    positions[i].x = Mathf.Clamp(positions[i].x, -halfWidth, halfWidth);
                    velocities[i].x *= -1f;
                }

                var targetY = laneTargets[i] + Mathf.Sin((tickIndex * 0.08f) + i) * 0.25f;
                positions[i].y = Mathf.MoveTowards(positions[i].y, targetY, dt * 2.5f);
                if (trackRuntime == null)
                {
                    positions[i].y = Mathf.Clamp(positions[i].y, -halfHeight, halfHeight);
                }

                if (trackRuntime != null && trackRuntime.IsOffTrack(positions[i]))
                {
                    var clamped = trackRuntime.ClampToTrack(positions[i]);
                    var correction = clamped - positions[i];
                    positions[i] = clamped;
                    velocities[i] = Vector2.Reflect(velocities[i], correction.normalized);
                    velocities[i] *= 0.75f;
                }

                car.localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
                if (Mathf.Abs(velocities[i].x) > 0.0001f)
                {
                    car.right = new Vector2(Mathf.Sign(velocities[i].x), 0f);
                }
            }

            var pipelineRenderer = pipelineRenderers != null ? pipelineRenderers[i] : null;
            if (activePipeline != null && pipelineRenderer != null)
            {
                var velocity = (positions[i] - oldPosition) / Mathf.Max(0.0001f, dt);
                activePipeline.ApplyVisual(pipelineRenderer, visualKeys[i], velocity, dt);
            }
        }
    }

    public void Shutdown()
    {
        DestroyCarsOnly();

        if (trackRoot != null)
        {
            Destroy(trackRoot.gameObject);
            trackRoot = null;
        }

        trackRuntime = null;
        pathSampler = null;
        trackRootAttempted = false;
        Debug.Log("RaceCarRunner Shutdown");
    }

    private void DestroyCarsOnly()
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
        lapS = null;
        speed = null;
        laneOffset = null;
        pipelineRenderers = null;
        visualKeys = null;
    }

    private void BuildCars(ScenarioConfig config)
    {
        DestroyCarsOnly();
        nextEntityId = 0;

        halfWidth = Mathf.Max(1f, (config?.world?.arenaWidth ?? 64) * 0.5f);
        halfHeight = Mathf.Max(1f, (config?.world?.arenaHeight ?? 64) * 0.5f);

        var carCount = Mathf.Max(2, config?.raceCar?.carCount ?? 10);

        cars = new Transform[carCount];
        identities = new EntityIdentity[carCount];
        positions = new Vector2[carCount];
        velocities = new Vector2[carCount];
        laneTargets = new float[carCount];
        lapS = new float[carCount];
        speed = new float[carCount];
        laneOffset = new float[carCount];
        pipelineRenderers = new GameObject[carCount];
        visualKeys = new VisualKey[carCount];

        ResolveArtPipeline();

        var rng = RngService.Fork("SIM:RaceCar:SPAWN");

        pathSampler = null;
        if (track != null && track.mainCenterline != null && track.mainCenterline.Length >= 2)
        {
            pathSampler = new TrackPathSampler(track.mainCenterline);
        }

        for (var i = 0; i < carCount; i++)
        {
            var identity = IdentityService.Create(
                entityId: nextEntityId++,
                teamId: i % 2,
                role: "car",
                variantCount: 4,
                scenarioSeed: config?.seed ?? 0,
                simIdOrSalt: "RaceCar");

            var groupRoot = SceneGraphUtil.EnsureEntityGroup(sceneGraph.EntitiesRoot, identity.teamId);

            var car = new GameObject($"Sim_{identity.entityId:0000}");
            car.transform.SetParent(groupRoot, false);

            var visualKey = VisualKeyBuilder.Create(
                simulationId: "RaceCar",
                entityType: "car",
                instanceId: identity.entityId,
                kind: string.IsNullOrWhiteSpace(identity.role) ? "car" : identity.role,
                state: "drive",
                facingMode: FacingMode.Auto,
                groupId: identity.teamId);

            var visualParent = car.transform;
            if (activePipeline != null)
            {
                pipelineRenderers[i] = activePipeline.CreateRenderer(visualKey, car.transform);
                if (pipelineRenderers[i] != null)
                {
                    visualParent = pipelineRenderers[i].transform;
                }
            }

            var iconRoot = new GameObject("IconRoot");
            iconRoot.transform.SetParent(visualParent, false);
            EntityIconFactory.BuildCar(iconRoot.transform, identity);

            var startX = rng.Range(-halfWidth, halfWidth);
            var lane = Mathf.Lerp(-halfHeight * 0.8f, halfHeight * 0.8f, (i + 0.5f) / carCount);
            var jitterY = rng.Range(-0.35f, 0.35f);
            var carSpeed = rng.Range(10f, 17f);

            positions[i] = new Vector2(startX, lane + jitterY);
            velocities[i] = new Vector2(carSpeed, 0f);
            laneTargets[i] = lane;
            speed[i] = carSpeed;
            laneOffset[i] = rng.Range(-Mathf.Max(0f, track != null ? track.trackWidth * 0.1f : 0f), Mathf.Max(0f, track != null ? track.trackWidth * 0.1f : 0f));
            lapS[i] = 0f;

            if (pathSampler != null)
            {
                var spawnAnchor = positions[i];
                if (track != null && track.startGridSlots != null && track.startGridSlots.Count > 0)
                {
                    var slot = track.startGridSlots[i % track.startGridSlots.Count];
                    spawnAnchor = slot.pos;
                    laneOffset[i] = 0f;
                }

                lapS[i] = pathSampler.FindNearestDistance(spawnAnchor);
                var directionMultiplier = ReverseDirection ? -1f : 1f;
                var (pathPos, tangent) = pathSampler.SampleByDistance(lapS[i]);
                var travelTangent = directionMultiplier > 0f ? tangent : -tangent;
                var normal = new Vector2(-travelTangent.y, travelTangent.x);
                var spawnPos = pathPos + normal * laneOffset[i];
                positions[i] = spawnPos;
                velocities[i] = travelTangent * speed[i];
                laneTargets[i] = spawnPos.y;

                car.transform.localPosition = new Vector3(spawnPos.x, spawnPos.y, 0f);
                if (travelTangent.sqrMagnitude > 0.0001f)
                {
                    car.transform.right = travelTangent;
                }
            }
            else
            {
                if (track != null && track.startGridSlots != null && track.startGridSlots.Count > 0)
                {
                    var slot = track.startGridSlots[i % track.startGridSlots.Count];
                    var dir = slot.dir.sqrMagnitude > 0.001f ? slot.dir.normalized : Vector2.right;
                    positions[i] = slot.pos;
                    velocities[i] = dir * Mathf.Abs(carSpeed);
                    laneTargets[i] = slot.pos.y;
                }

                car.transform.localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
                if (Mathf.Abs(carSpeed) > 0.001f)
                {
                    car.transform.right = new Vector2(Mathf.Sign(carSpeed), 0f);
                }
            }
            cars[i] = car.transform;
            identities[i] = identity;
            visualKeys[i] = visualKey;

            if (logSpawnIdentity && i < SpawnDebugCount)
            {
                Debug.Log($"{nameof(RaceCarRunner)} spawn[{i}] {identity}");
            }
        }
    }

    private void SetupTrackRoot()
    {
        if (trackRoot != null)
        {
            Destroy(trackRoot.gameObject);
            trackRoot = null;
        }

        trackRuntime = null;
        pathSampler = null;

        if (track == null)
        {
            Debug.LogWarning("RaceCarRunner.Track is NULL");
            return;
        }

        if (track.mainCenterline == null || track.mainCenterline.Length < 2)
        {
            Debug.LogWarning(
                $"RaceCarRunner[{name}]: track '{track.name}' has no valid mainCenterline (count={track.mainCenterline?.Length ?? 0}); skipping rendering.");
            return;
        }

        if (sceneGraph == null)
        {
            Debug.LogWarning($"RaceCarRunner[{name}]: SetupTrackRoot skipped because sceneGraph is null.");
            return;
        }

        Transform parent = sceneGraph.ArenaRoot;
        var parentPath = "ArenaRoot";
        if (parent == null)
        {
            parent = sceneGraph.WorldRoot != null ? sceneGraph.WorldRoot : transform;
            parentPath = sceneGraph.WorldRoot != null ? "WorldRoot (ArenaRoot missing)" : "Runner transform (ArenaRoot/WorldRoot missing)";
            Debug.LogWarning($"RaceCarRunner[{name}]: sceneGraph.ArenaRoot is null. Falling back to {parentPath}.");
        }

        var root = new GameObject("TrackRoot");
        root.transform.SetParent(parent, false);
        trackRoot = root.transform;
        trackRoot.localPosition = Vector3.zero;
        trackRoot.localRotation = Quaternion.identity;
        trackRoot.localScale = Vector3.one;

        var renderer = root.AddComponent<TrackRendererV1>();
        renderer.Render(track);

        trackRuntime = root.AddComponent<TrackRuntime>();
        trackRuntime.Initialize(track);

        var overlay = root.AddComponent<TrackStartFinishOverlay>();
        overlay.Render(track);

        var bounds = ComputePaddedTrackBounds(track);
        PresentationBoundsSync.Apply(bounds);
        halfWidth = bounds.width * 0.5f;
        halfHeight = bounds.height * 0.5f;

        if (!runtimeTrackBannerLogged)
        {
            runtimeTrackBannerLogged = true;
            LogRuntimeTrackBanner(track);
        }

        Debug.Log(track.BuildDebugReport());
        Debug.Log($"RaceCarRunner[{name}]: framing bounds rect min={bounds.min}, max={bounds.max}, center={bounds.center}, size={bounds.size}.");

        var parentTransformPath = BuildTransformPath(parent);
        Debug.Log(
            $"RaceCarRunner[{name}]: TrackRoot parent='{parent.name}', path='{parentTransformPath}', worldPosition={parent.position}.");

        var spriteRendererCount = root.GetComponentsInChildren<SpriteRenderer>(true).Length;
        var lineRendererCount = root.GetComponentsInChildren<LineRenderer>(true).Length;
        Debug.Log(
            $"RaceCarRunner[{name}]: TrackRoot renderers SpriteRenderer={spriteRendererCount}, LineRenderer={lineRendererCount}.");

        Debug.Log($"RaceCarRunner[{name}]: using track bounds center={bounds.center}, size={bounds.size}.");
        Debug.Log(
            $"RaceCarRunner[{name}]: TrackRoot created for '{track.name}' " +
            $"(centerline={track.mainCenterline?.Length ?? 0}, parent={parentPath}, position={trackRoot.position}).");
    }

    private static Rect ComputePaddedTrackBounds(TrackBakedData trackData)
    {
        var debugBounds = TrackBakedData.ComputeDebugMainBounds(trackData);
        var padding = Mathf.Max(1f, (trackData != null ? trackData.trackWidth : 8f) * 1.25f);
        var min = debugBounds.min - Vector2.one * padding;
        var max = debugBounds.max + Vector2.one * padding;

        if (max.x <= min.x)
        {
            max.x = min.x + 1f;
        }

        if (max.y <= min.y)
        {
            max.y = min.y + 1f;
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void LogRuntimeTrackBanner(TrackBakedData trackData)
    {
        var debugBounds = TrackBakedData.ComputeDebugMainBounds(trackData);
        var startFinishDir = trackData.startFinishDir.sqrMagnitude > 0.0001f
            ? trackData.startFinishDir.normalized
            : Vector2.zero;
        var pitCount = trackData.pitCenterline?.Length ?? 0;
        var pitLength = TrackBakedData.ComputePolylineLength(trackData.pitCenterline);

        Debug.Log(
            $"RaceCar Runtime Track Banner | name={trackData.name} | " +
            $"trackWidth={trackData.trackWidth:F3} | " +
            $"bounds min={debugBounds.min}, max={debugBounds.max} | " +
            $"startFinish pos={trackData.startFinishPos}, dir={startFinishDir} | " +
            $"startGridSlots={trackData.startGridSlots?.Count ?? 0} | " +
            $"pitCenterline count={pitCount}, length={pitLength:F3}");
    }

    private static string BuildTransformPath(Transform node)
    {
        if (node == null)
        {
            return "<null>";
        }

        var builder = new StringBuilder(64);
        var current = node;
        while (current != null)
        {
            if (builder.Length == 0)
            {
                builder.Insert(0, current.name);
            }
            else
            {
                builder.Insert(0, '/').Insert(0, current.name);
            }

            current = current.parent;
        }

        return builder.ToString();
    }

    private void ResolveArtPipeline()
    {
        artSelector = UnityEngine.Object.FindFirstObjectByType<ArtModeSelector>()
            ?? UnityEngine.Object.FindAnyObjectByType<ArtModeSelector>();

        activePipeline = artSelector != null ? artSelector.GetPipeline() : null;
        if (activePipeline != null)
        {
            Debug.Log($"{nameof(RaceCarRunner)} using art pipeline '{activePipeline.DisplayName}' ({activePipeline.Mode}).");
            return;
        }

        Debug.Log($"{nameof(RaceCarRunner)} no {nameof(ArtModeSelector)} / active pipeline found; using default car renderers.");
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
        var initialOrtho = Mathf.Max(halfHeight + 2f, 10f);
        var arenaCameraPolicy = Object.FindAnyObjectByType<ArenaCameraPolicy>();
        if (arenaCameraPolicy != null && arenaCameraPolicy.targetCamera == cameraComponent)
        {
            arenaCameraPolicy.SetOrthoFromExternal(initialOrtho, "RaceCarRunner.EnsureMainCamera", syncZoomLevel: true);
        }
        else
        {
            cameraComponent.orthographicSize = initialOrtho;
        }

        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
    }
}
