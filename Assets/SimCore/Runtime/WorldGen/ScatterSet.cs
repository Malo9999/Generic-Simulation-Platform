using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ScatterPoint
{
    public Vector2 pos;
    public float rotDeg;
    public float scale;
    public int typeId;
    public uint flags;
    public string[] tags;
}

[Serializable]
public class ScatterSet
{
    public string id;
    public List<ScatterPoint> points = new List<ScatterPoint>();
}
