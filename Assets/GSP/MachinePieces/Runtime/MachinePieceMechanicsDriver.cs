using System.Globalization;
using UnityEngine;

public sealed class MachinePieceMechanicsDriver : MonoBehaviour
{
    private BuiltPieceRuntime runtime;

    public void Initialize(BuiltPieceRuntime built)
    {
        runtime = built;
        ApplyStateToCollision();
    }

    public void SetGateOpen01(float open)
    {
        if (runtime?.Spec?.pieceType != "Gate") return;
        runtime.RuntimeState["open01"] = Mathf.Clamp01(open).ToString(CultureInfo.InvariantCulture);
        ApplyStateToCollision();
    }

    public void SetFlapAngle(float angle)
    {
        if (runtime?.Spec?.pieceType != "Flap") return;
        runtime.RuntimeState["angle"] = angle.ToString(CultureInfo.InvariantCulture);
        ApplyStateToCollision();
    }

    private void ApplyStateToCollision()
    {
        if (runtime?.Collider == null) return;

        if (runtime.Spec.pieceType == "Gate" && runtime.Collider is BoxCollider2D gateBox)
        {
            var open01 = Parse(runtime.RuntimeState, "open01", 0f);
            var size = runtime.Spec.shape.size.ToVector2();
            gateBox.size = new Vector2(Mathf.Max(0.05f, size.x * (1f - open01)), size.y);
        }

        if (runtime.Spec.pieceType == "Flap")
        {
            var angle = Parse(runtime.RuntimeState, "angle", 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, runtime.Instance.transform.rotation + angle);
        }
    }

    private static float Parse(System.Collections.Generic.Dictionary<string, string> state, string key, float fallback)
    {
        if (state == null || !state.TryGetValue(key, out var value)) return fallback;
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }
}
