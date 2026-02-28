using System;
using UnityEngine;

public sealed class SimulationSceneGraph
{
    public Transform WorldRoot { get; private set; }
    public Transform RunnerRoot { get; private set; }
    public Transform EntitiesRoot { get; private set; }
    public Transform DebugRoot { get; private set; }

    public Transform ArenaRootParent { get; private set; }
    public Transform ArenaRoot { get; private set; }
    public Transform DecorRoot { get; private set; }
    public Transform WorldObjectsRoot { get; private set; }

    public static SimulationSceneGraph Ensure(Transform simulationRoot)
    {
        if (simulationRoot == null)
        {
            throw new ArgumentNullException(nameof(simulationRoot));
        }

        var graph = new SimulationSceneGraph
        {
            WorldRoot = EnsureChild(simulationRoot, "WorldRoot"),
            RunnerRoot = EnsureChild(simulationRoot, "RunnerRoot"),
            EntitiesRoot = EnsureChild(simulationRoot, "EntitiesRoot"),
            DebugRoot = EnsureChild(simulationRoot, "DebugRoot")
        };

        graph.ArenaRootParent = graph.WorldRoot;
        graph.ArenaRoot = EnsureChild(graph.WorldRoot, "ArenaRoot");
        graph.DecorRoot = EnsureChild(graph.WorldRoot, "DecorRoot");
        graph.WorldObjectsRoot = EnsureChild(graph.WorldRoot, "WorldObjects");

        return graph;
    }

    public static SimulationSceneGraph Rebuild(Transform simulationRoot)
    {
        if (simulationRoot == null)
        {
            throw new ArgumentNullException(nameof(simulationRoot));
        }

        var graph = new SimulationSceneGraph
        {
            WorldRoot = RebuildChild(simulationRoot, "WorldRoot"),
            RunnerRoot = RebuildChild(simulationRoot, "RunnerRoot"),
            EntitiesRoot = RebuildChild(simulationRoot, "EntitiesRoot"),
            DebugRoot = RebuildChild(simulationRoot, "DebugRoot")
        };

        graph.ArenaRootParent = graph.WorldRoot;
        graph.ArenaRoot = RebuildChild(graph.WorldRoot, "ArenaRoot");
        graph.DecorRoot = RebuildChild(graph.WorldRoot, "DecorRoot");
        graph.WorldObjectsRoot = RebuildChild(graph.WorldRoot, "WorldObjects");

        return graph;
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private static Transform RebuildChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null)
        {
            existing.name = name + "_OLD_" + Time.frameCount;
            if (existing.gameObject != null)
            {
                existing.gameObject.SetActive(false);
            }
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }
}
