using System.Collections.Generic;
using UnityEngine;

public sealed class SnappedTrackGenerator
{
    private const int TargetSamples = 512;
    private const int MinSegments = 40;
    private const int MaxSegments = 120;
    private const int AttemptCount = 40;

    private struct Candidate
    {
        public List<Vector2Int> Nodes;
        public float Score;
        public int SegmentCount;
        public int DiagonalCount;
    }

    public Vector2[] BuildCenterline(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int variant)
    {
        var minHalf = Mathf.Min(arenaHalfWidth, arenaHalfHeight);
        var cellSize = Mathf.Clamp(minHalf * 0.08f, 3.5f, 9f);
        var marginCells = 2;
        var maxX = Mathf.Max(6, Mathf.FloorToInt(arenaHalfWidth / cellSize) - marginCells);
        var maxY = Mathf.Max(6, Mathf.FloorToInt(arenaHalfHeight / cellSize) - marginCells);

        Candidate? best = null;
        var sourceSeed = rng != null ? rng.Seed : variant;

        for (var attempt = 0; attempt < AttemptCount; attempt++)
        {
            var attemptRng = new SeededRng(StableMix(sourceSeed, variant, attempt, 0x68F1));
            var loop = BuildLoopCandidate(maxX, maxY, attemptRng);
            if (loop == null || loop.Count < MinSegments)
            {
                continue;
            }

            if (!Evaluate(loop, out var score, out var segments, out var diagonals))
            {
                continue;
            }

            if (!best.HasValue || score > best.Value.Score)
            {
                best = new Candidate { Nodes = loop, Score = score, SegmentCount = segments, DiagonalCount = diagonals };
            }
        }

        if (!best.HasValue)
        {
            return null;
        }

        return BuildWorldPolyline(best.Value.Nodes, cellSize);
    }

    public bool TryGetLoopStats(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int variant, out int segments, out int diagonals)
    {
        segments = 0;
        diagonals = 0;
        var minHalf = Mathf.Min(arenaHalfWidth, arenaHalfHeight);
        var cellSize = Mathf.Clamp(minHalf * 0.08f, 3.5f, 9f);
        var marginCells = 2;
        var maxX = Mathf.Max(6, Mathf.FloorToInt(arenaHalfWidth / cellSize) - marginCells);
        var maxY = Mathf.Max(6, Mathf.FloorToInt(arenaHalfHeight / cellSize) - marginCells);

        var sourceSeed = rng != null ? rng.Seed : variant;
        var bestScore = float.MinValue;
        var found = false;

        for (var attempt = 0; attempt < AttemptCount; attempt++)
        {
            var attemptRng = new SeededRng(StableMix(sourceSeed, variant, attempt, 0x68F1));
            var loop = BuildLoopCandidate(maxX, maxY, attemptRng);
            if (loop == null || !Evaluate(loop, out var score, out var segCount, out var diagCount))
            {
                continue;
            }

            if (score > bestScore)
            {
                bestScore = score;
                segments = segCount;
                diagonals = diagCount;
                found = true;
            }
        }

        return found;
    }

