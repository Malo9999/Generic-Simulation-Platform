using System;
using UnityEngine;

public sealed class AntDeterminismSignatureProbe : MonoBehaviour
{
    [SerializeField] private string simId = "AntColonies";
    [SerializeField] private int seed = 1337;
    [SerializeField] private int arenaWidth = 64;
    [SerializeField] private int arenaHeight = 64;

    [ContextMenu("Print Ant Determinism Signature")]
    public void PrintSignature()
    {
        var signatureA = BuildSignature(simId, seed);
        var signatureB = BuildSignature(simId, seed);
        var signatureC = BuildSignature(simId, seed + 1);

        Debug.Log(
            $"{nameof(AntDeterminismSignatureProbe)} simId={simId} seed={seed} " +
            $"sameSeedMatch={string.Equals(signatureA, signatureB, StringComparison.Ordinal)}\n" +
            $"A={signatureA}\nB={signatureB}\nNextSeed={signatureC}");
    }

    public string BuildSignature(string simulationId, int rootSeed)
    {
        var config = new ScenarioConfig
        {
            simulationId = simulationId,
            activeSimulation = simulationId,
            seed = rootSeed,
            world = new WorldConfig
            {
                arenaWidth = arenaWidth,
                arenaHeight = arenaHeight
            },
            antColonies = new AntColoniesConfig
            {
                worldRecipe = new AntWorldRecipe()
            }
        };

        RngService.SetGlobalSeed(rootSeed);
        var worldState = AntWorldGenerator.Generate(config);

        unchecked
        {
            uint hash = 2166136261u;
            for (var i = 0; i < worldState.obstacles.Count; i++)
            {
                var obstacle = worldState.obstacles[i];
                hash = HashFloat(hash, obstacle.position.x);
                hash = HashFloat(hash, obstacle.position.y);
                hash = HashFloat(hash, obstacle.radius);
            }

            for (var i = 0; i < worldState.decor.Count; i++)
            {
                var decor = worldState.decor[i];
                hash = HashFloat(hash, decor.position.x);
                hash = HashFloat(hash, decor.position.y);
                hash = HashString(hash, decor.spriteId);
            }

            for (var i = 0; i < worldState.nests.Count; i++)
            {
                var nest = worldState.nests[i];
                hash = HashFloat(hash, nest.position.x);
                hash = HashFloat(hash, nest.position.y);
            }

            return $"simId={simulationId}|seed={rootSeed}|hash={hash:X8}|obs={worldState.obstacles.Count}|decor={worldState.decor.Count}|nests={worldState.nests.Count}";
        }
    }

    private static uint HashFloat(uint hash, float value)
    {
        var quantized = Mathf.RoundToInt(value * 1000f);
        return HashInt(hash, quantized);
    }

    private static uint HashString(uint hash, string value)
    {
        return HashInt(hash, unchecked((int)StableHashUtility.Fnv1a32(value)));
    }

    private static uint HashInt(uint hash, int value)
    {
        hash ^= unchecked((uint)value);
        hash *= 16777619u;
        return hash;
    }
}
