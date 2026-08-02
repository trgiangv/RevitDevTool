using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace DevTools.FileMetadata.Revit;

/// <summary>
/// Extracts structured metadata from <c>Global/PartitionTable</c>:
/// view names with types, loaded families, and view templates.
/// </summary>
internal static partial class PartitionTableReader
{
    public static PartitionSummary? Read(byte[] decompressedPartitionTable)
    {
        if (decompressedPartitionTable.Length == 0) return null;

        var strings = ExtractUtf16Strings(decompressedPartitionTable);
        return strings.Count == 0 ? null : Classify(strings);
    }

    internal static byte[] Decompress(byte[] raw)
    {
        for (var i = 0; i < raw.Length - 1; i++)
        {
            if (raw[i] != 0x1F || raw[i + 1] != 0x8B) continue;

            try
            {
                using var input = new MemoryStream(raw, i, raw.Length - i, writable: false);
                using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
                using var output = new MemoryStream();
                gzip.CopyTo(output);
                if (output.Length > 0)
                    return output.ToArray();
            }
            catch
            {
                // ignored
            }
        }

        return [];
    }

    private static List<string> ExtractUtf16Strings(byte[] data)
    {
        var result = new List<string>();
        for (var i = 0; i < data.Length - 8; i += 2)
        {
            var len = 0;
            for (var j = i; j < data.Length - 1; j += 2)
            {
                var lo = data[j];
                var hi = data[j + 1];
                if (hi == 0 && lo is >= 0x20 and < 0x7F)
                    len++;
                else
                    break;
            }

            if (len < 5) continue;

            result.Add(Encoding.Unicode.GetString(data, i, len * 2));
            i += len * 2 - 2;
        }

        return result;
    }

    private static PartitionSummary Classify(List<string> strings)
    {
        var views = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var families = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var viewTemplates = new List<string>();

        foreach (var s in strings)
            ClassifyOne(s, views, families, viewTemplates);

        foreach (var list in views.Values) list.Sort(StringComparer.Ordinal);
        foreach (var list in families.Values) list.Sort(StringComparer.Ordinal);
        viewTemplates.Sort(StringComparer.Ordinal);

        return new PartitionSummary
        {
            Views = views.ToDictionary(
                kvp => kvp.Key, IReadOnlyList<string> (kvp) => kvp.Value,
                StringComparer.Ordinal),
            Families = families.ToDictionary(
                kvp => kvp.Key, IReadOnlyList<string> (kvp) => kvp.Value,
                StringComparer.Ordinal),
            ViewTemplates = viewTemplates,
        };
    }

    private static void ClassifyOne(
        string s,
        Dictionary<string, List<string>> views,
        Dictionary<string, List<string>> families,
        List<string> viewTemplates)
    {
        var viewMatch = ViewPattern.Match(s);
        if (viewMatch.Success)
        {
            AddToGroup(views, viewMatch.Groups[1].Value, viewMatch.Groups[2].Value);
            return;
        }

        var templateMatch = ViewTemplatePattern.Match(s);
        if (templateMatch.Success)
        {
            viewTemplates.Add(templateMatch.Groups[1].Value);
            return;
        }

        var familyMatch = FamilyPattern.Match(s);
        if (familyMatch.Success)
            AddToGroup(families, familyMatch.Groups[1].Value.Trim(), familyMatch.Groups[2].Value.Trim());
    }

    private static void AddToGroup(Dictionary<string, List<string>> dict, string key, string value)
    {
        if (!dict.TryGetValue(key, out var list))
        {
            list = [];
            dict[key] = list;
        }

        list.Add(value);
    }

    private static readonly Regex ViewPattern = new("^View \\\"(.+?): (.+)\\\"$", RegexOptions.CultureInvariant);
    private static readonly Regex ViewTemplatePattern = new("^View Template \\\"(.+)\\\"$", RegexOptions.CultureInvariant);
    private static readonly Regex FamilyPattern = new("^Family  : (.+?) : (.+)$", RegexOptions.CultureInvariant);
}

[PublicAPI]
public sealed record PartitionSummary
{
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Views { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Families { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();

    public IReadOnlyList<string> ViewTemplates { get; init; } = [];
}
