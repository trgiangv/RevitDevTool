using DevTools.Testing.Abstractions.Config;
using DevTools.Testing.Abstractions.Contracts;
using Microsoft.Testing.Platform.Configurations;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.TestAdapter;

internal static class HostOptionsLoader
{
    private const string MissingConfigMessage =
        "RevitDevTool.TestAdapter requires a '" + HostTestConfig.SectionName + "' section in " + HostTestConfig.FileName
        + ". Declare HostName and HostVersion in the test .csproj, or author " + HostTestConfig.FileName + " beside the .csproj.";

    internal static TestingHostOptions Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var hostName = TestingRunnerPaths.ReadEnvironment(TestingRunnerPaths.HostEnvironmentVariable)
            ?? ReadKey(configuration, HostTestConfig.Keys.HostName);
        var hostVersion = TestingRunnerPaths.ReadEnvironment(TestingRunnerPaths.HostVersionEnvironmentVariable)
            ?? ReadKey(configuration, HostTestConfig.Keys.HostVersion);
        if (string.IsNullOrWhiteSpace(hostName) || string.IsNullOrWhiteSpace(hostVersion))
            throw new InvalidOperationException(MissingConfigMessage);

        if (!int.TryParse(ReadKey(configuration, HostTestConfig.Keys.PerTestTimeoutSeconds), out var perTestTimeout)
            || perTestTimeout <= 0)
            throw new InvalidOperationException(MissingConfigMessage);
        if (!int.TryParse(ReadKey(configuration, HostTestConfig.Keys.LaunchTimeoutSeconds), out var launchTimeout)
            || launchTimeout <= 0)
            throw new InvalidOperationException(MissingConfigMessage);

        bool.TryParse(ReadKey(configuration, HostTestConfig.Keys.ForceLaunch), out var forceLaunch);

        var frameworkId = ReadKey(configuration, HostTestConfig.Keys.FrameworkId);
        if (string.IsNullOrWhiteSpace(frameworkId))
            throw new InvalidOperationException(
                "RevitDevTool.TestAdapter requires 'devtools.frameworkId' in testconfig.json.");
        var runnerPath = TestingRunnerPaths.ReadEnvironment(TestingRunnerPaths.RunnerPathEnvironmentVariable)
            ?? ReadKey(configuration, HostTestConfig.Keys.RunnerPath);

        return new TestingHostOptions(
            hostName!.Trim(),
            hostVersion!.Trim(),
            forceLaunch,
            perTestTimeout,
            launchTimeout,
            TestingRunnerPaths.ExpandPath(runnerPath),
            FrameworkId: frameworkId!.Trim());
    }

    private static string? ReadKey(IConfiguration configuration, string name) =>
        ReadConfigurationValue(configuration, HostTestConfig.Keys.Configuration(name));

    private static string? ReadConfigurationValue(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? null : value!.Trim();
    }
}
