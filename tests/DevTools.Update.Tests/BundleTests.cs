using DevTools.Update.Apply;
using DevTools.Update.Autodesk;
using DevTools.Update.Installations;
using DevTools.Update.Releases;
using System.IO.Compression;
using System.Xml.Linq;
using static DevTools.Update.Tests.Hosts;

namespace DevTools.Update.Tests;

[TestClass]
public sealed class BundleTests
{
    [TestMethod]
    public void Template_components_name_their_bundle()
    {
        var components = PackageContents.TemplateComponents();

        var revit = components.Single(component => component.Target == Revit("2024"));
        Assert.AreEqual("Revit 2024", revit.Description);
        Assert.AreEqual("RevitDevTool.bundle", revit.Bundle);
        Assert.AreEqual("Contents/2024/RevitDevTool.addin", revit.ModulePath);
        var acad = components.Single(component => component.Target == Acad("2027"));
        Assert.AreEqual("AcadDevTool.bundle", acad.Bundle);
        Assert.AreEqual("Contents/2027/AcadDevTool.dll", acad.ModulePath);
    }

    [TestMethod]
    public void Each_bundle_holds_one_platform()
    {
        foreach (var name in PackageContents.BundleNames)
        {
            var platforms = PackageContents.TemplateComponents().Where(component => component.Bundle == name).Select(component => component.Target.Platform).Distinct();
            Assert.HasCount(1, platforms.ToArray(), name);
        }

        Assert.AreEqual("AcadDevTool.bundle", PackageContents.BundleOf(Acad("2099")));
    }

