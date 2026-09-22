using System;
using System.Collections.Generic;
using System.Text;

namespace HOVS.Plugin;

/// <summary>
/// Safe storage format for raw XLSX source-cell snapshots.
/// v63-v73 used U+001F, which XML 1.0 forbids.
/// v74 writes U+241F and still reads legacy U+001F.
/// </summary>
public static class SourceCellCodec
{
    public const string Prefix = "__SourceCell.";
    public const char Separator = '\u241F';
    public const char LegacySeparator = '\u001F';

    public static string Encode(string header, string value)
    {
        return MakeXmlSafe(header) + Separator + MakeXmlSafe(value);
    }

    public static bool TryDecode(
        string payload,
        out string header,
        out string value)
    {
        payload = payload ?? "";

        var index = payload.IndexOf(Separator);
        if (index < 0)
            index = payload.IndexOf(LegacySeparator);

        if (index < 0)
        {
            header = "";
            value = payload;
            return false;
        }

        header = payload.Substring(0, index);
        value = payload.Substring(index + 1);
        return true;
    }

    public static Dictionary<string, string> MakeXmlSafe(
        IDictionary<string, string>? attributes)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        if (attributes == null)
            return result;

        foreach (var pair in attributes)
        {
            var key = MakeXmlSafe(pair.Key);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            result[key] = MakeXmlSafe(pair.Value);
        }

        return result;
    }

    public static string MakeXmlSafe(string? value)
    {
        // Use explicit flow checks. On Revit 2021-2024 / net48 the nullable
        // annotations of string.IsNullOrEmpty do not always narrow string?
        // strongly enough for the compiler.
        if (value == null)
            return "";

        if (value.Length == 0)
            return "";

        var sb = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];

            if (ch == LegacySeparator)
            {
                sb.Append(Separator);
                continue;
            }

            if (ch == '\t' || ch == '\n' || ch == '\r')
            {
                sb.Append(ch);
                continue;
            }

            if ((ch >= '\u0020' && ch <= '\uD7FF') ||
                (ch >= '\uE000' && ch <= '\uFFFD'))
            {
                sb.Append(ch);
                continue;
            }

            if (char.IsHighSurrogate(ch) &&
                i + 1 < value.Length &&
                char.IsLowSurrogate(value[i + 1]))
            {
                sb.Append(ch);
                sb.Append(value[++i]);
                continue;
            }

            sb.Append(' ');
        }

        return sb.ToString();
    }
}
