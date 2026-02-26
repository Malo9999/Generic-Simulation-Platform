using System.Collections.Generic;
using UnityEngine;

public class ArtModeSelector : MonoBehaviour
{
    [SerializeField] private string simulationId;
    [SerializeField] private bool followBootstrapper = true;
    [SerializeField] private ArtMode requestedMode = ArtMode.Simple;
    [SerializeField] private bool forceDebugPlaceholders = false;
    [SerializeField] private DebugPlaceholderMode debugMode = DebugPlaceholderMode.Replace;
    [SerializeField] private ArtPipelineRegistry registry;
    [SerializeField] private ArtManifest manifestOverride;

    public ArtPipelineBase ActivePipeline { get; private set; }

    private ArtManifest runtimeManifest;
    private string resolvedSimulationId;

    private void Awake()
    {
        EnsureResolved();
    }

    public ArtPipelineBase GetPipeline()
    {
        EnsureResolved();
        return ActivePipeline;
    }

    public List<ArtMode> GetAvailableModes()
    {
        EnsureRegistry();
        if (registry == null)
        {
            return new List<ArtMode>();
        }

        var effectiveSimulationId = ResolveSimulationId();
        ArtManifest manifest = GetManifest();
        return registry.GetAvailableModes(effectiveSimulationId, manifest);
    }

    private void EnsureResolved()
    {
        EnsureRegistry();
        if (registry == null)
        {
            Debug.LogWarning("[ArtModeSelector] No ArtPipelineRegistry assigned or found in Resources.");
            ActivePipeline = null;
            return;
        }

        var effectiveSimulationId = ResolveSimulationId();
        runtimeManifest = GetManifest();
        ActivePipeline = registry.Resolve(effectiveSimulationId, runtimeManifest, requestedMode);
        if (ActivePipeline != null)
        {
            ActivePipeline.ConfigureDebug(forceDebugPlaceholders, debugMode);
        }
    }

    private void EnsureRegistry()
    {
        if (registry == null)
        {
            registry = ArtPipelineRegistry.LoadDefault();
        }
    }

    private ArtManifest GetManifest()
    {
        if (manifestOverride != null)
        {
            return manifestOverride;
        }

        if (runtimeManifest == null)
        {
            runtimeManifest = ArtManifest.LoadForSimulation(resolvedSimulationId);
        }

        return runtimeManifest;
    }

    private string ResolveSimulationId()
    {
        var effectiveSimulationId = simulationId;

        if (manifestOverride == null && followBootstrapper)
        {
            var bootstrapper = UnityEngine.Object.FindFirstObjectByType<Bootstrapper>()
                ?? UnityEngine.Object.FindAnyObjectByType<Bootstrapper>();
            if (bootstrapper != null && !string.IsNullOrWhiteSpace(bootstrapper.CurrentSimulationId))
            {
                effectiveSimulationId = bootstrapper.CurrentSimulationId;
            }
        }

        if (string.IsNullOrWhiteSpace(effectiveSimulationId))
        {
            effectiveSimulationId = "MarbleRace";
        }

        if (!string.Equals(resolvedSimulationId, effectiveSimulationId))
        {
            resolvedSimulationId = effectiveSimulationId;
            runtimeManifest = null;
        }

        return resolvedSimulationId;
    }
}
