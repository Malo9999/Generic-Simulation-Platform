using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ExtraSimulationBinding
{
    public string simulationId;
    public SimSettingsBase settings;
    public SimVisualSettings visual;
}

[CreateAssetMenu(fileName = "BootstrapOptions", menuName = "GSP/Bootstrap Options")]
public class BootstrapOptions : ScriptableObject
{
    public string simulationId = "MarbleRace";
    public SimulationCatalog simulationCatalog;
    public TextAsset presetJson;
    public AntColoniesSimSettings antColoniesSettings;
    public MarbleRaceSimSettings marbleRaceSettings;
    public FantasySportSimSettings fantasySportSettings;
    public RaceCarSimSettings raceCarSettings;
    public SimVisualSettings antColoniesVisual;
    public SimVisualSettings marbleRaceVisual;
    public SimVisualSettings fantasySportVisual;
    public SimVisualSettings raceCarVisual;
    [SerializeField] public List<ExtraSimulationBinding> extraSimulations = new();
    public SeedPolicy seedPolicy = SeedPolicy.RandomEveryRun;
    public int fixedSeed = 12345;
    public bool allowHotkeySwitch = true;
    public bool showOverlay = true;
    public bool persistSelectionToPreset = false;
}
