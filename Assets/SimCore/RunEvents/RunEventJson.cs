using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class RunEventJson
{
    public static string SerializeEvent(RunEventBase runEvent)
    {
        var root = new Dictionary<string, object>
        {
            { "schemaVersion", runEvent.schemaVersion },
            { "tick", runEvent.tick },
            { "eventType", runEvent.eventType },
            { "payload", runEvent.payload ?? new Dictionary<string, object>() }
        };

        if (!string.IsNullOrEmpty(runEvent.entityId))
        {
            root["entityId"] = runEvent.entityId;
        }

        if (!string.IsNullOrEmpty(runEvent.teamId))
        {
            root["teamId"] = runEvent.teamId;
        }

        if (runEvent.position.HasValue)
        {
            var pos = runEvent.position.Value;
            root["position"] = new Dictionary<string, object>
            {
                { "x", pos.x },
                { "y", pos.y }
            };
        }

        return SerializeValue(root);
    }

    public static string SerializeValue(object value)
    {
        var builder = new StringBuilder(256);
        WriteValue(builder, value);
        return builder.ToString();
    }

    private static void WriteValue(StringBuilder builder, object value)
    {
        if (value == null)
        {
            builder.Append("null");
            return;
        }

        switch (value)
        {
            case string s:
                WriteEscapedString(builder, s);
                return;
            case bool b:
                builder.Append(b ? "true" : "false");
                return;
            case sbyte or byte or short or ushort or int or uint or long or ulong:
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            case float f:
                builder.Append(f.ToString("R", CultureInfo.InvariantCulture));
                return;
            case double d:
                builder.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            case decimal m:
                builder.Append(m.ToString(CultureInfo.InvariantCulture));
                return;
            case IDictionary<string, object> objectDictionary:
                WriteObject(builder, objectDictionary);
                return;
            case IList<object> objectList:
                WriteList(builder, objectList);
                return;
            case IDictionary dictionary:
                WriteUntypedDictionary(builder, dictionary);
                return;
            case IEnumerable enumerable when value is not string:
                WriteUntypedList(builder, enumerable);
                return;
            default:
                WriteEscapedString(builder, value.ToString());
                return;
        }
    }

    private static void WriteObject(StringBuilder builder, IDictionary<string, object> dictionary)
    {
        builder.Append('{');
        var first = true;
        foreach (var kvp in dictionary)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            WriteEscapedString(builder, kvp.Key);
            builder.Append(':');
            WriteValue(builder, kvp.Value);
        }
        builder.Append('}');
    }

    private static void WriteUntypedDictionary(StringBuilder builder, IDictionary dictionary)
    {
        builder.Append('{');
        var first = true;
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is not string key)
            {
                continue;
            }

            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            WriteEscapedString(builder, key);
            builder.Append(':');
            WriteValue(builder, entry.Value);
        }

        builder.Append('}');
    }

    private static void WriteList(StringBuilder builder, IList<object> list)
    {
        builder.Append('[');
        for (var i = 0; i < list.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            WriteValue(builder, list[i]);
        }

        builder.Append(']');
    }

    private static void WriteUntypedList(StringBuilder builder, IEnumerable values)
    {
        builder.Append('[');
        var first = true;
        foreach (var value in values)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            WriteValue(builder, value);
        }

        builder.Append(']');
    }

    private static void WriteEscapedString(StringBuilder builder, string value)
    {
        builder.Append('"');

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (c < 0x20)
                    {
                        builder.Append("\\u");
                        builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }
                    break;
            }
        }

        builder.Append('"');
    }
}
