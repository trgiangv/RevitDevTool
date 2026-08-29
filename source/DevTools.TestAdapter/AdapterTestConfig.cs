using System.Reflection;
using System.Text.Json;
using DevTools.Testing.Abstractions.Config;

namespace DevTools.TestAdapter;

internal static class AdapterTestConfig
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    internal sealed class PluginConfig
    {
        internal PluginConfig(string frameworkId, string mtpAssembly, string mtpEntry)
        {
            FrameworkId = frameworkId;
            MtpAssembly = mtpAssembly;
            MtpEntry = mtpEntry;
        }

        internal string FrameworkId { get; }
        internal string MtpAssembly { get; }
        internal string MtpEntry { get; }
    }

    internal static bool TryReadPluginConfig(out PluginConfig? config, out string? error)
    {
        config = null;
        error = null;

        foreach (var path in ResolveTestConfigPaths())
        {
            if (!TryReadPluginSection(path, out var section, out error))
                continue;

            if (section is null)
                continue;
            return TryBuildPluginConfig(section, out config, out error);
        }

        error = "RevitDevTool.TestAdapter requires a 'devtools' section with frameworkId, mtpAssembly, and mtpEntry in testconfig.json.";
        return false;
    }

    private static bool TryBuildPluginConfig(
        PluginSection section,
        out PluginConfig? config,
        out string? error)
    {
        config = null;
        error = null;

        if (string.IsNullOrWhiteSpace(section.FrameworkId))
        {
            error = "RevitDevTool.TestAdapter requires 'devtools.frameworkId' in testconfig.json.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(section.MtpAssembly))
        {
            error = "RevitDevTool.TestAdapter requires 'devtools.mtpAssembly' in testconfig.json.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(section.MtpEntry))
        {
            error = "RevitDevTool.TestAdapter requires 'devtools.mtpEntry' in testconfig.json.";
            return false;
        }

        config = new PluginConfig(
            section.FrameworkId!.Trim(),
            section.MtpAssembly!.Trim(),
            section.MtpEntry!.Trim());
        return true;
    }

    internal static string? TryReadMtpAssembly()
    {
        foreach (var path in ResolveTestConfigPaths())
        {
            if (!TryReadPluginSection(path, out var section, out _))
                continue;

            if (section is null)
                continue;

            if (!string.IsNullOrWhiteSpace(section.MtpAssembly))
                return section.MtpAssembly!.Trim();
        }

        return null;
    }

    private static IEnumerable<string> ResolveTestConfigPaths()
    {
        var baseDirectory = AppContext.BaseDirectory;
        yield return Path.Combine(baseDirectory, HostTestConfig.FileName);

        var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (!string.IsNullOrWhiteSpace(entryAssemblyName))
            yield return Path.Combine(baseDirectory, entryAssemblyName + ".testconfig.json");
    }

    private static bool TryReadPluginSection(
        string path,
        out PluginSection? section,
        out string? error)
    {
        section = null;
        error = null;
        if (!File.Exists(path))
            return false;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty(HostTestConfig.SectionName, out var devtools))
                return false;

            section = new PluginSection(
                ReadString(devtools, HostTestConfig.Keys.FrameworkId),
                ReadString(devtools, HostTestConfig.Keys.MTPAssembly),
                ReadString(devtools, HostTestConfig.Keys.MTPEntry));
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string? ReadString(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) ? value.GetString() : null;

    private sealed class PluginSection
    {
        internal PluginSection(string? frameworkId, string? mtpAssembly, string? mtpEntry)
        {
            FrameworkId = frameworkId;
            MtpAssembly = mtpAssembly;
            MtpEntry = mtpEntry;
        }

        internal string? FrameworkId { get; }
        internal string? MtpAssembly { get; }
        internal string? MtpEntry { get; }
    }
}
