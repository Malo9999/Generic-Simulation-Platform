using System;
using UnityEngine;

[GspBootstrap(GspBootstrapKind.Simulation, "Standalone machine pieces lab bootstrap")]
public sealed class MachinePiecesLabBootstrap : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private TextAsset[] pieceSpecs;
    [SerializeField] private TextAsset[] surfaceProfiles;
    [SerializeField] private TextAsset machineRecipe;

    [Header("Debug")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool showDebugGizmos = true;

    private BuiltMachineRuntime built;

    private void Start()
    {
        if (autoStart)
        {
            BuildLab();
        }
    }

    [ContextMenu("Build Lab")]
    public void BuildLab()
    {
        Teardown();

        var lib = MachinePieceJsonLoader.BuildLibraryOrThrow(pieceSpecs ?? Array.Empty<TextAsset>(), surfaceProfiles ?? Array.Empty<TextAsset>());
        var recipe = MachinePieceJsonLoader.LoadFromTextAssetOrThrow<MachineRecipe>(machineRecipe, "MachinePiecesLab recipe");

        var builder = new MachineBuilder();
        built = builder.BuildMachineOrThrow("MachinePiecesLabRuntime", recipe, lib, transform, showDebugGizmos);

        Debug.Log($"[MachinePiecesLab] Built machine '{recipe.id}' with {built.Pieces.Count} pieces.");
    }

    [ContextMenu("Teardown")]
    public void Teardown()
    {
        if (built?.Root != null)
        {
            DestroyImmediate(built.Root);
        }

        built = null;
    }
}
