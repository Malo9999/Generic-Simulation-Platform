using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class OutlineExtraction
{
    [Serializable]
    public sealed class OutlineReport
    {
        public float coverage;
        public RectInt bbox;
        public float threshold;
        public Vector2 centerOfMass;
        public Vector2 principalAxis;
        public int fragmentCount;
        public List<string> warnings = new();
    }

    public sealed class OutlineResult
    {
        public bool[] mask;
        public int width;
        public int height;
        public OutlineReport report;
    }

    public static string SelectBestTopdownImage(IEnumerable<string> files)
        => files.OrderByDescending(GetArea).ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

    public static bool TryExtract(string imagePath, out OutlineResult result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) return false;
        var bytes = File.ReadAllBytes(imagePath);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(bytes, false)) { UnityEngine.Object.DestroyImmediate(tex); return false; }

        var pixels = tex.GetPixels32();
        var width = tex.width;
        var height = tex.height;
        var bg = SampleCorners(pixels, width, height);
        var threshold = 0.23f;
        var mask = BuildMask(pixels, width, height, bg, threshold);
        var coverage = Coverage(mask);
        if (coverage < 0.01f || coverage > 0.65f)
        {
            threshold = coverage < 0.01f ? 0.12f : 0.35f;
            mask = BuildMask(pixels, width, height, bg, threshold);
        }

        mask = Morph(mask, width, height, true);
        mask = Morph(mask, width, height, false);
        mask = KeepLargestComponent(mask, width, height);
        var bbox = ComputeBounds(mask, width, height);
        var report = new OutlineReport { coverage = Coverage(mask), bbox = bbox, threshold = threshold, warnings = new List<string>() };
        if (bbox.width < 2 || bbox.height < 2) { report.warnings.Add("Mask too small."); UnityEngine.Object.DestroyImmediate(tex); return false; }
        report.centerOfMass = ComputeCenter(mask, width, height);
        report.principalAxis = ComputePrincipalAxis(mask, width, height, report.centerOfMass);
        report.fragmentCount = CountComponents(mask, width, height);
        if (report.fragmentCount > 1) report.warnings.Add($"Fragmented silhouette components={report.fragmentCount}");

        result = new OutlineResult { mask = mask, width = width, height = height, report = report };
        UnityEngine.Object.DestroyImmediate(tex);
        return true;
    }

    public static float Score(OutlineResult result)
    {
        if (result == null) return -1f;
        var bboxArea = Mathf.Max(1, result.report.bbox.width * result.report.bbox.height);
        var density = result.mask.Count(v => v) / (float)bboxArea;
        var fragmentPenalty = Mathf.Max(0, result.report.fragmentCount - 1) * 0.2f;
        return density * 2f + result.report.coverage - fragmentPenalty;
    }

    public static void SaveMaskPng(string outputPath, bool[] mask, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var px = new Color32[width * height];
        for (var i = 0; i < px.Length; i++) px[i] = mask[i] ? new Color32(255,255,255,255) : new Color32(0,0,0,0);
        tex.SetPixels32(px);
        tex.Apply(false);
        File.WriteAllBytes(outputPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static int GetArea(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2,2);
            if (!tex.LoadImage(bytes, true)) { UnityEngine.Object.DestroyImmediate(tex); return 0; }
            var area = tex.width * tex.height;
            UnityEngine.Object.DestroyImmediate(tex);
            return area;
        }
        catch { return 0; }
    }

    private static Color SampleCorners(Color32[] px, int w, int h)
    {
        var samples = new List<Color32>();
        var sizeX = Mathf.Min(10, w);
        var sizeY = Mathf.Min(10, h);
        void Sample(int sx, int sy) { for (var y = 0; y < sizeY; y++) for (var x = 0; x < sizeX; x++) samples.Add(px[(sy + y) * w + (sx + x)]); }
        Sample(0, 0); Sample(w - sizeX, 0); Sample(0, h - sizeY); Sample(w - sizeX, h - sizeY);
        float r=0,g=0,b=0,a=0;
        foreach (var c in samples) { r += c.r; g += c.g; b += c.b; a += c.a; }
        var n = Mathf.Max(1, samples.Count);
        return new Color(r/n/255f, g/n/255f, b/n/255f, a/n/255f);
    }

    private static bool[] BuildMask(Color32[] px, int w, int h, Color bg, float threshold)
    {
        var mask = new bool[w*h];
        for (var i=0;i<mask.Length;i++)
        {
            var c = (Color)px[i];
            var d = Vector3.Distance(new Vector3(c.r,c.g,c.b), new Vector3(bg.r,bg.g,bg.b));
            mask[i] = d > threshold && c.a > 0.05f;
        }
        return mask;
    }

    private static bool[] Morph(bool[] src, int w, int h, bool dilate)
    {
        var dst = new bool[src.Length];
        for (var y=0;y<h;y++) for (var x=0;x<w;x++)
        {
            var on = dilate ? false : true;
            for (var oy=-1;oy<=1;oy++) for (var ox=-1;ox<=1;ox++)
            {
                var nx=x+ox; var ny=y+oy;
                var v = nx>=0&&ny>=0&&nx<w&&ny<h && src[ny*w+nx];
                if (dilate) on |= v; else on &= v;
            }
            dst[y*w+x]=on;
        }
        return dst;
    }

    private static bool[] KeepLargestComponent(bool[] mask, int w, int h)
    {
        var vis = new bool[mask.Length];
        var q = new Queue<int>();
        var largest = new List<int>();
        for (var i = 0; i < mask.Length; i++)
        {
            if (!mask[i] || vis[i]) continue;
            var comp = new List<int>();
            vis[i] = true;
            q.Enqueue(i);
            while (q.Count > 0)
            {
                var idx = q.Dequeue();
                comp.Add(idx);
                var x = idx % w;
                var y = idx / w;
                for (var oy=-1;oy<=1;oy++) for (var ox=-1;ox<=1;ox++)
                {
                    if (ox==0&&oy==0) continue;
                    var nx=x+ox; var ny=y+oy;
                    if(nx<0||ny<0||nx>=w||ny>=h) continue;
                    var ni=ny*w+nx;
                    if(mask[ni]&&!vis[ni]) { vis[ni]=true; q.Enqueue(ni); }
                }
            }
            if (comp.Count > largest.Count) largest = comp;
        }

        var outMask = new bool[mask.Length];
        foreach (var idx in largest) outMask[idx] = true;
        return outMask;
    }

    private static RectInt ComputeBounds(bool[] mask, int w, int h)
    {
        var minX=w; var minY=h; var maxX=-1; var maxY=-1;
        for (var y=0;y<h;y++) for (var x=0;x<w;x++) if (mask[y*w+x]) { if (x<minX) minX=x; if (x>maxX) maxX=x; if (y<minY) minY=y; if (y>maxY) maxY=y; }
        if (maxX < minX || maxY < minY) return new RectInt();
        return new RectInt(minX,minY,maxX-minX+1,maxY-minY+1);
    }

    private static Vector2 ComputeCenter(bool[] mask, int w, int h)
    {
        float sx=0,sy=0,c=0;
        for (var y=0;y<h;y++) for (var x=0;x<w;x++) if (mask[y*w+x]) { sx+=x; sy+=y; c++; }
        if (c <= 0) return new Vector2(w*0.5f,h*0.5f);
        return new Vector2(sx/c,sy/c);
    }

    private static Vector2 ComputePrincipalAxis(bool[] mask, int w, int h, Vector2 c)
    {
        float xx=0,yy=0,xy=0;
        for (var y=0;y<h;y++) for (var x=0;x<w;x++) if(mask[y*w+x]) { var dx=x-c.x; var dy=y-c.y; xx+=dx*dx; yy+=dy*dy; xy+=dx*dy; }
        var trace = xx + yy;
        var det = xx * yy - xy * xy;
        var temp = Mathf.Sqrt(Mathf.Max(0f, trace * trace * 0.25f - det));
        var lambda = trace * 0.5f + temp;
        var axis = new Vector2(xy, lambda - xx);
        if (axis.sqrMagnitude < 1e-5f) axis = Vector2.up;
        return axis.normalized;
    }

    private static float Coverage(bool[] mask) => mask.Count(v => v) / (float)Mathf.Max(1, mask.Length);

    private static int CountComponents(bool[] mask, int w, int h)
    {
        var vis = new bool[mask.Length];
        var q = new Queue<int>();
        var count = 0;
        for (var i=0;i<mask.Length;i++)
        {
            if (!mask[i] || vis[i]) continue;
            count++; vis[i]=true; q.Enqueue(i);
            while (q.Count>0)
            {
                var idx=q.Dequeue(); var x=idx%w; var y=idx/w;
                for (var oy=-1;oy<=1;oy++) for (var ox=-1;ox<=1;ox++)
                {
                    if (ox==0&&oy==0) continue;
                    var nx=x+ox; var ny=y+oy;
                    if(nx<0||ny<0||nx>=w||ny>=h) continue;
                    var ni=ny*w+nx;
                    if(mask[ni]&&!vis[ni]) { vis[ni]=true; q.Enqueue(ni); }
                }
            }
        }
        return count;
    }
}
