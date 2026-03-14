using System;
using System.Linq;
using UnityEngine;

[GspBootstrap(GspBootstrapKind.Simulation, "Standalone piece mechanics verification bootstrap")]
public sealed class PieceTestSimBootstrap : MonoBehaviour
{
    public enum EnvironmentPreset
    {
        EarthLight,
        LowGravity
    }

    [SerializeField] private TextAsset[] pieceSpecs;
    [SerializeField] private TextAsset[] surfaceProfiles;
    [SerializeField] private bool includeSharedMachinePieceAssets = true;
    [SerializeField] private TextAsset machineRecipe;
    [SerializeField] private bool autoStart = true;
    [SerializeField] private EnvironmentPreset environmentPreset = EnvironmentPreset.EarthLight;

    private BuiltMachineRuntime built;
    private MachinePieceLibrary library;

    private void Start()
    {
        if (autoStart)
        {
            BuildAndRun();
        }
    }

    [ContextMenu("Build And Run")]
    public void BuildAndRun()
    {
        Cleanup();
        library = MachinePieceJsonLoader.BuildLibraryOrThrow(
            pieceSpecs ?? Array.Empty<TextAsset>(),
            surfaceProfiles ?? Array.Empty<TextAsset>(),
            includeSharedAssets: includeSharedMachinePieceAssets,
            diagnosticsPrefix: "PieceTestSim");
        var recipe = MachinePieceJsonLoader.LoadFromTextAssetOrThrow<MachineRecipe>(machineRecipe, "PieceTestSim recipe");

        var builder = new MachineBuilder();
        built = builder.BuildMachineOrThrow("PieceTestMachine", recipe, library, transform, true);

        WireMainCameraControls();

        var gravityY = environmentPreset == EnvironmentPreset.EarthLight ? -9.81f : -2.5f;
        Physics2D.gravity = new Vector2(0f, gravityY);

        SpawnProbe(new Vector2(0f, 5f));
        RunMechanicsSanity();
    }

    private static void WireMainCameraControls()
    {
        var arenaBoundsObject = GameObject.Find("ArenaBounds");
        var arenaBoundsCollider = arenaBoundsObject != null ? arenaBoundsObject.GetComponent<Collider2D>() : null;
        var arenaCameraPolicy = MainCameraRuntimeSetup.EnsureArenaCameraRig();

        if (arenaCameraPolicy != null)
        {
            arenaCameraPolicy.ApplySimCameraProfile("PieceTestSim");

            if (arenaBoundsCollider != null)
            {
                arenaCameraPolicy.BindArenaBounds(arenaBoundsCollider, fitToBounds: true);
            }
            else
            {
                arenaCameraPolicy.FitToBounds();
            }
        }

        var followController = FindAnyObjectByType<CameraFollowController>();
        if (followController != null)
        {
            followController.arenaCameraPolicy = arenaCameraPolicy;
        }
    }

    [ContextMenu("Switch Environment")]
    public void SwitchEnvironment()
    {
        environmentPreset = environmentPreset == EnvironmentPreset.EarthLight
            ? EnvironmentPreset.LowGravity
            : EnvironmentPreset.EarthLight;
        BuildAndRun();
    }

    private void SpawnProbe(Vector2 position)
    {
        var go = CreateProbeGameObject(position);
        EnsureProbePhysicsComponents(go);

        // Add the behaviour after required physics components exist so Awake can safely configure them.
        var probe = go.AddComponent<PieceTestSimProbe>();
        probe.Configure(environmentPreset == EnvironmentPreset.EarthLight ? 1f : 0.3f);

        Debug.Log($"[PieceTestSim] Probe spawn path: BuildAndRun -> SpawnProbe -> EnsureProbePhysicsComponents -> Add PieceTestSimProbe ({go.name}).");
    }

    private GameObject CreateProbeGameObject(Vector2 position)
    {
        var go = new GameObject($"Probe_{environmentPreset}");
        go.transform.position = position;
        go.transform.SetParent(transform, true);
        return go;
    }

    private static void EnsureProbePhysicsComponents(GameObject go)
    {
        if (go.GetComponent<Rigidbody2D>() == null)
            go.AddComponent<Rigidbody2D>();

        if (go.GetComponent<CircleCollider2D>() == null)
            go.AddComponent<CircleCollider2D>();

        if (go.GetComponent<SpriteRenderer>() == null)
            go.AddComponent<SpriteRenderer>();
    }

    private void RunMechanicsSanity()
    {
        var gate = built.Pieces.Values.FirstOrDefault(p => p.Spec.pieceType == "Gate");
        var flap = built.Pieces.Values.FirstOrDefault(p => p.Spec.pieceType == "Flap");
        var bin = built.Pieces.Values.FirstOrDefault(p => p.Spec.pieceType == "Bin");

        if (gate?.Root != null)
        {
            var driver = gate.Root.GetComponent<MachinePieceMechanicsDriver>();
            driver?.SetGateOpen01(0.65f);
            Debug.Log("[PieceTestSim] Gate open/close mechanics applied to collider.");
        }

        if (flap?.Root != null)
        {
            var driver = flap.Root.GetComponent<MachinePieceMechanicsDriver>();
            driver?.SetFlapAngle(28f);
            Debug.Log("[PieceTestSim] Flap angle mechanics applied to collision transform.");
        }

        if (bin?.Root != null)
        {
            Debug.Log("[PieceTestSim] Bin capture zone exposed via debug gizmo radius.");
        }
    }

    [ContextMenu("Cleanup")]
    public void Cleanup()
    {
        if (built?.Root != null)
        {
            DestroyImmediate(built.Root);
        }

        var probes = GetComponentsInChildren<PieceTestSimProbe>(true);
        foreach (var probe in probes)
        {
            if (probe != null)
            {
                DestroyImmediate(probe.gameObject);
            }
        }

        built = null;
    }
}
