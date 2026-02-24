using System.Collections.Generic;
using UnityEngine;

public class ArtModeSelector : MonoBehaviour
{
    [SerializeField] private string simulationId;
    [SerializeField] private ArtMode requestedMode = ArtMode.Simple;
    [SerializeField] private ArtPipelineRegistry registry;
    [SerializeField] private ArtManifest manifestOverride;

    public ArtPipelineBase ActivePipeline { get; private set; }

    private ArtManifest runtimeManifest;

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

        ArtManifest manifest = GetManifest();
        return registry.GetAvailableModes(simulationId, manifest);
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

        runtimeManifest = GetManifest();
        ActivePipeline = registry.Resolve(simulationId, runtimeManifest, requestedMode);
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
            runtimeManifest = ArtManifest.LoadForSimulation(simulationId);
        }

        return runtimeManifest;
    }
}
