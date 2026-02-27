using UnityEngine;

public static class RenderOrder
{
    public const string SortingLayerDefault = "Default";

    // World
    public const int WorldBase = 0;
    public const int WorldTiles = WorldBase + 0;
    public const int WorldDeco = WorldBase + 200;
    public const int GroundDecor = WorldDeco; // Backward-compatible alias
    public const int WorldAbove = WorldBase + 400;

    // Entities (ALL sims)
    public const int EntitiesBase = 1000;
    public const int EntityBody = EntitiesBase + 0;
    public const int EntityArrow = EntitiesBase + 2;
    public const int EntityFx = EntitiesBase + 100;

    // Selection / highlights
    public const int SelectionBase = 2000;
    public const int SelectionRing = SelectionBase + 0;
    public const int SelectionHalo = SelectionBase + 10;

    // Debug (always on top of world+entities)
    public const int DebugBase = 5000;
    public const int DebugEntity = DebugBase + 0;
    public const int DebugArrow = DebugBase + 2;

    public static void Apply(SpriteRenderer sr, int order)
    {
        if (sr == null)
        {
            return;
        }

        sr.sortingLayerName = SortingLayerDefault;
        sr.sortingOrder = order;
    }
}
