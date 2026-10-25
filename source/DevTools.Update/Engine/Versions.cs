using Velopack;

namespace DevTools.Update.Engine;

internal static class Versions
{
    public static bool IsNewer(string candidate, string? installed) =>
        installed is null || SemanticVersion.Parse(candidate).CompareTo(SemanticVersion.Parse(installed)) > 0;

    public static string? Max(IEnumerable<string?> versions) => versions.OfType<string>().MaxBy(SemanticVersion.Parse);
}
