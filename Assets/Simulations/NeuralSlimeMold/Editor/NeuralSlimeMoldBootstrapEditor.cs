#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(NeuralSlimeMoldBootstrap))]
public sealed class NeuralSlimeMoldBootstrapEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        Draw("autoStart");
        Draw("seed");
        Draw("agentCount");

        Draw("sensorDistance");
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
        Draw("foodNodeCount");
        Draw("spawnFromSeed");
        Draw("manualFoodConfigs");
        Draw("showFoodMarkers");
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
