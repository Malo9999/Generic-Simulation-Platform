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

            var assetPaths = EnsureAssetStructure(recipe.simulationId, referenceNeed.assetId, referenceNeed.minImages);
            createdPaths.AddRange(assetPaths);
        }

        return createdPaths.ToArray();
    }

    public static string[] EnsureAssetStructure(string simulationId, string assetId, int minImages = 1)
    {
        var createdPaths = new System.Collections.Generic.List<string>();
        if (string.IsNullOrWhiteSpace(simulationId) || string.IsNullOrWhiteSpace(assetId))
        {
            return createdPaths.ToArray();
        }

        var simulationFolder = Path.Combine(ProjectRoot(), "_References", simulationId);
        var assetFolder = Path.Combine(simulationFolder, assetId);
        var imagesFolder = Path.Combine(assetFolder, "Images");
        Directory.CreateDirectory(simulationFolder);
        Directory.CreateDirectory(assetFolder);
        Directory.CreateDirectory(imagesFolder);
        createdPaths.Add(assetFolder);
        createdPaths.Add(imagesFolder);

        var readmePath = Path.Combine(assetFolder, "README.txt");
        var builder = new StringBuilder();
        builder.AppendLine($"Drop top-down images into Images/. Minimum recommended images: {Mathf.Max(1, minImages)}.");
        File.WriteAllText(readmePath, builder.ToString());
        createdPaths.Add(readmePath);

        return createdPaths.ToArray();
    }
}
