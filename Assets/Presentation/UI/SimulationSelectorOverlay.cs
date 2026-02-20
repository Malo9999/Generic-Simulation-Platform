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

        GUILayout.BeginArea(new Rect(12f, 12f, 430f, 210f), GUI.skin.box);
        GUILayout.Label($"Simulation: {bootstrapper.CurrentSimulationId}");
        GUILayout.Label($"Seed: {bootstrapper.CurrentSeed}");
        GUILayout.Label($"Preset: {bootstrapper.CurrentPresetSource}");
        GUILayout.Label($"Tick: {bootstrapper.TickCount}");
        GUILayout.Label($"FPS: {bootstrapper.CurrentFps:F1}");

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(bootstrapper.IsPaused ? "Resume" : "Pause", GUILayout.Height(28f)))
        {
            bootstrapper.PauseOrResume();
        }

        GUI.enabled = bootstrapper.IsPaused;
        if (GUILayout.Button("Step", GUILayout.Height(28f)))
        {
            bootstrapper.StepSimulation();
        }

        GUI.enabled = true;
        if (GUILayout.Button("Reset", GUILayout.Height(28f)))
        {
            bootstrapper.ResetSimulation();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Prev", GUILayout.Height(28f)))
        {
            bootstrapper.SwitchToPreviousSimulation();
        }

        if (GUILayout.Button("Next", GUILayout.Height(28f)))
        {
            bootstrapper.SwitchToNextSimulation();
        }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
}
