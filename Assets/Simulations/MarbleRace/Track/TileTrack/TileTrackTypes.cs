using System.Collections.Generic;
using UnityEngine;

public enum TileDir
{
    North,
    East,
    South,
    West
}

public enum TilePieceType
{
    Straight,
    CornerLeft,
    CornerRight,
    Chicane,
    StraightLong
}

public readonly struct TilePlacedPiece
{
    public readonly int GridX;
    public readonly int GridY;
    public readonly TileDir EntryDir;
    public readonly TilePieceType PieceType;

    public TilePlacedPiece(int gridX, int gridY, TileDir entryDir, TilePieceType pieceType)
    {
        GridX = gridX;
        GridY = gridY;
        EntryDir = entryDir;
        PieceType = pieceType;
    }
}

public static class TileTrackTypes
{
    public static readonly float DefaultCellSize = 6f;

    public static TileDir Opposite(TileDir dir)
    {
        return (TileDir)(((int)dir + 2) & 3);
    }

    public static TileDir TurnLeft(TileDir heading)
    {
        return (TileDir)(((int)heading + 3) & 3);
    }

    public static TileDir TurnRight(TileDir heading)
    {
        return (TileDir)(((int)heading + 1) & 3);
    }

    public static Vector2Int DirToInt(TileDir dir)
    {
        switch (dir)
        {
            case TileDir.North: return new Vector2Int(0, 1);
            case TileDir.East: return new Vector2Int(1, 0);
            case TileDir.South: return new Vector2Int(0, -1);
            default: return new Vector2Int(-1, 0);
        }
    }

    public static TileDir ResolveExit(TilePieceType piece, TileDir entryDir)
    {
        var heading = Opposite(entryDir);
        switch (piece)
        {
            case TilePieceType.CornerLeft:
                return Opposite(TurnLeft(heading));
            case TilePieceType.CornerRight:
                return Opposite(TurnRight(heading));
            case TilePieceType.Chicane:
            case TilePieceType.StraightLong:
            case TilePieceType.Straight:
            default:
                return Opposite(heading);
        }
    }

    public static List<Vector2> GetLocalPoints(TilePieceType piece, TileDir entryDir)
    {
        var exitDir = ResolveExit(piece, entryDir);
        var start = EdgeMid(entryDir);
        var end = EdgeMid(exitDir);

        if (piece == TilePieceType.Straight || piece == TilePieceType.StraightLong)
        {
            return BuildLine(start, end, 7);
        }

        if (piece == TilePieceType.Chicane)
        {
            var center = new Vector2(0.5f, 0.5f);
            var p1 = Vector2.Lerp(start, center + (Vector2.Perpendicular(end - start).normalized * 0.24f), 0.5f);
            var p2 = Vector2.Lerp(center, end, 0.5f);
            return new List<Vector2>
            {
                start,
                Vector2.Lerp(start, p1, 0.5f),
                p1,
                center,
                p2,
                Vector2.Lerp(p2, end, 0.5f),
                end
            };
        }

        var cornerCenter = ResolveCornerCenter(entryDir, exitDir);
        return BuildQuarterArc(start, end, cornerCenter, 7);
    }

    private static List<Vector2> BuildLine(Vector2 a, Vector2 b, int steps)
    {
        var pts = new List<Vector2>(steps);
        for (var i = 0; i < steps; i++)
        {
            var t = i / (float)(steps - 1);
            pts.Add(Vector2.Lerp(a, b, t));
        }

        return pts;
    }

    private static List<Vector2> BuildQuarterArc(Vector2 start, Vector2 end, Vector2 center, int steps)
    {
        var pts = new List<Vector2>(steps);
        var startA = Mathf.Atan2(start.y - center.y, start.x - center.x);
        var endA = Mathf.Atan2(end.y - center.y, end.x - center.x);
        var delta = Mathf.DeltaAngle(startA * Mathf.Rad2Deg, endA * Mathf.Rad2Deg) * Mathf.Deg2Rad;

        if (Mathf.Abs(delta) > Mathf.PI * 0.75f)
        {
            delta = delta > 0f ? delta - (Mathf.PI * 2f) : delta + (Mathf.PI * 2f);
        }

        for (var i = 0; i < steps; i++)
        {
            var t = i / (float)(steps - 1);
            var a = startA + (delta * t);
            pts.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.5f);
        }

        pts[0] = start;
        pts[pts.Count - 1] = end;
        return pts;
    }

    private static Vector2 EdgeMid(TileDir dir)
    {
        switch (dir)
        {
            case TileDir.North: return new Vector2(0.5f, 1f);
            case TileDir.East: return new Vector2(1f, 0.5f);
            case TileDir.South: return new Vector2(0.5f, 0f);
            default: return new Vector2(0f, 0.5f);
        }
    }

    private static Vector2 ResolveCornerCenter(TileDir entryDir, TileDir exitDir)
    {
        if ((entryDir == TileDir.West && exitDir == TileDir.North) || (entryDir == TileDir.North && exitDir == TileDir.West)) return new Vector2(0f, 1f);
        if ((entryDir == TileDir.North && exitDir == TileDir.East) || (entryDir == TileDir.East && exitDir == TileDir.North)) return new Vector2(1f, 1f);
        if ((entryDir == TileDir.East && exitDir == TileDir.South) || (entryDir == TileDir.South && exitDir == TileDir.East)) return new Vector2(1f, 0f);
        return new Vector2(0f, 0f);
    }
}
