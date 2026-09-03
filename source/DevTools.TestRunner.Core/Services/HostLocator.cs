namespace DevTools.TestRunner.Core.Services;

public static class HostLocator
{
    /// <summary>
    /// Matching control pipes for <paramref name="host"/> + <paramref name="version"/>.
    /// Oldest matching session first (lowest PID). There is no PID picker or fan-out.
    /// </summary>
    public static IReadOnlyList<HostPipeInstance> Discover(string host, string version)
    {
        var expectedPrefix = $"{IpcConstants.TestPipePrefix}_{host}_{version}_";
        var instances = new List<HostPipeInstance>();

        foreach (var pipePath in Directory.GetFiles(@"\\.\pipe\"))
        {
            var pipeName = Path.GetFileName(pipePath);
            if (!pipeName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!HostPipeName.TryParse(pipeName, out _, out _, out var pid))
                continue;

            instances.Add(new HostPipeInstance(pipeName, pid));
        }

        return instances
            .OrderBy(instance => instance.ProcessId)
            .ToList();
    }
}
