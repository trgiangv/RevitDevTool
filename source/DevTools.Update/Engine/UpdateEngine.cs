using DevTools.Hosting;
using DevTools.Update.Apply;
using DevTools.Update.Autodesk;
using DevTools.Update.Installations;
using DevTools.Update.Releases;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Update.Engine;

/// <summary>
///     Runs the update cycle. The channel index names one <c>daemon</c> and one <c>addin</c> release per channel (0059 §2):
///     Velopack updates the Daemon from the first, the engine updates the payload folders from the second (0048–0052).
///     It does not stop host processes.
/// </summary>
public sealed class UpdateEngine(
    AutodeskBundles bundles,
    InstallationStore store,
    IChannelIndexSource channels,
    ReleaseReader reader,
    PayloadInstaller installer,
    DaemonUpdater daemon,
    IUpdateNotifier notifier,
    IEnumerable<IHostPathResolver> resolvers,
    ILogger<UpdateEngine> logger) : IDisposable
{
    private const int ParallelDownloads = 3;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private LockerWatcher? _lockers;
    private string? _offered;
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
    ///     of the channel's <c>addin</c> release. The list is empty if the channel has no <c>addin</c> release.
    /// </summary>
    public async Task<IReadOnlyList<TargetOption>> GetTargetsAsync(CancellationToken cancellationToken)
    {
        var installation = store.Load();
        var release = await FindSetupReleaseAsync(installation, cancellationToken).ConfigureAwait(false);
        if (release is null) return [];

        var hosts = DetectHosts();
        var selected = installation?.Targets.ToHashSet() ?? DefaultSelection(hosts.Keys.ToHashSet());
        var manifest = await reader.ReadManifestAsync(release, cancellationToken).ConfigureAwait(false);
        var released = manifest.Payloads.Select(payload => payload.Target).ToHashSet();

        var latest = manifest.Version;
        var installed = installation?.Targets.ToHashSet() ?? [];
        return PackageContents.TemplateComponents()
            .Where(component => released.Contains(component.Target))
            .Select(component =>
            {
                var payload = installed.Contains(component.Target)
                    ? installation!.Payloads.Find(entry => entry.Target == component.Target)
                    : null;
                return new TargetOption(component, hosts.GetValueOrDefault(component.Target, []), selected.Contains(component.Target),
                    payload?.Version, payload?.PendingVersion, latest);
            })
            .ToArray();
    }

    /// <summary>
    ///     The newest Daemon version of the install's channel, or null when the channel has no <c>daemon</c> release.
    /// </summary>
    public async Task<string?> GetLatestDaemonAsync(CancellationToken cancellationToken) =>
        (await PickAsync((store.Load()?.Policy ?? Policy).IsPrerelease, cancellationToken).ConfigureAwait(false)).Daemon?.Version;

    /// <summary>
    ///     Installs or changes the host selection (first run, Updates tab, <c>--select</c>). The payloads come from the
    ///     <c>addin</c> release of the install's channel, or from <paramref name="offlineBundles"/> (<c>RevitDevTool.bundle.zip</c>,
    ///     <c>AcadDevTool.bundle.zip</c>).
    /// </summary>
    public async Task<SelectionResult> ApplySelectionAsync(IReadOnlyList<AddinTarget> selection, UpdatePolicy? policy, IReadOnlyList<string>? offlineBundles,
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

            var batch = offlineBundles is null or []
                ? await InstallFromReleaseAsync(installation, progress, cancellationToken).ConfigureAwait(false)
                : InstallFromBundles(installation, offlineBundles);

            Commit(installation);
            Report(batch, null);
            return new SelectionResult(batch.Version, batch.Applied, batch.PendingTargets, batch.Unavailable);
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
            .Where(component => File.Exists(Path.Combine(bundles.Named(component.Bundle).Root, component.ModulePath)))
            .Select(component => component.Target)
            .ToHashSet();
        return onDisk.Count > 0 ? onDisk : onComputer;
    }

    public IReadOnlyList<AddinTarget> DetectInstalledTargets() => DetectHosts().Keys.ToArray();

    private Dictionary<AddinTarget, HostApp[]> DetectHosts() => InstalledHosts.Detect(resolvers);

    /// <summary>
    ///     Velopack uninstall hook. Removes the bundles and the <c>installation.json</c> that the engine wrote.
    /// </summary>
    public static void RemoveBundles(AutodeskBundles bundles, InstallationStore store)
    {
        foreach (var bundle in bundles.All.Where(bundle => Directory.Exists(bundle.Root))) Directory.Delete(bundle.Root, recursive: true);
        store.Delete();
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

        var (daemonRelease, addinRelease) = await PickAsync(installation.Policy.IsPrerelease, cancellationToken).ConfigureAwait(false);
        if (daemonRelease is null && addinRelease is null) return new UpdateCycleResult(UpdateCycleStatus.NoRelease);

        var manifest = addinRelease is null ? null : await reader.ReadManifestAsync(addinRelease, cancellationToken).ConfigureAwait(false);
        var skipped = manifest is not null && manifest.Version == installation.Policy.SkippedVersion && trigger != UpdateTrigger.UserApproved;
        var outdated = manifest is null || skipped ? [] : OutdatedPayloads(installation, manifest);
        var newerDaemon = daemonRelease is not null && daemon.InstalledVersion is { } installed && Versions.IsNewer(daemonRelease.Version, installed)
            ? daemonRelease
            : null;
        if (installation.Policy.Mode != UpdateMode.Auto && trigger != UpdateTrigger.UserApproved)
            return Offer(manifest, outdated, newerDaemon, trigger);

        var daemonTarget = newerDaemon is null ? null : await PrepareDaemonAsync(newerDaemon, cancellationToken).ConfigureAwait(false);
        var batch = new PayloadBatch(manifest?.Version ?? "");
        if (manifest is not null && !RequiresNewerUpdater(manifest))
            await UpdatePayloadsAsync(installation, addinRelease!, outdated, batch, cancellationToken).ConfigureAwait(false);

        Report(batch, manifest?.NotesUrl);
        SwapUpdater(installation, daemonTarget, trigger);

        var status = batch.Changed || daemonTarget is not null ? UpdateCycleStatus.Updated : UpdateCycleStatus.UpToDate;
        return new UpdateCycleResult(status, manifest?.Version, batch.Applied, batch.PendingTargets);
    }

    private async Task<(Release? Daemon, Release? Addin)> PickAsync(bool prerelease, CancellationToken cancellationToken) =>
        (await channels.GetAsync(cancellationToken).ConfigureAwait(false)).Pick(prerelease);

    private static ManifestPayload[] OutdatedPayloads(Installation installation, UpdateManifest manifest) => installation.Targets.Distinct()
        .Select(manifest.FindPayload)
        .OfType<ManifestPayload>()
        .Where(payload => Versions.IsNewer(manifest.Version, installation.InstalledVersion(payload.Target)))
        .ToArray();

    /// <summary>
    ///     Notify mode: offers the add-in and the Daemon separately, each when it is newer than the installed one.
    /// </summary>
    private UpdateCycleResult Offer(UpdateManifest? manifest, ManifestPayload[] outdated, Release? newerDaemon, UpdateTrigger trigger)
    {
        var addinVersion = outdated.Length > 0 ? manifest!.Version : null;
        if (addinVersion is null && newerDaemon is null) return new UpdateCycleResult(UpdateCycleStatus.UpToDate, manifest?.Version);

        var offered = $"{addinVersion}|{newerDaemon?.Version}";
        if (_offered == offered && trigger == UpdateTrigger.Scheduled) return new UpdateCycleResult(UpdateCycleStatus.Notified, manifest?.Version);

        _offered = offered;
        if (addinVersion is not null)
            notifier.Notify(new UpdateNotice(UpdateNoticeKind.Available, "Update available", $"{addinVersion} is available.", addinVersion, manifest!.NotesUrl));
        if (newerDaemon is not null)
            notifier.Notify(new UpdateNotice(UpdateNoticeKind.Available, "Daemon update available", $"DevTools Daemon {newerDaemon.Version} is available.", newerDaemon.Version));
        return new UpdateCycleResult(UpdateCycleStatus.Notified, manifest?.Version);
    }

    /// <summary>
    ///     A new major Daemon waits while an add-in of that major is staged and waiting for its host to close (0055 §8).
    ///     This is the mirror of <see cref="RequiresNewerUpdater"/>.
    /// </summary>
    private bool HoldsMajorUpdate(Installation installation, string target) =>
        daemon.InstalledVersion is { } installed
        && Versions.Major(target) > Versions.Major(installed)
        && installation.Payloads.Any(payload =>
            payload.PendingVersion is { } pending && Versions.Major(pending) == Versions.Major(target));

    private bool RequiresNewerUpdater(UpdateManifest manifest) =>
        daemon.InstalledVersion is { } installed && manifest.MinUpdaterVersion is { } minimum && Versions.IsNewer(minimum, installed);

    private async Task<string?> PrepareDaemonAsync(Release release, CancellationToken cancellationToken)
    {
        try
        {
            return await daemon.PrepareAsync(release, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.ZLogWarning(ex, $"The Daemon update to {release.Version} failed. The payload update continues");
            return null;
        }
    }

    private async Task UpdatePayloadsAsync(Installation installation, Release release, ManifestPayload[] outdated, PayloadBatch batch, CancellationToken cancellationToken)
    {
        foreach (var payload in outdated)
        {
            try
            {
                Apply(installation, payload.Target, await installer.StageAsync(release, payload, null, cancellationToken).ConfigureAwait(false), batch);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.ZLogError(ex, $"The update of {payload.Target} to {release.Version} failed");
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
        var wanted = installation.Targets.Distinct().ToArray();

        var batch = new PayloadBatch(manifest.Version);
        batch.Unavailable.AddRange(wanted.Where(target => manifest.FindPayload(target) is null));

        var missing = wanted.Select(manifest.FindPayload).OfType<ManifestPayload>().Where(payload => !IsInstalled(installation, payload.Target, manifest.Version)).ToArray();
        foreach (var payload in missing) progress?.Report(new PayloadProgress(payload.Target, PayloadStage.Waiting, 0));

        var staged = new string[missing.Length];
        await Parallel.ForAsync(0, missing.Length, new ParallelOptions { MaxDegreeOfParallelism = ParallelDownloads, CancellationToken = cancellationToken },
            async (index, token) => staged[index] = await installer.StageAsync(release, missing[index], progress, token).ConfigureAwait(false)).ConfigureAwait(false);

        for (var index = 0; index < missing.Length; index++)
            Apply(installation, missing[index].Target, staged[index], batch);

        return batch;
    }

    /// <summary>
    ///     Installs each selected target from the offline zip of its bundle. A target whose bundle zip is missing is unavailable.
    /// </summary>
    private PayloadBatch InstallFromBundles(Installation installation, IReadOnlyList<string> bundleZips)
    {
        var offline = bundleZips.Select(OfflineBundle.Read).ToArray();
        var versions = offline.Select(item => item.Version).Distinct(StringComparer.Ordinal).ToArray();
        if (versions.Length != 1)
            throw new InvalidOperationException($"The offline bundles have different versions ({string.Join(", ", versions)}); use the zips of one release.");

        var batch = new PayloadBatch(versions[0]);
        foreach (var target in installation.Targets.Distinct())
        {
            if (offline.FirstOrDefault(item => item.Bundle == PackageContents.BundleOf(target)) is { } source)
                Apply(installation, target, installer.StageFromBundle(source, target), batch);
            else
                batch.Unavailable.Add(target);
        }

        return batch;
    }

    private bool IsInstalled(Installation installation, AddinTarget target, string version) =>
        installation.InstalledVersion(target) == version && Directory.Exists(bundles.PayloadPath(target));

    private void RemoveUnselected(Installation installation)
    {
        var selected = installation.Targets.ToHashSet();
        installation.Payloads.RemoveAll(payload => !selected.Contains(payload.Target) && installer.TryRemove(payload.Target));
    }

    private void Apply(Installation installation, AddinTarget target, string stagedPath, PayloadBatch batch)
    {
        var payload = installation.Payload(target);
        var lockers = installer.TryApply(target, stagedPath);
        if (lockers.Count > 0)
        {
            payload.PendingVersion = batch.Version;
            batch.Pending.Add((target, lockers));
            (_lockers ??= new LockerWatcher(RetryPendingAsync, logger)).Watch(lockers, _lifetime.Token);
            return;
        }

        payload.RollbackVersion = payload.Version;
        payload.Version = batch.Version;
        payload.PendingVersion = null;
        batch.Applied.Add(target);
    }

    /// <summary>
    ///     Applies staged payloads after their lockers stop. It does not use the network.
    /// </summary>
    private void RetryPending(Installation installation)
    {
        var groups = installation.Payloads
            .Where(payload => payload.PendingVersion is { } version && installer.IsStaged(version, payload.Target))
            .GroupBy(payload => payload.PendingVersion!)
            .ToArray();

        var changed = false;
        foreach (var group in groups)
        {
            var batch = new PayloadBatch(group.Key);
            foreach (var payload in group)
                Apply(installation, payload.Target, installer.StagedPath(group.Key, payload.Target), batch);

            Report(batch, null);
            changed |= batch.Applied.Count > 0;
        }

        if (changed) Commit(installation);
    }

    private async Task RetryPendingAsync()
    {
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

    private void SwapUpdater(Installation installation, string? target, UpdateTrigger trigger)
    {
        if (target is null) return;

        if (HoldsMajorUpdate(installation, target))
        {
            notifier.Notify(new UpdateNotice(UpdateNoticeKind.DaemonPending, $"DevTools Daemon {target} is ready",
                "Close the host apps to finish the update. The add-ins update first.", target));
            return;
        }

        if (daemon.GetBlockingProcesses().Count == 0)
        {
            daemon.ApplyOnExit(showUpdates: trigger != UpdateTrigger.Scheduled);
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
        foreach (var bundle in bundles.All)
        {
            var versions = installation.Payloads.Where(payload => PackageContents.BundleOf(payload.Target) == bundle.Name).Select(payload => payload.Version);
            if (Versions.Max(versions) is { } version)
                PackageContents.Write(bundle, installation.Targets, version);
            else if (File.Exists(bundle.PackageContentsPath))
                File.Delete(bundle.PackageContentsPath);
        }

        CleanStaging(installation);
    }

    private void CleanStaging(Installation installation) =>
        installer.CleanStaging(installation.Payloads.Select(payload => payload.PendingVersion).OfType<string>());

    private async Task<Release> SetupReleaseAsync(Installation? installation, CancellationToken cancellationToken) =>
        await FindSetupReleaseAsync(installation, cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException("No release with update data is available.");

    private async Task<Release?> FindSetupReleaseAsync(Installation? installation, CancellationToken cancellationToken) =>
        (await PickAsync((installation?.Policy ?? Policy).IsPrerelease, cancellationToken).ConfigureAwait(false)).Addin;
}
