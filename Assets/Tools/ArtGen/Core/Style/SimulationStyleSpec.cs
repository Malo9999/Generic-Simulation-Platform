using UnityEngine;

[CreateAssetMenu(fileName = "SimulationStyleSpec", menuName = "GSP/Art/Simulation Style Spec")]
public sealed class SimulationStyleSpec : ScriptableObject
{
    [Header("Simulation")]
    public string simulationId = "AntColonies";
    public string styleId = "ANT_PIXEL_V1";

    [Header("Sheet Layout")]
    public int frameSize = 64;
    public int sheetCols = 5;
    public int sheetRows = 2;
    public int paddingPx = 2;

    [Header("Style")]
    public string[] paletteHex;
    public string outlineHex;
    public string shadingRule = "flat + 1 highlight + 1 shadow";
    [TextArea(4, 12)]
    public string promptPrefix;
}
