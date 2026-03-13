using UnityEngine;

[GspBootstrap(GspBootstrapKind.Simulation, "Standalone bootstrap for GranularFlow experimental sim")]
public sealed class GranularFlowBootstrap : MonoBehaviour
{
    [Header("Startup")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private int seed = 4242;
    [SerializeField, Min(0.001f)] private float tickDeltaTime = 1f / 60f;
    [SerializeField] private string scenarioName = "GranularFlow Lab";

    [Header("Runner")]
    [SerializeField] private GranularFlowRunner runner;

    private bool running;
    private int tick;

    private void Awake()
    {
        EnsureRunner();
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
        if (!running || runner == null)
        {
            return;
        }

        runner.Tick(tick++, tickDeltaTime);
    }

    private void OnDestroy()
    {
        StopSimulation();
    }

    [ContextMenu("Start Simulation")]
    public void StartSimulation()
    {
        EnsureRunner();
        if (runner == null)
        {
            return;
        }

        StopSimulation();

        var config = new ScenarioConfig
        {
            simulationId = "GranularFlow",
            activeSimulation = "GranularFlow",
            scenarioName = scenarioName,
            mode = "Sim",
            seed = seed,
            granularFlow = new GranularFlowConfig()
        };
        config.NormalizeAliases();

        tick = 0;
        runner.Initialize(config);
        running = true;
    }

    [ContextMenu("Stop Simulation")]
    public void StopSimulation()
    {
        if (runner != null)
        {
            runner.Shutdown();
        }

        running = false;
    }

    private void EnsureRunner()
    {
        if (runner == null)
        {
            runner = GetComponent<GranularFlowRunner>();
        }

        if (runner == null)
        {
            runner = gameObject.AddComponent<GranularFlowRunner>();
        }
    }
}
