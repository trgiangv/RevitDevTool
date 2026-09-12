using DevTools.Hosting;
namespace DevTools.TestRunner.Services;

/// <summary>
/// Locates a running host pipe or launches a host without blocking on startup dialogs.
/// <c>false</c> reuses a matching-version instance when one is already running
/// (oldest PID / first listed pipe), otherwise starts a new host.
/// <c>true</c> always starts a new host for this run and waits
/// for that process pipe. Does not kill a reused session. Cancel or launch
/// timeout kills only the process this run spawned.
/// Wait/dialog lifetime is <see cref="HostLaunchWaiter"/> (pytest style).
/// Oldest-PID reuse stays Runner policy, not Hosting.
/// </summary>
public interface ITestSession
{
    Task<TestHostPipe> EnsurePipeAsync(
        HostApp hostApp,
        string version,
        bool forceLaunch,
        TimeSpan launchTimeout,
        CancellationToken cancellationToken = default);
}

public sealed class TestSession(IHostLaunchService launchService) : ITestSession
{
    public async Task<TestHostPipe> EnsurePipeAsync(
        HostApp hostApp,
        string version,
        bool forceLaunch,
        TimeSpan launchTimeout,
        CancellationToken cancellationToken = default)
    {
        var hostName = hostApp.ToString();
        if (!forceLaunch)
        {
            var existing = Discover(hostName, version).FirstOrDefault();
            if (existing is not null)
                return existing;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var started = launchService.Start(
            new HostLaunchRequest(hostApp, version, FilePath: null, Options: null),
            cancellationToken);

        try
        {
            var status = await HostLaunchWaiter.UntilAsync(
                    started.Process,
                    () => Discover(hostName, version)
                        .Any(instance => instance.ProcessId == started.Process.Id),
                    launchTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            HostLaunchWaiter.TerminateIfIncomplete(started.Process, status);

            return status switch
            {
                HostStatus.Ready => Discover(hostName, version)
                    .First(instance => instance.ProcessId == started.Process.Id),
                HostStatus.Exited => throw new InvalidOperationException(
                    $"{hostApp} exited before the DevTools control pipe became available (PID={started.Process.Id})."),
                HostStatus.Cancelled => throw new OperationCanceledException(cancellationToken),
                _ => throw new TimeoutException(
                    $"{hostApp} {version} launched (PID={started.Process.Id}) but no control pipe appeared within {launchTimeout.TotalSeconds:0}s.")
            };
        }
        finally
        {
            started.DialogResolver?.Dispose();
        }
    }

    /// <summary>
    /// Finds matching host control pipes, ordered by PID so reuse is deterministic.
    /// Pipe discovery belongs to the session lifecycle and is not a reusable service.
    /// </summary>
    private static List<TestHostPipe> Discover(string host, string version)
    {
        var expectedPrefix = $"{IpcConstants.TestPipePrefix}_{host}_{version}_";
        var instances = new List<TestHostPipe>();

        foreach (var pipePath in Directory.GetFiles(@"\\.\pipe\"))
        {
            var pipeName = Path.GetFileName(pipePath);
            if (!pipeName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!HostPipeName.TryParse(pipeName, out _, out _, out var pid))
                continue;

            instances.Add(new TestHostPipe(pipeName, pid));
        }

        return instances
            .OrderBy(instance => instance.ProcessId)
            .ToList();
    }
}
