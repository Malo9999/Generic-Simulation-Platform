#if UNITY_EDITOR && GSP_TOOLING
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class ReferenceInboxWindow : EditorWindow
{
    private string simulationName = string.Empty;
    private string rootFolder;
    private int minWidth = 600;
    private Vector2 scroll;
    private string output = "Run Validate or Normalize to see details.";

    [MenuItem("GSP/Tooling/References/Normalize Inbox…")]
    public static void OpenWindow()
    {
        var window = GetWindow<ReferenceInboxWindow>("Reference Inbox");
        window.minSize = new Vector2(640f, 420f);
        window.Show();
    }

    private void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(rootFolder))
        {
            rootFolder = Path.Combine(ReferenceInboxService.GetProjectRoot(), "_References");
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Normalize Reference Inbox", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        simulationName = EditorGUILayout.TextField("SimulationName", simulationName);
        rootFolder = EditorGUILayout.TextField("RootFolder", rootFolder);
        minWidth = EditorGUILayout.IntField("MinWidth", minWidth);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate", GUILayout.Height(28f)))
            {
                RunValidate();
            }

            if (GUILayout.Button("Normalize", GUILayout.Height(28f)))
            {
                RunNormalize();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

        using var scrollView = new EditorGUILayout.ScrollViewScope(scroll);
        scroll = scrollView.scrollPosition;
        EditorGUILayout.TextArea(output, GUILayout.ExpandHeight(true));
    }

    private void RunValidate()
    {
        if (!EnsureInputs())
        {
            return;
        }

        var summary = ReferenceInboxService.ValidateSimulation(simulationName.Trim(), rootFolder.Trim(), minWidth);
        output = BuildValidationText(summary);
        Debug.Log(output);
    }

    private void RunNormalize()
    {
        if (!EnsureInputs())
        {
            return;
        }

        var report = ReferenceInboxService.NormalizeSimulation(simulationName.Trim(), rootFolder.Trim(), minWidth);
        output = BuildNormalizeText(report);
        AssetDatabase.Refresh();
        Debug.Log(output);
    }

    private bool EnsureInputs()
    {
        if (string.IsNullOrWhiteSpace(simulationName))
        {
            output = "SimulationName is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(rootFolder))
        {
            output = "RootFolder is required.";
            return false;
        }

        if (minWidth < 1)
        {
            minWidth = 1;
        }

        return true;
    }

    private static string BuildValidationText(ReferenceInboxService.ValidationSummary summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Validate simulation: {summary.simulation}");
        sb.AppendLine($"Root: {summary.rootFolder}");

        if (!summary.simulationFolderExists)
        {
            sb.AppendLine("Simulation folder missing.");
        }

        foreach (var warning in summary.warnings)
        {
            sb.AppendLine($"WARNING: {warning}");
        }

        foreach (var asset in summary.assets)
        {
            sb.AppendLine();
            sb.AppendLine($"Asset: {asset.assetName}");
            sb.AppendLine($"  profile folder: {(asset.profileFolderMissing ? "missing" : asset.profileFolderPath)}");
            sb.AppendLine($"  top folder: {(asset.topFolderPath ?? "missing")}");
            sb.AppendLine($"  profile images: {asset.profileFiles.Count}");
            sb.AppendLine($"  top images: {asset.topFiles.Count}");

            for (int i = 0; i < asset.profileFiles.Count; i++)
            {
                var file = asset.profileFiles[i];
                string to = $"profile_{i + 1:D3}{file.extension.ToLowerInvariant()}";
                sb.AppendLine($"    profile: {file.fileName} -> {to}");
            }

            for (int i = 0; i < asset.topFiles.Count; i++)
            {
                var file = asset.topFiles[i];
                string to = $"top_{i + 1:D3}{file.extension.ToLowerInvariant()}";
                sb.AppendLine($"    top: {file.fileName} -> {to}");
            }

            foreach (var warning in asset.warnings)
            {
                sb.AppendLine($"  WARNING: {warning}");
            }
        }

        return sb.ToString();
    }

    private static string BuildNormalizeText(ReferenceInboxService.NormalizeSessionReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Normalize simulation: {report.simulation}");
        sb.AppendLine($"Root: {report.rootFolder}");
        sb.AppendLine($"TimestampUtc: {report.timestampUtc}");

        foreach (var warning in report.warnings)
        {
            sb.AppendLine($"WARNING: {warning}");
        }

        foreach (var asset in report.assets)
        {
            sb.AppendLine();
            sb.AppendLine($"Asset: {asset.asset}");
            sb.AppendLine($"  profile renamed: {asset.profileRenamed}");
            sb.AppendLine($"  top renamed: {asset.topRenamed}");
            sb.AppendLine($"  manifest: {asset.manifestPath}");
            foreach (var warning in asset.warnings)
            {
                sb.AppendLine($"  WARNING: {warning}");
            }
        }

        return sb.ToString();
    }
}
#endif
