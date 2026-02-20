using UnityEngine;

public class SimulationSelectorOverlay : MonoBehaviour
{
    private Bootstrapper bootstrapper;

    private void Awake()
    {
        bootstrapper = GetComponent<Bootstrapper>();
    }

    private void OnGUI()
    {
        if (bootstrapper == null || !bootstrapper.ShowOverlay)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(12f, 12f, 420f, 120f), GUI.skin.box);
        GUILayout.Label($"Simulation: {bootstrapper.CurrentSimulationId}");
        GUILayout.Label($"Seed: {bootstrapper.CurrentSeed}");
        GUILayout.Label($"Preset: {bootstrapper.CurrentPresetSource}");
        GUILayout.Label("Switch: F1 AntColonies | F2 MarbleRace | F3 RaceCar | F4 FantasySport");
        GUILayout.EndArea();
    }
}
