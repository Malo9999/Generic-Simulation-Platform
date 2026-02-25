using UnityEngine;

public static class RenderOrder
{
    public const string SortingLayerDefault = "Default";

    // World
    public const int WorldBase = 0;
    public const int WorldTiles = WorldBase + 0;
    public const int WorldDeco = WorldBase + 20;
    public const int WorldAbove = WorldBase + 40;

    // Entities (ALL sims)
    public const int EntitiesBase = 100;
    public const int EntityBody = EntitiesBase + 0;
    public const int EntityArrow = EntitiesBase + 2;
    public const int EntityFx = EntitiesBase + 10;

    // Selection / highlights
    public const int SelectionBase = 200;
    public const int SelectionRing = SelectionBase + 0;
    public const int SelectionHalo = SelectionBase + 5;

    // Debug (always on top of world+entities)
    public const int DebugBase = 500;
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
