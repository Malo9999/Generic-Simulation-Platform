using UnityEngine;

public static class GraphGenerator
{
    public static WorldGraph GenerateOrganicRect(Rect bounds, int nodeCount, float minDist, int kNearest, float widthMin, float widthMax, WorldGenRng rng)
    {
        var nodes = PoissonDisk2D.Sample(bounds, minDist, nodeCount, rng.Fork("poisson"));
        return GraphConnectors.Build(nodes, kNearest, widthMin, widthMax, rng.Fork("connect"));
    }
}
