using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Presentation/Art Pipelines/Registry", fileName = "ArtPipelineRegistry")]
public class ArtPipelineRegistry : ScriptableObject
{
    [SerializeField] private List<ArtPipelineBase> pipelines = new();

    public List<ArtMode> GetAvailableModes(string simulationId, ArtManifest manifest)
    {
        List<ArtMode> availableModes = new();

        foreach (ArtPipelineBase pipeline in pipelines)
        {
            if (pipeline == null)
            {
                continue;
            }

            if (pipeline.IsAvailable(manifest))
            {
                availableModes.Add(pipeline.Mode);
            }
        }

        return availableModes;
    }

    public ArtPipelineBase Resolve(string simulationId, ArtManifest manifest, ArtMode requested)
    {
        ArtPipelineBase requestedPipeline = pipelines.FirstOrDefault(p => p != null && p.Mode == requested);
        if (requestedPipeline != null && requestedPipeline.IsAvailable(manifest))
        {
            return requestedPipeline;
        }

        if (requestedPipeline != null)
        {
            LogFallback(simulationId, requestedPipeline, manifest);
        }
        else
        {
            Debug.LogWarning($"[ArtPipelineRegistry] Requested pipeline '{requested}' is not registered for simulation '{simulationId}'. Falling back.");
        }

        ArtPipelineBase flatPipeline = pipelines.FirstOrDefault(p => p != null && p.Mode == ArtMode.Flat);
        if (flatPipeline != null && flatPipeline.IsAvailable(manifest))
        {
            return flatPipeline;
        }

        if (flatPipeline != null)
        {
            LogFallback(simulationId, flatPipeline, manifest);
        }

        ArtPipelineBase simplePipeline = pipelines.FirstOrDefault(p => p != null && p.Mode == ArtMode.Simple);
        if (simplePipeline != null && simplePipeline.IsAvailable(manifest))
        {
            return simplePipeline;
        }

        if (simplePipeline != null)
        {
            LogFallback(simulationId, simplePipeline, manifest);
        }

        Debug.LogWarning($"[ArtPipelineRegistry] No available art pipeline for simulation '{simulationId}'.");
        return null;
    }

    public static ArtPipelineRegistry LoadDefault()
    {
        return Resources.Load<ArtPipelineRegistry>("ArtPipelineRegistry");
    }

    private static void LogFallback(string simulationId, ArtPipelineBase unavailablePipeline, ArtManifest manifest)
    {
        List<string> missing = unavailablePipeline.MissingRequirements(manifest);
        string missingRequirements = missing.Count > 0 ? string.Join(", ", missing) : "unknown requirements";

        Debug.LogWarning(
            $"[ArtPipelineRegistry] Falling back for simulation '{simulationId}'. " +
            $"Pipeline '{unavailablePipeline.DisplayName}' ({unavailablePipeline.Mode}) is unavailable. " +
            $"Missing requirements: {missingRequirements}.");
    }
}
