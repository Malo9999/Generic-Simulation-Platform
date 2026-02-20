using UnityEngine;

public class SimRunner : MonoBehaviour, ITickableSimulationRunner, IRecordable, IReplayableSimulationRunner
{
    private const string SimRootName = "SimRoot";

    [Header("Arena Visual Debug")]
    [SerializeField] private bool showBackground = true;
    [SerializeField] private bool showBounds = true;

    private ISimulation currentSimulation;
    private Transform simRoot;

    public void Initialize(ScenarioConfig cfg)
    {
        EnsureSimRoot();

        if (currentSimulation != null)
        {
            currentSimulation.Dispose();
            currentSimulation = null;
        }

        BuildArenaVisuals(cfg);

        var selectedSimulationId = cfg?.activeSimulation ?? string.Empty;
        Debug.Log($"SimRunner: Selected simulation id '{selectedSimulationId}'.");

        if (SimRegistry.TryCreate(selectedSimulationId, out var simulation))
        {
            currentSimulation = simulation;
            currentSimulation?.Initialize(cfg, simRoot, RngService.Global);
        }
        else
        {
            currentSimulation = null;
            Debug.LogWarning($"SimRunner: No simulation factory registered for id '{selectedSimulationId}'.");
        }
    }

    public void Tick(int tickIndex, float dt)
    {
        currentSimulation?.Tick(dt);
    }

    public void Shutdown()
    {
        currentSimulation?.Dispose();
        currentSimulation = null;
    }

    public object CaptureState()
    {
        if (currentSimulation is IRecordable recordable)
        {
            return recordable.CaptureState();
        }

        return new FallbackSnapshot
        {
            simulationId = currentSimulation?.Id,
            status = currentSimulation == null ? "not_initialized" : "recordable_not_implemented"
        };
    }

    public void ApplyReplaySnapshot(int tick, object state)
    {
        if (currentSimulation is IReplayableState replayable)
        {
            replayable.ApplyReplayState(state);
            return;
        }

        if (tick == 0)
        {
            Debug.Log($"SimRunner: Replay snapshot feed active but simulation '{currentSimulation?.Id ?? "<none>"}' does not implement IReplayableState.");
        }
    }

    public void ApplyReplayEvent(int tick, string eventType, object payload)
    {
        if (currentSimulation is IReplayableState replayable)
        {
            replayable.ApplyReplayEvent(eventType, payload);
        }
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private void EnsureSimRoot()
    {
        if (simRoot != null)
        {
            return;
        }

        var existing = transform.Find(SimRootName);
        if (existing != null)
        {
            simRoot = existing;
            return;
        }

        var rootObject = new GameObject(SimRootName);
        rootObject.transform.SetParent(transform, false);
        simRoot = rootObject.transform;
    }

    private void BuildArenaVisuals(ScenarioConfig cfg)
    {
        if (simRoot == null)
        {
            return;
        }

        var arenaVisuals = simRoot.Find("ArenaVisuals");
        if (arenaVisuals == null)
        {
            var arenaVisualsObject = new GameObject("ArenaVisuals");
            arenaVisualsObject.transform.SetParent(simRoot, false);
            arenaVisuals = arenaVisualsObject.transform;
        }

        var width = Mathf.Max(1, cfg?.world?.arenaWidth ?? 64);
        var height = Mathf.Max(1, cfg?.world?.arenaHeight ?? 64);
        var center = new Vector3(width * 0.5f, height * 0.5f, 0f);

        var sortingLayer = ResolveSortingLayer();

        CreateOrUpdateBackground(arenaVisuals, width, height, center, sortingLayer, showBackground);
        CreateOrUpdateBounds(arenaVisuals, width, height, sortingLayer, showBounds);
    }

    private static void CreateOrUpdateBackground(Transform parent, float width, float height, Vector3 center, string sortingLayer, bool enabled)
    {
        var background = parent.Find("Background");

        if (!enabled)
        {
            if (background != null)
            {
                background.gameObject.SetActive(false);
            }

            return;
        }

        SpriteRenderer renderer;
        if (background == null)
        {
            var backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(parent, false);
            backgroundObject.transform.localPosition = new Vector3(0f, 0f, 2f);
            renderer = backgroundObject.AddComponent<SpriteRenderer>();
        }
        else
        {
            background.gameObject.SetActive(true);
            renderer = background.GetComponent<SpriteRenderer>() ?? background.gameObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite ??= Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        renderer.color = new Color(0.08f, 0.09f, 0.12f, 1f);
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = -20;
        renderer.drawMode = SpriteDrawMode.Simple;
        renderer.transform.localScale = new Vector3(width, height, 1f);
        renderer.transform.localPosition = new Vector3(center.x, center.y, 2f);
    }

    private static void CreateOrUpdateBounds(Transform parent, float width, float height, string sortingLayer, bool enabled)
    {
        var bounds = parent.Find("Bounds");

        if (!enabled)
        {
            if (bounds != null)
            {
                bounds.gameObject.SetActive(false);
            }

            return;
        }

        LineRenderer lineRenderer;
        if (bounds == null)
        {
            var boundsObject = new GameObject("Bounds");
            boundsObject.transform.SetParent(parent, false);
            lineRenderer = boundsObject.AddComponent<LineRenderer>();
        }
        else
        {
            bounds.gameObject.SetActive(true);
            lineRenderer = bounds.GetComponent<LineRenderer>() ?? bounds.gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.material ??= new Material(Shader.Find("Sprites/Default"));
        lineRenderer.loop = true;
        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = 4;
        lineRenderer.widthMultiplier = 0.1f;
        lineRenderer.startColor = new Color(0.7f, 0.8f, 1f, 0.95f);
        lineRenderer.endColor = lineRenderer.startColor;
        lineRenderer.sortingLayerName = sortingLayer;
        lineRenderer.sortingOrder = -10;

        lineRenderer.SetPosition(0, new Vector3(0f, 0f, 0f));
        lineRenderer.SetPosition(1, new Vector3(width, 0f, 0f));
        lineRenderer.SetPosition(2, new Vector3(width, height, 0f));
        lineRenderer.SetPosition(3, new Vector3(0f, height, 0f));
    }

    private static string ResolveSortingLayer()
    {
        foreach (var layer in SortingLayer.layers)
        {
            if (layer.name == "World")
            {
                return "World";
            }
        }

        return "Default";
    }

    private sealed class FallbackSnapshot
    {
        public string simulationId;
        public string status;
    }
}
