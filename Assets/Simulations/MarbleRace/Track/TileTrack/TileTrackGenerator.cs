using System.Collections.Generic;
using UnityEngine;

public sealed class TileTrackGenerator
{
    private const int TargetSamples = 512;
    private const int CandidateCount = 12;
    private const int MaxBuildAttempts = 20;
    private const int MinCells = 40;
    private const int MaxCells = 120;

    private struct Candidate
    {
        public List<TilePlacedPiece> Pieces;
        public float Score;
    }

    public Vector2[] BuildBestLoop(float arenaHalfWidth, float arenaHalfHeight, IRng rng, int variant)
    {
        var trackSeed = rng != null ? rng.Seed : variant;
        Candidate? best = null;

        for (var i = 0; i < CandidateCount; i++)
        {
            var candidateRng = Fork(trackSeed, variant, i);
            if (!TryBuildLoop(arenaHalfWidth, arenaHalfHeight, candidateRng, out var pieces))
            {
                continue;
            }

            var score = Score(pieces);
            if (!best.HasValue || score > best.Value.Score)
            {
                best = new Candidate { Pieces = pieces, Score = score };
            }
        }

        if (!best.HasValue)
        {
            return null;
        }

        return BuildCenterlineWorldPoints(best.Value.Pieces, TileTrackTypes.DefaultCellSize);
    }

