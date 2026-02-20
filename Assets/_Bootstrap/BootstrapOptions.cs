using UnityEngine;

[CreateAssetMenu(fileName = "BootstrapOptions", menuName = "GSP/Bootstrap Options")]
public class BootstrapOptions : ScriptableObject
{
    public string simulationId = "MarbleRace";
    public SimulationCatalog simulationCatalog;
    public TextAsset presetJson;
    public SeedPolicy seedPolicy = SeedPolicy.RandomEveryRun;
    public int fixedSeed = 12345;
    public bool allowHotkeySwitch = true;
    public bool showOverlay = true;
    public bool persistSelectionToPreset = false;
}
