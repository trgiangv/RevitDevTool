using DevTools.Testing.Abstractions.Contracts;
using Microsoft.Testing.Platform.Configurations;

namespace DevTools.TestAdapter;

internal static class HostOptionsLoader
{
    internal const string ConfigFileName = "testconfig.json";
    internal const string ConfigSectionName = "devtools";
    internal const string DefaultFrameworkId = "nunit";

    internal static class Keys
    {
        internal const string HostName = "hostName";
        internal const string HostVersion = "hostVersion";
        internal const string ForceLaunch = "forceLaunch";
        internal const string PerTestTimeoutSeconds = "perTestTimeoutSeconds";
        internal const string LaunchTimeoutSeconds = "launchTimeoutSeconds";
        internal const string RunnerPath = "runnerPath";
        internal const string FrameworkId = "frameworkId";

        internal static string Configuration(string name) => ConfigSectionName + ":" + name;
    }

    private const string MissingConfigMessage =
        "RevitDevTool.TestAdapter requires a '" + ConfigSectionName + "' section in " + ConfigFileName
        + ". Declare HostName and HostVersion in the test .csproj, or author " + ConfigFileName + " beside the .csproj.";

    internal static TestingHostOptions Load(IConfiguration configuration)
    {
        if (configuration is null)
            throw new ArgumentNullException(nameof(configuration));

        var hostName = TestingRunnerPaths.ReadEnvironment(TestingRunnerPaths.HostEnvironmentVariable)
            ?? ReadKey(configuration, Keys.HostName);
        var hostVersion = TestingRunnerPaths.ReadEnvironment(TestingRunnerPaths.HostVersionEnvironmentVariable)
            ?? ReadKey(configuration, Keys.HostVersion);
        if (string.IsNullOrWhiteSpace(hostName) || string.IsNullOrWhiteSpace(hostVersion))
            throw new InvalidOperationException(MissingConfigMessage);

        if (!int.TryParse(ReadKey(configuration, Keys.PerTestTimeoutSeconds), out var perTestTimeout)
            || perTestTimeout <= 0)
            throw new InvalidOperationException(MissingConfigMessage);
        if (!int.TryParse(ReadKey(configuration, Keys.LaunchTimeoutSeconds), out var launchTimeout)
            || launchTimeout <= 0)
            throw new InvalidOperationException(MissingConfigMessage);

        _ = bool.TryParse(ReadKey(configuration, Keys.ForceLaunch), out var forceLaunch);

        var frameworkId = ReadKey(configuration, Keys.FrameworkId);
        var runnerPath = TestingRunnerPaths.ReadEnvironment(TestingRunnerPaths.RunnerPathEnvironmentVariable)
            ?? ReadKey(configuration, Keys.RunnerPath);

        return new TestingHostOptions(
            hostName!.Trim(),
            hostVersion!.Trim(),
            forceLaunch,
            perTestTimeout,
            launchTimeout,
            TestingRunnerPaths.ExpandPath(runnerPath),
            FrameworkId: string.IsNullOrWhiteSpace(frameworkId) ? DefaultFrameworkId : frameworkId!.Trim());
    }

    private static string? ReadKey(IConfiguration configuration, string name) =>
        ReadConfigurationValue(configuration, Keys.Configuration(name));

    private static string? ReadConfigurationValue(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value!.Trim();
    }
}
