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

    public int spawnEveryNTicks = 30;
    public int maxAntsPerNest = 80;
    public int maxAntsGlobal = 400;

    public float walkSpeed = 1.6f;
    public float runSpeed = 2.4f;

    public float foodSenseRadius = 2.2f;
    public float pickupRadius = 1.1f;
    public float depositRadius = 1.4f;

    public float enemyNestAggroRadius = 1.6f;
    public float antCollisionRadius = 0.35f;

    public int fightDurationTicks = 60;
    public float antDpsPerTick = 2.5f;
    public float nestDpsPerTick = 1f;
    public float ageDrainPerTick = 0.003f;

    public int wanderTurnIntervalMinTicks = 20;
    public int wanderTurnIntervalMaxTicks = 60;
    public float wanderTurnRadians = 0.55f;

    public float antMaxHp = 100f;
    public float spawnOffsetRadius = 1.2f;

    public void Normalize()
    {
        nestCount = Mathf.Clamp(nestCount, 1, 64);
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

        spawnEveryNTicks = Mathf.Max(1, spawnEveryNTicks);
        maxAntsPerNest = Mathf.Clamp(maxAntsPerNest, 1, 500);
        maxAntsGlobal = Mathf.Clamp(maxAntsGlobal, 1, 2000);

        walkSpeed = Mathf.Max(0.1f, walkSpeed);
        runSpeed = Mathf.Max(walkSpeed, runSpeed);

        foodSenseRadius = Mathf.Max(0.1f, foodSenseRadius);
        pickupRadius = Mathf.Max(0.05f, pickupRadius);
        depositRadius = Mathf.Max(0.05f, depositRadius);

        enemyNestAggroRadius = Mathf.Max(0.1f, enemyNestAggroRadius);
        antCollisionRadius = Mathf.Max(0.05f, antCollisionRadius);

        fightDurationTicks = Mathf.Max(1, fightDurationTicks);
        antDpsPerTick = Mathf.Max(0f, antDpsPerTick);
        nestDpsPerTick = Mathf.Max(0f, nestDpsPerTick);
        ageDrainPerTick = Mathf.Max(0f, ageDrainPerTick);

        wanderTurnIntervalMinTicks = Mathf.Max(1, wanderTurnIntervalMinTicks);
        wanderTurnIntervalMaxTicks = Mathf.Max(wanderTurnIntervalMinTicks, wanderTurnIntervalMaxTicks);
        wanderTurnRadians = Mathf.Clamp(wanderTurnRadians, 0.05f, 3.14159f);

        antMaxHp = Mathf.Max(1f, antMaxHp);
        spawnOffsetRadius = Mathf.Max(0.05f, spawnOffsetRadius);
    }
}

[Serializable]
public class AntColoniesConfig
{
    public int nestCount = 2;
    public int antsPerNest = 12;
    public int maxAntsTotal = 50;

    public AntWorldRecipe worldRecipe = new();

    public void Normalize()
    {
        nestCount = Mathf.Clamp(nestCount, 1, 64);
        antsPerNest = Mathf.Clamp(antsPerNest, 0, 500);
        maxAntsTotal = Mathf.Clamp(maxAntsTotal, 1, 2000);

        worldRecipe ??= new AntWorldRecipe();
        worldRecipe.nestCount = nestCount;
        worldRecipe.maxAntsGlobal = maxAntsTotal;
        worldRecipe.maxAntsPerNest = Mathf.Clamp(worldRecipe.maxAntsPerNest, 1, maxAntsTotal);
        worldRecipe.Normalize();
    }
}
