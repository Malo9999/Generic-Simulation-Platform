using System;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class GranularFlowRunner : MonoBehaviour, ITickableSimulationRunner, IStateSnapshotProvider
{
    private struct GpuParticle
    {
        public Vector2 position;
        public Vector2 velocity;
        public uint colorId;
        public float radius;
    }

    [SerializeField] private ComputeShader particleCompute;
    [SerializeField] private Shader particleRenderShader;

    private ScenarioConfig activeConfig;
    private GranularFlowConfig gfConfig;
    private SplitterTowerMachine machine;
    private IGranularFlowBrain brain;

    private ComputeBuffer particleBuffer;
    private GpuParticle[] particleCpu;
    private Vector4[] palette;

    private Material particleMaterial;
    private Mesh particleMesh;
    private Bounds particleBounds;

    private int simulateKernel = -1;
    private int activeCount;
    private float spawnAccumulator;
    private float elapsed;
    private int throughputWindow;
    private int throughputWindowTicks;
    private int leftBinCount;
    private int rightBinCount;

    private const int Threads = 128;

    public void Initialize(ScenarioConfig config)
    {
        Shutdown();
        activeConfig = config ?? new ScenarioConfig();
        gfConfig = activeConfig.granularFlow ?? new GranularFlowConfig();
        gfConfig.Normalize();

        if (particleCompute == null)
        {
            particleCompute = Resources.Load<ComputeShader>("Simulations/GranularFlow/Presentation/GranularFlowParticles");
#if UNITY_EDITOR
            if (particleCompute == null)
            {
                particleCompute = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Simulations/GranularFlow/Presentation/GranularFlowParticles.compute");
            }
#endif
        }

        if (particleRenderShader == null)
        {
            particleRenderShader = Shader.Find("Simulations/GranularFlow/Particles");
        }

        if (particleCompute == null || particleRenderShader == null)
        {
            Debug.LogError("GranularFlowRunner: Missing compute shader or particle shader.");
            return;
        }

        machine = new SplitterTowerMachine(transform);
        brain = new GranularFlowRuleBrain();

        particleCpu = new GpuParticle[gfConfig.particles.maxParticles];
        for (var i = 0; i < particleCpu.Length; i++)
        {
            particleCpu[i].position = new Vector2(1000f, 1000f);
            particleCpu[i].velocity = Vector2.zero;
            particleCpu[i].colorId = 0;
            particleCpu[i].radius = gfConfig.particles.radius;
        }

        particleBuffer = new ComputeBuffer(gfConfig.particles.maxParticles, sizeof(float) * 6);
        particleBuffer.SetData(particleCpu);

        simulateKernel = particleCompute != null ? particleCompute.FindKernel("Simulate") : -1;

        palette = new Vector4[gfConfig.particles.palette.Length];
        for (var i = 0; i < palette.Length; i++)
        {
            var c = gfConfig.particles.palette[i];
            palette[i] = new Vector4(c.r, c.g, c.b, c.a);
        }

        particleMaterial = new Material(particleRenderShader);
        particleMaterial.SetBuffer("_Particles", particleBuffer);
        particleMaterial.SetVectorArray("_Palette", palette);
        particleMaterial.SetInt("_PaletteCount", palette.Length);

        particleMesh = BuildQuad();
        particleBounds = new Bounds(Vector3.zero, new Vector3(80f, 80f, 10f));

        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 9.5f;
            cam.transform.position = new Vector3(0f, 0f, -20f);
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.04f);
        }

        EventBusService.Global?.Publish("granular_flow.init", new
        {
            simulationId = activeConfig.simulationId,
            scenario = activeConfig.scenarioName,
            machineArchetype = gfConfig.machineArchetype,
            particleMax = gfConfig.particles.maxParticles,
            paletteCount = palette.Length
        });
    }

    public void Tick(int tickIndex, float dt)
    {
        if (particleBuffer == null || particleCompute == null || simulateKernel < 0)
        {
            return;
        }

        elapsed += dt;
        Spawn(dt, tickIndex);

        var sensors = BuildSensors(dt);
        var decision = brain.Decide(new GranularFlowBrainContext(sensors, gfConfig.ruleBrain, machine.GateOpen, machine.FlapState, elapsed), dt);
        machine.ApplyActuators(decision.GateTargetOpen, decision.FlapTarget, gfConfig.machine, dt);

        DispatchSimulation(dt, machine.GateOpen, machine.FlapState);

        Graphics.DrawMeshInstancedProcedural(particleMesh, 0, particleMaterial, particleBounds, Mathf.Max(1, activeCount));

        if (tickIndex % 120 == 0)
        {
            EventBusService.Global?.Publish("granular_flow.status", new
            {
                tick = tickIndex,
                activeCount,
                machine.GateOpen,
                machine.FlapState,
                sensors.leftBinFill,
                sensors.rightBinFill,
                sensors.throatJamEstimate
            });
        }
    }

    public void Shutdown()
    {
        if (particleBuffer != null)
        {
            particleBuffer.Release();
            particleBuffer = null;
        }

        if (particleMaterial != null)
        {
            Destroy(particleMaterial);
            particleMaterial = null;
        }

        if (particleMesh != null)
        {
            Destroy(particleMesh);
            particleMesh = null;
        }
    }

    public object CaptureState()
    {
        return new
        {
            simulationId = activeConfig?.simulationId ?? "GranularFlow",
            scenario = activeConfig?.scenarioName ?? "unknown",
            seed = activeConfig?.seed ?? 0,
            machineArchetype = gfConfig?.machineArchetype ?? "SplitterTower",
            activeCount,
            gateOpen = machine?.GateOpen ?? 0f,
            flapState = machine?.FlapState ?? 0f,
            leftBinCount,
            rightBinCount,
            particleRadius = gfConfig?.particles?.radius ?? 0f,
            ruleBrain = gfConfig?.ruleBrain
        };
    }

    private void Spawn(float dt, int tick)
    {
        spawnAccumulator += gfConfig.machine.feederRatePerSecond * dt;
        var spawnCount = Mathf.Min((int)spawnAccumulator, gfConfig.particles.maxParticles - activeCount);
        if (spawnCount <= 0)
        {
            return;
        }

        spawnAccumulator -= spawnCount;
        var rand = RngService.Fork($"GF:SPAWN:{tick}:{activeCount}");
        for (var i = 0; i < spawnCount; i++)
        {
            var idx = activeCount + i;
            var px = rand.Range(-0.7f, 0.7f) * gfConfig.particles.feederJitter;
            var py = 7.6f + rand.Range(-0.15f, 0.15f);
            particleCpu[idx].position = new Vector2(px, py);
            particleCpu[idx].velocity = new Vector2(rand.Range(-0.2f, 0.2f), rand.Range(-0.2f, 0.2f));
            particleCpu[idx].colorId = (uint)rand.Range(0, Mathf.Max(1, palette.Length));
            particleCpu[idx].radius = gfConfig.particles.radius;
        }

        particleBuffer.SetData(particleCpu, activeCount, activeCount, spawnCount);
        activeCount += spawnCount;
    }

    private void DispatchSimulation(float dt, float gateOpen, float flapState)
    {
        particleCompute.SetInt("_ActiveCount", activeCount);
        particleCompute.SetFloat("_Dt", dt);
        particleCompute.SetFloat("_Gravity", gfConfig.particles.gravity);
        particleCompute.SetFloat("_Damping", gfConfig.particles.damping);
        particleCompute.SetFloat("_Bounce", gfConfig.particles.collisionBounce);
        particleCompute.SetFloat("_GateOpen", gateOpen * gfConfig.machine.gateMaxOpen);
        particleCompute.SetFloat("_FlapState", flapState);
        particleCompute.SetBuffer(simulateKernel, "_Particles", particleBuffer);

        var groups = Mathf.CeilToInt(activeCount / (float)Threads);
        if (groups > 0)
        {
            particleCompute.Dispatch(simulateKernel, groups, 1, 1);
        }
    }

    private GranularFlowSensors BuildSensors(float dt)
    {
        if (activeCount == 0)
        {
            return default;
        }

        particleBuffer.GetData(particleCpu, 0, 0, activeCount);
        var upper = 0;
        var throat = 0;
        var lower = 0;
        leftBinCount = 0;
        rightBinCount = 0;
        var dominant = new int[Mathf.Max(1, palette.Length)];

        for (var i = 0; i < activeCount; i++)
        {
            var p = particleCpu[i].position;
            if (machine.UpperChamber.Contains(p)) upper++;
            if (machine.Throat.Contains(p)) throat++;
            if (machine.LowerChamber.Contains(p))
            {
                lower++;
                var ci = Mathf.Clamp((int)particleCpu[i].colorId, 0, dominant.Length - 1);
                dominant[ci]++;
            }

            if (machine.LeftBin.Contains(p))
            {
                leftBinCount++;
                throughputWindow++;
            }
            else if (machine.RightBin.Contains(p))
            {
                rightBinCount++;
                throughputWindow++;
            }
        }

        throughputWindowTicks++;
        var maxId = 0;
        var maxCount = -1;
        for (var i = 0; i < dominant.Length; i++)
        {
            if (dominant[i] > maxCount)
            {
                maxCount = dominant[i];
                maxId = i;
            }
        }

        if (throughputWindowTicks > 30)
        {
            throughputWindowTicks = 0;
            throughputWindow = Mathf.RoundToInt(throughputWindow * 0.35f);
        }

        var throughputPerSecond = dt > 0f ? throughputWindow / Mathf.Max(0.001f, dt * 30f) : 0f;
        return GranularFlowSensors.FromParticleStats(
            upper,
            throat,
            lower,
            leftBinCount,
            rightBinCount,
            Mathf.RoundToInt(throughputPerSecond),
            maxId,
            3200,
            900,
            2600,
            palette.Length);
    }

    private static Mesh BuildQuad()
    {
        var mesh = new Mesh { name = "GF_Quad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }
}
