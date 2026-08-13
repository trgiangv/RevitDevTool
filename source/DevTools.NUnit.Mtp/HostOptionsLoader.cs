using System.Text.Json;

namespace DevTools.NUnit.Mtp;

internal static class HostOptionsLoader
{
    private const string HostEnvironmentVariable = "DEVTOOLS_NUNIT_HOST";
    private const string HostVersionEnvironmentVariable = "DEVTOOLS_NUNIT_HOST_VERSION";
    private const string RunnerPathEnvironmentVariable = "DEVTOOLS_NUNIT_RUNNER_PATH";
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
        var host = ReadEnvironment(HostEnvironmentVariable) ?? options.Host;
        var hostVersion = ReadEnvironment(HostVersionEnvironmentVariable) ?? options.HostVersion;
        var runnerPath = ReadEnvironment(RunnerPathEnvironmentVariable) ?? options.RunnerPath;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(hostVersion))
            throw new InvalidOperationException(MissingConfigMessage);

        return options with
        {
            Host = host.Trim(),
            HostVersion = hostVersion.Trim(),
            RunnerPath = ExpandPath(runnerPath),
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
            ExpandPath(model.RunnerPath));
    }

    private static string? ReadEnvironment(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? ExpandPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Environment.ExpandEnvironmentVariables(value!.Trim());
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
