using DevTools.Ipc;
using DevTools.Update.Installations;
using DevTools.Update.Releases;
using System.Diagnostics;
using Velopack;
using Velopack.Locators;

namespace DevTools.Update.Engine;

/// <summary>
///     Updates the Daemon package with Velopack (0052, 0053). It operates only when Velopack installed the Daemon.
/// </summary>
public class DaemonUpdater(ReleaseReader reader)
{
    public const string PrereleaseChannel = "prerelease";

    private UpdateManager? _manager;
    private UpdateInfo? _pending;

    public virtual string? InstalledVersion =>
        VelopackLocator.IsCurrentSet && !VelopackLocator.Current.IsPortable
            ? VelopackLocator.Current.CurrentlyInstalledVersion?.ToString()
            : null;

    public bool IsInstalled => InstalledVersion is not null;

    /// <summary>
    ///     The update channel of the install: <c>prerelease</c> for a Setup from a prerelease, else <c>stable</c> (0054).
    /// </summary>
    public virtual string InstalledChannel =>
        IsInstalled && VelopackLocator.Current.Channel == PrereleaseChannel ? UpdatePolicy.PrereleaseChannel : UpdatePolicy.StableChannel;

    /// <summary>
    ///     Reads the Velopack feed of the release, in the channel of that release, so the user can move between channels. If an update is available, downloads it and returns its version.
    /// </summary>
    public virtual async Task<string?> PrepareAsync(Release release, CancellationToken cancellationToken)
    {
        if (!IsInstalled) return null;

        var options = new UpdateOptions { ExplicitChannel = release.Prerelease ? PrereleaseChannel : null };
        var manager = new UpdateManager(new VelopackReleaseSource(reader, release), options);
        var info = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
        if (info is null) return null;

        await manager.DownloadUpdatesAsync(info, cancelToken: cancellationToken).ConfigureAwait(false);
        _manager = manager;
        _pending = info;
        return info.TargetFullRelease.Version.ToString();
    }

    /// <summary>
    ///     Gets the other Daemon processes in the install root. MCP clients start them with <c>--stdio</c>.
    ///     The swap waits until none runs (0052).
    /// </summary>
    public virtual IReadOnlyList<int> GetBlockingProcesses()
    {
        var root = VelopackLocator.Current.RootAppDir!;
        var processes = Process.GetProcessesByName("DevTools.Daemon");
        try
        {
            return processes
                .Where(process => process.Id != Environment.ProcessId)
                .Where(process => process.MainModule!.FileName.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                .Select(process => process.Id)
                .ToArray();
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    /// <summary>
    ///     Gives the update to Velopack. Velopack waits for this process to stop, swaps the files, and starts the Daemon again.
    ///     The Daemon opens the Updates tab if <paramref name="showUpdates"/> is true, else it starts in the background.
    /// </summary>
    public virtual void ApplyOnExit(bool showUpdates) =>
        _manager!.WaitExitThenApplyUpdates(_pending!.TargetFullRelease, silent: true, restart: true, [RestartArg(showUpdates)]);

    /// <summary>
    ///     Applies the update now and opens the Updates tab. Velopack stops all processes in the install root, also the MCP <c>--stdio</c> processes.
    /// </summary>
    public virtual void ApplyNow()
    {
        if (_pending is not null) _manager!.ApplyUpdatesAndRestart(_pending.TargetFullRelease, [RestartArg(showUpdates: true)]);
    }

    private static string RestartArg(bool showUpdates) => showUpdates ? IpcConstants.DaemonArgs.ShowUpdates : IpcConstants.DaemonArgs.Background;
}
