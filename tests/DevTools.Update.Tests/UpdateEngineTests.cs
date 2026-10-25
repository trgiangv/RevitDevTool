using DevTools.Hosting;
using DevTools.Update.Apply;
using DevTools.Update.Autodesk;
using DevTools.Update.Engine;
using DevTools.Update.Installations;
using DevTools.Update.Releases;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace DevTools.Update.Tests;

[TestClass]
public sealed class UpdateEngineTests
{
    [TestMethod]
    public async Task Auto_mode_updates_free_payloads_and_keeps_busy_payloads_pending()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var harness = new Harness(key);
        harness.Releases.Publish("3.1.0", key, ("2024", ["AutoCAD*", "Revit"], "2024-new"), ("2025", ["Revit"], "2025-new"));
        harness.Temp.WritePayload("2024", "2024-old", "RevitDevTool.addin");
        harness.Temp.WritePayload("2025", "2025-old", "RevitDevTool.addin");
        harness.FolderLock.Lock(harness.Temp.Bundle.PayloadPath("2025"));
        harness.Store.Save(new Installation
        {
            Targets = [new AddinTarget("Revit", "2024"), new AddinTarget("AutoCAD*", "2024"), new AddinTarget("Revit", "2025")],
            Payloads = [new InstalledPayload { HostVersion = "2024", Version = "3.0.0" }, new InstalledPayload { HostVersion = "2025", Version = "3.0.0" }]
        });

