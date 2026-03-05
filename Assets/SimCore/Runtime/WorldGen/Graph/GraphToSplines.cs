using System.Collections.Generic;
using UnityEngine;

public static class GraphToSplines
{
    public static List<WorldSpline> Build(WorldGraph graph, float organicJitter, int smoothIterations, WorldGenRng rng)
    {
        var splines = new List<WorldSpline>();
        if (graph == null || graph.nodes == null || graph.edges == null) return splines;

        var nodesById = new Dictionary<int, GraphNode>();
        for (var i = 0; i < graph.nodes.Count; i++) nodesById[graph.nodes[i].id] = graph.nodes[i];

        for (var i = 0; i < graph.edges.Count; i++)
        {
            var edge = graph.edges[i];
            if (!nodesById.TryGetValue(edge.a, out var aNode) || !nodesById.TryGetValue(edge.b, out var bNode)) continue;

            var a = aNode.pos;
            var b = bNode.pos;
            var dir = (b - a).normalized;
            var normal = new Vector2(-dir.y, dir.x);
            var length = Vector2.Distance(a, b);
            var jitter = organicJitter * length;

            var p1 = Vector2.Lerp(a, b, 0.33f) + normal * ((rng.NextFloat01() * 2f - 1f) * jitter);
            var p2 = Vector2.Lerp(a, b, 0.66f) + normal * ((rng.NextFloat01() * 2f - 1f) * jitter);
            var points = new List<Vector2> { a, p1, p2, b };

            for (var s = 0; s < Mathf.Clamp(smoothIterations, 0, 3); s++)
            {
                points = Chaikin(points);
            }

            splines.Add(new WorldSpline
            {
                id = $"edge_{edge.a}_{edge.b}",
                baseWidth = edge.width,
                points = points,
                tags = new List<string> { edge.tag }
            });
        }

        return splines;
    }

    private static List<Vector2> Chaikin(List<Vector2> pts)
    {
        if (pts == null || pts.Count < 2) return pts;
        var result = new List<Vector2> { pts[0] };
        for (var i = 0; i < pts.Count - 1; i++)
        {
            var a = pts[i];
            var b = pts[i + 1];
            result.Add(Vector2.Lerp(a, b, 0.25f));
            result.Add(Vector2.Lerp(a, b, 0.75f));
        }
        result.Add(pts[pts.Count - 1]);
        return result;
    }
}
