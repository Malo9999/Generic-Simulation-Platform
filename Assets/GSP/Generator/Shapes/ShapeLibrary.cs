using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Shape Library", fileName = "ShapeLibrary")]
public class ShapeLibrary : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string id;
        public Sprite sprite;
        public ShapeTemplateBase template;
    }

    [SerializeField] private List<Entry> entries = new();

    public IReadOnlyList<Entry> Entries => entries;

    public bool TryGet(string id, out Sprite sprite)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].id, id, StringComparison.Ordinal))
            {
                sprite = entries[i].sprite;
                return sprite != null;
            }
        }

        sprite = null;
        return false;
    }

    public void Set(string id, Sprite sprite, ShapeTemplateBase template)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (!string.Equals(entries[i].id, id, StringComparison.Ordinal))
            {
                continue;
            }

            entries[i] = new Entry { id = id, sprite = sprite, template = template };
            return;
        }

        entries.Add(new Entry { id = id, sprite = sprite, template = template });
    }
}
