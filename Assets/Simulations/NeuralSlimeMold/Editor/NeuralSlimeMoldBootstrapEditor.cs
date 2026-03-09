#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(NeuralSlimeMoldBootstrap))]
public sealed class NeuralSlimeMoldBootstrapEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Simulation", EditorStyles.boldLabel);
        Draw("autoStart");
        Draw("seed");
        Draw("agentCount");
        Draw("sensorDistance");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Agent Motion", EditorStyles.boldLabel);
        Draw("sensorAngleDegrees");
        Draw("depositAmount");
        Draw("trailDecayPerSecond");
        Draw("foodStrength");
        Draw("foodCapacity");
        Draw("consumeRadius");
        Draw("consumeRate");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Advanced", EditorStyles.boldLabel);
        Draw("mapSize");
        Draw("trailResolution");
        Draw("trailDiffusion");
        Draw("speed");
        Draw("turnRateDegrees");
        Draw("explorationTurnNoise");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Food", EditorStyles.boldLabel);
        Draw("foodNodeCount");
        Draw("allowFoodRegrowth");
        Draw("foodReactivationDelay");
        Draw("regrowRate");
        Draw("foodReactivationThreshold");
        Draw("spawnFromSeed");
        Draw("manualFoodConfigs");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
        Draw("showFoodMarkers");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
        Draw("useGlowAgentShape");
        Draw("useFieldBlobOverlay");
        Draw("backgroundColor");
        Draw("autoFrameCamera");
        Draw("cameraPadding");

        serializedObject.ApplyModifiedProperties();
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