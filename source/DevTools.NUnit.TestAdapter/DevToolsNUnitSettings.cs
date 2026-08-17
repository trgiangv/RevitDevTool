using DevTools.NUnit.Core;
using DevTools.NUnit.TestAdapter.Models;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.TestAdapter;

internal sealed record DevToolsNUnitSettings(
    string Host,
    string HostVersion,
    bool HostLaunch,
    int HostTimeoutSeconds,
    int HostLaunchTimeoutSeconds,
    string? RunnerPath,
    bool CollectSourceInformation)
{
    private const int DefaultHostTimeoutSeconds = NUnitHostTiming.DefaultHostRequestTimeoutSeconds;
    private const int DefaultHostLaunchTimeoutSeconds = NUnitHostTiming.DefaultHostLaunchTimeoutSeconds;

    public static DevToolsNUnitSettings CreateDefault() =>
        new(
            DevToolsNUnitConstants.DefaultHost,
            DevToolsNUnitConstants.DefaultHostVersion,
            false,
            DefaultHostTimeoutSeconds,
            DefaultHostLaunchTimeoutSeconds,
            null,
            true);

    public static DevToolsNUnitSettings FromModel(DevToolsNUnitSettingsModel settings, RunConfigurationModel runConfiguration) =>
        new(
            Require(settings.HostName, nameof(settings.HostName)),
            Require(settings.HostVersion, nameof(settings.HostVersion)),
            ReadBool(settings.HostLaunch, false),
            ReadPositiveInt(settings.HostTimeout, DefaultHostTimeoutSeconds),
            ReadPositiveInt(settings.HostLaunchTimeout, DefaultHostLaunchTimeoutSeconds),
            NUnitRunnerPaths.ExpandPath(settings.RunnerPath),
            ReadCollectSourceInformation(runConfiguration.CollectSourceInformation));

    private static string Require(string? value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"DevTools.NUnit requires <{settingName}> in the generated runsettings. "
                + "Declare it in the test .csproj (for example <HostVersion>2024</HostVersion>) and rebuild.");
        }

        return value!.Trim();
    }

    private static bool ReadBool(string? value, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return bool.TryParse(value!.Trim(), out var parsed) ? parsed : fallback;
    }

    private static int ReadPositiveInt(string? value, int fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return int.TryParse(value!.Trim(), out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static bool ReadCollectSourceInformation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return !bool.TryParse(value!.Trim(), out var parsed) || parsed;
    }

    public HostRunOptions ToHostRunOptions() =>
        new(Host, HostVersion, HostLaunch, HostTimeoutSeconds, HostLaunchTimeoutSeconds, RunnerPath);

    public TestingHostOptions ToTestingHostOptions(int? debugParentPid = null) =>
        new(Host, HostVersion, HostLaunch, HostTimeoutSeconds, HostLaunchTimeoutSeconds, RunnerPath, debugParentPid);
}
