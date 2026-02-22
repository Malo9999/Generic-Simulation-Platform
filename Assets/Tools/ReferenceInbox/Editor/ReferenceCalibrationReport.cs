using System;
using System.Collections.Generic;

[Serializable]
public sealed class ReferenceCalibrationReport
{
    [Serializable]
    public sealed class AssetSummary
    {
        public string calibratorId;
        public string assetId;
        public string mappedSpeciesId;
        public string bestImagePath;
        public string silhouettePath;
        public float score;
        public int fragmentCount;
        public List<string> warnings = new();
    }

    public string simulationId;
    public int assetsProcessed;
    public int imagesAnalyzed;
    public int profilesUpdated;
    public List<string> warnings = new();
    public List<AssetSummary> assets = new();

    public string Summary => $"[References] Calibration complete for {simulationId}: assets={assetsProcessed}, images={imagesAnalyzed}, profiles={profilesUpdated}, warnings={warnings.Count}";
}
