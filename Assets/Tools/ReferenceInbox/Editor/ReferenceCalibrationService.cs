using System.Collections.Generic;

public static class ReferenceCalibrationService
{
    private static readonly List<IReferenceCalibrator> Calibrators = new()
    {
        new AntReferenceCalibrator()
    };

    public static ReferenceCalibrationReport Calibrate(PackRecipe recipe)
    {
        foreach (var calibrator in Calibrators)
        {
            if (calibrator.CanCalibrate(recipe))
            {
                return calibrator.Calibrate(recipe);
            }
        }

        return new ReferenceCalibrationReport
        {
            simulationId = recipe?.simulationId ?? "unknown",
            warnings = new List<string> { "No matching calibrator found for recipe." }
        };
    }
}
