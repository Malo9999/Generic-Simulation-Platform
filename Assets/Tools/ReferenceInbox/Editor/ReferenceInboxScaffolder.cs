using System.IO;
using System.Text;
using UnityEngine;

public static class ReferenceInboxScaffolder
{
    public static string ProjectRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    public static string[] EnsureStructure(PackRecipe recipe)
    {
        var createdPaths = new System.Collections.Generic.List<string>();
        if (recipe == null || string.IsNullOrWhiteSpace(recipe.simulationId))
        {
            return createdPaths.ToArray();
        }

        var simulationFolder = Path.Combine(ProjectRoot(), "_References", recipe.simulationId);
        Directory.CreateDirectory(simulationFolder);
        createdPaths.Add(simulationFolder);

        if (recipe.referenceAssets == null)
        {
            return createdPaths.ToArray();
        }

        foreach (var referenceNeed in recipe.referenceAssets)
        {
            if (referenceNeed == null || string.IsNullOrWhiteSpace(referenceNeed.assetId))
            {
                continue;
            }

            var assetFolder = Path.Combine(simulationFolder, referenceNeed.assetId);
            var imagesFolder = Path.Combine(assetFolder, "Images");

            Directory.CreateDirectory(assetFolder);
            Directory.CreateDirectory(imagesFolder);
            createdPaths.Add(assetFolder);
            createdPaths.Add(imagesFolder);

            var readmePath = Path.Combine(assetFolder, "README.txt");
            var builder = new StringBuilder();
            builder.AppendLine("Drop top-down images into Images/. Recommended 1–5 good images.");
            File.WriteAllText(readmePath, builder.ToString());
        }

        return createdPaths.ToArray();
    }
}
