using DevTools.Logging;
using DevTools.Utilities.Hosting;

namespace DevTools.NUnit.Runner.Services;

/// <summary>
/// Locates a running host pipe or launches a host without blocking on startup dialogs.
/// <c>false</c> reuses an existing instance when available, otherwise launches;
/// <c>true</c> always starts a new host and waits for that process pipe.
/// </summary>
public sealed class HostSession(IHostLaunchService launchService)
{
    public async Task<HostPipeInstance> EnsurePipeAsync(
        HostApp hostApp,
        string version,
        bool forceLaunch,
        TimeSpan launchTimeout,
        CancellationToken cancellationToken = default)
    {
        var hostName = hostApp.ToString();
        if (!forceLaunch)
        {
            var existing = HostLocator.Discover(hostName, version).FirstOrDefault();
            if (existing is not null)
                return existing;
        }

        var started = launchService.Start(hostApp, version, languageCode: null, filePath: null, cancellationToken);
        _ = started.DialogResolver;

        var deadline = DateTime.UtcNow + launchTimeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var match = HostLocator.Discover(hostName, version)
                .FirstOrDefault(instance => instance.ProcessId == started.Process.Id);
            if (match is not null)
                return match;

            if (started.Process.HasExited)
            {
                throw new InvalidOperationException(
                    $"{hostApp} exited before the DevTools control pipe became available (PID={started.Process.Id}).");
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"{hostApp} {version} launched (PID={started.Process.Id}) but no control pipe appeared within {launchTimeout.TotalSeconds:0}s.");
    }
}
