using System.Text;

namespace Brava.Api.Modules.Products;

/// <summary>
/// Minimal RFC4180-ish CSV reader/writer for the catalogue export/import —
/// no external dependency for a small, self-controlled format. Handles
/// quoted fields (embedded commas/newlines, doubled quotes) and a leading
/// UTF-8 BOM (Excel adds one on save, and expects one on the way out so
/// accented characters render correctly instead of getting mis-decoded).
/// </summary>
public static class CsvUtils
{
    public static string EscapeField(string? value)
    {
        value ??= string.Empty;
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    public static string BuildRow(IEnumerable<string?> fields) => string.Join(",", fields.Select(EscapeField));

    /// <summary>Parses full CSV text into rows of raw field strings.</summary>
    public static List<List<string>> Parse(string text)
    {
        const char byteOrderMark = '﻿';
        if (text.Length > 0 && text[0] == byteOrderMark)
        {
            text = text[1..];
        }

        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        void EndField()
        {
            row.Add(field.ToString());
            field.Clear();
        }

        void EndRow()
        {
            EndField();
            rows.Add(row);
            row = [];
        }

        while (i < text.Length)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuotes = false;
                    i++;
                    continue;
                }
                field.Append(c);
                i++;
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    i++;
                    break;
                case ',':
                    EndField();
                    i++;
                    break;
                case '\r':
                    i++;
                    break;
                case '\n':
                    EndRow();
                    i++;
                    break;
                default:
                    field.Append(c);
                    i++;
                    break;
            }
        }

        // A file not ending in a newline still has one last field/row pending.
        if (field.Length > 0 || row.Count > 0)
        {
            EndRow();
        }

        return rows;
    }
}
