namespace DevTools.NUnit.TestAdapter.Runner;

public static class RunnerClientFactory
{
    public static IRunnerClient Create() =>
        new ProcessRunnerClient(ResolveRunnerPath());

    public static string ResolveRunnerPath()
    {
        var configuredPath = AdapterSettings.Current.RunnerPath;
        if (IsRunnable(configuredPath))
            return Path.GetFullPath(configuredPath!);

        var explicitPath = Environment.GetEnvironmentVariable(DevToolsNUnitConstants.RunnerPathEnvironmentVariable);
        if (IsRunnable(explicitPath))
            return Path.GetFullPath(explicitPath!);

        var bundlePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk",
            "ApplicationPlugins",
            "RevitDevTool.bundle",
            "Contents",
            "DevTools.NUnit.Runner.exe");
        if (IsRunnable(bundlePath))
            return bundlePath;

        var onPath = FindOnPath("DevTools.NUnit.Runner.exe");
        if (onPath is not null)
            return onPath;

        throw new InvalidOperationException(
            "DevTools.NUnit.Runner.exe was not found. Install RevitDevTool or set DEVTOOLS_NUNIT_RUNNER_PATH.");
    }

    private static string? FindOnPath(string fileName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
            return null;

        foreach (var directory in pathValue.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;

            var candidate = Path.Combine(directory.Trim(), fileName);
            if (IsRunnable(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsRunnable(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path);
}
