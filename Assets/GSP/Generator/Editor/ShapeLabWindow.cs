#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class ShapeLabWindow : EditorWindow
{
    private ShapeTemplateBase selectedTemplate;
    private ShapeTemplatePack selectedPack;
    private Texture2D previewTexture;
    private Color previewTint = Color.white;

    [MenuItem("GSP/Generator/Shape Lab")]
    public static void Open()
    {
        GetWindow<ShapeLabWindow>("Shape Lab");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Template", EditorStyles.boldLabel);
        selectedTemplate = (ShapeTemplateBase)EditorGUILayout.ObjectField("Template Asset", selectedTemplate, typeof(ShapeTemplateBase), false);
        previewTint = EditorGUILayout.ColorField("Preview Tint", previewTint);

        if (selectedTemplate != null && GUILayout.Button("Refresh Preview"))
        {
            RebuildPreview();
        }

        if (previewTexture != null)
        {
            GUILayout.Label(previewTexture, GUILayout.Width(160), GUILayout.Height(160));
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Bake Selected") && selectedTemplate != null)
        {
            ShapeBaker.BakeTemplate(selectedTemplate, previewTint);
            AssetDatabase.Refresh();
        }

        selectedPack = (ShapeTemplatePack)EditorGUILayout.ObjectField("Template Pack", selectedPack, typeof(ShapeTemplatePack), false);
        if (GUILayout.Button($"Bake Batch ({(selectedPack != null ? selectedPack.templates.Count : 0)})") && selectedPack != null)
        {
            ShapeBaker.BakePack(selectedPack);
            AssetDatabase.Refresh();
        }

        if (GUILayout.Button("Generate Default Neon Pack"))
        {
            selectedPack = ShapeBaker.EnsureDefaultNeonPackAndBake();
            AssetDatabase.Refresh();
        }

        if (GUILayout.Button("Open Resources Folder"))
        {
            EditorUtility.RevealInFinder(ShapeBaker.OutputRoot);
        }
    }

    private void RebuildPreview()
    {
        if (selectedTemplate == null)
        {
            return;
        }

        var px = selectedTemplate.Rasterize(previewTint);
        if (previewTexture != null)
        {
            DestroyImmediate(previewTexture);
        }

        previewTexture = ShapeSpriteFactory.CreateTexture(selectedTemplate.TextureSize, px);
    }
}
#endif
