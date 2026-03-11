#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NeuralSlimeMoldBootstrap))]
public sealed class NeuralSlimeMoldBootstrapEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawSection("Simulation", new[]
        {
            "autoStart",
            "seed",
            "agentCount",
            "mapSize",
            "trailResolution",
            "trailDecayPerSecond",
            "trailDiffusion"
        });

        DrawSection("Quality / Performance", new[]
        {
            "qualityPreset",
            "overridePerformanceOptions",
            "overrideFieldStepInterval",
            "overrideFieldTextureRefreshInterval",
            "overrideMaxVisibleAgents"
        });

        DrawSection("Stress Test", new[]
        {
            "useStressTestProfile",
            "stressAgentCount",
            "stressTrailResolution"
        });

        DrawSection("Agent Motion", new[]
        {
            "sensorAngleDegrees",
            "sensorDistance",
            "speed",
            "turnRateDegrees",
            "depositAmount",
            "explorationTurnNoise"
        });

        DrawSection("Food", new[]
        {
            "foodNodeCount",
            "foodStrength",
            "foodCapacity",
            "consumeRadius",
            "consumeRate",
            "allowFoodRegrowth",
            "foodReactivationDelay",
            "regrowRate",
            "foodReactivationThreshold",
            "spawnFromSeed",
            "manualFoodConfigs"
        });

        DrawSection("Colony Hub", new[]
        {
            "useColonyHub",
            "colonyHub",
            "colonyHubRadius",
            "returnToHubWeight",
            "returnTrailBlend",
            "returnDepositBoost",
            "successfulReturnDepositBurst",
            "hubInfluenceRadius"
        });

        DrawSection("Loop Pruning", new[]
        {
            "nonUsefulLoopPruneStrength",
            "nonUsefulLoopTrailThreshold",
            "nonUsefulLoopCurvatureThreshold"
        });

        DrawSection("Network Maintenance", new[]
        {
            "bridgeReinforcementWeight",
            "hubOrbitSuppression",
            "staleCorridorDecayBoost",
            "connectorSearchRadius",
            "connectorSteerWeight",
            "hubTangentialPenalty",
            "connectorCorridorWidth",
            "returnOrbitDepositPenalty"
        });

        DrawSection("Branch Remodeling", new[]
        {
            "branchSpawnChance",
            "branchSpawnTrailThreshold",
            "branchPromotionThreshold",
            "branchRetractionBoost",
            "trunkStabilityBoost",
            "duplicateTubeSuppressionRadius"
        });

        DrawSection("World Obstacles", new[]
        {
            "useWorldObstacles",
            "worldObstacles",
            "corridorBands",
            "obstacleAvoidanceStrength",
            "obstaclePadding"
        });

        DrawSection("Rendering", new[]
        {
            "showFoodMarkers",
            "emphasizePrimaryTubes",
            "showExplorationBranches",
            "showFoodStateMarkers",
            "showActivityFocus",
            "tubeExposure",
            "staleTrailFade",
            "branchAlphaBias"
        });

        DrawSection("Palette", new[]
        {
            "useGlowAgentShape",
            "useFieldBlobOverlay",
            "backgroundColor"
        });

        DrawSection("Camera Framing", new[]
        {
            "autoFrameCamera",
            "adaptiveCameraFraming",
            "cameraPadding",
            "cameraFollowSmooth",
            "cameraZoomSmooth",
            "minimumCameraSize",
            "cameraLookAheadToActivity",
            "cameraDeadZoneRadius"
        });

        EditorGUILayout.Space(10f);
        DrawRuntimeButtons();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSection(string title, string[] propertyNames)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        for (var i = 0; i < propertyNames.Length; i++)
        {
            Draw(propertyNames[i]);
        }
    }

    private void DrawRuntimeButtons()
    {
        var bootstrap = target as NeuralSlimeMoldBootstrap;
        if (bootstrap == null)
        {
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Start / Reset Simulation"))
            {
                bootstrap.StartSimulation();
                EditorUtility.SetDirty(bootstrap);
            }

            if (GUILayout.Button("Reseed"))
            {
                bootstrap.Reseed();
                EditorUtility.SetDirty(bootstrap);
            }

            if (GUILayout.Button("Apply Stress Profile"))
            {
                bootstrap.ApplyStressTestProfile();
                EditorUtility.SetDirty(bootstrap);
            }
        }
    }

    private void Draw(string propertyName)
    {
        var prop = serializedObject.FindProperty(propertyName);
        if (prop != null)
        {
            EditorGUILayout.PropertyField(prop, true);
        }
    }
}
#endif