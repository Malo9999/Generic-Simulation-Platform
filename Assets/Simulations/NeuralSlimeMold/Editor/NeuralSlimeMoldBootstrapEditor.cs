#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NeuralSlimeMoldBootstrap))]
public sealed class NeuralSlimeMoldBootstrapEditor : Editor
{
    SerializedProperty useArenaPreset;
    SerializedProperty selectedArenaPreset;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawArenaPresetSection();

        EditorGUILayout.Space(10);

        DrawDefaultInspectorExceptArena();

        serializedObject.ApplyModifiedProperties();
    }

    void OnEnable()
    {
        useArenaPreset = serializedObject.FindProperty("useArenaPreset");
        selectedArenaPreset = serializedObject.FindProperty("selectedArenaPreset");
    }

    void DrawArenaPresetSection()
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Arena Presets (Readability Pass)", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(useArenaPreset, new GUIContent("Use Arena Preset"));

        if (useArenaPreset.boolValue)
        {
            EditorGUILayout.PropertyField(selectedArenaPreset, new GUIContent("Preset"));

            NeuralSlimeMoldBootstrap bootstrap = (NeuralSlimeMoldBootstrap)target;

            if (GUILayout.Button("Apply Arena Preset"))
            {
                Undo.RecordObject(bootstrap, "Apply Arena Preset");
                bootstrap.ApplyArenaPreset();
                EditorUtility.SetDirty(bootstrap);
            }

            EditorGUILayout.HelpBox(
                "Arena presets generate hub, food nodes, obstacles and corridor bands automatically. " +
                "Disable this to return to manual configuration.",
                MessageType.Info
            );
        }

        EditorGUILayout.EndVertical();
    }

    void DrawDefaultInspectorExceptArena()
    {
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "useArenaPreset",
            "selectedArenaPreset"
        );
    }
}
#endif