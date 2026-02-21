using UnityEditor;
using UnityEngine;

public static class BeetleBlueprintSynthesizer
{
    public static PixelBlueprint2D Build(ArchetypeSynthesisRequest req, bool grubLike)
    {
        var bp = AssetDatabase.LoadAssetAtPath<PixelBlueprint2D>(req.blueprintPath);
        if (bp == null)
        {
            bp = ScriptableObject.CreateInstance<PixelBlueprint2D>();
            bp.width = 32;
            bp.height = 32;
            AssetDatabase.CreateAsset(bp, req.blueprintPath);
        }

        bp.Clear("body");
        if (grubLike)
        {
            for (var i = 0; i < 4; i++) Oval(bp, 10 + i * 4, 16 + (i % 2), 4, 3);
        }
        else
        {
            Oval(bp, 16, 16, 8, 6);
            Line(bp, 8, 16, 4, 12 + (req.frameIndex % 2));
            Line(bp, 8, 18, 4, 22 - (req.frameIndex % 2));
            Line(bp, 24, 16, 28, 12 + (req.frameIndex % 2));
            Line(bp, 24, 18, 28, 22 - (req.frameIndex % 2));
        }
        EditorUtility.SetDirty(bp);
        return bp;
    }

    private static void Oval(PixelBlueprint2D bp, int cx, int cy, int rx, int ry) { for (var y=-ry; y<=ry; y++) for (var x=-rx; x<=rx; x++) if ((x*x)/(float)(rx*rx)+(y*y)/(float)(ry*ry)<=1f) bp.Set("body",cx+x,cy+y,1); }
    private static void Line(PixelBlueprint2D bp, int x0, int y0, int x1, int y1) { var dx=Mathf.Abs(x1-x0); var sx=x0<x1?1:-1; var dy=-Mathf.Abs(y1-y0); var sy=y0<y1?1:-1; var err=dx+dy; while(true){bp.Set("body",x0,y0,1); if(x0==x1&&y0==y1)break; var e2=err*2; if(e2>=dy){err+=dy; x0+=sx;} if(e2<=dx){err+=dx; y0+=sy;}} }
}
