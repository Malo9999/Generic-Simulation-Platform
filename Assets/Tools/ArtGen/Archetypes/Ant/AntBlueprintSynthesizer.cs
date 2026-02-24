using UnityEditor;
using UnityEngine;

public static class AntBlueprintSynthesizer
{
    public static (PixelBlueprint2D body, PixelBlueprint2D stripe) Build(AntSpeciesProfile profile, ArchetypeSynthesisRequest req)
    {
        var useAssetBackedBlueprint = !string.IsNullOrWhiteSpace(req.blueprintPath);
        PixelBlueprint2D body;

        if (useAssetBackedBlueprint)
        {
            body = AssetDatabase.LoadAssetAtPath<PixelBlueprint2D>(req.blueprintPath);
            if (body == null)
            {
                body = ScriptableObject.CreateInstance<PixelBlueprint2D>();
                body.width = 32;
                body.height = 32;
                AssetDatabase.CreateAsset(body, req.blueprintPath);
            }
        }
        else
        {
            body = ScriptableObject.CreateInstance<PixelBlueprint2D>();
            body.width = 32;
            body.height = 32;
        }

        if (req.stage == "egg")
        {
            body.Clear("body");
            DrawEllipse(body, "body", new Vector2Int(16, 16), 6, 4);
        }
        else if (req.stage == "larva")
        {
            body.Clear("body");
            for (var i = 0; i < 4; i++) DrawEllipse(body, "body", new Vector2Int(12 + i * 3, 16 + (i % 2)), 3, 2);
        }
        else if (req.stage == "pupa")
        {
            body.Clear("body");
            DrawEllipse(body, "body", new Vector2Int(16, 16), 8, 4);
        }
        else
        {
            var pose = AntPoseLibrary.Get(req.state, req.frameIndex);
            AntPixelRig.Draw(body, pose);
            DrawAbdomenStripe(body);
        }

        if (useAssetBackedBlueprint) EditorUtility.SetDirty(body);
        return (body, body);
    }

    private static void DrawAbdomenStripe(PixelBlueprint2D bp)
    {
        for (var y = 20; y <= 22; y++)
        for (var x = 12; x <= 20; x++)
            if (bp.Get("body", x, y) > 0) bp.Set("stripe", x, y, 1);
    }

    private static void DrawEllipse(PixelBlueprint2D bp, string layer, Vector2Int center, int rx, int ry)
    {
        for (var y = -ry; y <= ry; y++)
        for (var x = -rx; x <= rx; x++)
        {
            var nx = x / (float)Mathf.Max(1, rx);
            var ny = y / (float)Mathf.Max(1, ry);
            if (nx * nx + ny * ny <= 1f) bp.Set(layer, center.x + x, center.y + y, 1);
        }
    }
}
