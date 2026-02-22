#if UNITY_EDITOR && GSP_TOOLING
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RunEventSampleWriterMenu
{
    [MenuItem("GSP/Tooling/RunEvents/Write Sample events.jsonl")]
    public static void WriteSampleEventsJsonl()
    {
        var outputPath = Path.Combine(Application.temporaryCachePath, "RunEvents", "events.jsonl");
        var events = RunEventSample.GenerateSampleEvents();

        RunEventJsonlWriter.WriteEvents(outputPath, events);

        Debug.Log($"RunEventSampleWriterMenu: Wrote {events.Count} run events to '{outputPath}'.");
        EditorUtility.RevealInFinder(outputPath);
    }
}
#endif
