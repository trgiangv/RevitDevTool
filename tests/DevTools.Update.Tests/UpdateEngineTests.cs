using DevTools.Hosting;
using DevTools.Update.Apply;
using DevTools.Update.Autodesk;
using DevTools.Update.Engine;
using DevTools.Update.Installations;
using DevTools.Update.Releases;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Xml.Linq;
using static DevTools.Update.Tests.Hosts;

namespace DevTools.Update.Tests;

[TestClass]
public sealed class UpdateEngineTests
{
    [TestMethod]
    public async Task Auto_mode_updates_free_payloads_and_keeps_busy_payloads_pending()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var harness = new Harness(key);
        harness.Channels.PublishAddin("3.1.0", (Revit("2024"), "2024-new"), (Acad("2024"), "acad-new"), (Revit("2025"), "2025-new"));
        harness.Temp.WritePayload(Revit("2024"), "2024-old", "RevitDevTool.addin");
        harness.Temp.WritePayload(Acad("2024"), "acad-old", "AcadDevTool.dll");
        harness.Temp.WritePayload(Revit("2025"), "2025-old", "RevitDevTool.addin");
        harness.FolderLock.Lock(harness.Temp.Bundles.PayloadPath(Revit("2025")));
        harness.Store.Save(new Installation
        {
            Targets = [Revit("2024"), Acad("2024"), Revit("2025")],
            Payloads = [Installed(Revit("2024"), "3.0.0"), Installed(Acad("2024"), "3.0.0"), Installed(Revit("2025"), "3.0.0")]
        });