    public Vector2[] BuildCenterlineWorldPoints(List<TilePlacedPiece> pieces, float cellSize)
    {
        var stitched = new List<Vector2>(pieces.Count * 6);

        for (var i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            var local = TileTrackTypes.GetLocalPoints(piece.PieceType, piece.EntryDir);
            var center = new Vector2(piece.GridX * cellSize, piece.GridY * cellSize);
            var offset = new Vector2(-0.5f, -0.5f);

            for (var j = 0; j < local.Count; j++)
            {
                var world = center + ((local[j] + offset) * cellSize);
                if (stitched.Count > 0 && (stitched[stitched.Count - 1] - world).sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                stitched.Add(world);
            }
        }

        if (stitched.Count > 2 && (stitched[0] - stitched[stitched.Count - 1]).sqrMagnitude > 0.0001f)
        {
            stitched.Add(stitched[0]);
        }

        ChaikinClosed(stitched, 2);
        return ResampleArcLengthClosed(stitched, TargetSamples);
    }

    private static bool TryBuildLoop(float arenaHalfWidth, float arenaHalfHeight, IRng rng, out List<TilePlacedPiece> pieces)
    {
        pieces = null;
        var cell = TileTrackTypes.DefaultCellSize;
        var margin = cell * 2f;
        var maxX = Mathf.Max(4, Mathf.FloorToInt((arenaHalfWidth - margin) / cell));
        var maxY = Mathf.Max(4, Mathf.FloorToInt((arenaHalfHeight - margin) / cell));

        for (var attempt = 0; attempt < MaxBuildAttempts; attempt++)
        {
            var attemptRng = Fork(rng.Seed, maxX + maxY, attempt);
            var path = new List<TilePlacedPiece>(160);
            var occupiedCells = new HashSet<Vector2Int>();
            var occupiedEdges = new HashSet<ulong>();
            var turnSigns = new List<int>(160);

            var currentCell = Vector2Int.zero;
            var entry = TileDir.West;
            occupiedCells.Add(currentCell);

            var maxSteps = Mathf.Min(MaxCells, Mathf.Max(MinCells + 8, (maxX + maxY) * 6));
            for (var step = 0; step < maxSteps; step++)
            {
                if (step >= MinCells && TryFindClosure(currentCell, entry, occupiedCells, occupiedEdges, maxX, maxY, out var closure))
                {
                    path.AddRange(closure);
                    if (PassesQuality(path, turnSigns))
                    {
                        pieces = path;
                        return true;
                    }

                    break;
                }

                if (!TryPickNextPiece(currentCell, entry, attemptRng, occupiedCells, occupiedEdges, maxX, maxY, step, out var placed, out var nextCell, out var nextEntry, out var turnSign))
                {
                    break;
                }

                path.Add(placed);
                turnSigns.Add(turnSign);
                currentCell = nextCell;
                entry = nextEntry;
            }
        }

        return false;
    }

    private static bool TryPickNextPiece(Vector2Int currentCell, TileDir entry, IRng rng, HashSet<Vector2Int> occupiedCells, HashSet<ulong> occupiedEdges, int maxX, int maxY, int step, out TilePlacedPiece placed, out Vector2Int nextCell, out TileDir nextEntry, out int turnSign)
    {
        var options = new List<TilePieceType> { TilePieceType.Straight, TilePieceType.CornerLeft, TilePieceType.CornerRight };
        var weights = new List<float> { 0.60f, 0.20f, 0.20f };

        var heading = TileTrackTypes.Opposite(entry);
        var turnBudget = Mathf.Max(8, step / 2);
        if (step > turnBudget * 3)
        {
            weights[0] = 0.8f;
            weights[1] = 0.1f;
            weights[2] = 0.1f;
        }

        for (var pick = 0; pick < options.Count; pick++)
        {
            var index = rng.PickIndexWeighted(weights);
            var piece = options[index];
            weights[index] = 0f;

            var exit = TileTrackTypes.ResolveExit(piece, entry);
            var delta = TileTrackTypes.DirToInt(exit);
            nextCell = currentCell + delta;
            nextEntry = TileTrackTypes.Opposite(exit);
            turnSign = piece == TilePieceType.CornerLeft ? 1 : (piece == TilePieceType.CornerRight ? -1 : 0);


            if (Mathf.Abs(nextCell.x) > maxX || Mathf.Abs(nextCell.y) > maxY)
            {
                continue;
            }

            if (occupiedCells.Contains(nextCell))
            {
                continue;
            }

            var edge = MakeEdge(currentCell, nextCell);
            if (occupiedEdges.Contains(edge))
            {
                continue;
            }

            if (CreatesPinch(occupiedCells, nextCell))
            {
                continue;
            }

            placed = new TilePlacedPiece(currentCell.x, currentCell.y, entry, piece);
            occupiedEdges.Add(edge);
            occupiedCells.Add(nextCell);
            return true;
        }

        placed = default;
        nextCell = currentCell;
        nextEntry = heading;
        turnSign = 0;
        return false;
    }

    private static bool TryFindClosure(Vector2Int currentCell, TileDir entry, HashSet<Vector2Int> occupiedCells, HashSet<ulong> occupiedEdges, int maxX, int maxY, out List<TilePlacedPiece> closure)
    {
        closure = null;
        var start = Vector2Int.zero;
        var targetEntry = TileDir.West;
        var queue = new Queue<(Vector2Int cell, TileDir entry, int depth)>();
        var cameFrom = new Dictionary<(Vector2Int, TileDir), ((Vector2Int, TileDir) prev, TilePieceType piece)>();
        var visited = new HashSet<(Vector2Int, TileDir)>();

        var root = (currentCell, entry);
        queue.Enqueue((currentCell, entry, 0));
        visited.Add(root);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node.depth > 12)
            {
                continue;
            }

            if (node.depth > 0 && node.cell == start && node.entry == targetEntry)
            {
                closure = Reconstruct(cameFrom, (node.cell, node.entry), root);
                return closure != null && closure.Count > 0;
            }

            var pieces = new[] { TilePieceType.Straight, TilePieceType.CornerLeft, TilePieceType.CornerRight };
            for (var i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                var exit = TileTrackTypes.ResolveExit(piece, node.entry);
                var nextCell = node.cell + TileTrackTypes.DirToInt(exit);
                var nextEntry = TileTrackTypes.Opposite(exit);

                if (Mathf.Abs(nextCell.x) > maxX || Mathf.Abs(nextCell.y) > maxY)
                {
                    continue;
                }

                var visitingStart = nextCell == start;
                if (!visitingStart && occupiedCells.Contains(nextCell))
                {
                    continue;
                }

                var edge = MakeEdge(node.cell, nextCell);
                if (occupiedEdges.Contains(edge))
                {
                    continue;
                }

                var key = (nextCell, nextEntry);
                if (visited.Contains(key))
                {
                    continue;
                }

                visited.Add(key);
                cameFrom[key] = ((node.cell, node.entry), piece);
                queue.Enqueue((nextCell, nextEntry, node.depth + 1));
            }
        }

