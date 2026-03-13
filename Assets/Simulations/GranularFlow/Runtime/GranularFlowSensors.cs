using UnityEngine;

public struct GranularFlowSensors
{
    public float upperChamberFill;
    public float throatJamEstimate;
    public float lowerDominantColorNorm;
    public float leftBinFill;
    public float rightBinFill;
    public float recentThroughputEstimate;

    public static GranularFlowSensors FromParticleStats(
        int upperCount,
        int throatCount,
        int lowerCount,
        int leftBinCount,
        int rightBinCount,
        int throughputInWindow,
        int dominantColorId,
        int upperCapacity,
        int throatCapacity,
        int binCapacity,
        int paletteCount)
    {
        var sensors = new GranularFlowSensors
        {
            upperChamberFill = upperCapacity > 0 ? Mathf.Clamp01(upperCount / (float)upperCapacity) : 0f,
            throatJamEstimate = throatCapacity > 0 ? Mathf.Clamp01(throatCount / (float)throatCapacity) : 0f,
            leftBinFill = binCapacity > 0 ? Mathf.Clamp01(leftBinCount / (float)binCapacity) : 0f,
            rightBinFill = binCapacity > 0 ? Mathf.Clamp01(rightBinCount / (float)binCapacity) : 0f,
            recentThroughputEstimate = Mathf.Max(0f, throughputInWindow),
            lowerDominantColorNorm = paletteCount > 1 ? Mathf.Clamp01(dominantColorId / (float)(paletteCount - 1)) : 0f
        };

        if (lowerCount == 0)
        {
            sensors.lowerDominantColorNorm = 0.5f;
        }

        return sensors;
    }
}
