public static class RngService
{
    public static IRng Global { get; private set; } = new SeededRng(0);

    public static void SetGlobal(IRng rng)
    {
        if (rng != null)
        {
            Global = rng;
        }
    }
}
