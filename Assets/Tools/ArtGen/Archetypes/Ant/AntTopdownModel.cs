using System;
using UnityEngine;

[Serializable]
public sealed class AntTopdownModel
{
    public Vector2 headCenter01 = new(0.5f, 0.2f);
    public Vector2 thoraxCenter01 = new(0.5f, 0.45f);
    public Vector2 abdomenCenter01 = new(0.5f, 0.72f);
    public Vector2 headRadii01 = new(0.08f, 0.08f);
    public Vector2 thoraxRadii01 = new(0.1f, 0.1f);
    public Vector2 abdomenRadii01 = new(0.15f, 0.18f);
    public float pinchStrength = 0.5f;
    public Vector2[] legAnchors01 = new Vector2[6];
    public float[] legAnglesDeg = new float[6];
    public float[] legLengths01 = new float[6];
    public Vector2[] antennaAnchors01 = new Vector2[2];
    public float[] antennaAnglesDeg = new float[2];
    public float antennaLen01 = 0.15f;
    public Vector2 eyeLeft01 = new(0.46f, 0.16f);
    public Vector2 eyeRight01 = new(0.54f, 0.16f);
    public string sourceImagePath;
    public int sourceW;
    public int sourceH;
}
