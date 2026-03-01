using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class PredatorPreyDocuMapEditorWindow : EditorWindow
{
    private const string PreviewRootName = "PredatorPreyDocuMapPreview";

    private PredatorPreyDocuMapRecipe recipe;
    private int selectedPoint = -1;

    [MenuItem("GSP/PredatorPreyDocu/Map Editor")]
    public static void Open()
    {
        GetWindow<PredatorPreyDocuMapEditorWindow>("PredatorPreyDocu Map Editor");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Map Recipe", EditorStyles.boldLabel);
        recipe = (PredatorPreyDocuMapRecipe)EditorGUILayout.ObjectField("Recipe", recipe, typeof(PredatorPreyDocuMapRecipe), false);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create New"))
            {
                CreateRecipeAsset();
            }

            using (new EditorGUI.DisabledScope(recipe == null))
            {
                if (GUILayout.Button("Auto-generate default river (8 points)"))
                {
                    Undo.RecordObject(recipe, "Auto Generate River");
                    recipe.EnsureDefaultRiver(8);
                    EditorUtility.SetDirty(recipe);
                    Repaint();
                    SceneView.RepaintAll();
                }
            }
        }

        if (recipe == null)
        {
            EditorGUILayout.HelpBox("Assign or create a PredatorPreyDocuMapRecipe asset.", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add point"))
            {
                AddPoint();
            }

            using (new EditorGUI.DisabledScope(selectedPoint < 0 || selectedPoint >= recipe.riverPoints.Count))
            {
                if (GUILayout.Button("Remove selected"))
                {
                    RemoveSelected();
                }
            }
        }

        if (GUILayout.Button("Apply to Preview"))
        {
            ApplyToPreview();
        }

        if (GUILayout.Button("Frame Preview"))
        {
            FramePreview();
        }

        EditorGUILayout.Space();
        DrawDefaultInspectorLikeFields();
    }

    private void DrawDefaultInspectorLikeFields()
    {
        EditorGUI.BeginChangeCheck();

        var arenaWidth = EditorGUILayout.FloatField("Arena Width", recipe.arenaWidth);
        var arenaHeight = EditorGUILayout.FloatField("Arena Height", recipe.arenaHeight);
        var riverWidthNorth = EditorGUILayout.FloatField("River Width North", recipe.riverWidthNorth);
        var riverWidthSouth = EditorGUILayout.FloatField("River Width South", recipe.riverWidthSouth);
        var floodplainExtra = EditorGUILayout.FloatField("Floodplain Extra", recipe.floodplainExtra);
        var bankExtra = EditorGUILayout.FloatField("Bank Extra", recipe.bankExtra);
        var creekCount = EditorGUILayout.IntField("Creek Count", recipe.creekCount);
        var treeClusterCount = EditorGUILayout.IntField("Tree Cluster Count", recipe.treeClusterCount);
        var grassDotCount = EditorGUILayout.IntField("Grass Dot Count", recipe.grassDotCount);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(recipe, "Edit Recipe Settings");
            recipe.arenaWidth = arenaWidth;
            recipe.arenaHeight = arenaHeight;
            recipe.riverWidthNorth = riverWidthNorth;
            recipe.riverWidthSouth = riverWidthSouth;
            recipe.floodplainExtra = floodplainExtra;
            recipe.bankExtra = bankExtra;
            recipe.creekCount = creekCount;
            recipe.treeClusterCount = treeClusterCount;
            recipe.grassDotCount = grassDotCount;
            recipe.NormalizeAndValidate();
            EditorUtility.SetDirty(recipe);
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Normalize + Validate"))
        {
            Undo.RecordObject(recipe, "Normalize Recipe");
            recipe.NormalizeAndValidate();
            EditorUtility.SetDirty(recipe);
            SceneView.RepaintAll();
        }

        EditorGUILayout.LabelField($"Control Points: {recipe.riverPoints.Count}");
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (recipe == null)
        {
            return;
        }

        var halfW = Mathf.Max(0.5f, recipe.arenaWidth * 0.5f);
        var halfH = Mathf.Max(0.5f, recipe.arenaHeight * 0.5f);

        Handles.color = new Color(1f, 1f, 1f, 0.8f);
        var tl = new Vector3(-halfW, halfH, 0f);
        var tr = new Vector3(halfW, halfH, 0f);
        var br = new Vector3(halfW, -halfH, 0f);
        var bl = new Vector3(-halfW, -halfH, 0f);
        Handles.DrawPolyLine(tl, tr, br, bl, tl);

        if (recipe.riverPoints.Count > 1)
        {
            Handles.color = new Color(0.2f, 0.7f, 1f, 0.9f);
            for (var i = 0; i < recipe.riverPoints.Count - 1; i++)
            {
                Handles.DrawLine(ToWorld(recipe.riverPoints[i], halfW, halfH), ToWorld(recipe.riverPoints[i + 1], halfW, halfH));
            }
        }

        for (var i = 0; i < recipe.riverPoints.Count; i++)
        {
            var world = ToWorld(recipe.riverPoints[i], halfW, halfH);
            var size = HandleUtility.GetHandleSize(world) * 0.08f;

            Handles.color = i == selectedPoint ? Color.yellow : Color.cyan;
            if (Handles.Button(world, Quaternion.identity, size, size, Handles.SphereHandleCap))
            {
                selectedPoint = i;
                Repaint();
            }

            if (i != selectedPoint)
            {
                continue;
            }

            EditorGUI.BeginChangeCheck();
            var moved = Handles.PositionHandle(world, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(recipe, "Move River Point");
                recipe.riverPoints[i] = ToNormalized(moved, halfW, halfH);
                recipe.NormalizeAndValidate();
                EditorUtility.SetDirty(recipe);
                Repaint();
                SceneView.RepaintAll();
            }
        }

        Repaint();
    }

    private void AddPoint()
    {
        Undo.RecordObject(recipe, "Add River Point");
        if (recipe.riverPoints == null)
        {
            recipe.riverPoints = new System.Collections.Generic.List<Vector2>();
        }

        if (recipe.riverPoints.Count == 0)
        {
            recipe.riverPoints.Add(new Vector2(0.5f, 1f));
            recipe.riverPoints.Add(new Vector2(0.5f, 0f));
            selectedPoint = 0;
        }
        else
        {
            var index = Mathf.Clamp(selectedPoint, 0, recipe.riverPoints.Count - 1);
            var current = recipe.riverPoints[index];
            var insert = index + 1 < recipe.riverPoints.Count
                ? Vector2.Lerp(current, recipe.riverPoints[index + 1], 0.5f)
                : new Vector2(current.x, Mathf.Clamp01(current.y - 0.1f));

            recipe.riverPoints.Insert(index + 1, insert);
            selectedPoint = index + 1;
        }

        recipe.NormalizeAndValidate();
        EditorUtility.SetDirty(recipe);
        Repaint();
        SceneView.RepaintAll();
    }

    private void RemoveSelected()
    {
        if (selectedPoint < 0 || selectedPoint >= recipe.riverPoints.Count)
        {
            return;
        }

        Undo.RecordObject(recipe, "Remove River Point");
        recipe.riverPoints.RemoveAt(selectedPoint);
        selectedPoint = Mathf.Clamp(selectedPoint - 1, -1, recipe.riverPoints.Count - 1);
        recipe.NormalizeAndValidate();
        EditorUtility.SetDirty(recipe);
        Repaint();
        SceneView.RepaintAll();
    }

    private void ApplyToPreview()
    {
        recipe.NormalizeAndValidate();
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();

        var previewRoot = GetOrCreatePreviewRoot();
        ClearChildren(previewRoot);

        var mapBuilder = new PredatorPreyDocuMapBuilder();
        mapBuilder.BuildFromRecipe(previewRoot, recipe);

        if (Application.isPlaying)
        {
            var runners = UnityEngine.Object.FindObjectsByType<PredatorPreyDocuRunner>(FindObjectsSortMode.None);
            for (var i = 0; i < runners.Length; i++)
            {
                runners[i].RebuildMapPreview();
            }

            Debug.Log($"[PredatorPreyDocuMapEditor] Updated scene preview and applied recipe to {runners.Length} runner(s).");
        }
        else
        {
            Debug.Log("[PredatorPreyDocuMapEditor] Updated edit-mode scene preview.");
        }

        EditorSceneManager.MarkSceneDirty(previewRoot.gameObject.scene);
        Selection.activeGameObject = previewRoot.gameObject;
        SceneView.RepaintAll();
    }

    private void FramePreview()
    {
        var previewRoot = FindPreviewRoot();
        if (previewRoot == null)
        {
            Debug.LogWarning("[PredatorPreyDocuMapEditor] No preview root found to frame. Click 'Apply to Preview' first.");
            return;
        }

        Selection.activeGameObject = previewRoot.gameObject;
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }
    }

    private static Transform FindPreviewRoot()
    {
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == PreviewRootName)
            {
                return roots[i].transform;
            }
        }

        return null;
    }

    private static Transform GetOrCreatePreviewRoot()
    {
        var existing = FindPreviewRoot();
        if (existing != null)
        {
            return existing;
        }

        return new GameObject(PreviewRootName).transform;
    }

    private static void ClearChildren(Transform root)
    {
        for (var i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Object.Destroy(child);
            }
            else
            {
                Object.DestroyImmediate(child);
            }
        }
    }

    private void CreateRecipeAsset()
    {
        var path = EditorUtility.SaveFilePanelInProject("Create Map Recipe", "PredatorPreyDocuMapRecipe", "asset", "Choose recipe asset path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var asset = CreateInstance<PredatorPreyDocuMapRecipe>();
        asset.EnsureDefaultRiver(8);
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(asset);
        recipe = asset;
        selectedPoint = -1;
    }

    private static Vector3 ToWorld(Vector2 normalized, float halfW, float halfH)
    {
        var x = Mathf.Lerp(-halfW, halfW, Mathf.Clamp01(normalized.x));
        var y = Mathf.Lerp(-halfH, halfH, Mathf.Clamp01(normalized.y));
        return new Vector3(x, y, 0f);
    }

    private static Vector2 ToNormalized(Vector3 world, float halfW, float halfH)
    {
        var x = Mathf.InverseLerp(-halfW, halfW, world.x);
        var y = Mathf.InverseLerp(-halfH, halfH, world.y);
        return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
    }
}
