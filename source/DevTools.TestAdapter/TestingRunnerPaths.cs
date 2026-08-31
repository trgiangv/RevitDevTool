using DevTools.Utilities;
// ReSharper disable RedundantSuppressNullableWarningExpression
namespace DevTools.TestAdapter;

/// <summary>
/// Bundle Runner path and environment overrides for the host-test adapter.
/// </summary>
public static class TestingRunnerPaths
{
    public const string HostEnvironmentVariable = "DEVTOOLS_TESTING_HOST";
    public const string HostVersionEnvironmentVariable = "DEVTOOLS_TESTING_HOST_VERSION";
    public const string RunnerPathEnvironmentVariable = "DEVTOOLS_TESTING_RUNNER_PATH";

    private const string MissingInstallMessage =
        "RevitDevTool is not installed. Install it from https://github.com/trgiangv/RevitDevTool";

    public static string ResolveRunnerPath(string? configuredPath)
    {
        if (IsRunnable(configuredPath))
            return Path.GetFullPath(configuredPath!);

        var bundlePath = AppUtils.GetTestRunnerExePath();
        return IsRunnable(bundlePath) ? bundlePath : throw new InvalidOperationException(MissingInstallMessage);
    }

    public static string? ExpandPath(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : Environment.ExpandEnvironmentVariables(value!.Trim());
    }

    public static string? ReadEnvironment(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsRunnable(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path);
}