        var result = await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None);

        Assert.AreEqual(UpdateCycleStatus.Updated, result.Status);
        CollectionAssert.AreEqual(new[] { Revit("2024"), Acad("2024") }, result.Applied!.ToArray());
        CollectionAssert.AreEqual(new[] { Revit("2025") }, result.Pending!.ToArray());
        Assert.AreEqual("2024-new", harness.Temp.Marker(Revit("2024")));
        Assert.AreEqual("acad-new", harness.Temp.Marker(Acad("2024")));
        Assert.AreEqual("2025-old", harness.Temp.Marker(Revit("2025")));
        Assert.AreEqual("3.1.0", harness.Store.Load()!.Payloads.Single(payload => payload.Target == Revit("2025")).PendingVersion);
        Assert.IsTrue(harness.Notifier.Notices.Any(notice => notice.Kind == UpdateNoticeKind.Pending && notice.Message.Contains("Revit.exe", StringComparison.Ordinal)));

        harness.FolderLock.Unlock(harness.Temp.Bundles.PayloadPath(Revit("2025")));
        await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None);

        Assert.AreEqual("2025-new", harness.Temp.Marker(Revit("2025")));
        Assert.IsNull(harness.Store.Load()!.Payloads.Single(payload => payload.Target == Revit("2025")).PendingVersion);
        Assert.HasCount(2, XDocument.Load(harness.Temp.Revit.PackageContentsPath).Root!.Elements("Components"));
        Assert.HasCount(1, XDocument.Load(harness.Temp.Acad.PackageContentsPath).Root!.Elements("Components"));
    }

    [TestMethod]
    public async Task Busy_Revit_does_not_hold_AutoCad()
    {
        using var harness = new Harness(null);
        harness.Channels.PublishAddin("3.1.0", (Revit("2025"), "revit-new"), (Acad("2025"), "acad-new"));
        harness.Temp.WritePayload(Revit("2025"), "revit-old", "RevitDevTool.addin");
        harness.Temp.WritePayload(Acad("2025"), "acad-old", "AcadDevTool.dll");
        harness.FolderLock.Lock(harness.Temp.Bundles.PayloadPath(Revit("2025")));
        harness.Store.Save(new Installation
        {
            Targets = [Revit("2025"), Acad("2025")],
            Payloads = [Installed(Revit("2025"), "3.0.0"), Installed(Acad("2025"), "3.0.0")]
        });

        var result = await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { Acad("2025") }, result.Applied!.ToArray());
        Assert.AreEqual("acad-new", harness.Temp.Marker(Acad("2025")));
        Assert.AreEqual("revit-old", harness.Temp.Marker(Revit("2025")));
    }

    [TestMethod]
    public async Task Notify_mode_offers_and_changes_nothing_until_the_user_approves()
    {
        using var harness = new Harness(null);
        harness.Channels.PublishAddin("3.1.0", (Revit("2024"), "2024-new"));
        harness.Temp.WritePayload(Revit("2024"), "2024-old", "RevitDevTool.addin");
        harness.Store.Save(new Installation
        {
            Policy = { Mode = UpdateMode.Notify },
            Targets = [Revit("2024")],
            Payloads = [Installed(Revit("2024"), "3.0.0")]
        });

        Assert.AreEqual(UpdateCycleStatus.Notified, (await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None)).Status);
        Assert.AreEqual("2024-old", harness.Temp.Marker(Revit("2024")));
        Assert.AreEqual(UpdateNoticeKind.Available, harness.Notifier.Notices.Single().Kind);

        Assert.AreEqual(UpdateCycleStatus.Updated, (await harness.Engine.CheckAsync(UpdateTrigger.UserApproved, CancellationToken.None)).Status);
        Assert.AreEqual("2024-new", harness.Temp.Marker(Revit("2024")));
    }

    [TestMethod]
    public async Task Skipped_version_is_ignored_and_off_mode_does_not_check_on_schedule()
    {
        using var harness = new Harness(null);
        harness.Channels.PublishAddin("3.1.0", (Revit("2024"), "2024-new"));
        harness.Temp.WritePayload(Revit("2024"), "2024-old", "RevitDevTool.addin");
        harness.Store.Save(new Installation
        {
            Policy = { SkippedVersion = "3.1.0" },
            Targets = [Revit("2024")],
            Payloads = [Installed(Revit("2024"), "3.0.0")]
        });

        Assert.AreEqual(UpdateCycleStatus.UpToDate, (await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None)).Status);
        Assert.AreEqual("2024-old", harness.Temp.Marker(Revit("2024")));

        harness.Store.Update(installation => installation.Policy.Mode = UpdateMode.Off);
        var requests = harness.Channels.IndexRequests;
        Assert.AreEqual(UpdateCycleStatus.Disabled, (await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None)).Status);
        Assert.AreEqual(requests, harness.Channels.IndexRequests);
    }

    [TestMethod]
    public async Task First_selection_installs_the_payloads_of_the_selected_targets()
    {
        using var harness = new Harness(null, (HostApp.Revit, "2024"), (HostApp.AutoCad, "2026"));
        harness.Channels.PublishAddin("3.1.0", (Revit("2024"), "2024-new"), (Acad("2024"), "2024-new"), (Revit("2026"), "2026-new"), (Acad("2026"), "2026-new"));

        var options = await harness.Engine.GetTargetsAsync(CancellationToken.None);
        Assert.HasCount(4, options);
        CollectionAssert.AreEquivalent(
            new[] { Revit("2024"), Acad("2026") },
            options.Where(option => option.Selected).Select(option => option.Component.Target).ToArray());

        var reports = new List<PayloadProgress>();
        var result = await harness.Engine.ApplySelectionAsync([Revit("2024"), Acad("2026")], new UpdatePolicy(), null,
            new SynchronousProgress<PayloadProgress>(reports.Add), CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { Revit("2024"), Acad("2026") }, result.Applied.ToArray());
        Assert.IsTrue(reports.Any(report => report.Target == Revit("2024") && report is { Stage: PayloadStage.Downloading, Percent: 100 }));
        Assert.IsTrue(reports.Any(report => report.Target == Acad("2026") && report is { Stage: PayloadStage.Installing, Percent: null }));
        Assert.AreEqual("3.1.0 is installed.", harness.Notifier.Notices.Single(notice => notice.Kind == UpdateNoticeKind.Applied).Message);

        Assert.AreEqual(2, harness.Channels.PayloadDownloads);
        Assert.IsFalse(Directory.Exists(harness.Temp.Revit.StagingPath("3.1.0")));
        Assert.IsFalse(Directory.Exists(harness.Temp.Acad.StagingPath("3.1.0")));
        CollectionAssert.AreEqual(new[] { "./Contents/2024/RevitDevTool.addin" }, Modules(harness.Temp.Revit));
        CollectionAssert.AreEqual(new[] { "./Contents/2026/AcadDevTool.dll" }, Modules(harness.Temp.Acad));

        var rows = await harness.Engine.GetTargetsAsync(CancellationToken.None);
        Assert.IsTrue(rows.All(row => row.Latest == "3.1.0"));
        Assert.AreEqual("3.1.0", rows.Single(row => row.Component.Target == Revit("2024")).Installed);
        Assert.IsNull(rows.Single(row => row.Component.Target == Acad("2024")).Installed);

        reports.Clear();
        await harness.Engine.ApplySelectionAsync([Revit("2024"), Acad("2024"), Acad("2026")], null, null,
            new SynchronousProgress<PayloadProgress>(reports.Add), CancellationToken.None);
        Assert.IsTrue(reports.All(report => report.Target == Acad("2024")));
        Assert.AreEqual(3, harness.Channels.PayloadDownloads);
        CollectionAssert.AreEquivalent(new[] { "./Contents/2024/AcadDevTool.dll", "./Contents/2026/AcadDevTool.dll" }, Modules(harness.Temp.Acad));
    }

    [TestMethod]
    public async Task AutoCad_based_products_select_the_AutoCad_component()
    {
        using var harness = new Harness(null, (HostApp.Civil3D, "2026"), (HostApp.Plant3D, "2027"));
        harness.Channels.PublishAddin("4.0.0", (Revit("2026"), "a"), (Acad("2026"), "a"), (Revit("2027"), "b"), (Acad("2027"), "b"));

        var options = await harness.Engine.GetTargetsAsync(CancellationToken.None);

        var civil = options.Single(option => option.Component.Target == Acad("2026"));
        Assert.IsTrue(civil.Selected);
        CollectionAssert.AreEqual(new[] { HostApp.Civil3D }, civil.Hosts.ToArray());
        Assert.IsTrue(options.Single(option => option.Component.Target == Acad("2027")).OnComputer);
        Assert.IsFalse(options.Single(option => option.Component.Target == Revit("2026")).OnComputer);
    }

    [TestMethod]
    public async Task New_target_on_the_computer_is_reported_one_time()
    {
        using var harness = new Harness(null, (HostApp.Revit, "2027"));
        harness.Channels.PublishAddin("3.0.0");
        harness.Store.Save(new Installation { Targets = [Revit("2024")] });

        await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None);
        await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None);

        var notice = harness.Notifier.Notices.Single(item => item.Kind == UpdateNoticeKind.NewTarget);
        CollectionAssert.AreEqual(new[] { Revit("2027") }, notice.Targets!.ToArray());
        StringAssert.Contains(notice.Message, "Revit 2027");
    }

    [TestMethod]
    public async Task Empty_index_gives_no_update()
    {
        using var harness = new Harness(null);
        harness.Store.Save(new Installation { Targets = [Revit("2024")] });

        Assert.IsEmpty(await harness.Engine.GetTargetsAsync(CancellationToken.None));
        Assert.AreEqual(UpdateCycleStatus.NoRelease, (await harness.Engine.CheckAsync(UpdateTrigger.Manual, CancellationToken.None)).Status);
    }

    [TestMethod]
    public async Task First_run_uses_channel_addin()
    {
        using var harness = new Harness(null, daemon: reader => new InstalledDaemon(reader, "4.0.0-beta.2", UpdatePolicy.PrereleaseChannel));
        harness.Channels.PublishAddin("4.0.0-beta.1", (Revit("2025"), "beta"));
        harness.Channels.PublishAddin("3.9.0", (Revit("2025"), "other"));
        harness.Channels.PublishDaemon("4.0.0-beta.2");

        var result = await harness.Engine.ApplySelectionAsync([Revit("2025")], null, null, null, CancellationToken.None);

        Assert.AreEqual("4.0.0-beta.1", result.Version);
        Assert.AreEqual("beta", harness.Temp.Marker(Revit("2025")));
        Assert.AreEqual(UpdatePolicy.PrereleaseChannel, harness.Store.Load()!.Policy.Channel);
    }

    [TestMethod]
    public async Task Daemon_without_a_release_uses_the_latest_release_of_the_chosen_channel()
    {
        using var harness = new Harness(null, daemon: reader => new InstalledDaemon(reader, "4.0.0-alpha.480", UpdatePolicy.StableChannel));
        harness.Channels.PublishDaemon("3.0.0");
        harness.Channels.PublishAddin("4.0.0-beta.1", (Revit("2025"), "beta"));

        Assert.IsEmpty(await harness.Engine.GetTargetsAsync(CancellationToken.None));

        harness.Engine.SetPolicy(new UpdatePolicy { Channel = UpdatePolicy.PrereleaseChannel });
        Assert.IsFalse(harness.Store.Exists);
        Assert.HasCount(1, await harness.Engine.GetTargetsAsync(CancellationToken.None));

        var result = await harness.Engine.ApplySelectionAsync([Revit("2025")], null, null, null, CancellationToken.None);

        Assert.AreEqual("4.0.0-beta.1", result.Version);
        Assert.AreEqual(UpdatePolicy.PrereleaseChannel, harness.Store.Load()!.Policy.Channel);
    }

    [TestMethod]
    public async Task First_selection_defaults_to_the_payloads_an_earlier_install_left_on_disk()
    {
        using var harness = new Harness(null, (HostApp.Revit, "2026"));
        harness.Channels.PublishAddin("4.0.0", (Revit("2024"), "a"), (Acad("2024"), "a"), (Revit("2026"), "b"), (Acad("2026"), "b"));
        harness.Temp.WritePayload(Revit("2024"), "inno", "RevitDevTool.addin");

        var selected = (await harness.Engine.GetTargetsAsync(CancellationToken.None)).Where(option => option.Selected).Select(option => option.Component.Target).ToArray();

        CollectionAssert.AreEqual(new[] { Revit("2024") }, selected);
    }

    [TestMethod]
    public async Task Prerelease_option_updates_to_the_newest_release_of_both_channels()
    {
        using var harness = new Harness(null);
        harness.Channels.PublishAddin("4.0.0", (Revit("2024"), "stable"));
        harness.Channels.PublishAddin("4.1.0-beta.1", (Revit("2024"), "beta"));
        harness.Temp.WritePayload(Revit("2024"), "old", "RevitDevTool.addin");
        harness.Store.Save(new Installation
        {
            Targets = [Revit("2024")],
            Payloads = [Installed(Revit("2024"), "3.0.0")]
        });

        Assert.AreEqual("4.0.0", (await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None)).Version);
        Assert.AreEqual("stable", harness.Temp.Marker(Revit("2024")));

        harness.Engine.SetPolicy(new UpdatePolicy { Channel = UpdatePolicy.PrereleaseChannel });
        Assert.AreEqual("4.1.0-beta.1", (await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None)).Version);
        Assert.AreEqual("beta", harness.Temp.Marker(Revit("2024")));
    }

    [TestMethod]
    public async Task Daemon_restarts_on_the_Updates_tab_only_after_a_check_that_the_user_started()
    {
        UpdatableDaemon? daemon = null;
        using var harness = new Harness(null, reader => daemon = new UpdatableDaemon(reader));
        harness.Channels.PublishDaemon("3.1.0");
        harness.Channels.PublishAddin("3.1.0", (Revit("2024"), "2024-new"));
        harness.Store.Save(new Installation { Targets = [Revit("2024")] });

        await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None);
        await harness.Engine.CheckAsync(UpdateTrigger.Manual, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { false, true }, daemon!.Restarts);
    }

    [TestMethod]
    public async Task Major_Daemon_update_waits_while_an_add_in_of_that_major_is_pending()
    {
        UpdatableDaemon? daemon = null;
        using var harness = new Harness(null, reader => daemon = new UpdatableDaemon(reader));
        harness.Channels.PublishDaemon("4.0.0");
        harness.Channels.PublishAddin("4.0.0", (Revit("2024"), "2024-new"));
        harness.Temp.WritePayload(Revit("2024"), "2024-old", "RevitDevTool.addin");
        harness.FolderLock.Lock(harness.Temp.Bundles.PayloadPath(Revit("2024")));
        harness.Store.Save(new Installation
        {
            Targets = [Revit("2024")],
            Payloads = [Installed(Revit("2024"), "3.0.0")]
        });

        await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None);

        Assert.IsEmpty(daemon!.Restarts);
        Assert.IsTrue(harness.Notifier.Notices.Any(notice => notice.Kind == UpdateNoticeKind.DaemonPending && notice.Version == "4.0.0"));

        harness.FolderLock.Unlock(harness.Temp.Bundles.PayloadPath(Revit("2024")));
        await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None);

        Assert.AreEqual("2024-new", harness.Temp.Marker(Revit("2024")));
        CollectionAssert.AreEqual(new[] { false }, daemon.Restarts);
    }

    [TestMethod]
    public async Task Major_Daemon_update_does_not_wait_for_an_add_in_the_release_no_longer_ships()
    {
        UpdatableDaemon? daemon = null;
        using var harness = new Harness(null, reader => daemon = new UpdatableDaemon(reader));
        harness.Channels.PublishDaemon("4.0.0");
        harness.Channels.PublishAddin("4.0.0", (Revit("2025"), "2025-new"));
        harness.Temp.WritePayload(Revit("2024"), "2024-old", "RevitDevTool.addin");
        harness.Store.Save(new Installation
        {
            Targets = [Revit("2024")],
            Payloads = [Installed(Revit("2024"), "3.0.0")]
        });

        await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { false }, daemon!.Restarts);
    }

    [TestMethod]
    public async Task Minor_Daemon_update_does_not_wait_for_a_busy_add_in()
    {
        UpdatableDaemon? daemon = null;
        using var harness = new Harness(null, reader => daemon = new UpdatableDaemon(reader));
        harness.Channels.PublishDaemon("3.1.0");
        harness.Channels.PublishAddin("3.1.0", (Revit("2024"), "2024-new"));
        harness.Temp.WritePayload(Revit("2024"), "2024-old", "RevitDevTool.addin");
        harness.FolderLock.Lock(harness.Temp.Bundles.PayloadPath(Revit("2024")));
        harness.Store.Save(new Installation
        {
            Targets = [Revit("2024")],
            Payloads = [Installed(Revit("2024"), "3.0.0")]
        });

        await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { false }, daemon!.Restarts);
    }

    [TestMethod]
    public async Task Scheduler_runs_the_cycle_after_the_startup_delay()
    {
        using var harness = new Harness(null);
        harness.Channels.PublishAddin("3.1.0", (Revit("2024"), "2024-new"));
        harness.Temp.WritePayload(Revit("2024"), "2024-old", "RevitDevTool.addin");
        harness.Store.Save(new Installation
        {
            Targets = [Revit("2024")],
            Payloads = [Installed(Revit("2024"), "3.0.0")]
        });
        using var scheduler = new UpdateScheduler(harness.Engine, Options.Create(new UpdateEngineOptions { StartupDelay = TimeSpan.Zero }),
            NullLogger<UpdateScheduler>.Instance);

        await scheduler.StartAsync(CancellationToken.None);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (harness.Temp.Marker(Revit("2024")) != "2024-new" && DateTime.UtcNow < deadline) await Task.Delay(50);
        await scheduler.StopAsync(CancellationToken.None);

        Assert.AreEqual("2024-new", harness.Temp.Marker(Revit("2024")));
    }

    [TestMethod]
    public async Task Stable_install_ignores_prerelease()
    {
        UpdatableDaemon? daemon = null;
        using var harness = new Harness(null, reader => daemon = new UpdatableDaemon(reader));
        harness.Channels.PublishAddin("3.1.0-beta.1", (Revit("2024"), "beta"));
        harness.Channels.PublishDaemon("3.1.0-beta.1");
        harness.Temp.WritePayload(Revit("2024"), "old", "RevitDevTool.addin");
        harness.Store.Save(new Installation
        {
            Targets = [Revit("2024")],
            Payloads = [Installed(Revit("2024"), "3.0.0")]
        });

        Assert.AreEqual(UpdateCycleStatus.NoRelease, (await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None)).Status);
        Assert.AreEqual("old", harness.Temp.Marker(Revit("2024")));
        Assert.IsEmpty(daemon!.Prepared);
    }

    [TestMethod]
    public async Task Prerelease_install_takes_newer_stable()
    {
        UpdatableDaemon? daemon = null;
        using var harness = new Harness(null, reader => daemon = new UpdatableDaemon(reader));
        harness.Channels.PublishAddin("3.1.0-beta.1", (Revit("2024"), "beta"));
        harness.Channels.PublishDaemon("3.1.0-beta.1");
        harness.Channels.PublishAddin("3.1.0", (Revit("2024"), "stable"));
        harness.Channels.PublishDaemon("3.1.0");
        harness.Temp.WritePayload(Revit("2024"), "old", "RevitDevTool.addin");
        harness.Store.Save(new Installation
        {
            Policy = { Channel = UpdatePolicy.PrereleaseChannel },
            Targets = [Revit("2024")],
            Payloads = [Installed(Revit("2024"), "3.0.0")]
        });

        Assert.AreEqual("3.1.0", (await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None)).Version);
        Assert.AreEqual("stable", harness.Temp.Marker(Revit("2024")));
        CollectionAssert.AreEqual(new[] { "3.1.0" }, daemon!.Prepared);
    }

    [TestMethod]
    public async Task Daemon_only_change_skips_payloads()
    {
        UpdatableDaemon? daemon = null;
        using var harness = new Harness(null, reader => daemon = new UpdatableDaemon(reader));
        harness.Channels.PublishAddin("3.0.0", (Revit("2024"), "same"));
        harness.Channels.PublishDaemon("3.0.1");
        harness.Temp.WritePayload(Revit("2024"), "installed", "RevitDevTool.addin");
        harness.Store.Save(new Installation
        {
            Targets = [Revit("2024")],
            Payloads = [Installed(Revit("2024"), "3.0.0")]
        });

        Assert.AreEqual(UpdateCycleStatus.Updated, (await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None)).Status);
        Assert.AreEqual(0, harness.Channels.PayloadDownloads);
        CollectionAssert.AreEqual(new[] { "3.0.1" }, daemon!.Prepared);
    }

    [TestMethod]
    public async Task Addin_only_change_keeps_Daemon()
    {
        UpdatableDaemon? daemon = null;
        using var harness = new Harness(null, reader => daemon = new UpdatableDaemon(reader));
        harness.Channels.PublishDaemon("3.0.0");
        harness.Channels.PublishAddin("3.0.1", (Revit("2024"), "new"));
        harness.Temp.WritePayload(Revit("2024"), "old", "RevitDevTool.addin");
        harness.Store.Save(new Installation
        {
            Targets = [Revit("2024")],
            Payloads = [Installed(Revit("2024"), "3.0.0")]
        });

        await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None);

        Assert.AreEqual("new", harness.Temp.Marker(Revit("2024")));
        Assert.IsEmpty(daemon!.Prepared);
        Assert.IsEmpty(daemon.Restarts);
    }

    [TestMethod]
    public async Task Notify_mode_offers_parts_separately()
    {
        using var harness = new Harness(null, reader => new UpdatableDaemon(reader));
        harness.Channels.PublishDaemon("3.0.1");
        harness.Channels.PublishAddin("3.1.0", (Revit("2024"), "new"));
        harness.Store.Save(new Installation
        {
            Policy = { Mode = UpdateMode.Notify },
            Targets = [Revit("2024")],
            Payloads = [Installed(Revit("2024"), "3.0.0")]
        });

        await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "3.1.0", "3.0.1" }, harness.Notifier.Notices.Select(notice => notice.Version).ToArray());
        Assert.IsTrue(harness.Notifier.Notices.All(notice => notice.Kind == UpdateNoticeKind.Available));
    }

    [TestMethod]
    public async Task Bad_index_signature_changes_nothing()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var harness = new Harness(key, new FakeChannels(other), null);
        harness.Channels.PublishAddin("3.1.0", (Revit("2024"), "new"));
        harness.Temp.WritePayload(Revit("2024"), "old", "RevitDevTool.addin");
        harness.Store.Save(new Installation
        {
            Targets = [Revit("2024")],
            Payloads = [Installed(Revit("2024"), "3.0.0")]
        });

        await Assert.ThrowsExactlyAsync<UpdateIntegrityException>(() => harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None));
        Assert.AreEqual("old", harness.Temp.Marker(Revit("2024")));
        Assert.AreEqual(0, harness.Channels.PayloadDownloads);
    }

    [TestMethod]
    public async Task Newer_min_updater_holds_payloads()
    {
        using var harness = new Harness(null, reader => new InstalledDaemon(reader, "4.0.0", UpdatePolicy.StableChannel));
        harness.Channels.MinUpdaterVersion = "4.1.0";
        harness.Channels.PublishAddin("4.1.0", (Revit("2024"), "new"));
        harness.Temp.WritePayload(Revit("2024"), "old", "RevitDevTool.addin");
        harness.Store.Save(new Installation
        {
            Targets = [Revit("2024")],
            Payloads = [Installed(Revit("2024"), "4.0.0")]
        });

        Assert.AreEqual(UpdateCycleStatus.UpToDate, (await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None)).Status);
        Assert.AreEqual("old", harness.Temp.Marker(Revit("2024")));
        Assert.AreEqual(0, harness.Channels.PayloadDownloads);
    }

    private static InstalledPayload Installed(AddinTarget target, string version) =>
        new() { Platform = target.Platform, HostVersion = target.HostVersion, Version = version };

    private static string[] Modules(AutodeskBundle bundle) => XDocument.Load(bundle.PackageContentsPath).Root!.Elements("Components")
        .Select(component => component.Element("ComponentEntry")!.Attribute("ModuleName")!.Value)
        .ToArray();

    private sealed class Harness : IDisposable
    {
        public Harness(ECDsa? key, params (HostApp App, string Version)[] installed) : this(key, null, installed)
        {
        }

        public Harness(ECDsa? key, Func<ReleaseReader, DaemonUpdater>? daemon, params (HostApp App, string Version)[] installed)
            : this(key, new FakeChannels(key), daemon, installed)
        {
        }

        public Harness(ECDsa? key, FakeChannels channels, Func<ReleaseReader, DaemonUpdater>? daemon, params (HostApp App, string Version)[] installed)
        {
            Channels = channels;
            Store = new InstallationStore(Temp.InstallationPath);
            var reader = new ReleaseReader(new HttpClient(Channels), new ReleaseSignature(key?.ExportSubjectPublicKeyInfoPem()));
            Engine = new UpdateEngine(
                Temp.Bundles,
                Store,
                new ChannelIndexSource(reader, FakeChannels.IndexUrl),
                reader,
                new PayloadInstaller(Temp.Bundles, FolderLock, reader),
                daemon?.Invoke(reader) ?? new DaemonUpdater(reader),
                Notifier,
                [new FixedResolver(installed)],
                NullLogger<UpdateEngine>.Instance);
        }

        public TempPlugins Temp { get; } = new();
        public FakeChannels Channels { get; }
        public FakeFolderLock FolderLock { get; } = new();
        public RecordingNotifier Notifier { get; } = new();
        public InstallationStore Store { get; }
        public UpdateEngine Engine { get; }

        public void Dispose()
        {
            Engine.Dispose();
            Temp.Dispose();
        }
    }
}
