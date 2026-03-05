using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct WidthKey
{
    public float t;
    public float w;
}

[Serializable]
public class WorldSpline
{
    public string id;
    public List<Vector2> points = new List<Vector2>();
    public bool closed;
    public float baseWidth = 1f;
    public List<WidthKey> widthKeys = new List<WidthKey>();
    public List<string> tags = new List<string>();
}
