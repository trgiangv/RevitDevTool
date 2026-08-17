using System.Text.RegularExpressions;

namespace DevTools.Hosting.Revit;

public static partial class RevitVersionSelector
{
    [GeneratedRegex(@"20\d{2}")]
    private static partial Regex YearPattern();
    private static readonly Regex YearRegex = YearPattern();

    /// <summary>
    /// File year is a minimum. Picks the oldest installed version that is still &gt;= the file year.
    /// When <paramref name="documentYear"/> is missing, returns the newest installed version.
    /// </summary>
    public static string? FindCompatibleVersion(
        string? documentYear,
        IReadOnlyList<string> installedVersions)
    {
        if (installedVersions.Count == 0)
            return null;

        var fileYearText = ExtractYear(documentYear);
        if (fileYearText is null || !int.TryParse(fileYearText, out var fileYear))
            return installedVersions.OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

        return installedVersions
            .Where(v => int.TryParse(v, out var year) && year >= fileYear)
            .OrderBy(v => v, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static string? ExtractYear(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = YearRegex.Match(value);
        return match.Success ? match.Value : null;
    }
}
