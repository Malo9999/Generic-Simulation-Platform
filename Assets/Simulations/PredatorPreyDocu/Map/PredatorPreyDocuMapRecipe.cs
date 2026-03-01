using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "GSP/PredatorPreyDocu/Map Recipe", fileName = "PredatorPreyDocuMapRecipe")]
public sealed class PredatorPreyDocuMapRecipe : ScriptableObject
{
    [Tooltip("Arena width used while authoring this recipe (world units).")]
    public float arenaWidth = 320f;

    [Tooltip("Arena height used while authoring this recipe (world units).")]
    public float arenaHeight = 200f;

    [Tooltip("River control points in normalized [0..1] space (x: west->east, y: south->north).")]
    public List<Vector2> riverPoints = new();

    public float riverWidthNorth = 10f;
    public float riverWidthSouth = 6f;
    public float floodplainExtra = 26f;
    public float bankExtra = 2.5f;
    public int creekCount = 6;
    public int treeClusterCount = 55;
    public int grassDotCount = 1500;

    public void NormalizeAndValidate()
    {
        arenaWidth = Mathf.Max(1f, arenaWidth);
        arenaHeight = Mathf.Max(1f, arenaHeight);
        riverWidthNorth = Mathf.Clamp(riverWidthNorth, 0.5f, 80f);
        riverWidthSouth = Mathf.Clamp(riverWidthSouth, 0.5f, 80f);
        floodplainExtra = Mathf.Clamp(floodplainExtra, 1f, 120f);
        bankExtra = Mathf.Clamp(bankExtra, 0.5f, 32f);
        creekCount = Mathf.Clamp(creekCount, 0, 64);
        treeClusterCount = Mathf.Clamp(treeClusterCount, 0, 500);
        grassDotCount = Mathf.Clamp(grassDotCount, 0, 20000);

        if (riverPoints == null)
        {
            riverPoints = new List<Vector2>();
        }

        for (var i = 0; i < riverPoints.Count; i++)
        {
            var p = riverPoints[i];
            p.x = Mathf.Clamp01(p.x);
            p.y = Mathf.Clamp01(p.y);
            riverPoints[i] = p;
        }

        riverPoints.Sort((a, b) => b.y.CompareTo(a.y));

        if (riverPoints.Count > 0)
        {
            var first = riverPoints[0];
            first.y = 1f;
            riverPoints[0] = first;

            var last = riverPoints[riverPoints.Count - 1];
            last.y = 0f;
            riverPoints[riverPoints.Count - 1] = last;
        }
    }

    public void EnsureDefaultRiver(int controlPointCount = 8)
    {
        controlPointCount = Mathf.Clamp(controlPointCount, 2, 64);

        if (riverPoints == null)
        {
            riverPoints = new List<Vector2>(controlPointCount);
        }
        else
        {
            riverPoints.Clear();
        }

        for (var i = 0; i < controlPointCount; i++)
        {
            var t = i / (float)(controlPointCount - 1);
            var y = 1f - t;
            var wave = Mathf.Sin(t * Mathf.PI * 2.2f);
            var x = Mathf.Clamp01(0.5f + (wave * 0.18f));
            riverPoints.Add(new Vector2(x, y));
        }

        NormalizeAndValidate();
    }
}
