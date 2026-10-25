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
///     One row of the Updates tab: a component of the release, if its host is on this computer, and if the user selected it.
/// </summary>
public sealed record TargetOption(AddinComponent Component, bool OnComputer, bool Selected);

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
    private static readonly (HostApp App, string Platform)[] Platforms =
    [
        (HostApp.Revit, PackageContents.RevitPlatform),
        (HostApp.AutoCad, PackageContents.AutoCadPlatform)
    ];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<int> _watched = [];
    private string? _offeredVersion;

    public InstallationStore Store => store;

    /// <summary>
    ///     The saved policy. Before the first selection, the default policy in the channel of the install.
    /// </summary>
    public UpdatePolicy Policy => store.Load()?.Policy ?? new UpdatePolicy { Channel = daemon.InstalledChannel };

    /// <summary>
    ///     Saves a policy change. Before the first selection, <see cref="ApplySelectionAsync"/> saves the policy.
    /// </summary>
    public void SetPolicy(UpdatePolicy policy)
    {
        if (store.Exists) store.Update(installation => installation.Policy = policy);
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

        var onComputer = DetectInstalledTargets().ToHashSet();
        var selected = installation?.Targets.ToHashSet() ?? DefaultSelection(onComputer);
        var manifest = await reader.ReadManifestAsync(release, cancellationToken).ConfigureAwait(false);
        var released = manifest.Payloads
            .SelectMany(payload => payload.Platforms.Select(platform => new AddinTarget(platform, payload.HostVersion)))
            .ToHashSet();

        return PackageContents.TemplateComponents()
            .Where(component => released.Contains(component.Target))
            .Select(component => new TargetOption(component, onComputer.Contains(component.Target), selected.Contains(component.Target)))
            .ToArray();
    }

    /// <summary>
    ///     Installs or changes the host selection (first run, Updates tab, <c>--select</c>). The payloads come from the release
    ///     of the Daemon version, or from <paramref name="offlineBundle"/> (<c>RevitDevTool.bundle.zip</c>).
    /// </summary>
    public async Task<SelectionResult> ApplySelectionAsync(IReadOnlyList<AddinTarget> selection, UpdatePolicy? policy, string? offlineBundle, CancellationToken cancellationToken)
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
                ? await InstallFromReleaseAsync(installation, cancellationToken).ConfigureAwait(false)
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

    public IReadOnlyList<AddinTarget> DetectInstalledTargets() => Platforms
        .SelectMany(platform => resolvers
            .Where(resolver => resolver.Supports(platform.App))
            .SelectMany(resolver => resolver.GetInstalledVersions(platform.App))
            .Select(hostVersion => new AddinTarget(platform.Platform, hostVersion)))
        .Distinct()
        .ToArray();

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
        var scope = outdated.Length > 0 ? string.Join(", ", outdated.Select(payload => payload.HostVersion)) : "DevTools Daemon";
        notifier.Notify(new UpdateNotice(UpdateNoticeKind.Available, $"RevitDevTool {manifest.Version} is available", $"It updates {scope}.", manifest.Version, manifest.NotesUrl));
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
                Apply(installation, payload.HostVersion, await installer.StageAsync(release, payload, cancellationToken).ConfigureAwait(false), batch);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "The update of {HostVersion} to {Version} failed", payload.HostVersion, release.Version);
                notifier.Notify(new UpdateNotice(UpdateNoticeKind.Failed, $"The update to {release.Version} failed",
                    $"{payload.HostVersion} stays on {installation.InstalledVersion(payload.HostVersion)}.", release.Version));
            }
        }

        if (batch.Changed) Commit(installation);
        installer.CleanStaging(installation.Payloads.Select(payload => payload.PendingVersion).OfType<string>());
    }

    private async Task<PayloadBatch> InstallFromReleaseAsync(Installation installation, CancellationToken cancellationToken)
    {
        var release = await SetupReleaseAsync(installation, cancellationToken).ConfigureAwait(false);
        var manifest = await reader.ReadManifestAsync(release, cancellationToken).ConfigureAwait(false);
        var wanted = installation.SelectedHostVersions().ToArray();

        var batch = new PayloadBatch(manifest.Version);
        batch.Unavailable.AddRange(wanted.Where(hostVersion => manifest.FindPayload(hostVersion) is null));

        var missing = wanted.Select(manifest.FindPayload).OfType<ManifestPayload>().Where(payload => !IsInstalled(installation, payload.HostVersion, manifest.Version));
        foreach (var payload in missing)
            Apply(installation, payload.HostVersion, await installer.StageAsync(release, payload, cancellationToken).ConfigureAwait(false), batch);

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
        {
            notifier.Notify(new UpdateNotice(UpdateNoticeKind.Applied, "RevitDevTool is updated",
                $"{string.Join(", ", batch.Applied)} now use {batch.Version}.", batch.Version, notesUrl));
        }

        foreach (var (hostVersion, lockers) in batch.Pending)
        {
            notifier.Notify(new UpdateNotice(UpdateNoticeKind.Pending, $"The {hostVersion} update waits",
                $"{hostVersion} updates to {batch.Version} when {string.Join(", ", lockers.Select(locker => locker.Name).Distinct())} closes.", batch.Version));
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
    }

    private async Task<Release> SetupReleaseAsync(Installation? installation, CancellationToken cancellationToken) =>
        await FindSetupReleaseAsync(installation, cancellationToken).ConfigureAwait(false) is { HasManifest: true } release
            ? release
            : throw new InvalidOperationException("No release with update data is available.");

    private Task<Release?> FindSetupReleaseAsync(Installation? installation, CancellationToken cancellationToken) =>
        daemon.InstalledVersion is { } version
            ? provider.GetAsync(version, cancellationToken)
            : provider.GetLatestAsync((installation?.Policy.Channel ?? daemon.InstalledChannel) == UpdatePolicy.PrereleaseChannel, cancellationToken);

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
