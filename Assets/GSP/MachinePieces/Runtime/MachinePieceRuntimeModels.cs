using System.Collections.Generic;
using UnityEngine;

public sealed class MachinePieceLibrary
{
    public readonly Dictionary<string, PieceSpec> PieceSpecs = new(System.StringComparer.Ordinal);
    public readonly Dictionary<string, SurfaceProfile> SurfaceProfiles = new(System.StringComparer.Ordinal);
}

public sealed class BuiltPieceRuntime
{
    public string InstanceId;
    public string PieceId;
    public PieceSpec Spec;
    public PieceInstance Instance;
    public SurfaceProfile Surface;
    public GameObject Root;
    public Collider2D Collider;
    public readonly Dictionary<string, Transform> Anchors = new(System.StringComparer.Ordinal);
    public readonly Dictionary<string, string> RuntimeState = new(System.StringComparer.Ordinal);
}

public sealed class BuiltMachineRuntime
{
    public string MachineId;
    public MachineRecipe Recipe;
    public GameObject Root;
    public readonly Dictionary<string, BuiltPieceRuntime> Pieces = new(System.StringComparer.Ordinal);
}
