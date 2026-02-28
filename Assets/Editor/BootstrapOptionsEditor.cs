#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BootstrapOptions))]
public class BootstrapOptionsInspector : Editor
{
    private SerializedProperty simulationIdProp;
    private SerializedProperty simulationCatalogProp;
    private SerializedProperty presetJsonProp;
    private SerializedProperty antColoniesSettingsProp;
    private SerializedProperty marbleRaceSettingsProp;
    private SerializedProperty fantasySportSettingsProp;
    private SerializedProperty raceCarSettingsProp;
    private SerializedProperty antColoniesVisualProp;
    private SerializedProperty marbleRaceVisualProp;
    private SerializedProperty fantasySportVisualProp;
    private SerializedProperty raceCarVisualProp;
    private SerializedProperty seedPolicyProp;
    private SerializedProperty fixedSeedProp;
    private SerializedProperty allowHotkeySwitchProp;
    private SerializedProperty showOverlayProp;
    private SerializedProperty persistSelectionToPresetProp;

    private void OnEnable()
    {
        simulationIdProp = serializedObject.FindProperty("simulationId");
        simulationCatalogProp = serializedObject.FindProperty("simulationCatalog");
        presetJsonProp = serializedObject.FindProperty("presetJson");
        antColoniesSettingsProp = serializedObject.FindProperty("antColoniesSettings");
        marbleRaceSettingsProp = serializedObject.FindProperty("marbleRaceSettings");
        fantasySportSettingsProp = serializedObject.FindProperty("fantasySportSettings");
        raceCarSettingsProp = serializedObject.FindProperty("raceCarSettings");
        antColoniesVisualProp = serializedObject.FindProperty("antColoniesVisual");
        marbleRaceVisualProp = serializedObject.FindProperty("marbleRaceVisual");
        fantasySportVisualProp = serializedObject.FindProperty("fantasySportVisual");
        raceCarVisualProp = serializedObject.FindProperty("raceCarVisual");
        seedPolicyProp = serializedObject.FindProperty("seedPolicy");
        fixedSeedProp = serializedObject.FindProperty("fixedSeed");
        allowHotkeySwitchProp = serializedObject.FindProperty("allowHotkeySwitch");
        showOverlayProp = serializedObject.FindProperty("showOverlay");
        persistSelectionToPresetProp = serializedObject.FindProperty("persistSelectionToPreset");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(simulationCatalogProp);
        DrawSimulationIdField();

        EditorGUILayout.PropertyField(presetJsonProp);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Per-Simulation Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(antColoniesSettingsProp);
        EditorGUILayout.PropertyField(marbleRaceSettingsProp);
        EditorGUILayout.PropertyField(fantasySportSettingsProp);
        EditorGUILayout.PropertyField(raceCarSettingsProp);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Per-Simulation Visual Baseline", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(antColoniesVisualProp);
        EditorGUILayout.PropertyField(marbleRaceVisualProp);
        EditorGUILayout.PropertyField(fantasySportVisualProp);
        EditorGUILayout.PropertyField(raceCarVisualProp);

        EditorGUILayout.PropertyField(seedPolicyProp);
        EditorGUILayout.PropertyField(fixedSeedProp);
        EditorGUILayout.PropertyField(allowHotkeySwitchProp);
        EditorGUILayout.PropertyField(showOverlayProp);
        EditorGUILayout.PropertyField(persistSelectionToPresetProp);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSimulationIdField()
    {
        var catalog = simulationCatalogProp.objectReferenceValue as SimulationCatalog;
        if (catalog == null)
        {
            EditorGUILayout.HelpBox("Assign a SimulationCatalog to choose a simulation from a dropdown.", MessageType.Warning);
            EditorGUILayout.PropertyField(simulationIdProp);
            return;
        }

        var simulationIds = catalog.Simulations
            .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.simulationId))
            .Select(entry => entry.simulationId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (simulationIds.Length == 0)
        {
            EditorGUILayout.HelpBox("SimulationCatalog has no simulation entries. Enter a simulationId manually.", MessageType.Warning);
            EditorGUILayout.PropertyField(simulationIdProp);
            return;
        }

        var currentSimulationId = simulationIdProp.stringValue;
        var selectedIndex = Array.FindIndex(simulationIds, id =>
            string.Equals(id, currentSimulationId, StringComparison.OrdinalIgnoreCase));

        if (selectedIndex < 0 && !string.IsNullOrWhiteSpace(currentSimulationId))
        {
            EditorGUILayout.HelpBox($"Current simulationId '{currentSimulationId}' is not in the SimulationCatalog.", MessageType.Info);
        }

        selectedIndex = Mathf.Max(0, selectedIndex);
        var newIndex = EditorGUILayout.Popup("Simulation Id", selectedIndex, simulationIds);
        if (newIndex >= 0 && newIndex < simulationIds.Length)
        {
            simulationIdProp.stringValue = simulationIds[newIndex];
        }
    }
}
#endif
