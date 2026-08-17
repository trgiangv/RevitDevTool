using System.Text.Json;
using DevTools.NUnit.Provider;

namespace DevTools.NUnit.Mtp;

internal static class HostOptionsLoader
{
    internal const string OptionsFileName = "devtools.nunit.host.json";

    private const string MissingConfigMessage =
        "DevTools.NUnit requires 'devtools.nunit.host.json' beside the test exe. Declare HostName, HostVersion, HostLaunch, HostTimeout, and HostLaunchTimeout in the test .csproj.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    internal static HostRunOptions Load(string? baseDirectory = null)
    {
        var directory = baseDirectory ?? AppContext.BaseDirectory;
        var path = Path.Combine(directory, OptionsFileName);
        if (!File.Exists(path))
            throw new InvalidOperationException(MissingConfigMessage);

        var options = ReadFile(path);
        var host = NUnitRunnerPaths.ReadEnvironment(NUnitRunnerPaths.HostEnvironmentVariable) ?? options.Host;
        var hostVersion = NUnitRunnerPaths.ReadEnvironment(NUnitRunnerPaths.HostVersionEnvironmentVariable)
            ?? options.HostVersion;
        var runnerPath = NUnitRunnerPaths.ReadEnvironment(NUnitRunnerPaths.RunnerPathEnvironmentVariable)
            ?? options.RunnerPath;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(hostVersion))
            throw new InvalidOperationException(MissingConfigMessage);

        return options with
        {
            Host = host.Trim(),
            HostVersion = hostVersion.Trim(),
            RunnerPath = NUnitRunnerPaths.ExpandPath(runnerPath),
        };
    }

    private static HostRunOptions ReadFile(string path)
    {
        var model = JsonSerializer.Deserialize<HostOptionsFile>(File.ReadAllText(path), JsonOptions);
        if (model is null
            || string.IsNullOrWhiteSpace(model.Host)
            || string.IsNullOrWhiteSpace(model.HostVersion)
            || model.HostTimeoutSeconds <= 0
            || model.HostLaunchTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException(MissingConfigMessage);
        }

        return new HostRunOptions(
            model.Host!.Trim(),
            model.HostVersion!.Trim(),
            model.HostLaunch,
            model.HostTimeoutSeconds,
            model.HostLaunchTimeoutSeconds,
            NUnitRunnerPaths.ExpandPath(model.RunnerPath));
    }

    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    private sealed class HostOptionsFile
    {
        public string? Host { get; set; }
        public string? HostVersion { get; set; }
        public bool HostLaunch { get; set; }
        public int HostTimeoutSeconds { get; set; }
        public int HostLaunchTimeoutSeconds { get; set; }
        public string? RunnerPath { get; set; }
    }
}
