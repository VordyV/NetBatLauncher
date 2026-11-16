
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NBL.Services;

namespace NBLApp.Localization;

public sealed class MapLocalization
{
    private readonly Dictionary<string, (string English, string Russian)> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    public int Count => _entries.Count;

    public static MapLocalization Load(string path)
    {
        var localization = new MapLocalization();

        if (!System.IO.File.Exists(path))
            return localization;

        string[] delimiters = [",", ";", "\t", "|"];

        foreach (string delimiter in delimiters)
        {
            try
            {
                using var parser = new TextFieldParser(path, Encoding.UTF8, true)
                {
                    TextFieldType = FieldType.Delimited,
                    HasFieldsEnclosedInQuotes = true,
                    TrimWhiteSpace = true
                };

                parser.SetDelimiters(delimiter);

                if (parser.EndOfData)
                    continue;

                string[]? headers = parser.ReadFields();

                if (headers == null)
                    continue;

                int idIndex = Array.FindIndex(headers,
                    h => Clean(h).Equals("StringID", StringComparison.OrdinalIgnoreCase));

                int enIndex = Array.FindIndex(headers,
                    h => Clean(h).Equals("English", StringComparison.OrdinalIgnoreCase));

                int ruIndex = Array.FindIndex(headers,
                    h => Clean(h).Equals("Russian", StringComparison.OrdinalIgnoreCase));

                if (idIndex < 0)
                    continue;

                while (!parser.EndOfData)
                {
                    string[]? fields = parser.ReadFields();

                    if (fields == null || fields.Length <= idIndex)
                        continue;

                    string id = Clean(fields[idIndex]);

                    if (string.IsNullOrWhiteSpace(id) || id.All(c => c == '-'))
                        continue;

                    string english = enIndex >= 0 && fields.Length > enIndex
                        ? Clean(fields[enIndex])
                        : "";

                    string russian = ruIndex >= 0 && fields.Length > ruIndex
                        ? Clean(fields[ruIndex])
                        : "";

                    localization._entries[id] = (english, russian);
                }

                if (localization.Count > 0)
                    return localization;
            }
            catch (MalformedLineException)
            {
                // Пробуем следующий разделитель.
            }
        }

        return localization;
    }

    private static string Clean(string value)
    {
        return value.Trim().Trim('\uFEFF', '|').Trim();
    }

    public string Get(string key, string fallback = "")
    {
        key = key?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(key) ||
            !_entries.TryGetValue(key, out var entry))
            return fallback;

        bool russian = Locale.CurrentCulture.TwoLetterISOLanguageName == "ru";

        string value = russian ? entry.Russian : entry.English;

        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
