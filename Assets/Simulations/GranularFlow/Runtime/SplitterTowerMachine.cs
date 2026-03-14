using UnityEngine;

public sealed class SplitterTowerMachine
{
    private const int CollisionSegmentCapacity = 64;

    private static readonly Vector2 HopperApex = new(0f, 2.25f);
    private static readonly Vector2 HopperLeftSlopeStart = new(-4.1f, 5.95f);
    private static readonly Vector2 HopperRightSlopeStart = new(4.1f, 5.95f);
    private static readonly Vector2 GateCenter = new(0f, 2.02f);
    private static readonly float GateHalfWidth = 0.58f;
    private static readonly Vector2 FlapCenter = new(0f, -0.72f);
    private const float FlapHalfWidth = 1.7f;

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

        UpperChamber = new Rect(-4.8f, 2.2f, 9.6f, 4.8f);
        Throat = new Rect(-0.72f, 1.45f, 1.44f, 0.9f);
        LowerChamber = new Rect(-4.9f, -2.6f, 9.8f, 3.7f);
        LeftBin = new Rect(-8.95f, -6.8f, 3.5f, 2.5f);
        RightBin = new Rect(5.45f, -6.8f, 3.5f, 2.5f);

        CreateBody();
        upperFillVisual = CreateRect("UpperAccumulation", new Vector2(0f, UpperChamber.yMin), new Vector2(8.5f, 0.05f), new Color(0.95f, 0.74f, 0.2f, 0.92f));
        gateApertureVisual = CreateRect("GateAperture", new Vector2(GateCenter.x, Throat.center.y), new Vector2(0.22f, 0.22f), new Color(0.22f, 0.88f, 0.4f, 0.95f));
        gateVisual = CreateRect("SlidingGate", GateCenter, new Vector2(1.5f, 0.36f), new Color(0.22f, 0.24f, 0.28f));
        flapVisual = CreateRect("DiverterFlap", FlapCenter, new Vector2(FlapHalfWidth * 2f, 0.28f), new Color(0.35f, 0.37f, 0.41f));
        leftBinFillVisual = CreateRect("BinLeftFill", new Vector2(LeftBin.center.x, LeftBin.yMin), new Vector2(LeftBin.width - 0.5f, 0.05f), new Color(0.22f, 0.76f, 0.98f, 0.92f));
        rightBinFillVisual = CreateRect("BinRightFill", new Vector2(RightBin.center.x, RightBin.yMin), new Vector2(RightBin.width - 0.5f, 0.05f), new Color(0.97f, 0.47f, 0.22f, 0.92f));

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
            gateVisual.localPosition = new Vector3(GateCenter.x, GateCenter.y + GateOpen * 0.5f, 0f);
        }

        if (gateApertureVisual != null)
        {
            gateApertureVisual.localScale = new Vector3(Mathf.Lerp(0.2f, 1.4f, GateOpen), gateApertureVisual.localScale.y, 1f);
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
            upperFillVisual.localScale = new Vector3(8.5f, h, 1f);
            upperFillVisual.localPosition = new Vector3(0f, UpperChamber.yMin + h * 0.5f, 0f);
        }

        UpdateBinFill(leftBinFillVisual, LeftBin, sensors.leftBinFill);
        UpdateBinFill(rightBinFillVisual, RightBin, sensors.rightBinFill);
    }

    public int WriteCollisionSegments(SplitterTowerConfig cfg, Vector4[] output)
    {
        if (output == null || output.Length == 0)
        {
            return 0;
        }

        var count = 0;

        // Top hopper enclosure + funnel slopes into a single center throat.
        AddSegment(ref count, output, new Vector2(UpperChamber.xMin, UpperChamber.yMax), new Vector2(UpperChamber.xMax, UpperChamber.yMax));
        AddSegment(ref count, output, new Vector2(UpperChamber.xMin, UpperChamber.yMax), new Vector2(UpperChamber.xMin, 5.65f));
        AddSegment(ref count, output, new Vector2(UpperChamber.xMax, UpperChamber.yMax), new Vector2(UpperChamber.xMax, 5.65f));
        AddSegment(ref count, output, HopperLeftSlopeStart, HopperApex);
        AddSegment(ref count, output, HopperRightSlopeStart, HopperApex);

        // Gate segment (moving actuator).
        var gateY = GateCenter.y + (GateOpen * 0.5f);
        AddSegment(ref count, output, new Vector2(-GateHalfWidth, gateY), new Vector2(GateHalfWidth, gateY));

        // Throat side walls (funnel neck).
        AddSegment(ref count, output, new Vector2(-0.72f, HopperApex.y), new Vector2(-0.72f, LowerChamber.yMax));
        AddSegment(ref count, output, new Vector2(0.72f, HopperApex.y), new Vector2(0.72f, LowerChamber.yMax));

        // Lower control chamber enclosure, with lower side exits.
        AddSegment(ref count, output, new Vector2(LowerChamber.xMin, LowerChamber.yMax), new Vector2(LowerChamber.xMax, LowerChamber.yMax));
        AddSegment(ref count, output, new Vector2(LowerChamber.xMin, LowerChamber.yMax), new Vector2(LowerChamber.xMin, -1.25f));
        AddSegment(ref count, output, new Vector2(LowerChamber.xMin, -2.1f), new Vector2(LowerChamber.xMin, LowerChamber.yMin));
        AddSegment(ref count, output, new Vector2(LowerChamber.xMax, LowerChamber.yMax), new Vector2(LowerChamber.xMax, -1.25f));
        AddSegment(ref count, output, new Vector2(LowerChamber.xMax, -2.1f), new Vector2(LowerChamber.xMax, LowerChamber.yMin));
        AddSegment(ref count, output, new Vector2(LowerChamber.xMin, LowerChamber.yMin), new Vector2(-4.15f, LowerChamber.yMin));
        AddSegment(ref count, output, new Vector2(4.15f, LowerChamber.yMin), new Vector2(LowerChamber.xMax, LowerChamber.yMin));

        // Diverter flap (moving actuator).
        var flapRot = Quaternion.Euler(0f, 0f, -FlapState * cfg.flapMaxAngle);
        var flapDir = flapRot * Vector3.right;
        AddSegment(ref count, output, FlapCenter - ((Vector2)flapDir * FlapHalfWidth), FlapCenter + ((Vector2)flapDir * FlapHalfWidth));

        // Lower side exits and chutes to bins.
        AddSegment(ref count, output, new Vector2(LowerChamber.xMin, -1.25f), new Vector2(-6.05f, -3.25f));
        AddSegment(ref count, output, new Vector2(LowerChamber.xMin, -2.1f), new Vector2(-6.65f, -4.0f));
        AddSegment(ref count, output, new Vector2(LowerChamber.xMax, -1.25f), new Vector2(6.05f, -3.25f));
        AddSegment(ref count, output, new Vector2(LowerChamber.xMax, -2.1f), new Vector2(6.65f, -4.0f));

        // Bin walls.
        AddRectWalls(ref count, output, LeftBin);
        AddRectWalls(ref count, output, RightBin);

        return count;
    }

    public int GetCollisionSegmentCapacity()
    {
        return CollisionSegmentCapacity;
    }

    private static void AddRectWalls(ref int count, Vector4[] output, Rect rect)
    {
        AddSegment(ref count, output, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax));
        AddSegment(ref count, output, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax));
        AddSegment(ref count, output, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin));
    }

    private static void AddSegment(ref int count, Vector4[] output, Vector2 a, Vector2 b)
    {
        if (count >= output.Length)
        {
            return;
        }

        output[count++] = new Vector4(a.x, a.y, b.x, b.y);
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
        var frame = new Color(0.09f, 0.1f, 0.12f);
        var wall = new Color(0.16f, 0.17f, 0.19f);

        CreateRect("UpperShell", new Vector2(0f, 4.65f), new Vector2(10.4f, 5.2f), frame);
        CreateRect("LowerShell", new Vector2(0f, -0.75f), new Vector2(10.8f, 4.9f), frame);
        CreateRect("Feeder", new Vector2(0f, 7.4f), new Vector2(1.8f, 1.4f), new Color(0.22f, 0.23f, 0.24f));

        // Hopper walls.
        CreateSegmentVisual("HopperRoof", new Vector2(UpperChamber.xMin, UpperChamber.yMax), new Vector2(UpperChamber.xMax, UpperChamber.yMax), 0.22f, wall);
        CreateSegmentVisual("HopperWallL", new Vector2(UpperChamber.xMin, UpperChamber.yMax), new Vector2(UpperChamber.xMin, 5.65f), 0.22f, wall);
        CreateSegmentVisual("HopperWallR", new Vector2(UpperChamber.xMax, UpperChamber.yMax), new Vector2(UpperChamber.xMax, 5.65f), 0.22f, wall);
        CreateSegmentVisual("HopperSlopeL", HopperLeftSlopeStart, HopperApex, 0.24f, wall);
        CreateSegmentVisual("HopperSlopeR", HopperRightSlopeStart, HopperApex, 0.24f, wall);
        CreateSegmentVisual("ThroatWallL", new Vector2(-0.72f, HopperApex.y), new Vector2(-0.72f, LowerChamber.yMax), 0.2f, wall);
        CreateSegmentVisual("ThroatWallR", new Vector2(0.72f, HopperApex.y), new Vector2(0.72f, LowerChamber.yMax), 0.2f, wall);

        // Lower chamber walls and side exits.
        CreateSegmentVisual("LowerTop", new Vector2(LowerChamber.xMin, LowerChamber.yMax), new Vector2(LowerChamber.xMax, LowerChamber.yMax), 0.22f, wall);
        CreateSegmentVisual("LowerWallLT", new Vector2(LowerChamber.xMin, LowerChamber.yMax), new Vector2(LowerChamber.xMin, -1.25f), 0.22f, wall);
        CreateSegmentVisual("LowerWallLB", new Vector2(LowerChamber.xMin, -2.1f), new Vector2(LowerChamber.xMin, LowerChamber.yMin), 0.22f, wall);
        CreateSegmentVisual("LowerWallRT", new Vector2(LowerChamber.xMax, LowerChamber.yMax), new Vector2(LowerChamber.xMax, -1.25f), 0.22f, wall);
        CreateSegmentVisual("LowerWallRB", new Vector2(LowerChamber.xMax, -2.1f), new Vector2(LowerChamber.xMax, LowerChamber.yMin), 0.22f, wall);
        CreateSegmentVisual("LowerFloorL", new Vector2(LowerChamber.xMin, LowerChamber.yMin), new Vector2(-4.15f, LowerChamber.yMin), 0.22f, wall);
        CreateSegmentVisual("LowerFloorR", new Vector2(4.15f, LowerChamber.yMin), new Vector2(LowerChamber.xMax, LowerChamber.yMin), 0.22f, wall);

        // Exit chutes.
        CreateSegmentVisual("ExitLUpper", new Vector2(LowerChamber.xMin, -1.25f), new Vector2(-6.05f, -3.25f), 0.22f, wall);
        CreateSegmentVisual("ExitLLower", new Vector2(LowerChamber.xMin, -2.1f), new Vector2(-6.65f, -4.0f), 0.22f, wall);
        CreateSegmentVisual("ExitRUpper", new Vector2(LowerChamber.xMax, -1.25f), new Vector2(6.05f, -3.25f), 0.22f, wall);
        CreateSegmentVisual("ExitRLower", new Vector2(LowerChamber.xMax, -2.1f), new Vector2(6.65f, -4.0f), 0.22f, wall);

        CreateRect("BinLeft", LeftBin.center, LeftBin.size, new Color(0.15f, 0.16f, 0.17f));
        CreateRect("BinRight", RightBin.center, RightBin.size, new Color(0.15f, 0.16f, 0.17f));
    }

    private Transform CreateSegmentVisual(string name, Vector2 a, Vector2 b, float thickness, Color color)
    {
        var center = (a + b) * 0.5f;
        var dir = b - a;
        var length = dir.magnitude;
        var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        return CreateRect(name, center, new Vector2(length, thickness), color, angle);
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
