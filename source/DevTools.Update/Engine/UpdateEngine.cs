using DevTools.Hosting;
using DevTools.Update.Apply;
using DevTools.Update.Autodesk;
using DevTools.Update.Installations;
using DevTools.Update.Releases;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DevTools.Update.Engine;

public enum UpdateTrigger
{
    Scheduled,
    Manual,
    UserApproved
}

public enum UpdateCycleStatus
{
    NotConfigured,
    Disabled,
    NoRelease,
    UpToDate,
    Notified,
    Updated
}

public sealed record UpdateCycleResult(
    UpdateCycleStatus Status,
    string? Version = null,
    IReadOnlyList<string>? Applied = null,
    IReadOnlyList<string>? Pending = null);

/// <summary>
///     One row of the Updates tab.
/// </summary>
public sealed record TargetOption(AddinComponent Component, IReadOnlyList<HostApp> Hosts, bool Selected, string? Installed, string? Pending, string Latest)
{
    public bool OnComputer => Hosts.Count > 0;
}

public sealed record SelectionResult(string Version, IReadOnlyList<string> Applied, IReadOnlyList<string> Pending, IReadOnlyList<string> Unavailable);

/// <summary>
///     Runs the update cycle. First Velopack updates the Daemon, then the engine updates the payload folders (0048–0052).
///     It does not stop host processes.
/// </summary>
public sealed class UpdateEngine(
    AutodeskBundle bundle,
    InstallationStore store,
    IReleaseProvider provider,
    ReleaseReader reader,
    PayloadInstaller installer,
    DaemonUpdater daemon,
    IUpdateNotifier notifier,
    IEnumerable<IHostPathResolver> resolvers,
    ILogger<UpdateEngine> logger) : IDisposable
{
    private static readonly (HostApp App, string Platform)[] Platforms = Enum.GetValues<HostApp>()
        .Where(app => app == HostApp.Revit || app.IsAcadFamily())
        .Select(app => (app, app == HostApp.Revit ? PackageContents.RevitPlatform : PackageContents.AutoCadPlatform))
        .ToArray();

    private const int ParallelDownloads = 3;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<int> _watched = [];
    private string? _offeredVersion;
    private UpdatePolicy? _chosenPolicy;

    public InstallationStore Store => store;

    /// <summary>
    ///     The saved policy. Before the first selection, the policy that the user chose, else the default policy in the channel of the install.
    /// </summary>
    public UpdatePolicy Policy => store.Load()?.Policy ?? _chosenPolicy ?? new UpdatePolicy { Channel = daemon.InstalledChannel };

    /// <summary>
    ///     Saves a policy change. Before the first selection, keeps it in memory and <see cref="ApplySelectionAsync"/> saves it.
    /// </summary>
    public void SetPolicy(UpdatePolicy policy)
    {
        if (store.Exists) store.Update(installation => installation.Policy = policy);
        else _chosenPolicy = policy;
    }

    public async Task<UpdateCycleResult> CheckAsync(UpdateTrigger trigger, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RunCycleAsync(trigger, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    ///     Gets the rows for the Updates tab (0047): the template components that are in the <c>update-manifest.json</c>
    ///     of the release. The list is empty if no release has a manifest.
    /// </summary>
    public async Task<IReadOnlyList<TargetOption>> GetTargetsAsync(CancellationToken cancellationToken)
    {
        var installation = store.Load();
        var release = await FindSetupReleaseAsync(installation, cancellationToken).ConfigureAwait(false);
        if (release is not { HasManifest: true }) return [];

        var hosts = DetectHosts();
        var selected = installation?.Targets.ToHashSet() ?? DefaultSelection(hosts.Keys.ToHashSet());
        var manifest = await reader.ReadManifestAsync(release, cancellationToken).ConfigureAwait(false);
        var released = manifest.Payloads
            .SelectMany(payload => payload.Platforms.Select(platform => new AddinTarget(platform, payload.HostVersion)))
            .ToHashSet();

        var newest = await provider.GetLatestAsync((installation?.Policy ?? Policy).IsPrerelease, cancellationToken).ConfigureAwait(false);
        var latest = Versions.Max([manifest.Version, newest is { HasManifest: true } ? newest.Version : null])!;
        var installed = installation?.Targets.ToHashSet() ?? [];
        return PackageContents.TemplateComponents()
            .Where(component => released.Contains(component.Target))
            .Select(component =>
            {
                var payload = installed.Contains(component.Target)
                    ? installation!.Payloads.Find(entry => entry.HostVersion == component.Target.HostVersion)
                    : null;
                return new TargetOption(component, hosts.GetValueOrDefault(component.Target, []), selected.Contains(component.Target),
                    payload?.Version, payload?.PendingVersion, latest);
            })
            .ToArray();
    }

    /// <summary>
    ///     Installs or changes the host selection (first run, Updates tab, <c>--select</c>). The payloads come from the release
    ///     of the Daemon version, or from <paramref name="offlineBundle"/> (<c>RevitDevTool.bundle.zip</c>).
    /// </summary>
    public async Task<SelectionResult> ApplySelectionAsync(IReadOnlyList<AddinTarget> selection, UpdatePolicy? policy, string? offlineBundle,
        IProgress<PayloadProgress>? progress, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var installation = store.Load() ?? new Installation { Policy = Policy };
            if (policy is not null) installation.Policy = policy;
            installation.Targets = selection.Distinct().ToList();
            installation.KnownTargets = installation.KnownTargets.Union(DetectInstalledTargets()).ToList();
            RemoveUnselected(installation);

            var batch = offlineBundle is null
                ? await InstallFromReleaseAsync(installation, progress, cancellationToken).ConfigureAwait(false)
                : InstallFromBundle(installation, offlineBundle);

            Commit(installation);
            Report(batch, null);
            return new SelectionResult(batch.Version, batch.Applied, batch.PendingHostVersions, batch.Unavailable);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void ApplyDaemonNow() => daemon.ApplyNow();

    /// <summary>
    ///     Before the first selection: the targets that an earlier install left in the bundle (3.x, 0054),
    ///     else the hosts on this computer.
    /// </summary>
    private HashSet<AddinTarget> DefaultSelection(HashSet<AddinTarget> onComputer)
    {
        var onDisk = PackageContents.TemplateComponents()
            .Where(component => File.Exists(Path.Combine(bundle.Root, component.ModulePath)))
            .Select(component => component.Target)
            .ToHashSet();
        return onDisk.Count > 0 ? onDisk : onComputer;
    }

    public IReadOnlyList<AddinTarget> DetectInstalledTargets() => DetectHosts().Keys.ToArray();

    private Dictionary<AddinTarget, HostApp[]> DetectHosts() => Platforms
        .SelectMany(platform => resolvers
            .Where(resolver => resolver.Supports(platform.App))
            .SelectMany(resolver => resolver.GetInstalledVersions(platform.App))
            .Select(hostVersion => (Target: new AddinTarget(platform.Platform, hostVersion), platform.App)))
        .GroupBy(host => host.Target, host => host.App)
        .ToDictionary(group => group.Key, group => group.Distinct().ToArray());

    /// <summary>
    ///     Velopack uninstall hook. Removes the bundle that the engine installed.
    /// </summary>
    public static void RemoveBundle(AutodeskBundle bundle)
    {
        if (Directory.Exists(bundle.Root)) Directory.Delete(bundle.Root, recursive: true);
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        _gate.Dispose();
    }

    private async Task<UpdateCycleResult> RunCycleAsync(UpdateTrigger trigger, CancellationToken cancellationToken)
    {
        var installation = store.Load();
        if (installation is null) return new UpdateCycleResult(UpdateCycleStatus.NotConfigured);
        if (installation.Policy.Mode == UpdateMode.Off && trigger == UpdateTrigger.Scheduled) return new UpdateCycleResult(UpdateCycleStatus.Disabled);

        RetryPending(installation);
        ReportNewTargets(installation);

        var release = await provider.GetLatestAsync(installation.Policy.IsPrerelease, cancellationToken).ConfigureAwait(false);
        if (release is not { HasManifest: true }) return new UpdateCycleResult(UpdateCycleStatus.NoRelease);

        var manifest = await reader.ReadManifestAsync(release, cancellationToken).ConfigureAwait(false);
        var skipped = manifest.Version == installation.Policy.SkippedVersion && trigger != UpdateTrigger.UserApproved;
        var outdated = skipped ? [] : OutdatedPayloads(installation, manifest);
        if (installation.Policy.Mode != UpdateMode.Auto && trigger != UpdateTrigger.UserApproved)
            return Offer(manifest, outdated, !skipped && daemon.InstalledVersion is { } installed && Versions.IsNewer(manifest.Version, installed), trigger);

        var daemonTarget = await PrepareDaemonAsync(release, cancellationToken).ConfigureAwait(false);
        var batch = new PayloadBatch(manifest.Version);
        if (!WaitsForDaemon(manifest)) await UpdatePayloadsAsync(installation, release, outdated, batch, cancellationToken).ConfigureAwait(false);

        Report(batch, manifest.NotesUrl);
        ApplyDaemonUpdate(daemonTarget);

        var status = batch.Changed || daemonTarget is not null ? UpdateCycleStatus.Updated : UpdateCycleStatus.UpToDate;
        return new UpdateCycleResult(status, manifest.Version, batch.Applied, batch.PendingHostVersions);
    }

    private static ManifestPayload[] OutdatedPayloads(Installation installation, UpdateManifest manifest) => installation.SelectedHostVersions()
        .Select(manifest.FindPayload)
        .OfType<ManifestPayload>()
        .Where(payload => Versions.IsNewer(manifest.Version, installation.InstalledVersion(payload.HostVersion)))
        .ToArray();

    private UpdateCycleResult Offer(UpdateManifest manifest, ManifestPayload[] outdated, bool daemonOutdated, UpdateTrigger trigger)
    {
        if (outdated.Length == 0 && !daemonOutdated) return new UpdateCycleResult(UpdateCycleStatus.UpToDate, manifest.Version);
        if (_offeredVersion == manifest.Version && trigger == UpdateTrigger.Scheduled) return new UpdateCycleResult(UpdateCycleStatus.Notified, manifest.Version);

        _offeredVersion = manifest.Version;
        notifier.Notify(new UpdateNotice(UpdateNoticeKind.Available, "Update available", $"{manifest.Version} is available.", manifest.Version, manifest.NotesUrl));
        return new UpdateCycleResult(UpdateCycleStatus.Notified, manifest.Version);
    }

    private bool WaitsForDaemon(UpdateManifest manifest) =>
        daemon.InstalledVersion is { } installed && manifest.MinUpdaterVersion is { } minimum && Versions.IsNewer(minimum, installed);

    private async Task<string?> PrepareDaemonAsync(Release release, CancellationToken cancellationToken)
    {
        try
        {
            return await daemon.PrepareAsync(release, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The Daemon update to {Version} failed. The payload update continues", release.Version);
            return null;
        }
    }

    private async Task UpdatePayloadsAsync(Installation installation, Release release, ManifestPayload[] outdated, PayloadBatch batch, CancellationToken cancellationToken)
    {
        foreach (var payload in outdated)
        {
            try
            {
                Apply(installation, payload.HostVersion, await installer.StageAsync(release, payload, null, cancellationToken).ConfigureAwait(false), batch);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "The update of {HostVersion} to {Version} failed", payload.HostVersion, release.Version);
                notifier.Notify(new UpdateNotice(UpdateNoticeKind.Failed, "Update failed",
                    $"{release.Version} is not installed. The current version stays in use.", release.Version));
            }
        }

        if (batch.Changed) Commit(installation);
        else CleanStaging(installation);
    }

    private async Task<PayloadBatch> InstallFromReleaseAsync(Installation installation, IProgress<PayloadProgress>? progress, CancellationToken cancellationToken)
    {
        var release = await SetupReleaseAsync(installation, cancellationToken).ConfigureAwait(false);
        var manifest = await reader.ReadManifestAsync(release, cancellationToken).ConfigureAwait(false);
        var wanted = installation.SelectedHostVersions().ToArray();

        var batch = new PayloadBatch(manifest.Version);
        batch.Unavailable.AddRange(wanted.Where(hostVersion => manifest.FindPayload(hostVersion) is null));

        var missing = wanted.Select(manifest.FindPayload).OfType<ManifestPayload>().Where(payload => !IsInstalled(installation, payload.HostVersion, manifest.Version)).ToArray();
        foreach (var payload in missing) progress?.Report(new PayloadProgress(payload.HostVersion, PayloadStage.Waiting, 0));

        var staged = new string[missing.Length];
        await Parallel.ForAsync(0, missing.Length, new ParallelOptions { MaxDegreeOfParallelism = ParallelDownloads, CancellationToken = cancellationToken },
            async (index, token) => staged[index] = await installer.StageAsync(release, missing[index], progress, token).ConfigureAwait(false)).ConfigureAwait(false);

        for (var index = 0; index < missing.Length; index++)
            Apply(installation, missing[index].HostVersion, staged[index], batch);

        return batch;
    }

    private PayloadBatch InstallFromBundle(Installation installation, string bundleZip)
    {
        var batch = new PayloadBatch(PayloadInstaller.ReadBundleVersion(bundleZip));
        foreach (var hostVersion in installation.SelectedHostVersions())
            Apply(installation, hostVersion, installer.StageFromBundle(bundleZip, hostVersion, batch.Version), batch);

        return batch;
    }

    private bool IsInstalled(Installation installation, string hostVersion, string version) =>
        installation.InstalledVersion(hostVersion) == version && Directory.Exists(bundle.PayloadPath(hostVersion));

    private void RemoveUnselected(Installation installation)
    {
        var selected = installation.SelectedHostVersions().ToHashSet(StringComparer.Ordinal);
        installation.Payloads.RemoveAll(payload => !selected.Contains(payload.HostVersion) && installer.TryRemove(payload.HostVersion));
    }

    private void Apply(Installation installation, string hostVersion, string stagedPath, PayloadBatch batch)
    {
        var payload = installation.Payload(hostVersion);
        var lockers = installer.TryApply(hostVersion, stagedPath);
        if (lockers.Count > 0)
        {
            payload.PendingVersion = batch.Version;
            batch.Pending.Add((hostVersion, lockers));
            WatchLockers(lockers);
            return;
        }

        payload.RollbackVersion = payload.Version;
        payload.Version = batch.Version;
        payload.PendingVersion = null;
        batch.Applied.Add(hostVersion);
    }

    /// <summary>
    ///     Applies staged payloads after their lockers stop. It does not use the network.
    /// </summary>
    private void RetryPending(Installation installation)
    {
        var groups = installation.Payloads
            .Where(payload => payload.PendingVersion is { } version && installer.IsStaged(version, payload.HostVersion))
            .GroupBy(payload => payload.PendingVersion!)
            .ToArray();

        var changed = false;
        foreach (var group in groups)
        {
            var batch = new PayloadBatch(group.Key);
            foreach (var payload in group)
                Apply(installation, payload.HostVersion, installer.StagedPath(group.Key, payload.HostVersion), batch);

            Report(batch, null);
            changed |= batch.Applied.Count > 0;
        }

        if (changed) Commit(installation);
    }

    private void WatchLockers(IEnumerable<LockingProcess> lockers)
    {
        foreach (var locker in lockers)
        {
            lock (_watched)
            {
                if (!_watched.Add(locker.ProcessId)) continue;
            }

            _ = Task.Run(() => RetryAfterExitAsync(locker.ProcessId))
                .ContinueWith(task => logger.LogWarning(task.Exception, "A pending payload could not be applied"), TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    private async Task RetryAfterExitAsync(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            await process.WaitForExitAsync(_lifetime.Token).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
        }
        finally
        {
            lock (_watched) _watched.Remove(processId);
        }

        await _gate.WaitAsync(_lifetime.Token).ConfigureAwait(false);
        try
        {
            if (store.Load() is { } installation) RetryPending(installation);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ApplyDaemonUpdate(string? target)
    {
        if (target is null) return;

        if (daemon.GetBlockingProcesses().Count == 0)
        {
            daemon.ApplyOnExit();
            notifier.RequestShutdownForUpdate();
            return;
        }

        notifier.Notify(new UpdateNotice(UpdateNoticeKind.DaemonPending, $"DevTools Daemon {target} is ready",
            "Restart your MCP clients (Claude, Cursor, VS Code) to complete the update.", target));
    }

    private void Report(PayloadBatch batch, string? notesUrl)
    {
        if (batch.Applied.Count > 0)
            notifier.Notify(new UpdateNotice(UpdateNoticeKind.Applied, "Update installed", $"{batch.Version} is installed.", batch.Version, notesUrl));

        if (batch.Pending.Count > 0)
        {
            var names = batch.Pending.SelectMany(item => item.Lockers).Select(locker => locker.Name).Distinct();
            notifier.Notify(new UpdateNotice(UpdateNoticeKind.Pending, "Update waits",
                $"{batch.Version} is installed when {string.Join(", ", names)} closes.", batch.Version));
        }
    }

    private void ReportNewTargets(Installation installation)
    {
        var found = DetectInstalledTargets().Except(installation.KnownTargets).Except(installation.Targets).ToArray();
        if (found.Length == 0) return;

        installation.KnownTargets.AddRange(found);
        store.Save(installation);

        var names = PackageContents.TemplateComponents().Where(component => found.Contains(component.Target)).Select(component => component.Description);
        notifier.Notify(new UpdateNotice(UpdateNoticeKind.NewTarget, "New host found", $"{string.Join(", ", names)} is installed. Add RevitDevTool?", Targets: found));
    }

    private void Commit(Installation installation)
    {
        store.Save(installation);
        if (Versions.Max(installation.Payloads.Select(payload => payload.Version)) is { } version)
            PackageContents.Write(bundle, installation.Targets, version);
        else
            File.Delete(bundle.PackageContentsPath);
        CleanStaging(installation);
    }

    private void CleanStaging(Installation installation) =>
        installer.CleanStaging(installation.Payloads.Select(payload => payload.PendingVersion).OfType<string>());

    private async Task<Release> SetupReleaseAsync(Installation? installation, CancellationToken cancellationToken) =>
        await FindSetupReleaseAsync(installation, cancellationToken).ConfigureAwait(false) is { HasManifest: true } release
            ? release
            : throw new InvalidOperationException("No release with update data is available.");

    private async Task<Release?> FindSetupReleaseAsync(Installation? installation, CancellationToken cancellationToken)
    {
        var release = daemon.InstalledVersion is { } version
            ? await provider.GetAsync(version, cancellationToken).ConfigureAwait(false)
            : null;
        return release is { HasManifest: true }
            ? release
            : await provider.GetLatestAsync((installation?.Policy ?? Policy).IsPrerelease, cancellationToken).ConfigureAwait(false);
    }

    private sealed class PayloadBatch(string version)
    {
        public string Version { get; } = version;
        public List<string> Applied { get; } = [];
        public List<(string HostVersion, IReadOnlyList<LockingProcess> Lockers)> Pending { get; } = [];
        public List<string> Unavailable { get; } = [];
        public bool Changed => Applied.Count > 0 || Pending.Count > 0;
        public string[] PendingHostVersions => Pending.Select(item => item.HostVersion).ToArray();
    }
}
