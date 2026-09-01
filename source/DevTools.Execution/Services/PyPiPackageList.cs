using System.Text.Json;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Services;

internal static class PyPiPackageList
{
    public static IReadOnlyList<Package> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        var packages = new List<Package>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (!TryGetString(item, "name", out var name))
                continue;

            var version = TryGetString(item, "version", out var v) ? v : null;
            packages.Add(new Package(
                Marketplace.PyPi,
                name,
                version,
                version,
                PyEnvironmentProvider.RequirePackages.ContainsKey(name)));
        }

        return packages;
    }

    private static bool TryGetString(JsonElement item, string propertyName, out string value)
    {
        value = string.Empty;
        if (!item.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return false;

        var text = property.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        value = text!;
        return true;
    }
}
