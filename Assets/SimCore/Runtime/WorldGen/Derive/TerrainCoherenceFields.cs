using System;

[Serializable]
public sealed class TerrainCoherenceFields
{
    public ScalarField normalizedHeight;
    public ScalarField slopeApprox;
    public ScalarField distanceToMainChannel;
    public ScalarField valleyMask;
    public ScalarField floodplainMask;
    public ScalarField ridgeMask;
    public ScalarField rockinessMask;

    public TerrainCoherenceFields(WorldGridSpec grid)
    {
        normalizedHeight = new ScalarField("terrain_height", grid);
        slopeApprox = new ScalarField("terrain_slope", grid);
        distanceToMainChannel = new ScalarField("terrain_distance_to_main_channel", grid);
        valleyMask = new ScalarField("terrain_valley", grid);
        floodplainMask = new ScalarField("terrain_floodplain", grid);
        ridgeMask = new ScalarField("terrain_ridge", grid);
        rockinessMask = new ScalarField("terrain_rockiness", grid);
    }

    public void WriteTo(WorldMap map)
    {
        map.scalars[normalizedHeight.id] = normalizedHeight;
        map.scalars[slopeApprox.id] = slopeApprox;
        map.scalars[distanceToMainChannel.id] = distanceToMainChannel;
        map.scalars[valleyMask.id] = valleyMask;
        map.scalars[floodplainMask.id] = floodplainMask;
        map.scalars[ridgeMask.id] = ridgeMask;
        map.scalars[rockinessMask.id] = rockinessMask;
    }
}
