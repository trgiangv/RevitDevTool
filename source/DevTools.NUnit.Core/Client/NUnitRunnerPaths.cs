namespace DevTools.NUnit.Core;

/// <summary>
/// Bundle Runner path and environment overrides shared by MTP and VSTest.
/// </summary>
public static class NUnitRunnerPaths
{
    public const string HostEnvironmentVariable = "DEVTOOLS_NUNIT_HOST";
    public const string HostVersionEnvironmentVariable = "DEVTOOLS_NUNIT_HOST_VERSION";
    public const string RunnerPathEnvironmentVariable = "DEVTOOLS_NUNIT_RUNNER_PATH";

    public const string MissingInstallMessage =
        "RevitDevTool is not installed. Install it from https://github.com/trgiangv/RevitDevTool";

    public static string ResolveRunnerPath(string? configuredPath)
    {
        if (IsRunnable(configuredPath))
            return Path.GetFullPath(configuredPath!);

        var bundlePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk",
            "ApplicationPlugins",
            "RevitDevTool.bundle",
            "Contents",
            "DevTools.TestRunner.exe");
        if (IsRunnable(bundlePath))
            return bundlePath;

        throw new InvalidOperationException(MissingInstallMessage);
    }

    public static string? ExpandPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Environment.ExpandEnvironmentVariables(value!.Trim());
    }

    public static string? ReadEnvironment(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsRunnable(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path);
}
