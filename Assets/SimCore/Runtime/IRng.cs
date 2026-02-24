using System.Collections.Generic;
using UnityEngine;

public interface IRng
{
    int Seed { get; }

    uint NextUInt();
    int NextInt(int minInclusive, int maxExclusive);
    float NextFloat01();
    float Range(float minInclusive, float maxInclusive);
    bool Chance(float p01);
    void Shuffle<T>(IList<T> list);

    Vector2 InsideUnitCircle();
    int Sign();
    int PickIndexWeighted(IReadOnlyList<float> weights);

    // Back-compat APIs.
    float Value();
    int Range(int minInclusive, int maxExclusive);
    double NextDouble();
}