        var result = await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None);

        Assert.AreEqual(UpdateCycleStatus.Updated, result.Status);
        CollectionAssert.AreEqual(new[] { "2024" }, result.Applied!.ToArray());
        CollectionAssert.AreEqual(new[] { "2025" }, result.Pending!.ToArray());
        Assert.AreEqual("2024-new", harness.Temp.Marker("2024"));
        Assert.AreEqual("2025-old", harness.Temp.Marker("2025"));
        Assert.AreEqual("3.1.0", harness.Store.Load()!.Payloads.Single(payload => payload.HostVersion == "2025").PendingVersion);
        Assert.IsTrue(harness.Notifier.Notices.Any(notice => notice.Kind == UpdateNoticeKind.Pending && notice.Message.Contains("Revit.exe", StringComparison.Ordinal)));

        harness.FolderLock.Unlock(harness.Temp.Bundle.PayloadPath("2025"));
        await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None);

        Assert.AreEqual("2025-new", harness.Temp.Marker("2025"));
        Assert.IsNull(harness.Store.Load()!.Payloads.Single(payload => payload.HostVersion == "2025").PendingVersion);
        Assert.HasCount(3, XDocument.Load(harness.Temp.Bundle.PackageContentsPath).Root!.Elements("Components"));
    }

    [TestMethod]
    public async Task Notify_mode_offers_and_changes_nothing_until_the_user_approves()
    {
        using var harness = new Harness(null);
        harness.Releases.Publish("3.1.0", null, ("2024", ["Revit"], "2024-new"));
        harness.Temp.WritePayload("2024", "2024-old", "RevitDevTool.addin");
        harness.Store.Save(new Installation
        {
            Policy = { Mode = UpdateMode.Notify },
            Targets = [new AddinTarget("Revit", "2024")],
            Payloads = [new InstalledPayload { HostVersion = "2024", Version = "3.0.0" }]
        });

        Assert.AreEqual(UpdateCycleStatus.Notified, (await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None)).Status);
        Assert.AreEqual("2024-old", harness.Temp.Marker("2024"));
        Assert.AreEqual(UpdateNoticeKind.Available, harness.Notifier.Notices.Single().Kind);

        Assert.AreEqual(UpdateCycleStatus.Updated, (await harness.Engine.CheckAsync(UpdateTrigger.UserApproved, CancellationToken.None)).Status);
        Assert.AreEqual("2024-new", harness.Temp.Marker("2024"));
    }

    [TestMethod]
    public async Task Skipped_version_is_ignored_and_off_mode_does_not_check_on_schedule()
    {
        using var harness = new Harness(null);
        harness.Releases.Publish("3.1.0", null, ("2024", ["Revit"], "2024-new"));
        harness.Temp.WritePayload("2024", "2024-old", "RevitDevTool.addin");
        harness.Store.Save(new Installation
        {
            Policy = { SkippedVersion = "3.1.0" },
            Targets = [new AddinTarget("Revit", "2024")],
            Payloads = [new InstalledPayload { HostVersion = "2024", Version = "3.0.0" }]
        });

        Assert.AreEqual(UpdateCycleStatus.UpToDate, (await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None)).Status);
        Assert.AreEqual("2024-old", harness.Temp.Marker("2024"));

        harness.Store.Update(installation => installation.Policy.Mode = UpdateMode.Off);
        var requests = harness.Releases.Requests;
        Assert.AreEqual(UpdateCycleStatus.Disabled, (await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None)).Status);
        Assert.AreEqual(requests, harness.Releases.Requests);
    }

    [TestMethod]
    public async Task First_selection_installs_the_payloads_of_the_selected_targets()
    {
        using var harness = new Harness(null, (HostApp.Revit, "2024"), (HostApp.AutoCad, "2026"));
        harness.Releases.Publish("3.1.0", null, ("2024", ["AutoCAD*", "Revit"], "2024-new"), ("2026", ["AutoCAD*", "Revit"], "2026-new"));

        var options = await harness.Engine.GetTargetsAsync(CancellationToken.None);
        Assert.HasCount(4, options);
        CollectionAssert.AreEquivalent(
            new[] { new AddinTarget("Revit", "2024"), new AddinTarget("AutoCAD*", "2026") },
            options.Where(option => option.Selected).Select(option => option.Component.Target).ToArray());

        var result = await harness.Engine.ApplySelectionAsync([new AddinTarget("Revit", "2024"), new AddinTarget("AutoCAD*", "2026")], new UpdatePolicy(), null, CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { "2024", "2026" }, result.Applied.ToArray());
        var modules = XDocument.Load(harness.Temp.Bundle.PackageContentsPath).Root!.Elements("Components")
            .Select(component => component.Element("ComponentEntry")!.Attribute("ModuleName")!.Value).ToArray();
        CollectionAssert.AreEquivalent(new[] { "./Contents/2024/RevitDevTool.addin", "./Contents/2026/AcadDevTool.dll" }, modules);
    }

    [TestMethod]
    public async Task New_target_on_the_computer_is_reported_one_time()
    {
        using var harness = new Harness(null, (HostApp.Revit, "2027"));
        harness.Releases.Publish("3.0.0", null);
        harness.Store.Save(new Installation { Targets = [new AddinTarget("Revit", "2024")] });

        await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None);
        await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None);

        var notice = harness.Notifier.Notices.Single(item => item.Kind == UpdateNoticeKind.NewTarget);
        CollectionAssert.AreEqual(new[] { new AddinTarget("Revit", "2027") }, notice.Targets!.ToArray());
        StringAssert.Contains(notice.Message, "Revit 2027");
    }

    [TestMethod]
    public async Task Release_without_a_manifest_gives_no_targets_and_no_update()
    {
        using var harness = new Harness(null);
        harness.Releases.PublishWithoutManifest("3.0.0");
        harness.Store.Save(new Installation { Targets = [new AddinTarget("Revit", "2024")] });

        Assert.IsEmpty(await harness.Engine.GetTargetsAsync(CancellationToken.None));
        Assert.AreEqual(UpdateCycleStatus.NoRelease, (await harness.Engine.CheckAsync(UpdateTrigger.Manual, CancellationToken.None)).Status);
    }

    [TestMethod]
    public async Task Prerelease_install_selects_its_own_release_and_keeps_the_prerelease_channel()
    {
        using var harness = new Harness(null, daemon: reader => new InstalledDaemon(reader, "4.0.0-beta.1", UpdatePolicy.PrereleaseChannel));
        harness.Releases.Publish("4.0.0-beta.1", null, ("2025", ["Revit"], "beta"));
        harness.Releases.Publish("3.9.0", null, ("2025", ["Revit"], "other"));

        var result = await harness.Engine.ApplySelectionAsync([new AddinTarget("Revit", "2025")], null, null, CancellationToken.None);

        Assert.AreEqual("4.0.0-beta.1", result.Version);
        Assert.AreEqual("beta", harness.Temp.Marker("2025"));
        Assert.AreEqual(UpdatePolicy.PrereleaseChannel, harness.Store.Load()!.Policy.Channel);
    }

    [TestMethod]
    public async Task First_selection_defaults_to_the_payloads_an_earlier_install_left_on_disk()
    {
        using var harness = new Harness(null, (HostApp.Revit, "2026"));
        harness.Releases.Publish("4.0.0", null, ("2024", ["AutoCAD*", "Revit"], "a"), ("2026", ["AutoCAD*", "Revit"], "b"));
        harness.Temp.WritePayload("2024", "inno", "RevitDevTool.addin");

        var selected = (await harness.Engine.GetTargetsAsync(CancellationToken.None)).Where(option => option.Selected).Select(option => option.Component.Target).ToArray();

        CollectionAssert.AreEqual(new[] { new AddinTarget("Revit", "2024") }, selected);
    }

    [TestMethod]
    public async Task Prerelease_option_updates_to_the_newest_release_of_both_channels()
    {
        using var harness = new Harness(null);
        harness.Releases.Publish("4.0.0", null, ("2024", ["Revit"], "stable"));
        harness.Releases.Publish("4.1.0-beta.1", null, ("2024", ["Revit"], "beta"));
        harness.Temp.WritePayload("2024", "old", "RevitDevTool.addin");
        harness.Store.Save(new Installation
        {
            Targets = [new AddinTarget("Revit", "2024")],
            Payloads = [new InstalledPayload { HostVersion = "2024", Version = "3.0.0" }]
        });

        Assert.AreEqual("4.0.0", (await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None)).Version);
        Assert.AreEqual("stable", harness.Temp.Marker("2024"));

        harness.Engine.SetPolicy(new UpdatePolicy { Channel = UpdatePolicy.PrereleaseChannel });
        Assert.AreEqual("4.1.0-beta.1", (await harness.Engine.CheckAsync(UpdateTrigger.Scheduled, CancellationToken.None)).Version);
        Assert.AreEqual("beta", harness.Temp.Marker("2024"));
    }

    private sealed class Harness : IDisposable
    {
        public Harness(ECDsa? key, params (HostApp App, string Version)[] installed) : this(key, null, installed)
        {
        }

        public Harness(ECDsa? key, Func<ReleaseReader, DaemonUpdater>? daemon, params (HostApp App, string Version)[] installed)
        {
            Store = new InstallationStore(Temp.Bundle);
            var reader = new ReleaseReader(new HttpClient(Releases), new ReleaseSignature(key?.ExportSubjectPublicKeyInfoPem()));
            Engine = new UpdateEngine(
                Temp.Bundle,
                Store,
                Releases,
                reader,
                new PayloadInstaller(Temp.Bundle, FolderLock, reader),
                daemon?.Invoke(reader) ?? new DaemonUpdater(reader),
                Notifier,
                [new FixedResolver(installed)],
                NullLogger<UpdateEngine>.Instance);
        }

        public TempBundle Temp { get; } = new();
        public FakeReleases Releases { get; } = new();
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
