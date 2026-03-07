using System;
using System.Collections.Generic;

[Serializable]
public class NoiseSet
{
    public List<NoiseDescriptor> descriptors = new List<NoiseDescriptor>();

    private Dictionary<string, int> indexById;

    public void Register(NoiseDescriptor descriptor, int seed)
    {
        if (string.IsNullOrEmpty(descriptor.id)) return;

        descriptor.offset = NoiseSampler.DeriveOffset(seed, descriptor.id);
        var idx = IndexOf(descriptor.id);
        if (idx >= 0) descriptors[idx] = descriptor;
        else descriptors.Add(descriptor);
        indexById = null;
    }

    public NoiseDescriptor Get(string id)
    {
        var idx = IndexOf(id);
        if (idx < 0) throw new KeyNotFoundException($"Noise descriptor '{id}' not found.");
        return descriptors[idx];
    }

    public NoiseDescriptor EnsureDefault(string id, int seed)
    {
        var idx = IndexOf(id);
        if (idx >= 0) return descriptors[idx];

        var descriptor = NoiseDescriptor.CreateDefault(id);
        descriptor.offset = NoiseSampler.DeriveOffset(seed, id);
        descriptors.Add(descriptor);
        indexById = null;
        return descriptor;
    }

    private int IndexOf(string id)
    {
        if (indexById == null || indexById.Count != descriptors.Count)
        {
            indexById = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < descriptors.Count; i++)
            {
                if (!string.IsNullOrEmpty(descriptors[i].id)) indexById[descriptors[i].id] = i;
            }
        }

        return indexById.TryGetValue(id, out var idx) ? idx : -1;
    }
}

[Serializable]
public class NoiseDescriptorSet : NoiseSet
{
}
