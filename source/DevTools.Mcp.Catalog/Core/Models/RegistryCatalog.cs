namespace DevTools.Mcp.Core.Models;

public sealed record RegistryCatalog
{
    public IReadOnlyList<RegisteredTool> Tools { get; init; } = [];
    public IReadOnlyList<RegisteredResource> Resources { get; init; } = [];
    public IReadOnlyDictionary<string, string> Failures { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public static RegistryCatalog Empty { get; } = new();

    public static RegistryCatalog Failed(string path, string message) =>
        new() { Failures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [path] = message } };

    public RegistryCatalog Merge(RegistryCatalog other) => new()
    {
        Tools = [.. Tools, .. other.Tools],
        Resources = [.. Resources, .. other.Resources],
        Failures = MergeFailures(Failures, other.Failures)
    };

    internal static IReadOnlyDictionary<string, string> MergeFailures(params IReadOnlyDictionary<string, string>[] sources)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            foreach (var (path, message) in source)
                merged[path] = message;
        }

        return merged;
    }
}
