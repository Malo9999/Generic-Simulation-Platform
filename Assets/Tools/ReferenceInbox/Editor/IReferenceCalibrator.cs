public interface IReferenceCalibrator
{
    string CalibratorId { get; }
    bool CanCalibrate(PackRecipe recipe);
    ReferenceCalibrationReport Calibrate(PackRecipe recipe);
}
