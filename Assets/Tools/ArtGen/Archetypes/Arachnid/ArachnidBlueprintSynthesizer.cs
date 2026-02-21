using UnityEditor;
using UnityEngine;

public static class ArachnidBlueprintSynthesizer
{
    public static PixelBlueprint2D Build(ArchetypeSynthesisRequest req)
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
        Oval(bp, 14, 16, 5, 4);
        Oval(bp, 20, 16, 6, 5);
        var phase = req.frameIndex % 2 == 0 ? 1 : -1;
        for (var i = -2; i <= 1; i++)
        {
            Line(bp, 14, 16 + i, 6, 12 + i * 2 + phase);
            Line(bp, 20, 16 + i, 28, 12 + i * 2 - phase);
        }
        EditorUtility.SetDirty(bp);
        return bp;
    }

    private static void Oval(PixelBlueprint2D bp, int cx, int cy, int rx, int ry) { for (var y=-ry; y<=ry; y++) for (var x=-rx; x<=rx; x++) if ((x*x)/(float)(rx*rx)+(y*y)/(float)(ry*ry)<=1f) bp.Set("body",cx+x,cy+y,1); }
    private static void Line(PixelBlueprint2D bp, int x0, int y0, int x1, int y1) { var dx=Mathf.Abs(x1-x0); var sx=x0<x1?1:-1; var dy=-Mathf.Abs(y1-y0); var sy=y0<y1?1:-1; var err=dx+dy; while(true){bp.Set("body",x0,y0,1); if(x0==x1&&y0==y1)break; var e2=err*2; if(e2>=dy){err+=dy; x0+=sx;} if(e2<=dx){err+=dx; y0+=sy;}} }
}
