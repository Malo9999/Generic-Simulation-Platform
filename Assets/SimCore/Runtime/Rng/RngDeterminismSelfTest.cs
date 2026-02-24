using UnityEngine;

public sealed class RngDeterminismSelfTest : MonoBehaviour
{
    [SerializeField] private int seed = 1337;

    [ContextMenu("Print Determinism Signature")]
    public void PrintSignature()
    {
        var a = RngService.BuildSignature(seed);
        var b = RngService.BuildSignature(seed);
        var c = RngService.BuildSignature(seed + 1);
        Debug.Log($"RNG SelfTest sameSeedMatch={string.Equals(a, b, System.StringComparison.Ordinal)}\nA={a}\nB={b}\nNextSeed={c}");
    }
}