    private static List<Vector2Int> BuildLoopCandidate(int maxX, int maxY, IRng rng)
    {
        var nodes = new List<Vector2Int>(MaxSegments + 2) { Vector2Int.zero };
        var visited = new HashSet<Vector2Int> { Vector2Int.zero };
        var edges = new HashSet<Segment>();
        var dir = (int)Dir8.E;

        for (var step = 0; step < MaxSegments; step++)
        {
            var current = nodes[nodes.Count - 1];
            var committed = false;

            for (var retry = 0; retry < 12 && !committed; retry++)
            {
                var nextDir = PickDirection(rng, dir, nodes, step);
                var opposite = (dir + 4) & 7;
                if (nextDir == opposite)
                {
                    continue;
                }

                var next = current + SnappedTrackTypes.DirDelta8[nextDir];
                if (!Inside(next, maxX, maxY))
                {
                    continue;
                }

                var nearStart = step >= MinSegments && next == Vector2Int.zero;
                if (!nearStart && visited.Contains(next))
                {
                    continue;
                }

                var seg = new Segment(current, next);
                if (edges.Contains(seg) || IntersectsExisting(current, next, nodes, false))
                {
                    continue;
                }

                nodes.Add(next);
                edges.Add(seg);
                if (!nearStart)
                {
                    visited.Add(next);
                }

                dir = nextDir;
                committed = true;
            }

            if (!committed)
            {
                return null;
            }

            var now = nodes[nodes.Count - 1];
            if (step >= MinSegments)
            {
                var dist = Mathf.Abs(now.x) + Mathf.Abs(now.y);
                if (dist >= 1 && dist <= 2)
                {
                    var closeDir = DirTo(now, Vector2Int.zero);
                    if (closeDir >= 0)
                    {
                        var closeSeg = new Segment(now, Vector2Int.zero);
                        if (!edges.Contains(closeSeg) && !IntersectsExisting(now, Vector2Int.zero, nodes, true))
                        {
                            nodes.Add(Vector2Int.zero);
                            return nodes;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static int PickDirection(IRng rng, int dir, List<Vector2Int> nodes, int step)
    {
        var t = rng.NextFloat01();
        var hasRecentTurns = false;
        if (nodes.Count > 6)
        {
            var prev = nodes[nodes.Count - 1] - nodes[nodes.Count - 2];
            var prevDir = VecToDir(prev);
            var older = nodes[nodes.Count - 2] - nodes[nodes.Count - 3];
            var olderDir = VecToDir(older);
            hasRecentTurns = prevDir >= 0 && olderDir >= 0 && prevDir != olderDir;
        }

        if (t < 0.55f)
        {
            return dir;
        }

        if (t < 0.75f)
        {
            return rng.Chance(0.5f) ? SnappedTrackTypes.TurnLeft45(dir) : SnappedTrackTypes.TurnRight45(dir);
        }

        if (t < 0.95f)
        {
            return rng.Chance(0.5f) ? SnappedTrackTypes.TurnLeft90(dir) : SnappedTrackTypes.TurnRight90(dir);
        }

        if (!hasRecentTurns && step > 14)
        {
            return rng.Chance(0.5f) ? SnappedTrackTypes.TurnLeft90(SnappedTrackTypes.TurnLeft90(dir)) : SnappedTrackTypes.TurnRight90(SnappedTrackTypes.TurnRight90(dir));
        }

        return rng.Chance(0.5f) ? SnappedTrackTypes.TurnLeft90(dir) : SnappedTrackTypes.TurnRight90(dir);
    }

    private static bool Evaluate(List<Vector2Int> nodes, out float score, out int segments, out int diagonalCount)
    {
        score = 0f;
        segments = Mathf.Max(0, nodes.Count - 1);
        diagonalCount = 0;
        if (nodes.Count < MinSegments + 1)
        {
            return false;
        }

        var dirs = new List<int>(segments);
        for (var i = 0; i < nodes.Count - 1; i++)
        {
            var dir = VecToDir(nodes[i + 1] - nodes[i]);
            if (dir < 0)
            {
                return false;
            }

            if (SnappedTrackTypes.IsDiagonal(dir))
            {
                diagonalCount++;
            }

            dirs.Add(dir);
        }

        if (diagonalCount < 6)
        {
            return false;
        }

        var longStraightRuns = 0;
        var runLen = 1;
        for (var i = 1; i < dirs.Count; i++)
        {
            if (dirs[i] == dirs[i - 1])
            {
                runLen++;
            }
            else
            {
                if (runLen >= 8)
                {
                    longStraightRuns++;
                }

                runLen = 1;
            }
        }

        if (runLen >= 8)
        {
            longStraightRuns++;
        }

        if (longStraightRuns < 2)
        {
            return false;
        }

        var hairpin = false;
        var chicane = false;
        var changes = new List<int>(dirs.Count);
        for (var i = 1; i < dirs.Count; i++)
        {
            var delta = NormalizeTurn(dirs[i] - dirs[i - 1]);
            changes.Add(delta);
        }

        for (var i = 0; i < changes.Count; i++)
        {
            if (Mathf.Abs(changes[i]) == 2)
            {
                for (var j = i + 1; j < Mathf.Min(changes.Count, i + 5); j++)
                {
                    if (Mathf.Abs(changes[j]) == 2)
                    {
                        hairpin = true;
                        break;
                    }
                }
            }

            if (changes[i] != 0)
            {
                for (var j = i + 1; j < Mathf.Min(changes.Count, i + 7); j++)
                {
                    if (changes[j] != 0 && Mathf.Sign(changes[j]) != Mathf.Sign(changes[i]))
                    {
                        chicane = true;
                        break;
                    }
                }
            }

            if (hairpin && chicane)
            {
                break;
            }
        }

        if (!hairpin || !chicane)
        {
            return false;
        }

        var min = nodes[0];
        var max = nodes[0];
        for (var i = 1; i < nodes.Count; i++)
        {
            min = Vector2Int.Min(min, nodes[i]);
            max = Vector2Int.Max(max, nodes[i]);
        }

        var w = Mathf.Max(1, max.x - min.x);
        var h = Mathf.Max(1, max.y - min.y);
        var aspect = Mathf.Max(w, h) / (float)Mathf.Min(w, h);
        if (aspect > 2.8f)
        {
            return false;
        }

        score = (segments * 0.45f) + (diagonalCount * 2f) + (longStraightRuns * 8f) + (hairpin ? 20f : 0f) + (chicane ? 20f : 0f) - (aspect * 4f);
        return true;
    }

    private static Vector2[] BuildWorldPolyline(List<Vector2Int> nodes, float cellSize)
    {
        var pts = new List<Vector2>(nodes.Count * 8);
        var radius = cellSize * 0.35f;

        for (var i = 0; i < nodes.Count - 1; i++)
        {
            var p0 = GridToWorld(nodes[i], cellSize);
            var p1 = GridToWorld(nodes[i + 1], cellSize);

            var prevDir = i > 0 ? (p0 - GridToWorld(nodes[i - 1], cellSize)).normalized : (p1 - p0).normalized;
            var nextDir = (p1 - p0).normalized;
            var hasCornerIn = i > 0 && Vector2.Dot(prevDir, nextDir) < 0.999f;

            var start = hasCornerIn ? p0 + (prevDir * radius) : p0;
            AddUnique(pts, start);

            if (hasCornerIn)
            {
                var cornerPts = Mathf.RoundToInt(Mathf.Lerp(5f, 9f, 1f - Mathf.Clamp01(Vector2.Dot(prevDir, nextDir))));
                var center = p0;
                for (var k = 1; k < cornerPts - 1; k++)
                {
                    var t = k / (float)(cornerPts - 1);
                    var a = Vector2.Lerp(-prevDir, nextDir, t).normalized;
                    AddUnique(pts, center + (a * radius));
                }
            }

            var nextHasCorner = i < nodes.Count - 2 && Vector2.Dot(nextDir, (GridToWorld(nodes[i + 2], cellSize) - p1).normalized) < 0.999f;
            var end = nextHasCorner ? p1 - (nextDir * radius) : p1;
            AddUnique(pts, end);
        }

        if (pts.Count > 2 && (pts[0] - pts[pts.Count - 1]).sqrMagnitude > 0.0001f)
        {
            pts.Add(pts[0]);
        }

        ChaikinClosed(pts, 2);
        return ResampleArcLengthClosed(pts, TargetSamples);
    }

    private static Vector2 GridToWorld(Vector2Int n, float cell) => new Vector2(n.x * cell, n.y * cell);

    private static bool IntersectsExisting(Vector2Int a, Vector2Int b, List<Vector2Int> nodes, bool closing)
    {
        for (var i = 0; i < nodes.Count - 1; i++)
        {
            var p = nodes[i];
            var q = nodes[i + 1];
            if (SharesEndpoint(a, b, p, q))
            {
                continue;
            }

            if (SegmentsIntersect(a, b, p, q))
            {
                return true;
            }
        }

        if (closing && nodes.Count > 1)
        {
            for (var i = 0; i < nodes.Count - 2; i++)
            {
                var p = nodes[i];
                var q = nodes[i + 1];
                if (SharesEndpoint(a, b, p, q))
                {
                    continue;
                }

                if (SegmentsIntersect(a, b, p, q))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SharesEndpoint(Vector2Int a, Vector2Int b, Vector2Int p, Vector2Int q)
    {
        return a == p || a == q || b == p || b == q;
    }

    private static bool SegmentsIntersect(Vector2Int a, Vector2Int b, Vector2Int c, Vector2Int d)
    {
        var o1 = Orientation(a, b, c);
        var o2 = Orientation(a, b, d);
        var o3 = Orientation(c, d, a);
        var o4 = Orientation(c, d, b);

        if (o1 != o2 && o3 != o4)
        {
            return true;
        }

        if (o1 == 0 && OnSegment(a, c, b)) return true;
        if (o2 == 0 && OnSegment(a, d, b)) return true;
        if (o3 == 0 && OnSegment(c, a, d)) return true;
        if (o4 == 0 && OnSegment(c, b, d)) return true;
        return false;
    }

    private static int Orientation(Vector2Int a, Vector2Int b, Vector2Int c)
    {
        var v = (long)(b.y - a.y) * (c.x - b.x) - (long)(b.x - a.x) * (c.y - b.y);
        if (v == 0) return 0;
        return v > 0 ? 1 : 2;
    }

    private static bool OnSegment(Vector2Int a, Vector2Int b, Vector2Int c)
    {
        return b.x <= Mathf.Max(a.x, c.x) && b.x >= Mathf.Min(a.x, c.x) && b.y <= Mathf.Max(a.y, c.y) && b.y >= Mathf.Min(a.y, c.y);
    }

    private static bool Inside(Vector2Int p, int maxX, int maxY)
    {
        return Mathf.Abs(p.x) <= maxX && Mathf.Abs(p.y) <= maxY;
    }

    private static int DirTo(Vector2Int from, Vector2Int to)
    {
        var d = to - from;
        return VecToDir(new Vector2Int(Mathf.Clamp(d.x, -1, 1), Mathf.Clamp(d.y, -1, 1)));
    }

    private static int VecToDir(Vector2Int v)
    {
        for (var i = 0; i < SnappedTrackTypes.DirDelta8.Length; i++)
        {
            if (SnappedTrackTypes.DirDelta8[i] == v)
            {
                return i;
            }
        }

        return -1;
    }

    private static int NormalizeTurn(int turn)
    {
        var t = turn;
        while (t > 4) t -= 8;
        while (t < -4) t += 8;
        return t;
    }

    private static void AddUnique(List<Vector2> pts, Vector2 p)
    {
        if (pts.Count == 0 || (pts[pts.Count - 1] - p).sqrMagnitude > 0.0001f)
        {
            pts.Add(p);
        }
    }

    private static void ChaikinClosed(List<Vector2> points, int passes)
    {
        if (points == null || points.Count < 4)
        {
            return;
        }

        for (var pass = 0; pass < passes; pass++)
        {
            var src = new List<Vector2>(points);
            points.Clear();
            var n = src.Count;
            for (var i = 0; i < n; i++)
            {
                var p0 = src[i];
                var p1 = src[(i + 1) % n];
                var q = Vector2.Lerp(p0, p1, 0.25f);
                var r = Vector2.Lerp(p0, p1, 0.75f);
                points.Add(q);
                points.Add(r);
            }
        }
    }

    private static Vector2[] ResampleArcLengthClosed(List<Vector2> points, int target)
    {
        if (points == null || points.Count < 3)
        {
            return null;
        }

        var n = points.Count;
        var lengths = new float[n + 1];
        var total = 0f;
        lengths[0] = 0f;
        for (var i = 0; i < n; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % n];
            total += Vector2.Distance(a, b);
            lengths[i + 1] = total;
        }

        var outPts = new Vector2[target];
        for (var i = 0; i < target; i++)
        {
            var d = (i / (float)target) * total;
            var seg = 0;
            while (seg < n - 1 && lengths[seg + 1] < d)
            {
                seg++;
            }

            var segStart = points[seg];
            var segEnd = points[(seg + 1) % n];
            var segLen = Mathf.Max(1e-6f, lengths[seg + 1] - lengths[seg]);
            var t = Mathf.Clamp01((d - lengths[seg]) / segLen);
            outPts[i] = Vector2.Lerp(segStart, segEnd, t);
        }

        return outPts;
    }

    private static int StableMix(int a, int b, int c, int d)
    {
        unchecked
        {
            var h = 17;
            h = (h * 31) ^ a;
            h = (h * 31) ^ (b * unchecked((int)0x9E3779B9));
            h = (h * 31) ^ (c * unchecked((int)0x85EBCA6B));
            h = (h * 31) ^ (d * unchecked((int)0xC2B2AE35));
            h ^= h >> 16;
            return h;
        }
    }
}
