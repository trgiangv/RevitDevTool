using DevTools.Update.Apply;
using DevTools.Update.Autodesk;
using DevTools.Update.Installations;
using DevTools.Update.Releases;
using System.IO.Compression;
using System.Xml.Linq;

namespace DevTools.Update.Tests;

[TestClass]
public sealed class BundleTests
{
    [TestMethod]
    public void Template_components_have_platform_host_version_and_description()
    {
        var components = PackageContents.TemplateComponents();

        var revit = components.Single(component => component.Target == new AddinTarget("Revit", "2024"));
        Assert.AreEqual("Revit 2024", revit.Description);
        Assert.AreEqual("Contents/2024/RevitDevTool.addin", revit.ModulePath);
        Assert.IsTrue(components.Any(component => component.Target == new AddinTarget("AutoCAD*", "2027")));
    }

    [TestMethod]
    public void PackageContents_keeps_only_selected_targets_that_are_on_disk()
    {
        using var temp = new TempBundle();
        temp.WritePayload("2024", "a", "RevitDevTool.addin", "AcadDevTool.dll");
        temp.WritePayload("2025", "b", "AcadDevTool.dll");

        var written = PackageContents.Write(temp.Bundle,
            [new AddinTarget("Revit", "2024"), new AddinTarget("AutoCAD*", "2025"), new AddinTarget("Revit", "2025"), new AddinTarget("Revit", "2026")],
            "3.1.0");

        CollectionAssert.AreEquivalent(new[] { new AddinTarget("Revit", "2024"), new AddinTarget("AutoCAD*", "2025") }, written.ToArray());
        var document = XDocument.Load(temp.Bundle.PackageContentsPath);
        Assert.AreEqual("3.1.0", document.Root!.Attribute("AppVersion")!.Value);
        Assert.HasCount(2, document.Root.Elements("Components"));
    }

    [TestMethod]
    public void Installation_round_trips_with_a_lowercase_mode()
    {
        using var temp = new TempBundle();
        var store = new InstallationStore(temp.Bundle);
        store.Save(new Installation { Policy = { Mode = UpdateMode.Notify }, Targets = [new AddinTarget("Revit", "2024")] });

        StringAssert.Contains(File.ReadAllText(temp.Bundle.InstallationPath), "\"mode\": \"notify\"");
        var loaded = store.Load()!;
        Assert.AreEqual(UpdateMode.Notify, loaded.Policy.Mode);
        Assert.AreEqual(new AddinTarget("Revit", "2024"), loaded.Targets.Single());
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
        using var temp = new TempBundle();
        var folderLock = new FakeFolderLock();
        var current = temp.WritePayload("2024", "old");
        var staged = temp.WritePayload("staged", "new");
        folderLock.Lock(current);

        var lockers = Installer(temp, folderLock).TryApply("2024", staged);

        Assert.HasCount(1, lockers);
        Assert.AreEqual("old", temp.Marker("2024"));
        Assert.IsTrue(Directory.Exists(staged));
    }

    [TestMethod]
    public void Free_payload_swaps_keeps_one_rollback_and_rolls_back()
    {
        using var temp = new TempBundle();
        var installer = Installer(temp, new FakeFolderLock());
        temp.WritePayload("2024", "v1");

        Assert.IsEmpty(installer.TryApply("2024", Stage(temp, "v2")));
        Assert.IsEmpty(installer.TryApply("2024", Stage(temp, "v3")));

        Assert.AreEqual("v3", temp.Marker("2024"));
        Assert.HasCount(1, Directory.GetDirectories(temp.Bundle.Rollback));
        Assert.IsTrue(installer.TryRollback("2024"));
        Assert.AreEqual("v2", temp.Marker("2024"));
    }

    [TestMethod]
    public async Task Staging_refuses_a_zip_that_does_not_match_the_manifest()
    {
        using var temp = new TempBundle();
        var releases = new FakeReleases();
        var release = releases.Publish("3.1.0", null, ("2024", ["Revit"], "new"));
        releases.Replace(release, "RevitDevTool-3.1.0-2024.zip", FakeReleases.PayloadZip("changed"));
        var reader = new ReleaseReader(new HttpClient(releases), new ReleaseSignature(null));
        var installer = new PayloadInstaller(temp.Bundle, new FakeFolderLock(), reader);
        var manifest = await reader.ReadManifestAsync(release, CancellationToken.None);

        await Assert.ThrowsExactlyAsync<UpdateIntegrityException>(() => installer.StageAsync(release, manifest.FindPayload("2024")!, null, CancellationToken.None));
        Assert.IsFalse(installer.IsStaged("3.1.0", "2024"));
    }

    [TestMethod]
    public void Offline_bundle_stages_one_payload_and_gives_the_release_version()
    {
        using var temp = new TempBundle();
        var zip = Path.Combine(temp.Root, "RevitDevTool.bundle.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            Write(archive, "RevitDevTool.bundle/PackageContents.xml", """<ApplicationPackage AppVersion="3.1.0"><CompanyDetails /></ApplicationPackage>""");
            Write(archive, "RevitDevTool.bundle/Contents/2024/marker.txt", "offline");
            Write(archive, "RevitDevTool.bundle/Contents/2025/marker.txt", "other");
        }

        var staged = Installer(temp, new FakeFolderLock()).StageFromBundle(zip, "2024", "3.1.0");

        Assert.AreEqual("3.1.0", PayloadInstaller.ReadBundleVersion(zip));
        Assert.AreEqual("offline", File.ReadAllText(Path.Combine(staged, "marker.txt")));
        Assert.HasCount(1, Directory.GetFiles(staged));
    }

    [TestMethod]
    public void Restart_manager_reports_this_process_for_an_open_dll()
    {
        using var temp = new TempBundle();
        var folder = temp.WritePayload("2024", "x", "Locked.dll");
        var folderLock = new RestartManagerFolderLock();

        Assert.IsEmpty(folderLock.GetLockers(folder));
        using (new FileStream(Path.Combine(folder, "Locked.dll"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            CollectionAssert.Contains(folderLock.GetLockers(folder).Select(locker => locker.ProcessId).ToArray(), Environment.ProcessId);
        }
    }

    private static PayloadInstaller Installer(TempBundle temp, IFolderLock folderLock) =>
        new(temp.Bundle, folderLock, new ReleaseReader(new HttpClient(new FakeReleases()), new ReleaseSignature(null)));

    private static string Stage(TempBundle temp, string marker)
    {
        var path = Path.Combine(temp.Bundle.StagingPath(marker), "2024");
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
