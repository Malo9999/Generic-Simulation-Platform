using System;
using System.Collections.Generic;

[Serializable]
public sealed class ReferenceCalibrationReport
{
    public string simulationId;
    public int assetsProcessed;
    public int imagesAnalyzed;
    public int profilesUpdated;
    public List<string> warnings = new();

    public string Summary => $"[References] Calibration complete for {simulationId}: assets={assetsProcessed}, images={imagesAnalyzed}, profiles={profilesUpdated}, warnings={warnings.Count}";
}
