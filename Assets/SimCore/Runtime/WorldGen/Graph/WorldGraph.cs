using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct GraphNode
{
    public int id;
    public Vector2 pos;
}

[Serializable]
public struct GraphEdge
{
    public int a;
    public int b;
    public float width;
    public string tag;
}

[Serializable]
public class WorldGraph
{
    public List<GraphNode> nodes = new List<GraphNode>();
    public List<GraphEdge> edges = new List<GraphEdge>();
}
