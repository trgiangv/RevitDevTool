namespace DevTools.TestAdapter;

/// <summary>
/// Runner path check and host environment overrides for the host-test adapter. The package
/// targets write the runner inside the package as <c>devtools.runnerPath</c>.
/// </summary>
public static class TestingRunnerPaths
{
    public const string HostEnvironmentVariable = "DEVTOOLS_TESTING_HOST";
    public const string HostVersionEnvironmentVariable = "DEVTOOLS_TESTING_HOST_VERSION";

    public static string ResolveRunnerPath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            throw new InvalidOperationException(
                "RevitDevTool.TestAdapter has no TestRunner path. Restore the RevitDevTool.TestAdapter package again.");

        var fullPath = Path.GetFullPath(configuredPath!);
        return File.Exists(fullPath)
            ? fullPath
            : throw new InvalidOperationException(
                $"RevitDevTool.TestAdapter did not find the TestRunner at '{fullPath}'. Restore the RevitDevTool.TestAdapter package again.");
    }

    public static string? ExpandPath(string? value)
    {
        return value is null || string.IsNullOrWhiteSpace(value)
            ? null
            : Environment.ExpandEnvironmentVariables(value.Trim());
    }

    public static string? ReadEnvironment(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
