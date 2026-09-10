using DevTools.Testing.Abstractions.Config;
using DevTools.Testing.Abstractions.Contracts;
using Microsoft.Testing.Platform.Configurations;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.TestAdapter;

internal static class TestRunSettingsLoader
{
    private const string MissingConfigMessage =
        "RevitDevTool.TestAdapter requires a '" + TestConfig.SectionName + "' section in " + TestConfig.FileName
        + ". Declare HostName and HostVersion in the test .csproj, or author " + TestConfig.FileName + " beside the .csproj.";

    internal static TestRunSettings Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var hostName = TestingRunnerPaths.ReadEnvironment(TestingRunnerPaths.HostEnvironmentVariable)
            ?? ReadKey(configuration, TestConfig.Keys.HostName);
        var hostVersion = TestingRunnerPaths.ReadEnvironment(TestingRunnerPaths.HostVersionEnvironmentVariable)
            ?? ReadKey(configuration, TestConfig.Keys.HostVersion);
        if (string.IsNullOrWhiteSpace(hostName) || string.IsNullOrWhiteSpace(hostVersion))
            throw new InvalidOperationException(MissingConfigMessage);

        if (!int.TryParse(ReadKey(configuration, TestConfig.Keys.PerTestTimeoutSeconds), out var perTestTimeout)
            || perTestTimeout <= 0)
            throw new InvalidOperationException(MissingConfigMessage);
        if (!int.TryParse(ReadKey(configuration, TestConfig.Keys.LaunchTimeoutSeconds), out var launchTimeout)
            || launchTimeout <= 0)
            throw new InvalidOperationException(MissingConfigMessage);

        bool.TryParse(ReadKey(configuration, TestConfig.Keys.ForceLaunch), out var forceLaunch);

        var frameworkId = ReadKey(configuration, TestConfig.Keys.FrameworkId);
        if (string.IsNullOrWhiteSpace(frameworkId)
            || !Enum.TryParse(frameworkId, ignoreCase: true, out TestFrameworkId parsed)
            || !Enum.IsDefined(parsed))
        {
            throw new InvalidOperationException(
                "RevitDevTool.TestAdapter requires 'devtools.frameworkId' in testconfig.json to be "
                + nameof(TestFrameworkId.NUnit) + " or " + nameof(TestFrameworkId.TUnit) + ".");
        }
        var runnerPath = TestingRunnerPaths.ReadEnvironment(TestingRunnerPaths.RunnerPathEnvironmentVariable)
            ?? ReadKey(configuration, TestConfig.Keys.RunnerPath);

        return new TestRunSettings(
            new TestHostOptions(
                hostName!.Trim(),
                hostVersion!.Trim(),
                forceLaunch,
                perTestTimeout,
                launchTimeout),
            parsed,
            TestingRunnerPaths.ExpandPath(runnerPath));
    }

    private static string? ReadKey(IConfiguration configuration, string name) =>
        ReadConfigurationValue(configuration, TestConfig.Keys.Configuration(name));

    private static string? ReadConfigurationValue(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? null : value!.Trim();
    }
}
