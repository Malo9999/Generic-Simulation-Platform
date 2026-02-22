using System.IO;
using System.Text;
using UnityEngine;

public static class ReferenceInboxScaffolder
{
    public static string ProjectRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    public static void EnsureStructure(PackRecipe recipe)
    {
        if (recipe == null || string.IsNullOrWhiteSpace(recipe.simulationId))
        {
            return;
        }

        var simulationFolder = Path.Combine(ProjectRoot(), "_References", recipe.simulationId);
        Directory.CreateDirectory(simulationFolder);

        if (recipe.referenceAssets == null)
        {
            return;
        }

        foreach (var referenceNeed in recipe.referenceAssets)
        {
            if (referenceNeed == null || string.IsNullOrWhiteSpace(referenceNeed.assetId))
            {
                continue;
            }

            var assetFolder = Path.Combine(simulationFolder, referenceNeed.assetId);
            var imagesFolder = Path.Combine(assetFolder, "Images");
            var topviewFolder = Path.Combine(assetFolder, "Topview");

            Directory.CreateDirectory(assetFolder);
            Directory.CreateDirectory(imagesFolder);
            Directory.CreateDirectory(topviewFolder);

            var readmePath = Path.Combine(assetFolder, "README.txt");
            if (!File.Exists(readmePath))
            {
                var builder = new StringBuilder();
                builder.AppendLine($"Reference inbox for simulation '{recipe.simulationId}' asset '{referenceNeed.assetId}'.");
                builder.AppendLine();
                builder.AppendLine("- Put profile/reference photos in Images/");
                builder.AppendLine("- Put optional top-down images in Topview/");
                builder.AppendLine("- Run \"Normalize Inbox...\" after dropping images to auto-rename consistently.");
                File.WriteAllText(readmePath, builder.ToString());
            }
        }
    }
}
