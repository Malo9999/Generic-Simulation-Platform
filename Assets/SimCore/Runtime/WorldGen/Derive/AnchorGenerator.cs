using UnityEngine;

public static class AnchorGenerator
{
    public static void PopulateAnchors(WorldMap map, WorldGraph graph, float laneSampleSpacing)
    {
        var nodeSet = new ScatterSet { id = "anchors_nodes" };
        var laneSet = new ScatterSet { id = "anchors_lane" };

        if (graph?.nodes != null)
        {
            for (var i = 0; i < graph.nodes.Count; i++)
            {
                var pos = graph.nodes[i].pos;
                if (!InBounds(map.grid, pos)) continue;
                nodeSet.points.Add(new ScatterPoint { pos = pos, scale = 1.2f, tags = new[] { "node" } });
            }
        }

        laneSampleSpacing = Mathf.Max(0.5f, laneSampleSpacing);
        if (map?.splines != null)
        {
            for (var s = 0; s < map.splines.Count; s++)
            {
                var spline = map.splines[s];
                if (spline?.points == null || spline.points.Count < 2) continue;

                float carry = 0f;
                for (var i = 1; i < spline.points.Count; i++)
                {
                    var a = spline.points[i - 1];
                    var b = spline.points[i];
                    var len = Vector2.Distance(a, b);
                    var d = carry;
                    while (d <= len)
                    {
                        var t = len < 0.0001f ? 0f : d / len;
                        var pos = Vector2.Lerp(a, b, t);
                        if (InBounds(map.grid, pos))
                        {
                            laneSet.points.Add(new ScatterPoint { pos = pos, scale = 0.8f, tags = new[] { "lane" } });
                        }

                        d += laneSampleSpacing;
                    }

                    carry = d - len;
                }
            }
        }

        map.scatters[nodeSet.id] = nodeSet;
        map.scatters[laneSet.id] = laneSet;
    }

    private static bool InBounds(WorldGridSpec grid, Vector2 pos)
    {
        var max = grid.originWorld + new Vector2(grid.width * grid.cellSize, grid.height * grid.cellSize);
        return pos.x >= grid.originWorld.x && pos.y >= grid.originWorld.y && pos.x <= max.x && pos.y <= max.y;
    }
}
