using System.Collections.Generic;
using System.IO;
using System.Text;

public static class RunEventJsonlWriter
{
    public static void WriteEvents(string path, IEnumerable<RunEventBase> events)
    {
        EnsureDirectory(path);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));

        foreach (var runEvent in events)
        {
            writer.WriteLine(RunEventJson.SerializeEvent(runEvent));
        }
    }

    public static void AppendEvent(string path, RunEventBase runEvent)
    {
        EnsureDirectory(path);

        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.WriteLine(RunEventJson.SerializeEvent(runEvent));
    }

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
