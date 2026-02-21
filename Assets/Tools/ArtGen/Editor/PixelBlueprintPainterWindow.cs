using UnityEditor;
using UnityEngine;

public sealed class PixelBlueprintPainterWindow : EditorWindow
{
    private enum PaintTool
    {
        Paint,
        Erase
    }

    private PixelBlueprint2D blueprint;
    private string layerName = "body";
    private PaintTool activeTool = PaintTool.Paint;
    private float zoom = 16f;
    private byte[] clipboard;

    [MenuItem("Tools/Generic Simulation Platform/Art/Blueprint Painter…")]
    public static void OpenWindow()
    {
        var window = GetWindow<PixelBlueprintPainterWindow>("Blueprint Painter");
        window.minSize = new Vector2(700f, 500f);
    }

    private void OnGUI()
    {
        blueprint = (PixelBlueprint2D)EditorGUILayout.ObjectField("Blueprint", blueprint, typeof(PixelBlueprint2D), false);
        if (blueprint == null)
        {
            EditorGUILayout.HelpBox("Select a PixelBlueprint2D asset to begin painting.", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            layerName = EditorGUILayout.TextField("Layer", layerName);
            if (GUILayout.Button("Body", GUILayout.Width(60f))) layerName = "body";
            if (GUILayout.Button("Stripe", GUILayout.Width(60f))) layerName = "stripe";
        }

        activeTool = (PaintTool)GUILayout.Toolbar((int)activeTool, new[] { "Paint", "Erase" });
        zoom = EditorGUILayout.Slider("Grid Zoom", zoom, 8f, 24f);
        EditorGUILayout.HelpBox($"Layer: {layerName}  |  Tool: {activeTool}  |  Shortcuts: LMB uses tool, RMB erase, Shift+LMB erase.", MessageType.None);

        DrawTools();

        var layer = blueprint.EnsureLayer(layerName);
        DrawPaintGrid(layer);
        DrawPreview();
    }

    private void DrawTools()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Copy Layer"))
            {
                var src = blueprint.EnsureLayer(layerName);
                clipboard = (byte[])src.pixels.Clone();
            }

            if (GUILayout.Button("Paste Layer") && clipboard != null)
            {
                var dst = blueprint.EnsureLayer(layerName);
                var len = Mathf.Min(dst.pixels.Length, clipboard.Length);
                for (var i = 0; i < len; i++) dst.pixels[i] = clipboard[i];
                MarkDirty();
            }

            if (GUILayout.Button("Mirror X")) { Mirror(true); MarkDirty(); }
            if (GUILayout.Button("Mirror Y")) { Mirror(false); MarkDirty(); }
            if (GUILayout.Button("Nudge ←")) { Nudge(-1, 0); MarkDirty(); }
            if (GUILayout.Button("Nudge →")) { Nudge(1, 0); MarkDirty(); }
            if (GUILayout.Button("Nudge ↑")) { Nudge(0, -1); MarkDirty(); }
            if (GUILayout.Button("Nudge ↓")) { Nudge(0, 1); MarkDirty(); }
            if (GUILayout.Button("Clear Layer")) { blueprint.Clear(layerName); MarkDirty(); }
        }
    }

    private void DrawPaintGrid(PixelBlueprint2D.Layer layer)
    {
        var size = new Vector2(blueprint.width * zoom, blueprint.height * zoom);
        var rect = GUILayoutUtility.GetRect(size.x, size.y, GUILayout.ExpandWidth(false));
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

        for (var y = 0; y < blueprint.height; y++)
        {
            for (var x = 0; x < blueprint.width; x++)
            {
                var value = layer.pixels[(y * blueprint.width) + x] > 0;
                var cell = new Rect(rect.x + (x * zoom), rect.y + (y * zoom), zoom - 1f, zoom - 1f);
                if (value)
                {
                    EditorGUI.DrawRect(cell, layerName == "stripe" ? new Color(0.95f, 0.95f, 0.25f) : Color.white);
                }
                else
                {
                    EditorGUI.DrawRect(cell, new Color(0.25f, 0.25f, 0.25f));
                }
            }
        }

        var evt = Event.current;
        var mouseInGrid = rect.Contains(evt.mousePosition);
        if (mouseInGrid)
        {
            var hoverX = Mathf.FloorToInt((evt.mousePosition.x - rect.x) / zoom);
            var hoverY = Mathf.FloorToInt((evt.mousePosition.y - rect.y) / zoom);
            if (hoverX >= 0 && hoverY >= 0 && hoverX < blueprint.width && hoverY < blueprint.height)
            {
                var value = layer.pixels[(hoverY * blueprint.width) + hoverX] > 0 ? 1 : 0;
                EditorGUILayout.LabelField($"Cursor: ({hoverX}, {hoverY})  Value: {value}");
            }

            if ((evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag) && (evt.button == 0 || evt.button == 1))
            {
                var x = Mathf.FloorToInt((evt.mousePosition.x - rect.x) / zoom);
                var y = Mathf.FloorToInt((evt.mousePosition.y - rect.y) / zoom);
                var erase = evt.button == 1 || (evt.button == 0 && evt.shift) || activeTool == PaintTool.Erase;
                blueprint.Set(layerName, x, y, erase ? (byte)0 : (byte)1);
                MarkDirty();
                evt.Use();
            }
        }
    }

    private void DrawPreview()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        DrawPreviewTexture(1);
        DrawPreviewTexture(4);
    }

    private void DrawPreviewTexture(int upscale)
    {
        var w = blueprint.width * upscale;
        var h = blueprint.height * upscale;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var body = blueprint.EnsureLayer("body");
        var stripe = blueprint.EnsureLayer("stripe");
        var pixels = new Color32[w * h];

        for (var y = 0; y < blueprint.height; y++)
        {
            for (var x = 0; x < blueprint.width; x++)
            {
                var color = new Color32(0, 0, 0, 0);
                if (body.pixels[(y * blueprint.width) + x] > 0) color = new Color32(45, 39, 32, 255);
                if (stripe.pixels[(y * blueprint.width) + x] > 0) color = new Color32(170, 170, 170, 255);
                for (var oy = 0; oy < upscale; oy++)
                {
                    for (var ox = 0; ox < upscale; ox++)
                    {
                        var tx = (x * upscale) + ox;
                        var ty = (y * upscale) + oy;
                        pixels[(ty * w) + tx] = color;
                    }
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        GUILayout.Label(tex, GUILayout.Width(w), GUILayout.Height(h));
        DestroyImmediate(tex);
    }

    private void Mirror(bool xAxis)
    {
        var layer = blueprint.EnsureLayer(layerName);
        var outPixels = new byte[layer.pixels.Length];
        for (var y = 0; y < blueprint.height; y++)
        {
            for (var x = 0; x < blueprint.width; x++)
            {
                var sx = xAxis ? (blueprint.width - 1 - x) : x;
                var sy = xAxis ? y : (blueprint.height - 1 - y);
                outPixels[(y * blueprint.width) + x] = layer.pixels[(sy * blueprint.width) + sx];
            }
        }

        layer.pixels = outPixels;
    }

    private void Nudge(int dx, int dy)
    {
        var layer = blueprint.EnsureLayer(layerName);
        var outPixels = new byte[layer.pixels.Length];
        for (var y = 0; y < blueprint.height; y++)
        {
            for (var x = 0; x < blueprint.width; x++)
            {
                var sx = x - dx;
                var sy = y - dy;
                if (sx >= 0 && sy >= 0 && sx < blueprint.width && sy < blueprint.height)
                {
                    outPixels[(y * blueprint.width) + x] = layer.pixels[(sy * blueprint.width) + sx];
                }
            }
        }

        layer.pixels = outPixels;
    }

    private void MarkDirty()
    {
        EditorUtility.SetDirty(blueprint);
        Repaint();
    }
}
