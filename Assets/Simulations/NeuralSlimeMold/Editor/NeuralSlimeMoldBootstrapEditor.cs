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
        Draw("foodCapacity");
        Draw("consumeRadius");
        Draw("consumeRate");
        Draw("trailDecayPerSecond");
    }

    private void DrawExperimentalControls()
    {
        Draw("foodNodeCount");
        Draw("foodStrength");
        Draw("foodCapacity");
        Draw("consumeRadius");
        Draw("consumeRate");
        Draw("trailDecayPerSecond");

        Draw("worldPreset");
        Draw("seed");
        Draw("showFoodMarkers");
        Draw("showFoodGizmos");
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
