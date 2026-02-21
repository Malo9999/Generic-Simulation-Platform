using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class ReferenceFetchWindow : EditorWindow
{
    private const string DefaultAssets = "FireAnt\nCarpenterAnt\nLionMale\nLionFemale\nLionCub";

    private ReferenceNeeds needsAsset;
    private string simulationName = "AntColonies";
    private string assetsText = DefaultAssets;
    private int imagesPerAsset = 12;
    private bool allowCc0 = true;
    private bool allowPublicDomain = true;
    private bool allowCcBy;
    private bool allowCcBySa;
    private int minWidth = 800;
    private bool dryRun;

    [MenuItem("Tools/Generic Simulation Platform/References/Fetch References…")]
    public static void Open() => GetWindow<ReferenceFetchWindow>("Reference Fetch");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Needs", EditorStyles.boldLabel);
        needsAsset = (ReferenceNeeds)EditorGUILayout.ObjectField("Reference Needs", needsAsset, typeof(ReferenceNeeds), false);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Load Needs", GUILayout.Width(100)))
            {
                ApplyNeeds();
            }
        }

        EditorGUILayout.Space();

        simulationName = EditorGUILayout.TextField("Simulation Name", simulationName);
        EditorGUILayout.LabelField("Assets (one per line)");
        assetsText = EditorGUILayout.TextArea(assetsText, GUILayout.MinHeight(90));
        imagesPerAsset = EditorGUILayout.IntField("Images Per Asset", Mathf.Max(1, imagesPerAsset));
        minWidth = EditorGUILayout.IntField("Min Width", Mathf.Max(1, minWidth));

        EditorGUILayout.LabelField("Allowed Licenses", EditorStyles.boldLabel);
        allowCc0 = EditorGUILayout.ToggleLeft("CC0", allowCc0);
        allowPublicDomain = EditorGUILayout.ToggleLeft("Public Domain", allowPublicDomain);
        allowCcBy = EditorGUILayout.ToggleLeft("CC-BY", allowCcBy);
        allowCcBySa = EditorGUILayout.ToggleLeft("CC-BY-SA", allowCcBySa);
        dryRun = EditorGUILayout.Toggle("Dry Run", dryRun);

        if (!allowCc0 && !allowPublicDomain && !allowCcBy && !allowCcBySa)
        {
            EditorGUILayout.HelpBox("Select at least one license.", MessageType.Warning);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Fetch"))
        {
            Fetch();
        }
    }

    private void ApplyNeeds()
    {
        if (needsAsset == null)
        {
            return;
        }

        simulationName = needsAsset.simulationName;
        assetsText = string.Join(Environment.NewLine, needsAsset.assets ?? new List<string>());
        imagesPerAsset = Mathf.Max(1, needsAsset.imagesPerAsset);
        allowCcBy = needsAsset.allowCCBY;
        allowCcBySa = needsAsset.allowCCBYSA;
        allowCc0 = needsAsset.allowCC0;
        allowPublicDomain = needsAsset.allowPublicDomain;
        minWidth = Mathf.Max(1, needsAsset.minWidth);
    }

    private void Fetch()
    {
        var assets = assetsText
            .Split(new[] { '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim())
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrWhiteSpace(simulationName) || assets.Count == 0)
        {
            EditorUtility.DisplayDialog("Reference Fetch", "Simulation Name and at least one asset are required.", "OK");
            return;
        }

        var licenseFilter = new LicenseFilter
        {
            AllowCc0 = allowCc0,
            AllowPublicDomain = allowPublicDomain,
            AllowCcBy = allowCcBy,
            AllowCcBySa = allowCcBySa,
        };

        if (!licenseFilter.IsAllowed("cc0") && !licenseFilter.IsAllowed("public domain") && !licenseFilter.IsAllowed("cc-by") && !licenseFilter.IsAllowed("cc-by-sa"))
        {
            EditorUtility.DisplayDialog("Reference Fetch", "Enable at least one allowed license.", "OK");
            return;
        }

        var request = new ReferenceFetchRequest
        {
            SimulationName = simulationName.Trim(),
            Assets = assets,
            ImagesPerAsset = Mathf.Max(1, imagesPerAsset),
            MinWidth = Mathf.Max(1, minWidth),
            DryRun = dryRun,
            LicenseFilter = licenseFilter,
        };

        try
        {
            new ReferenceFetchService().Fetch(request);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Reference Fetch", dryRun ? "Dry run complete. Check Console for planned downloads." : "Reference fetch complete.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Reference Fetch", $"Fetch failed: {ex.Message}", "OK");
        }
    }
}
