using System.Reflection;
using System.Text.Json;
using DevTools.Testing.Abstractions.Config;

namespace DevTools.TestAdapter;

internal static class AdapterTestConfig
{
    internal static string RequireFrameworkId()
    {
        if (TryReadFrameworkId(out var frameworkId))
            return frameworkId!;

        throw new InvalidOperationException(
            "RevitDevTool.TestAdapter requires 'devtools.frameworkId' in testconfig.json "
            + "(generated from <TestingFramework> in the test .csproj).");
    }

    internal static bool TryReadFrameworkId(out string? frameworkId)
    {
        foreach (var path in ResolveTestConfigPaths())
        {
            if (TryReadFrameworkId(path, out frameworkId))
                return true;
        }

        frameworkId = null;
        return false;
    }

    private static IEnumerable<string> ResolveTestConfigPaths()
    {
        var baseDirectory = AppContext.BaseDirectory;
        yield return Path.Combine(baseDirectory, HostTestConfig.FileName);

        var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (!string.IsNullOrWhiteSpace(entryAssemblyName))
            yield return Path.Combine(baseDirectory, entryAssemblyName + ".testconfig.json");
    }

    private static bool TryReadFrameworkId(string path, out string? frameworkId)
    {
        frameworkId = null;
        if (!File.Exists(path))
            return false;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty(HostTestConfig.SectionName, out var section))
                return false;
            if (!section.TryGetProperty(HostTestConfig.Keys.FrameworkId, out var valueElement))
                return false;

            var value = valueElement.GetString();
            if (string.IsNullOrWhiteSpace(value))
                return false;

            frameworkId = value!.Trim();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
