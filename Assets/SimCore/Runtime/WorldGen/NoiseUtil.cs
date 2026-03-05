public static class NoiseUtil
{
    public static float Sample2D(NoiseDescriptor descriptor, float x, float y, int seed)
    {
        return NoiseSampler.Sample2D(descriptor, x, y, seed);
    }
}
