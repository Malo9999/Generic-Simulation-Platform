using System;
using System.Collections.Generic;
using UnityEngine;

public static class AntTopdownFitter
{
    public static bool TryFit(bool[] mask, int width, int height, RectInt bbox, out AntTopdownModel model)
    {
        model = null;
        if (mask == null || mask.Length != width * height || bbox.width < 4 || bbox.height < 6) return false;
        var profile = BuildWidthProfile(mask, width, bbox);
        Smooth(profile, 5);

        var headY = FindPeak(profile, 0, profile.Length / 3);
        var thoraxY = FindPeak(profile, profile.Length / 3, (profile.Length * 2) / 3);
        var abdomenY = FindPeak(profile, (profile.Length * 2) / 3, profile.Length);
        if (headY < 0 || thoraxY < 0 || abdomenY < 0) return false;

        var m = new AntTopdownModel();
        FitBand(mask, width, bbox, Mathf.Max(0, headY - 4), Mathf.Min(profile.Length - 1, headY + 4), out m.headCenter01, out m.headRadii01);
        FitBand(mask, width, bbox, Mathf.Max(0, thoraxY - 4), Mathf.Min(profile.Length - 1, thoraxY + 4), out m.thoraxCenter01, out m.thoraxRadii01);
        FitBand(mask, width, bbox, Mathf.Max(0, abdomenY - 6), Mathf.Min(profile.Length - 1, abdomenY + 6), out m.abdomenCenter01, out m.abdomenRadii01);

        var dipY = Mathf.Clamp((thoraxY + abdomenY) / 2, 0, profile.Length - 1);
        m.pinchStrength = Mathf.Clamp01(1f - profile[dipY] / Mathf.Max(1f, Mathf.Max(profile[thoraxY], profile[abdomenY])));

        InitLegs(m);
        InitAntennae(m);
        Clamp(m);
        model = m;
        return true;
    }

    private static float[] BuildWidthProfile(bool[] mask, int width, RectInt bbox)
    {
        var rows = new float[bbox.height];
        for (var y = 0; y < bbox.height; y++)
        {
            var left = -1; var right = -1;
            for (var x = bbox.xMin; x < bbox.xMax; x++)
            {
                if (!mask[(bbox.yMin + y) * width + x]) continue;
                if (left < 0) left = x;
                right = x;
            }
            rows[y] = (left >= 0 && right >= left) ? right - left + 1 : 0;
        }
        return rows;
    }

    private static void Smooth(float[] v, int window)
    {
        var copy = (float[])v.Clone();
        var half = window / 2;
        for (var i = 0; i < v.Length; i++)
        {
            float sum = 0; var count = 0;
            for (var k = -half; k <= half; k++) { var j = i + k; if (j < 0 || j >= v.Length) continue; sum += copy[j]; count++; }
            v[i] = sum / Mathf.Max(1, count);
        }
    }

    private static int FindPeak(float[] v, int start, int end)
    {
        var best = -1; var value = -1f;
        for (var i = Mathf.Max(0, start); i < Mathf.Min(v.Length, end); i++) if (v[i] > value) { value = v[i]; best = i; }
        return best;
    }

    private static void FitBand(bool[] mask, int width, RectInt bbox, int y0, int y1, out Vector2 center01, out Vector2 radii01)
    {
        float sx = 0, sy = 0, count = 0;
        for (var y = y0; y <= y1; y++)
        for (var x = bbox.xMin; x < bbox.xMax; x++)
        {
            if (!mask[(bbox.yMin + y) * width + x]) continue;
            sx += x; sy += bbox.yMin + y; count++;
        }

        if (count <= 0)
        {
            center01 = new Vector2(0.5f, 0.5f);
            radii01 = new Vector2(0.08f, 0.08f);
            return;
        }

        var cx = sx / count; var cy = sy / count;
        float xx = 0, yy = 0;
        for (var y = y0; y <= y1; y++)
        for (var x = bbox.xMin; x < bbox.xMax; x++)
        {
            if (!mask[(bbox.yMin + y) * width + x]) continue;
            var dx = x - cx; var dy = (bbox.yMin + y) - cy;
            xx += dx * dx; yy += dy * dy;
        }

        center01 = new Vector2((cx - bbox.xMin) / Mathf.Max(1f, bbox.width), (cy - bbox.yMin) / Mathf.Max(1f, bbox.height));
        radii01 = new Vector2(Mathf.Sqrt(xx / count) / Mathf.Max(1f, bbox.width) * 1.5f, Mathf.Sqrt(yy / count) / Mathf.Max(1f, bbox.height) * 1.5f);
    }

    private static void InitLegs(AntTopdownModel m)
    {
        var y = m.thoraxCenter01.y;
        for (var i = 0; i < 3; i++)
        {
            var t = i / 2f;
            m.legAnchors01[i] = new Vector2(m.thoraxCenter01.x - m.thoraxRadii01.x * 0.8f, y - 0.1f + t * 0.2f);
            m.legAnchors01[i + 3] = new Vector2(m.thoraxCenter01.x + m.thoraxRadii01.x * 0.8f, y - 0.1f + t * 0.2f);
            m.legAnglesDeg[i] = -150f + i * 20f;
            m.legAnglesDeg[i + 3] = -30f + i * 20f;
            m.legLengths01[i] = 0.18f;
            m.legLengths01[i + 3] = 0.18f;
        }
    }

    private static void InitAntennae(AntTopdownModel m)
    {
        m.antennaAnchors01[0] = new Vector2(m.headCenter01.x - m.headRadii01.x * 0.5f, m.headCenter01.y - m.headRadii01.y * 0.9f);
        m.antennaAnchors01[1] = new Vector2(m.headCenter01.x + m.headRadii01.x * 0.5f, m.headCenter01.y - m.headRadii01.y * 0.9f);
        m.antennaAnglesDeg[0] = -130f;
        m.antennaAnglesDeg[1] = -50f;
        m.antennaLen01 = 0.16f;
    }

    private static void Clamp(AntTopdownModel m)
    {
        m.headRadii01 = ClampRadii(m.headRadii01);
        m.thoraxRadii01 = ClampRadii(m.thoraxRadii01);
        m.abdomenRadii01 = ClampRadii(m.abdomenRadii01);
        for (var i = 0; i < m.legLengths01.Length; i++) m.legLengths01[i] = Mathf.Clamp(m.legLengths01[i], 0.08f, 0.35f);
        m.antennaLen01 = Mathf.Clamp(m.antennaLen01, 0.08f, 0.35f);
    }

    private static Vector2 ClampRadii(Vector2 v) => new(Mathf.Clamp(v.x, 0.03f, 0.25f), Mathf.Clamp(v.y, 0.03f, 0.25f));
}