        return false;
    }

    private static List<TilePlacedPiece> Reconstruct(Dictionary<(Vector2Int, TileDir), ((Vector2Int, TileDir), TilePieceType)> cameFrom, (Vector2Int, TileDir) end, (Vector2Int, TileDir) root)
    {
        var reversed = new List<TilePlacedPiece>(16);
        var cursor = end;

        while (cursor != root)
        {
            if (!cameFrom.TryGetValue(cursor, out var data))
            {
                return null;
            }

            var prev = data.Item1;
            reversed.Add(new TilePlacedPiece(prev.Item1.x, prev.Item1.y, prev.Item2, data.Item2));
            cursor = prev;
        }

        reversed.Reverse();
        return reversed;
    }

    private static bool PassesQuality(List<TilePlacedPiece> path, List<int> turnSigns)
    {
        if (path == null || path.Count < MinCells || path.Count > MaxCells)
        {
            return false;
        }

        var corners = 0;
        var longStraights = 0;
        var run = 0;
        var min = new Vector2Int(int.MaxValue, int.MaxValue);
        var max = new Vector2Int(int.MinValue, int.MinValue);

        for (var i = 0; i < path.Count; i++)
        {
            var p = path[i];
            min = Vector2Int.Min(min, new Vector2Int(p.GridX, p.GridY));
            max = Vector2Int.Max(max, new Vector2Int(p.GridX, p.GridY));

            if (p.PieceType == TilePieceType.Straight)
            {
                run++;
            }
            else
            {
                corners++;
                if (run >= 6)
                {
                    longStraights++;
                }

                run = 0;
            }
        }

        if (run >= 6)
        {
            longStraights++;
        }

        var size = max - min;
        var ratio = Mathf.Max(size.x + 1, size.y + 1) / (float)Mathf.Max(1, Mathf.Min(size.x + 1, size.y + 1));

        var hasChicane = false;
        var hasHairpinish = false;
        for (var i = 1; i < turnSigns.Count; i++)
        {
            if (turnSigns[i - 1] != 0 && turnSigns[i] != 0 && turnSigns[i - 1] != turnSigns[i])
            {
                hasChicane = true;
            }

            if (turnSigns[i - 1] != 0 && turnSigns[i] == turnSigns[i - 1])
            {
                hasHairpinish = true;
            }
        }

        return corners >= 4 && longStraights >= 2 && ratio <= 2.4f && (hasChicane || hasHairpinish);
    }

    private static float Score(List<TilePlacedPiece> pieces)
    {
        var corners = 0;
        var straights = 0;
        for (var i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].PieceType == TilePieceType.Straight)
            {
                straights++;
            }
            else
            {
                corners++;
            }
        }

        var balance = 1f - Mathf.Abs((corners / (float)Mathf.Max(1, pieces.Count)) - 0.35f);
        return pieces.Count + (corners * 1.2f) + (balance * 20f) + (straights * 0.3f);
    }

    private static bool CreatesPinch(HashSet<Vector2Int> occupied, Vector2Int candidate)
    {
        for (var oy = -1; oy <= 0; oy++)
        {
            for (var ox = -1; ox <= 0; ox++)
            {
                var c = 0;
                for (var y = 0; y < 2; y++)
                {
                    for (var x = 0; x < 2; x++)
                    {
                        var cell = new Vector2Int(candidate.x + ox + x, candidate.y + oy + y);
                        if (cell == candidate || occupied.Contains(cell))
                        {
                            c++;
                        }
                    }
                }

                if (c >= 3)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IRng Fork(int seed, int a, int b)
    {
        unchecked
        {
            var mixed = seed;
            mixed = (mixed * 397) ^ a;
            mixed = (mixed * 397) ^ (b * 7919);
            return new SeededRng(mixed);
        }
    }

    private static ulong MakeEdge(Vector2Int a, Vector2Int b)
    {
        var ax = a.x + 2048;
        var ay = a.y + 2048;
        var bx = b.x + 2048;
        var by = b.y + 2048;
        ulong p1 = (ulong)((ax & 0xFFF) | ((ay & 0xFFF) << 12));
        ulong p2 = (ulong)((bx & 0xFFF) | ((by & 0xFFF) << 12));
        return p1 < p2 ? (p1 << 24) | p2 : (p2 << 24) | p1;
    }

    private static void ChaikinClosed(List<Vector2> points, int passes)
    {
        for (var pass = 0; pass < passes; pass++)
        {
            var next = new List<Vector2>(points.Count * 2);
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                next.Add(Vector2.Lerp(a, b, 0.25f));
                next.Add(Vector2.Lerp(a, b, 0.75f));
            }

            points.Clear();
            points.AddRange(next);
        }
    }

    private static Vector2[] ResampleArcLengthClosed(List<Vector2> points, int target)
    {
        var n = points.Count;
        var cumulative = new float[n + 1];
        for (var i = 0; i < n; i++)
        {
            cumulative[i + 1] = cumulative[i] + Vector2.Distance(points[i], points[(i + 1) % n]);
        }

        var total = Mathf.Max(0.001f, cumulative[n]);
        var output = new Vector2[target];
        for (var i = 0; i < target; i++)
        {
            var d = (i / (float)target) * total;
            var seg = 0;
            while (seg < n - 1 && cumulative[seg + 1] < d)
            {
                seg++;
            }

            var segLen = Mathf.Max(0.0001f, cumulative[seg + 1] - cumulative[seg]);
            var t = Mathf.Clamp01((d - cumulative[seg]) / segLen);
            output[i] = Vector2.Lerp(points[seg], points[(seg + 1) % n], t);
        }

        return output;
    }
}
