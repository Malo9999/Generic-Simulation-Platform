using UnityEngine;

public class WorldDebugOverlay : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.F9;
    [SerializeField] private WorldMapAsset worldAssetOverride;
    [SerializeField] private bool showOverlay;
    [SerializeField] private bool drawSplines = true;
    [SerializeField] private bool drawNodeAnchors = true;
    [SerializeField] private bool drawLaneAnchors = true;

    private Material lineMaterial;
    private WorldMapAsset activeAsset;
    private WorldMapRuntime runtime;
    private Camera activeCamera;
    private float nextDiscoveryTime;
    private Vector2 hoverWorld;
    private int hoverX;
    private int hoverY;
    private bool hoverInBounds;

    private void Awake()
    {
        EnsureLineMaterial();
        TryRefreshRuntime(force: true);
    }

    private void OnDestroy()
    {
        if (lineMaterial != null)
        {
            Destroy(lineMaterial);
            lineMaterial = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) showOverlay = !showOverlay;
        if (!showOverlay) return;

        TryRefreshRuntime(false);
        activeCamera = Camera.main;
        if (activeCamera == null) return;

        var ray = activeCamera.ScreenPointToRay(Input.mousePosition);
        var ground = new Plane(Vector3.up, Vector3.zero);
        hoverInBounds = false;
        if (!ground.Raycast(ray, out var enter)) return;

        var hit = ray.GetPoint(enter);
        hoverWorld = new Vector2(hit.x, hit.z);
        if (runtime == null) return;
        hoverInBounds = WorldMapQuery.TryGetCell(runtime.grid, hoverWorld, out hoverX, out hoverY);
    }

    private void OnRenderObject()
    {
        if (!showOverlay || runtime == null || lineMaterial == null) return;
        lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.Begin(GL.LINES);

        if (drawSplines)
        {
            GL.Color(new Color(1f, 0.84f, 0.1f, 0.95f));
            foreach (var pair in runtime.splines)
            {
                DrawSplineLines(pair.Value);
            }
        }

        if (drawNodeAnchors)
        {
            DrawScatterCrosses("anchors_nodes", 0.35f, new Color(0.15f, 1f, 0.2f, 1f));
        }

        if (drawLaneAnchors)
        {
            DrawScatterCrosses("anchors_lanes", 0.15f, new Color(0.2f, 0.75f, 1f, 1f));
        }

        GL.End();
        GL.PopMatrix();
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(12f, 360f, 460f, 220f), GUI.skin.box);
        showOverlay = GUILayout.Toggle(showOverlay, "World Debug Overlay (F9)");
        if (!showOverlay)
        {
            GUILayout.EndArea();
            return;
        }

        if (runtime == null)
        {
            GUILayout.Label("No World loaded");
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label($"World: {runtime.mapId} ({runtime.recipeId}) seed:{runtime.seed}");
        GUILayout.Label($"Mouse world: {hoverWorld.x:F2}, {hoverWorld.y:F2}");
        GUILayout.Label($"Cell: {(hoverInBounds ? $"{hoverX}, {hoverY}" : "<out of bounds>")}");
        GUILayout.Label($"Walkable: {WorldMapQuery.IsWalkable(runtime, hoverWorld)}");
        GUILayout.Label($"Zone: {WorldMapQuery.GetZoneId(runtime, hoverWorld)}");

        var nearestSpline = WorldMapQuery.GetNearestSpline(runtime, hoverWorld);
        GUILayout.Label($"Nearest spline: {(nearestSpline != null ? nearestSpline.id : "<none>")}");

        var nearestNodeIndex = WorldMapQuery.GetNearestNodeIndex(runtime, hoverWorld);
        GUILayout.Label($"Nearest node: {(nearestNodeIndex >= 0 ? nearestNodeIndex.ToString() : "<none>")}");

        drawSplines = GUILayout.Toggle(drawSplines, "Draw splines");
        drawNodeAnchors = GUILayout.Toggle(drawNodeAnchors, "Draw node anchors");
        drawLaneAnchors = GUILayout.Toggle(drawLaneAnchors, "Draw lane anchors");
        GUILayout.EndArea();
    }

    private void TryRefreshRuntime(bool force = false)
    {
        var candidate = worldAssetOverride;
        if (candidate == null && (force || runtime == null || Time.unscaledTime >= nextDiscoveryTime))
        {
            nextDiscoveryTime = Time.unscaledTime + 1f;
            var all = Resources.FindObjectsOfTypeAll<WorldMapAsset>();
            if (all != null && all.Length > 0) candidate = all[0];
        }

        if (!force && candidate == activeAsset) return;

        activeAsset = candidate;
        runtime = activeAsset != null ? WorldRuntime.Load(activeAsset) : null;
    }

    private void DrawSplineLines(WorldSpline spline)
    {
        if (spline?.points == null || spline.points.Count < 2) return;

        for (var i = 1; i < spline.points.Count; i++)
        {
            var a = spline.points[i - 1];
            var b = spline.points[i];
            GL.Vertex3(a.x, 0.07f, a.y);
            GL.Vertex3(b.x, 0.07f, b.y);
        }

        if (!spline.closed || spline.points.Count <= 2) return;
        var c = spline.points[spline.points.Count - 1];
        var d = spline.points[0];
        GL.Vertex3(c.x, 0.07f, c.y);
        GL.Vertex3(d.x, 0.07f, d.y);
    }

    private void DrawScatterCrosses(string scatterId, float radius, Color color)
    {
        if (!runtime.TryGetScatter(scatterId, out var scatter) || scatter?.points == null) return;
        GL.Color(color);
        for (var i = 0; i < scatter.points.Count; i++)
        {
            var p = scatter.points[i].pos;
            GL.Vertex3(p.x - radius, 0.08f, p.y);
            GL.Vertex3(p.x + radius, 0.08f, p.y);
            GL.Vertex3(p.x, 0.08f, p.y - radius);
            GL.Vertex3(p.x, 0.08f, p.y + radius);
        }
    }

    private void EnsureLineMaterial()
    {
        if (lineMaterial != null) return;

        var shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) return;

        lineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }
}
