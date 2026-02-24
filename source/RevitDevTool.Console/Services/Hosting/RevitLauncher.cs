using System.Diagnostics;
using RevitDevTool.Bridge.Abstractions;
using RevitDevTool.Bridge.IPC;

namespace RevitDevTool.Console.Services.Hosting;

/// <summary>
/// Launches Revit processes and waits for their EngineHost pipes to become available.
/// </summary>
public sealed class RevitLauncher : IHostLauncher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private readonly IStartupDialogResolver _startupDialogResolver;

    public RevitLauncher(IStartupDialogResolver? startupDialogResolver = null)
    {
        _startupDialogResolver = startupDialogResolver ?? new StartupDialogResolver();
    }

    public string AppId => "revit";

    public async Task<IHostInstance> LaunchAsync(string version, TimeSpan timeout, CancellationToken ct = default)
    {
        var revitPath = GetRevitExePath(version);
        if (!File.Exists(revitPath))
            throw new FileNotFoundException($"Revit {version} not found at: {revitPath}");

        System.Console.WriteLine($"[launch:{version}] Starting Revit process...");
        var process = Process.Start(BuildProcessStartInfo(revitPath));

        if (process == null)
            throw new InvalidOperationException($"[launch:{version}] Failed to start Revit process.");

        System.Console.WriteLine($"[launch:{version}] Process started (PID {process.Id}).");

        return await WaitForReadyInstanceAsync(version, process.Id, timeout, ct).ConfigureAwait(false);
    }

    public async Task<List<IHostInstance>> EnsureInstancesAsync(
        IEnumerable<string> requiredVersions,
        IReadOnlyList<IHostInstance> existingInstances,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        var runningVersions = existingInstances
            .Select(i => i.HostVersion)
            .ToHashSet();

        var launched = new List<IHostInstance>();

        foreach (var version in requiredVersions.Distinct().Order())
        {
            if (runningVersions.Contains(version))
            {
                System.Console.WriteLine($"Revit {version} already running");
                continue;
            }

            var instance = await LaunchAsync(version, timeout, ct).ConfigureAwait(false);
            launched.Add(instance);
        }

        return launched;
    }

    private async Task<RevitHostInstance> WaitForPipeByPidAsync(
        string version, int pid, TimeSpan timeout, CancellationToken ct)
    {
        var expectedPipeName = PipeNaming.Build(AppId, version, pid);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            ct.ThrowIfCancellationRequested();

            if (PipeExists(expectedPipeName))
                return new RevitHostInstance(version, pid, expectedPipeName);

            await Task.Delay(PollInterval, ct).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Revit {version} (PID {pid}) did not start EngineHost within {timeout.TotalSeconds}s. " +
            "Ensure RevitDevTool addin is installed.");
    }

    private static bool PipeExists(string expectedName)
    {
        try
        {
            return Directory.GetFiles(@"\\.\pipe\", expectedName).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string GetRevitExePath(string version)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return Path.Combine(programFiles, "Autodesk", $"Revit {version}", "Revit.exe");
    }

    private static ProcessStartInfo BuildProcessStartInfo(string revitPath)
    {
        return new ProcessStartInfo
        {
            FileName = revitPath,
            UseShellExecute = true
        };
    }

    private async Task<RevitHostInstance> WaitForReadyInstanceAsync(
        string version,
        int processId,
        TimeSpan timeout,
        CancellationToken ct)
    {
        using var resolverCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        resolverCts.CancelAfter(timeout);
        using var crashWatcherCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        crashWatcherCts.CancelAfter(timeout);

        System.Console.WriteLine($"[launch:{version}:{processId}] Stage=ResolverRunning");
        var resolverTask = _startupDialogResolver.RunAsync(processId, version, resolverCts.Token);
        var crashWatcherTask = RevitCrashWatcher.MonitorAsync(processId, version, crashWatcherCts.Token);

        System.Console.WriteLine($"[launch:{version}:{processId}] Stage=WaitingForPipe");
        var waitForPipeTask = WaitForPipeByPidAsync(version, processId, timeout, ct);
        var completedTask = await Task.WhenAny(waitForPipeTask, resolverTask, crashWatcherTask).ConfigureAwait(false);

        if (completedTask == resolverTask)
        {
            await resolverTask.ConfigureAwait(false);
            throw new InvalidOperationException(
                $"[launch:{version}:{processId}] Stage=ResolverFailed before pipe ready.");
        }

        if (completedTask == crashWatcherTask)
        {
            await crashWatcherTask.ConfigureAwait(false);
            throw new InvalidOperationException(
                $"[launch:{version}:{processId}] Stage=CrashDetected before pipe ready.");
        }

        var instance = await waitForPipeTask.ConfigureAwait(false);
        await resolverCts.CancelAsync().ConfigureAwait(false);
        await crashWatcherCts.CancelAsync().ConfigureAwait(false);
        await AwaitResolverStoppedAsync(resolverTask).ConfigureAwait(false);
        await AwaitCrashWatcherStoppedAsync(crashWatcherTask).ConfigureAwait(false);

        System.Console.WriteLine($"[launch:{version}:{processId}] Stage=PipeReady ({instance.PipeName})");
        return instance;
    }

    private static async Task AwaitResolverStoppedAsync(Task resolverTask)
    {
        try
        {
            await resolverTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected after pipe is ready.
        }
    }

    private static async Task AwaitCrashWatcherStoppedAsync(Task crashWatcherTask)
    {
        try
        {
            await crashWatcherTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected after pipe is ready.
        }
    }
}
