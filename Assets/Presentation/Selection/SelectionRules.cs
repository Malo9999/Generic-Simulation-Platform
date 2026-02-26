using UnityEngine;

public static class SelectionRules
{
    public static Transform FindSimulationRoot()
    {
        var simulationRoot = GameObject.Find("SimulationRoot")?.transform;
        if (simulationRoot != null)
        {
            return simulationRoot;
        }

        return GameObject.Find("SimRoot")?.transform;
    }

    public static Transform FindEntitiesRoot()
    {
        var simulationRoot = FindSimulationRoot();
        if (simulationRoot == null)
        {
            return null;
        }

        if (simulationRoot.name == "EntitiesRoot")
        {
            return simulationRoot;
        }

        foreach (var child in simulationRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == "EntitiesRoot")
            {
                return child;
            }
        }

        return null;
    }

    public static Transform ResolveSelectableSim(Transform candidate, Transform entitiesRoot)
    {
        if (candidate == null || entitiesRoot == null)
        {
            return null;
        }

        var current = candidate;
        while (current != null && current != entitiesRoot.parent)
        {
            if (IsSelectableSim(current, entitiesRoot))
            {
                return current;
            }

            if (current == entitiesRoot)
            {
                break;
            }

            current = current.parent;
        }

        return null;
    }

    public static bool IsSelectableSim(Transform candidate, Transform entitiesRoot)
    {
        if (candidate == null || entitiesRoot == null)
        {
            return false;
        }

        if (!candidate.IsChildOf(entitiesRoot) && candidate != entitiesRoot)
        {
            return false;
        }

        if (candidate == entitiesRoot)
        {
            return false;
        }

        var go = candidate.gameObject;
        if (go.name.StartsWith("Sim_"))
        {
            return true;
        }

        if (go.GetComponent<SelectableSim>() != null)
        {
            return true;
        }

        return go.GetComponent("Identity") != null;
    }
}
