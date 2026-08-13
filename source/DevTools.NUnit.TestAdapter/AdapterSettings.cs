using DevTools.NUnit.TestAdapter.Models;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace DevTools.NUnit.TestAdapter;

internal static class AdapterSettings
{
    private static readonly object Gate = new();
    private static DevToolsNUnitSettings _current = DevToolsNUnitSettings.CreateDefault();
    private static bool _isConfigured;

    public static DevToolsNUnitSettings Current
    {
        get
        {
            lock (Gate)
                return _current;
        }
    }

    public static bool IsConfigured
    {
        get
        {
            lock (Gate)
                return _isConfigured;
        }
    }

    public static void Apply(IRunSettings? runSettings) => ApplyXml(runSettings?.SettingsXml);

    public static void TryApplyFromAssembly(string assemblyPath)
    {
        if (IsConfigured)
            return;

        if (string.IsNullOrWhiteSpace(assemblyPath))
            return;

        var directory = Path.GetDirectoryName(Path.GetFullPath(assemblyPath));
        if (string.IsNullOrWhiteSpace(directory))
            return;

        var sidecar = Path.Combine(directory, DevToolsNUnitConstants.SidecarRunSettingsFileName);
        if (!File.Exists(sidecar))
            return;

        ApplyXml(File.ReadAllText(sidecar));
    }

    public static void Reset()
    {
        lock (Gate)
        {
            _isConfigured = false;
            _current = DevToolsNUnitSettings.CreateDefault();
        }
    }

    private static void ApplyXml(string? settingsXml)
    {
        var parsed = RunSettingsParser.Parse(settingsXml);
        if (!parsed.IsDevToolsNUnitEnabled)
        {
            lock (Gate)
            {
                _isConfigured = false;
                _current = DevToolsNUnitSettings.CreateDefault();
            }

            return;
        }

        ApplyEnvironmentOverrides(parsed.DevToolsNUnit);

        lock (Gate)
        {
            _current = DevToolsNUnitSettings.FromModel(parsed.DevToolsNUnit, parsed.RunConfiguration);
            _isConfigured = true;
        }
    }

    private static void ApplyEnvironmentOverrides(DevToolsNUnitSettingsModel model)
    {
        model.HostName = ReadEnvironment(DevToolsNUnitConstants.HostEnvironmentVariable, model.HostName);
        model.HostVersion = ReadEnvironment(DevToolsNUnitConstants.HostVersionEnvironmentVariable, model.HostVersion);
        model.RunnerPath = ReadEnvironment(DevToolsNUnitConstants.RunnerPathEnvironmentVariable, model.RunnerPath);
    }

    private static string? ReadEnvironment(string variable, string? current)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? current : value.Trim();
    }
}
