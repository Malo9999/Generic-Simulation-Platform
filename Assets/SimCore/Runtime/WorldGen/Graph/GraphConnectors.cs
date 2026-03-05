using System.Collections.Generic;
using UnityEngine;

public static class GraphConnectors
{
    public static WorldGraph Build(IReadOnlyList<Vector2> points, int kNearest, float widthMin, float widthMax, WorldGenRng rng)
    {
        var graph = new WorldGraph();
        if (points == null || points.Count == 0) return graph;

        for (var i = 0; i < points.Count; i++)
        {
            graph.nodes.Add(new GraphNode { id = i, pos = points[i] });
        }

        kNearest = Mathf.Clamp(kNearest, 1, 8);
        var uf = new UnionFind(points.Count);
        var edgeSet = new HashSet<ulong>();

        for (var i = 0; i < points.Count; i++)
        {
            var nearest = FindNearestIndices(points, i, kNearest);
            for (var n = 0; n < nearest.Count; n++)
            {
                AddEdge(i, nearest[n]);
            }
        }

        ConnectComponents(points, uf, AddEdge);
        return graph;

        void AddEdge(int a, int b)
        {
            if (a == b) return;
            var key = EdgeKey(a, b);
            if (!edgeSet.Add(key)) return;

            var crossing = false;
            var pa = points[a];
            var pb = points[b];
            for (var i = 0; i < graph.edges.Count; i++)
            {
                var e = graph.edges[i];
                if (e.a == a || e.b == a || e.a == b || e.b == b) continue;
                if (SegmentsIntersect(pa, pb, points[e.a], points[e.b]))
                {
                    crossing = true;
                    break;
                }
            }

            graph.edges.Add(new GraphEdge
            {
                a = Mathf.Min(a, b),
                b = Mathf.Max(a, b),
                width = Mathf.Lerp(widthMin, widthMax, rng.NextFloat01()),
                tag = crossing ? "crossing" : "lane"
            });
            uf.Union(a, b);
        }
    }

    private static List<int> FindNearestIndices(IReadOnlyList<Vector2> points, int index, int count)
    {
        var distances = new List<(int idx, float d2)>();
        for (var i = 0; i < points.Count; i++)
        {
            if (i == index) continue;
            distances.Add((i, (points[i] - points[index]).sqrMagnitude));
        }

        distances.Sort((a, b) => a.d2.CompareTo(b.d2));
        var result = new List<int>();
        for (var i = 0; i < Mathf.Min(count, distances.Count); i++) result.Add(distances[i].idx);
        return result;
    }

    private static void ConnectComponents(IReadOnlyList<Vector2> points, UnionFind uf, System.Action<int, int> addEdge)
    {
        while (true)
        {
            var roots = new HashSet<int>();
            for (var i = 0; i < points.Count; i++) roots.Add(uf.Find(i));
            if (roots.Count <= 1) return;

            var bestA = -1;
            var bestB = -1;
            var bestD2 = float.MaxValue;
            for (var i = 0; i < points.Count; i++)
            for (var j = i + 1; j < points.Count; j++)
            {
                if (uf.Find(i) == uf.Find(j)) continue;
                var d2 = (points[i] - points[j]).sqrMagnitude;
                if (d2 >= bestD2) continue;
                bestD2 = d2;
                bestA = i;
                bestB = j;
            }

            if (bestA < 0) return;
            addEdge(bestA, bestB);
        }
    }

    private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        var o1 = Orientation(p1, p2, q1);
        var o2 = Orientation(p1, p2, q2);
        var o3 = Orientation(q1, q2, p1);
        var o4 = Orientation(q1, q2, p2);
        return o1 * o2 < 0f && o3 * o4 < 0f;
    }

    private static float Orientation(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    private static ulong EdgeKey(int a, int b)
    {
        var low = (uint)Mathf.Min(a, b);
        var high = (uint)Mathf.Max(a, b);
        return ((ulong)high << 32) | low;
    }

    private sealed class UnionFind
    {
        private readonly int[] parent;

        public UnionFind(int size)
        {
            parent = new int[size];
            for (var i = 0; i < size; i++) parent[i] = i;
        }

        public int Find(int x)
        {
            if (parent[x] != x) parent[x] = Find(parent[x]);
            return parent[x];
        }

        public void Union(int a, int b)
        {
            var ra = Find(a);
            var rb = Find(b);
            if (ra != rb) parent[ra] = rb;
        }
    }
}
