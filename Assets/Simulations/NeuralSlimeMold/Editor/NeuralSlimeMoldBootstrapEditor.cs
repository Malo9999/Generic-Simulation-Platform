#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NeuralSlimeMoldBootstrap))]
public sealed class NeuralSlimeMoldBootstrapEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawCoreControls();

        var mode = serializedObject.FindProperty("simulationMode");
        if (mode.enumValueIndex == (int)NeuralSlimeMoldBootstrap.SimulationMode.Experimental)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Experimental Controls", EditorStyles.boldLabel);
            DrawExperimentalControls();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCoreControls()
    {
        Draw("simulationMode");
        Draw("autoStart");
        Draw("seed");
        Draw("agentCount");
        Draw("mapSize");
        Draw("trailResolution");
        Draw("trailDiffusion");
        Draw("trailDecayPerSecond");
        Draw("sensorDistance");
        Draw("sensorAngleDegrees");
        Draw("turnRateDegrees");
        Draw("depositAmount");
        Draw("enableStaticFood");
        Draw("foodStrength");
    }

    private void DrawExperimentalControls()
    {
        Draw("speed");
        Draw("trailFollowWeight");
        Draw("foodAttractionWeight");
        Draw("foodSenseRadius");
        Draw("foodTurnBias");
        Draw("turnNoise");
        Draw("localLoopSuppression");
        Draw("depositNearFoodMultiplier");
        Draw("pathPersistenceBias");

        Draw("foodPulseEnabled");
        Draw("foodPulsePeriod");
        Draw("foodPulseStrength");
        Draw("localTrailScrubEnabled");
        Draw("localTrailScrubThreshold");
        Draw("localTrailScrubAmount");

        Draw("foodInfluenceDebug");
        Draw("showFoodMarkers");
        Draw("showFoodGizmos");
        Draw("debugFoodLogging");
        Draw("debugFoodPreset");
        Draw("strongFoodDebugMode");
        Draw("strongFoodStrengthMultiplier");

        Draw("boundaryMode");
        Draw("wallMargin");
        Draw("worldPreset");
        Draw("useCustomWorldOverrides");
        Draw("enableFoodNodes");
        Draw("indirectFoodBias");
        Draw("foodNodeCount");
        Draw("foodRadius");
        Draw("spawnFromSeed");
        Draw("allowFoodRegrowth");
        Draw("foodCapacity");
        Draw("depletionRate");
        Draw("regrowRate");
        Draw("depletedFoodStrengthMultiplier");
        Draw("foodReactivationDelay");
        Draw("foodReactivationThreshold");
        Draw("migrationRestlessness");
        Draw("manualFoodNodes");
        Draw("manualFoodConfigs");

        Draw("enableObstacles");
        Draw("manualObstacles");
        Draw("obstacleThickness");
        Draw("corridorGapSize");
        Draw("obstacleCoverage");
        Draw("presetScale");
        Draw("smallBlockerCount");

        Draw("useGlowAgentShape");
        Draw("useFieldBlobOverlay");
        Draw("backgroundColor");
        Draw("autoFrameCamera");
        Draw("cameraPadding");
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
