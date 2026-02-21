using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AntTilesetSpriteIndexMap", menuName = "GSP/Ant Colonies/Tileset Sprite Index Map")]
public sealed class AntTilesetSpriteIndexMap : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string tileName;
        public AntTilesheetKind sheet;
        public int spriteIndex;
    }

    [SerializeField] private List<Entry> entries = new();

    public IReadOnlyList<Entry> Entries => entries;

    public void SetEntries(IEnumerable<Entry> updatedEntries)
    {
        entries = new List<Entry>(updatedEntries);
    }
}

public enum AntTilesheetKind
{
    Surface,
    Underground
}
