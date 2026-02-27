using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class MarbleRaceTrackGenLabWindow : EditorWindow
{
    private sealed class SeedReport
    {
        public int Seed;
        public bool Passed;
        public IReadOnlyList<string> Reasons;
    }

    private readonly List<SeedReport> reports = new();

    private int baseSeed = 1000;
    private int seedCount = 25;
    private int marbleCount = 12;
    private float arenaWidth = 64f;
    private float arenaHeight = 64f;
    private int trackVariant;
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

        if (GUILayout.Button("Generate + Validate Seeds"))
        {
            RunLab();
        }

        using (new EditorGUI.DisabledScope(selectedIndex < 0 || selectedIndex >= reports.Count))
        {
            if (GUILayout.Button("Preview Selected Seed In Scene"))
            {
                PreviewSelected();
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"Results ({reports.Count})", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (var i = 0; i < reports.Count; i++)
        {
            var report = reports[i];
            var color = GUI.color;
            GUI.color = report.Passed ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.65f, 0.65f);
            if (GUILayout.Button($"[{(report.Passed ? "PASS" : "FAIL")}] seed={report.Seed}"))
            {
                selectedIndex = i;
            }

            GUI.color = color;

            if (selectedIndex == i)
            {
                EditorGUI.indentLevel++;
                for (var r = 0; r < report.Reasons.Count; r++)
                {
                    EditorGUILayout.LabelField($"- {report.Reasons[r]}", EditorStyles.wordWrappedMiniLabel);
                }

                EditorGUI.indentLevel--;
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void RunLab()
    {
        reports.Clear();
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
                    Passed = validation.Passed,
                    Reasons = validation.Reasons
                });
            }
        }
        finally
        {
            renderer.Clear();
            DestroyImmediate(labRoot);
        }

        var passCount = 0;
        for (var i = 0; i < reports.Count; i++)
        {
            if (reports[i].Passed)
            {
                passCount++;
            }
        }

        Debug.Log($"[TrackGenLab] Completed {reports.Count} seeds. pass={passCount} fail={reports.Count - passCount}");
    }

    private void PreviewSelected()
    {
        var report = reports[selectedIndex];
        var seed = report.Seed;
        var variant = trackVariant + selectedIndex;
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
        Selection.activeGameObject = root;
        Debug.Log($"[TrackGenLab] Previewed seed={seed} variant={variant}");
    }
}
