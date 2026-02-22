using System;
using UnityEngine;

[Serializable]
public class AntWorldRecipe
{
    public int nestCount = 5;
    public float nestMinDistance = 18f;
    public float nestBorderMargin = 8f;
    public int nestHp = 500;

    public int foodCount = 10;
    public int foodAmount = 60;
    public float foodEdgeMargin = 5f;
    public int foodRespawnDelayTicks = 900;

    public int obstacleCountMin = 2;
    public int obstacleCountMax = 4;
    public Vector2 obstacleRadiusRange = new(1.5f, 2.5f);

    public int decorCountMin = 80;
    public int decorCountMax = 200;
    public int decorTargetCount = 120;
    public float decorBorderMargin = 2.5f;
    public float decorClearCenterRadius = 7f;
    public float decorMinSpacing = 0.6f;
    public int decorMaxAttempts = 5000;

    public float baseGrassRatio = 0.8f;
    public int dirtPatches = 12;
    public float pathStrength = 0.7f;

    public int hardDecorCap = 300;

    public void Normalize()
    {
        nestCount = 5;
        nestMinDistance = Mathf.Max(2f, nestMinDistance);
        nestBorderMargin = Mathf.Max(0.5f, nestBorderMargin);
        nestHp = Mathf.Max(1, nestHp);

        foodCount = Mathf.Max(1, foodCount);
        foodAmount = Mathf.Max(1, foodAmount);
        foodEdgeMargin = Mathf.Max(0.5f, foodEdgeMargin);
        foodRespawnDelayTicks = Mathf.Max(1, foodRespawnDelayTicks);

        obstacleCountMin = Mathf.Clamp(obstacleCountMin, 0, 20);
        obstacleCountMax = Mathf.Clamp(obstacleCountMax, obstacleCountMin, 20);
        obstacleRadiusRange.x = Mathf.Max(0.3f, obstacleRadiusRange.x);
        obstacleRadiusRange.y = Mathf.Max(obstacleRadiusRange.x, obstacleRadiusRange.y);

        decorCountMin = Mathf.Clamp(decorCountMin, 0, hardDecorCap);
        decorCountMax = Mathf.Clamp(decorCountMax, decorCountMin, hardDecorCap);
        decorTargetCount = Mathf.Clamp(decorTargetCount, decorCountMin, decorCountMax);
        decorBorderMargin = Mathf.Max(0f, decorBorderMargin);
        decorClearCenterRadius = Mathf.Max(0f, decorClearCenterRadius);
        decorMinSpacing = Mathf.Max(0.05f, decorMinSpacing);
        decorMaxAttempts = Mathf.Max(100, decorMaxAttempts);

        baseGrassRatio = Mathf.Clamp01(baseGrassRatio);
        dirtPatches = Mathf.Clamp(dirtPatches, 0, 100);
        pathStrength = Mathf.Clamp01(pathStrength);
    }
}

[Serializable]
public class AntColoniesConfig
{
    public AntWorldRecipe worldRecipe = new();

    public void Normalize()
    {
        worldRecipe ??= new AntWorldRecipe();
        worldRecipe.Normalize();
    }
}
