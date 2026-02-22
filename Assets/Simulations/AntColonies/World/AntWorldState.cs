using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AntWorldState
{
    public const int TileSize = 64;

    [Serializable]
    public struct NestEntry
    {
        public string speciesId;
        public int teamId;
        public Vector2 position;
        public int hp;
        public Color teamColor;
        public int foodStored;
    }

    [Serializable]
    public struct FoodPileEntry
    {
        public int id;
        public Vector2 position;
        public int remaining;
        public int respawnAtTick;
    }

    [Serializable]
    public struct ObstacleEntry
    {
        public Vector2 position;
        public float radius;
    }

    [Serializable]
    public struct DecorEntry
    {
        public Vector2 position;
        public string spriteId;
        public float rotation;
        public float scale;
        public float alpha;
    }

    public List<NestEntry> nests = new();
    public List<FoodPileEntry> foodPiles = new();
    public List<ObstacleEntry> obstacles = new();
    public List<DecorEntry> decor = new();

    public string[] tileSpriteIds = new string[TileSize * TileSize];
    public bool[] pathMask = new bool[TileSize * TileSize];
}
