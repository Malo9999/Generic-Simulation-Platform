using UnityEngine;

public sealed class SplitterTowerMachine
{
    private readonly Transform root;
    private readonly Transform gateVisual;
    private readonly Transform flapVisual;
    private readonly Transform upperFillVisual;
    private readonly Transform gateApertureVisual;
    private readonly Transform leftBinFillVisual;
    private readonly Transform rightBinFillVisual;

    public readonly Rect UpperChamber;
    public readonly Rect Throat;
    public readonly Rect LowerChamber;
    public readonly Rect LeftBin;
    public readonly Rect RightBin;

    public float GateOpen { get; private set; }
    public float FlapState { get; private set; }

    public SplitterTowerMachine(Transform parent)
    {
        root = new GameObject("SplitterTower").transform;
        root.SetParent(parent, false);

        UpperChamber = new Rect(-4.8f, 2.2f, 9.6f, 5.0f);
        Throat = new Rect(-1.3f, 1.1f, 2.6f, 1.0f);
        LowerChamber = new Rect(-5.5f, -2.8f, 11.0f, 3.9f);
        LeftBin = new Rect(-8.8f, -6.8f, 4.0f, 2.5f);
        RightBin = new Rect(4.8f, -6.8f, 4.0f, 2.5f);

        CreateBody();
        upperFillVisual = CreateRect("UpperAccumulation", new Vector2(0f, 2.35f), new Vector2(8.9f, 0.05f), new Color(0.95f, 0.74f, 0.2f, 0.92f));
        gateApertureVisual = CreateRect("GateAperture", new Vector2(0f, 1.7f), new Vector2(0.3f, 0.22f), new Color(0.22f, 0.88f, 0.4f, 0.95f));
        gateVisual = CreateRect("SlidingGate", new Vector2(0f, 1.8f), new Vector2(2.8f, 0.45f), new Color(0.22f, 0.24f, 0.28f));
        flapVisual = CreateRect("DiverterFlap", new Vector2(0f, -0.45f), new Vector2(3.2f, 0.28f), new Color(0.35f, 0.37f, 0.41f));
        leftBinFillVisual = CreateRect("BinLeftFill", new Vector2(LeftBin.center.x, LeftBin.yMin), new Vector2(3.5f, 0.05f), new Color(0.22f, 0.76f, 0.98f, 0.92f));
        rightBinFillVisual = CreateRect("BinRightFill", new Vector2(RightBin.center.x, RightBin.yMin), new Vector2(3.5f, 0.05f), new Color(0.97f, 0.47f, 0.22f, 0.92f));

        if (leftBinFillVisual != null)
        {
            leftBinFillVisual.localPosition = new Vector3(leftBinFillVisual.localPosition.x, LeftBin.yMin, 0f);
        }

        if (rightBinFillVisual != null)
        {
            rightBinFillVisual.localPosition = new Vector3(rightBinFillVisual.localPosition.x, RightBin.yMin, 0f);
        }
    }

    public void ApplyActuators(float gateTarget, float flapTarget, SplitterTowerConfig cfg, float dt)
    {
        GateOpen = Mathf.MoveTowards(GateOpen, Mathf.Clamp01(gateTarget), cfg.gateMoveSpeed * dt);
        FlapState = Mathf.MoveTowards(FlapState, Mathf.Clamp(flapTarget, -1f, 1f), cfg.flapMoveSpeed * dt);

        if (gateVisual != null)
        {
            gateVisual.localPosition = new Vector3(0f, 1.8f + GateOpen * 0.55f, 0f);
        }

        if (gateApertureVisual != null)
        {
            gateApertureVisual.localScale = new Vector3(Mathf.Lerp(0.3f, 2.5f, GateOpen), gateApertureVisual.localScale.y, 1f);
        }

        if (flapVisual != null)
        {
            flapVisual.localRotation = Quaternion.Euler(0f, 0f, -FlapState * cfg.flapMaxAngle);
        }
    }

    public void UpdateIndicators(in GranularFlowSensors sensors)
    {
        if (upperFillVisual != null)
        {
            var h = Mathf.Lerp(0.05f, UpperChamber.height - 0.3f, sensors.upperChamberFill);
            upperFillVisual.localScale = new Vector3(8.9f, h, 1f);
            upperFillVisual.localPosition = new Vector3(0f, UpperChamber.yMin + h * 0.5f, 0f);
        }

        UpdateBinFill(leftBinFillVisual, LeftBin, sensors.leftBinFill);
        UpdateBinFill(rightBinFillVisual, RightBin, sensors.rightBinFill);
    }

    private static void UpdateBinFill(Transform fillVisual, Rect bin, float fill)
    {
        if (fillVisual == null)
        {
            return;
        }

        var h = Mathf.Lerp(0.05f, bin.height - 0.15f, Mathf.Clamp01(fill));
        fillVisual.localScale = new Vector3(bin.width - 0.5f, h, 1f);
        fillVisual.localPosition = new Vector3(bin.center.x, bin.yMin + h * 0.5f, 0f);
    }

    private void CreateBody()
    {
        CreateRect("UpperFrame", new Vector2(0f, 4.7f), new Vector2(10f, 5.6f), new Color(0.1f, 0.11f, 0.13f));
        CreateRect("LowerFrame", new Vector2(0f, -0.9f), new Vector2(12.0f, 4.9f), new Color(0.09f, 0.1f, 0.12f));
        CreateRect("Feeder", new Vector2(0f, 7.4f), new Vector2(1.8f, 1.4f), new Color(0.22f, 0.23f, 0.24f));
        CreateRect("LaneLeft", new Vector2(-5.9f, -3.9f), new Vector2(4.0f, 0.8f), new Color(0.18f, 0.19f, 0.2f), 28f);
        CreateRect("LaneRight", new Vector2(5.9f, -3.9f), new Vector2(4.0f, 0.8f), new Color(0.18f, 0.19f, 0.2f), -28f);
        CreateRect("BinLeft", LeftBin.center, LeftBin.size, new Color(0.15f, 0.16f, 0.17f));
        CreateRect("BinRight", RightBin.center, RightBin.size, new Color(0.15f, 0.16f, 0.17f));
    }

    private Transform CreateRect(string name, Vector2 center, Vector2 size, Color color, float angle = 0f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(center.x, center.y, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

        var renderer = go.GetComponent<MeshRenderer>();
        var material = new Material(Shader.Find("Sprites/Default"));
        material.color = color;
        renderer.sharedMaterial = material;

        var collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            Object.Destroy(collider);
        }

        return go.transform;
    }
}
