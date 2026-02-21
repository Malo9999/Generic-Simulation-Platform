using UnityEngine;

public static class SheetLayout
{
    public static Rect CellRect(int index, int columns, int cellSize, int totalCount)
    {
        var rows = Mathf.CeilToInt(totalCount / (float)columns);
        var col = index % columns;
        var row = rows - 1 - index / columns;
        return new Rect(col * cellSize, row * cellSize, cellSize, cellSize);
    }
}