    [TestMethod]
    public void Only_the_newest_AutoCad_component_is_open()
    {
        var open = PackageContents.LoadTemplate("AcadDevTool.bundle").Root!.Elements("Components")
            .Select(component => component.Element("RuntimeRequirements")!)
            .Where(requirements => requirements.Attribute("SeriesMax") is null)
            .Select(requirements => requirements.Attribute("SeriesMin")!.Value)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "R26.0" }, open);
    }

    [TestMethod]
    public void PackageContents_keeps_only_selected_targets_that_are_on_disk()
    {
        using var temp = new TempPlugins();
        temp.WritePayload(Revit("2024"), "a", "RevitDevTool.addin");
        temp.WritePayload(Revit("2025"), "b");

        var written = PackageContents.Write(temp.Revit, [Revit("2024"), Revit("2025"), Revit("2026"), Acad("2024")], "3.1.0");

        CollectionAssert.AreEqual(new[] { Revit("2024") }, written.ToArray());
        var document = XDocument.Load(temp.Revit.PackageContentsPath);
        Assert.AreEqual("3.1.0", document.Root!.Attribute("AppVersion")!.Value);
        Assert.AreEqual("RevitDevTool", document.Root.Attribute("Name")!.Value);
        Assert.HasCount(1, document.Root.Elements("Components"));
    }

    [TestMethod]
    public void Installation_round_trips_with_a_lowercase_mode()
    {
        using var temp = new TempPlugins();
        var store = new InstallationStore(temp.InstallationPath);
        store.Save(new Installation { Policy = { Mode = UpdateMode.Notify }, Targets = [Revit("2024")] });

        StringAssert.Contains(File.ReadAllText(temp.InstallationPath), "\"mode\": \"notify\"");
        var loaded = store.Load()!;
        Assert.AreEqual(UpdateMode.Notify, loaded.Policy.Mode);
        Assert.AreEqual(Revit("2024"), loaded.Targets.Single());
    }

    [TestMethod]
    public void Interval_has_a_minimum_of_fifteen_minutes()
    {
        Assert.AreEqual(TimeSpan.FromMinutes(15), new UpdatePolicy { IntervalMinutes = 4 }.Interval);
        Assert.AreEqual(TimeSpan.FromMinutes(60), new UpdatePolicy().Interval);
    }

    [TestMethod]
    public void Busy_payload_is_not_swapped()
    {
        using var temp = new TempPlugins();
        var folderLock = new FakeFolderLock();
        var current = temp.WritePayload(Revit("2024"), "old");
        var staged = Stage(temp, Revit("2024"), "new");
        folderLock.Lock(current);

        var lockers = Installer(temp, folderLock).TryApply(Revit("2024"), staged);

        Assert.HasCount(1, lockers);
        Assert.AreEqual("old", temp.Marker(Revit("2024")));
        Assert.IsTrue(Directory.Exists(staged));
    }

    [TestMethod]
    public void Free_payload_swaps_keeps_one_rollback_and_rolls_back()
    {
        using var temp = new TempPlugins();
        var installer = Installer(temp, new FakeFolderLock());
        temp.WritePayload(Acad("2024"), "v1");

        Assert.IsEmpty(installer.TryApply(Acad("2024"), Stage(temp, Acad("2024"), "v2")));
        Assert.IsEmpty(installer.TryApply(Acad("2024"), Stage(temp, Acad("2024"), "v3")));

        Assert.AreEqual("v3", temp.Marker(Acad("2024")));
        Assert.HasCount(1, Directory.GetDirectories(temp.Acad.Rollback));
        Assert.IsTrue(installer.TryRollback(Acad("2024")));
        Assert.AreEqual("v2", temp.Marker(Acad("2024")));
        Assert.IsFalse(Directory.Exists(temp.Revit.Root));
    }

    [TestMethod]
    public async Task Staging_refuses_a_zip_that_does_not_match_the_manifest()
    {
        using var temp = new TempPlugins();
        var releases = new FakeChannels();
        var release = releases.PublishAddin("3.1.0", (Revit("2024"), "new"));
        releases.Add(release.Asset("RevitDevTool-3.1.0-2024.zip"), FakeChannels.PayloadZip("changed"));
        var reader = new ReleaseReader(new HttpClient(releases), new ReleaseSignature(null));
        var installer = new PayloadInstaller(temp.Bundles, new FakeFolderLock(), reader);
        var manifest = await reader.ReadManifestAsync(release, CancellationToken.None);

        await Assert.ThrowsExactlyAsync<UpdateIntegrityException>(() => installer.StageAsync(release, manifest.FindPayload(Revit("2024"))!, null, CancellationToken.None));
        Assert.IsFalse(installer.IsStaged("3.1.0", Revit("2024")));
    }

    [TestMethod]
    public void Offline_bundle_stages_one_payload_and_gives_the_release_version()
    {
        using var temp = new TempPlugins();
        var zip = Path.Combine(temp.Root, "AcadDevTool.bundle.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            Write(archive, "AcadDevTool.bundle/PackageContents.xml", """<ApplicationPackage AppVersion="3.1.0"><CompanyDetails /></ApplicationPackage>""");
            Write(archive, "AcadDevTool.bundle/Contents/2024/marker.txt", "offline");
            Write(archive, "AcadDevTool.bundle/Contents/2025/marker.txt", "other");
        }

        var offline = OfflineBundle.Read(zip);
        var staged = Installer(temp, new FakeFolderLock()).StageFromBundle(offline, Acad("2024"));

        Assert.AreEqual(new OfflineBundle(zip, "AcadDevTool.bundle", "3.1.0"), offline);
        Assert.AreEqual("offline", File.ReadAllText(Path.Combine(staged, "marker.txt")));
        Assert.HasCount(1, Directory.GetFiles(staged));
    }

    [TestMethod]
    public void Restart_manager_reports_this_process_for_an_open_dll()
    {
        using var temp = new TempPlugins();
        var folder = temp.WritePayload(Revit("2024"), "x", "Locked.dll");
        var folderLock = new RestartManagerFolderLock();

        Assert.IsEmpty(folderLock.GetLockers(folder));
        using (new FileStream(Path.Combine(folder, "Locked.dll"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            CollectionAssert.Contains(folderLock.GetLockers(folder).Select(locker => locker.ProcessId).ToArray(), Environment.ProcessId);
        }
    }

    private static PayloadInstaller Installer(TempPlugins temp, IFolderLock folderLock) =>
        new(temp.Bundles, folderLock, new ReleaseReader(new HttpClient(new FakeChannels()), new ReleaseSignature(null)));

    private static string Stage(TempPlugins temp, AddinTarget target, string marker)
    {
        var path = Installer(temp, new FakeFolderLock()).StagedPath(marker, target);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "marker.txt"), marker);
        return path;
    }

    private static void Write(ZipArchive archive, string name, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(text);
    }
}
