using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Presentation/Art Pipelines/Art Manifest", fileName = "ArtManifest")]
public class ArtManifest : ScriptableObject
{
    [SerializeField] private string simulationId;
    [SerializeField] private List<string> requirements = new();

    public string SimulationId => simulationId;
    public IReadOnlyList<string> Requirements => requirements;

    public bool Has(string req)
    {
        if (string.IsNullOrWhiteSpace(req))
        {
            return false;
        }

        return requirements.Contains(req);
    }

    public static ArtManifest LoadForSimulation(string simulationId)
    {
        if (string.IsNullOrWhiteSpace(simulationId))
        {
            return null;
        }

        return Resources.Load<ArtManifest>($"ArtManifests/{simulationId}");
    }
}
