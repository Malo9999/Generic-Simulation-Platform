using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RunnerContract
{
    public static ITickableSimulationRunner RequireTickable(GameObject root, string simId, string context)
    {
        var sceneName = root != null ? root.scene.name : SceneManager.GetActiveScene().name;
        var rootName = root != null ? root.name : "<null>";

        if (root == null)
        {
            throw BuildContractException(
                simId,
                rootName,
                sceneName,
                context,
                "No runner root GameObject was provided.",
                "<none>");
        }

        var allComponents = root.GetComponentsInChildren<MonoBehaviour>(true);
        var tickableRunners = new List<ITickableSimulationRunner>();
        for (var i = 0; i < allComponents.Length; i++)
        {
            if (allComponents[i] is ITickableSimulationRunner tickable)
            {
                tickableRunners.Add(tickable);
            }
        }

        if (tickableRunners.Count == 1)
        {
            return tickableRunners[0];
        }

        var availableComponents = DescribeComponents(root);
        var detail = tickableRunners.Count == 0
            ? "No ITickableSimulationRunner implementation was found on the runner root or its children."
            : $"Found {tickableRunners.Count} ITickableSimulationRunner implementations ({DescribeRunnerTypes(tickableRunners)}), but exactly one is required.";

        throw BuildContractException(simId, rootName, sceneName, context, detail, availableComponents);
    }

    private static InvalidOperationException BuildContractException(
        string simId,
        string rootName,
        string sceneName,
        string context,
        string detail,
        string availableComponents)
    {
        var message =
            $"RunnerContract: Invalid runner setup for simulation '{simId}' during {context}. " +
            $"scene='{sceneName}', root='{rootName}'. {detail} " +
            "Fix: ensure the selected simulation prefab has exactly one component implementing ITickableSimulationRunner on the root or child hierarchy. " +
            $"Components on root hierarchy: {availableComponents}";

        Debug.LogError(message);
        return new InvalidOperationException(message);
    }

    private static string DescribeComponents(GameObject root)
    {
        var components = root.GetComponentsInChildren<Component>(true);
        if (components == null || components.Length == 0)
        {
            return "<none>";
        }

        var names = new string[components.Length];
        for (var i = 0; i < components.Length; i++)
        {
            names[i] = components[i] == null ? "<MissingScript>" : components[i].GetType().FullName;
        }

        return string.Join(", ", names);
    }

    private static string DescribeRunnerTypes(List<ITickableSimulationRunner> runners)
    {
        var names = new string[runners.Count];
        for (var i = 0; i < runners.Count; i++)
        {
            names[i] = runners[i].GetType().FullName;
        }

        return string.Join(", ", names);
    }
}
