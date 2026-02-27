using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class MarbleRaceTrackGenLabWindow : EditorWindow
{
    private sealed class SeedReport
    {
        public int Seed;
        public int Variant;
        public bool ValidityPassed;
        public int QualityScore;
        public MarbleRaceTrackValidator.QualityBand Band;
        public IReadOnlyList<string> ValidityReasons;
        public IReadOnlyList<string> QualityIssues;
        public MarbleRaceTrackValidator.QualityReport Quality;
    }

    private readonly List<SeedReport> reports = new();
    private readonly List<SeedReport> filteredReports = new();

    private int baseSeed = 1000;
    private int seedCount = 25;
    private int marbleCount = 12;
    private float arenaWidth = 64f;
    private float arenaHeight = 64f;
    private int trackVariant;
    private int minQualityScore = 0;
    private bool sortDescending = true;
    private int selectedIndex = -1;
    private Vector2 scroll;

    [MenuItem("Tools/MarbleRace/TrackGen Lab")]
    private static void Open()
    {
        GetWindow<MarbleRaceTrackGenLabWindow>("TrackGen Lab");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MarbleRace TrackGen Lab", EditorStyles.boldLabel);
        baseSeed = EditorGUILayout.IntField("Base Seed", baseSeed);
        seedCount = EditorGUILayout.IntSlider("Seed Count", seedCount, 1, 200);
        marbleCount = EditorGUILayout.IntSlider("Marble Count", marbleCount, 2, 30);
        trackVariant = EditorGUILayout.IntField("Start Variant", trackVariant);
        arenaWidth = Mathf.Max(16f, EditorGUILayout.FloatField("Arena Width", arenaWidth));
        arenaHeight = Mathf.Max(16f, EditorGUILayout.FloatField("Arena Height", arenaHeight));

        EditorGUILayout.Space(6f);
        minQualityScore = EditorGUILayout.IntSlider("Min Quality Score", minQualityScore, 0, 100);
        sortDescending = EditorGUILayout.ToggleLeft("Sort by quality score (desc)", sortDescending);

        if (GUILayout.Button("Generate + Validate Seeds"))
        {
            RunLab();
        }

        RebuildFiltered();

        using (new EditorGUI.DisabledScope(selectedIndex < 0 || selectedIndex >= filteredReports.Count))
        {
            if (GUILayout.Button("Preview Selected Seed In Scene"))
            {
                PreviewSelected();
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"Results ({filteredReports.Count}/{reports.Count})", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (var i = 0; i < filteredReports.Count; i++)
        {
            var report = filteredReports[i];
            var color = GUI.color;
            GUI.color = BandColor(report.Band);
            var validity = report.ValidityPassed ? "VALID" : "INVALID";
            if (GUILayout.Button($"[{validity}] score={report.QualityScore} seed={report.Seed} variant={report.Variant}"))
            {
                selectedIndex = i;
            }

            GUI.color = color;
            if (selectedIndex == i)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Validity:", EditorStyles.boldLabel);
                for (var r = 0; r < report.ValidityReasons.Count; r++)
                {
                    EditorGUILayout.LabelField($"- {report.ValidityReasons[r]}", EditorStyles.wordWrappedMiniLabel);
                }

                EditorGUILayout.LabelField("Quality:", EditorStyles.boldLabel);
                for (var r = 0; r < report.QualityIssues.Count; r++)
                {
                    EditorGUILayout.LabelField($"- {report.QualityIssues[r]}", EditorStyles.wordWrappedMiniLabel);
                }

                EditorGUI.indentLevel--;
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private static Color BandColor(MarbleRaceTrackValidator.QualityBand band)
    {
        switch (band)
        {
            case MarbleRaceTrackValidator.QualityBand.Green:
                return new Color(0.6f, 1f, 0.6f);
            case MarbleRaceTrackValidator.QualityBand.Yellow:
                return new Color(1f, 0.9f, 0.45f);
            default:
                return new Color(1f, 0.65f, 0.65f);
        }
    }

    private void RunLab()
    {
        reports.Clear();
        filteredReports.Clear();
        selectedIndex = -1;

        var generator = new MarbleRaceTrackGenerator();
        var renderer = new MarbleRaceTrackRenderer();
        var labRoot = new GameObject("TrackGenLabTempRoot");
        try
        {
            for (var i = 0; i < seedCount; i++)
            {
                var seed = baseSeed + i;
                var variant = trackVariant + i;
                var trackSeed = unchecked(seed ^ (variant * (int)0x9E3779B9) ^ 0x7F4A7C15);
                var rng = new SeededRng(trackSeed);

                var track = generator.Build(arenaWidth * 0.5f, arenaHeight * 0.5f, rng, seed, variant, -1, out _);
                renderer.Apply(labRoot.transform, track);
                var validation = MarbleRaceTrackValidator.Validate(track, marbleCount, renderer.TrackRoot);
                reports.Add(new SeedReport
                {
                    Seed = seed,
                    Variant = variant,
                    ValidityPassed = validation.ValidityPassed,
                    QualityScore = validation.QualityScore,
                    Band = validation.Band,
                    ValidityReasons = validation.ValidityReasons,
                    QualityIssues = validation.QualityIssues,
                    Quality = validation.Quality
                });
            }
        }
        finally
        {
            renderer.Clear();
            DestroyImmediate(labRoot);
        }

        RebuildFiltered();
        var green = 0;
        var yellow = 0;
        var red = 0;
        for (var i = 0; i < reports.Count; i++)
        {
            switch (reports[i].Band)
            {
                case MarbleRaceTrackValidator.QualityBand.Green:
                    green++;
                    break;
                case MarbleRaceTrackValidator.QualityBand.Yellow:
                    yellow++;
                    break;
                default:
                    red++;
                    break;
            }
        }

        Debug.Log($"[TrackGenLab] Completed {reports.Count} seeds. green={green} yellow={yellow} red={red}");
    }

    private void RebuildFiltered()
    {
        filteredReports.Clear();
        for (var i = 0; i < reports.Count; i++)
        {
            if (reports[i].QualityScore >= minQualityScore)
            {
                filteredReports.Add(reports[i]);
            }
        }

        filteredReports.Sort((a, b) => sortDescending ? b.QualityScore.CompareTo(a.QualityScore) : a.QualityScore.CompareTo(b.QualityScore));
        if (selectedIndex >= filteredReports.Count)
        {
            selectedIndex = -1;
        }
    }

    private void PreviewSelected()
    {
        var report = filteredReports[selectedIndex];
        var seed = report.Seed;
        var variant = report.Variant;
        var trackSeed = unchecked(seed ^ (variant * (int)0x9E3779B9) ^ 0x7F4A7C15);
        var rng = new SeededRng(trackSeed);

        var generator = new MarbleRaceTrackGenerator();
        var track = generator.Build(arenaWidth * 0.5f, arenaHeight * 0.5f, rng, seed, variant, -1, out _);

        var root = GameObject.Find("TrackGenLabPreview");
        if (root == null)
        {
            root = new GameObject("TrackGenLabPreview");
        }

        var renderer = new MarbleRaceTrackRenderer();
        renderer.Apply(root.transform, track);

        var overlay = root.GetComponent<TrackGenLabQualityGizmos>();
        if (overlay == null)
        {
            overlay = root.AddComponent<TrackGenLabQualityGizmos>();
        }

        overlay.Assign(track, report.Quality);
        Selection.activeGameObject = root;
        Debug.Log($"[TrackGenLab] Previewed seed={seed} variant={variant} score={report.QualityScore}");
    }
}
