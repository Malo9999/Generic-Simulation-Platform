using UnityEngine;

public static class SceneGraphUtil
{
    public static SimulationSceneGraph PrepareRunner(Transform runnerTransform, string simId)
    {
        var simulationRoot = ResolveSimulationRoot(runnerTransform);
        var graph = SimulationSceneGraph.Ensure(simulationRoot);

        runnerTransform.SetParent(graph.RunnerRoot, false);
        runnerTransform.name = "Runner";

        var marker = runnerTransform.GetComponent<SimulationRunnerMarker>();
        if (marker == null)
        {
            marker = runnerTransform.gameObject.AddComponent<SimulationRunnerMarker>();
        }

        marker.SimId = simId;
        return graph;
    }

    public static Transform EnsureEntityGroup(Transform entitiesRoot, int teamId)
    {
        var resolvedTeamId = teamId >= 0 ? teamId : 1;
        return EnsureChild(entitiesRoot, $"Group_{resolvedTeamId:00}");
    }

    public static Transform EnsureDefaultEntityGroup(Transform entitiesRoot)
    {
        return EnsureEntityGroup(entitiesRoot, 1);
    }

    private static Transform ResolveSimulationRoot(Transform runnerTransform)
    {
        var current = runnerTransform;
        while (current != null)
        {
            if (current.name == "SimulationRoot")
            {
                return current;
            }

            current = current.parent;
        }

        if (runnerTransform.parent != null && runnerTransform.parent.name == "RunnerRoot" && runnerTransform.parent.parent != null)
        {
            return runnerTransform.parent.parent;
        }

        return runnerTransform.parent != null ? runnerTransform.parent : runnerTransform;
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
}

public class SimulationRunnerMarker : MonoBehaviour
{
    public string SimId;
}
