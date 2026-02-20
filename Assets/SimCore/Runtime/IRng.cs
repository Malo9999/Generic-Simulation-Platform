public interface IRng
{
    int Seed { get; }
    float Value();
    float Range(float minInclusive, float maxInclusive);
    int Range(int minInclusive, int maxExclusive);
    double NextDouble();
}
