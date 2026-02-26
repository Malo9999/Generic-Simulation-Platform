using System.Collections.Generic;
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

        GUILayout.BeginArea(new Rect(12f, 12f, 430f, 340f), GUI.skin.box);
        GUILayout.Label($"Simulation: {bootstrapper.CurrentSimulationId}");
        GUILayout.Label($"Seed: {bootstrapper.CurrentSeed}");
        GUILayout.Label($"Preset: {bootstrapper.CurrentPresetSource}");
        GUILayout.Label($"Tick: {bootstrapper.TickCount}");
        GUILayout.Label($"FPS: {bootstrapper.CurrentFps:F1}");
        GUILayout.Label($"ContentPack: {bootstrapper.CurrentContentPackName}");

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

        var artSelector = GetComponent<ArtModeSelector>();
        var modes = artSelector != null
            ? artSelector.GetAvailableModes()
            : new List<ArtMode> { ArtMode.Simple, ArtMode.Flat, ArtMode.IsoL1_4Dir };
        if (modes == null || modes.Count == 0)
        {
            modes = new List<ArtMode> { ArtMode.Simple, ArtMode.Flat, ArtMode.IsoL1_4Dir };
        }

        GUILayout.Space(8f);
        GUILayout.Label($"Art: {bootstrapper.CurrentArtMode}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Art Prev", GUILayout.Height(24f)))
        {
            var selected = CycleMode(modes, bootstrapper.CurrentArtMode, -1);
            bootstrapper.SetArtModeForCurrent(selected);
        }

        if (GUILayout.Button("Art Next", GUILayout.Height(24f)))
        {
            var selected = CycleMode(modes, bootstrapper.CurrentArtMode, 1);
            bootstrapper.SetArtModeForCurrent(selected);
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button(bootstrapper.CurrentUsePlaceholders ? "Placeholders: ON" : "Placeholders: OFF", GUILayout.Height(24f)))
        {
            bootstrapper.TogglePlaceholdersForCurrent();
        }

        if (GUILayout.Button($"DebugMode: {bootstrapper.CurrentDebugMode}", GUILayout.Height(24f)))
        {
            bootstrapper.CycleDebugModeForCurrent();
        }
        GUILayout.EndArea();
    }

    private static ArtMode CycleMode(List<ArtMode> modes, ArtMode currentMode, int direction)
    {
        if (modes == null || modes.Count == 0)
        {
            return currentMode;
        }

        var currentIndex = modes.IndexOf(currentMode);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = (currentIndex + direction + modes.Count) % modes.Count;
        return modes[nextIndex];
    }
}
