using DevTools.Hosting;
using DevTools.Update.Apply;
using DevTools.Update.Autodesk;
using DevTools.Update.Engine;
using DevTools.Update.Releases;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace DevTools.Update.Tests;

internal sealed class TempBundle : IDisposable
{
    public TempBundle()
    {
        Root = Path.Combine(Path.GetTempPath(), "DevTools.Update.Tests", Guid.NewGuid().ToString("N"), AutodeskBundle.BundleName);
        Directory.CreateDirectory(Root);
        Bundle = new AutodeskBundle(Root);
    }

    public string Root { get; }
    public AutodeskBundle Bundle { get; }

    public string WritePayload(string hostVersion, string marker, params string[] files)
    {
        var folder = Bundle.PayloadPath(hostVersion);
        Directory.CreateDirectory(folder);
        foreach (var file in files.Append("marker.txt")) File.WriteAllText(Path.Combine(folder, file), marker);
        return folder;
    }

    public string Marker(string hostVersion) => File.ReadAllText(Path.Combine(Bundle.PayloadPath(hostVersion), "marker.txt"));

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(Root)!, recursive: true);
}

/// <summary>
///     Locks a folder with this test process as the locker, so a lock watcher waits until the test ends.
/// </summary>
internal sealed class FakeFolderLock : IFolderLock
{
    private readonly HashSet<string> _locked = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<LockingProcess> GetLockers(string folder) =>
        _locked.Contains(Path.GetFullPath(folder)) ? [new LockingProcess(Environment.ProcessId, "Revit.exe")] : [];

    public void Lock(string folder) => _locked.Add(Path.GetFullPath(folder));

    public void Unlock(string folder) => _locked.Remove(Path.GetFullPath(folder));
}

/// <summary>
///     An in-memory release provider and the HTTP server for its assets.
/// </summary>
internal sealed class FakeReleases : HttpMessageHandler, IReleaseProvider
{
    private readonly Dictionary<Uri, byte[]> _files = [];
    private readonly List<Release> _releases = [];

    public int Requests { get; private set; }

    public Release Publish(string version, ECDsa? key, params (string HostVersion, string[] Platforms, string Marker)[] payloads)
    {
        var assets = new Dictionary<string, Uri>(StringComparer.Ordinal);
        var entries = payloads.Select(payload =>
        {
            var asset = $"RevitDevTool-{version}-{payload.HostVersion}.zip";
            var zip = PayloadZip(payload.Marker);
            Add(assets, version, asset, zip);
            return new ManifestPayload { HostVersion = payload.HostVersion, Platforms = payload.Platforms, Asset = asset, Sha256 = Convert.ToHexStringLower(SHA256.HashData(zip)), Size = zip.Length };
        }).ToArray();

        var manifest = new UpdateManifest { Schema = UpdateManifest.SupportedSchema, Version = version, Channel = "stable", Payloads = entries }.ToJson();
        Add(assets, version, UpdateManifest.FileName, manifest);
        if (key is not null) Add(assets, version, UpdateManifest.FileName + ReleaseSignature.Extension, Encoding.ASCII.GetBytes(Convert.ToBase64String(key.SignData(manifest, HashAlgorithmName.SHA256))));

        var release = new Release(version, version.Contains('-', StringComparison.Ordinal), assets);
        _releases.Add(release);
        return release;
    }

    public void PublishWithoutManifest(string version)
    {
        var assets = new Dictionary<string, Uri>(StringComparer.Ordinal);
        Add(assets, version, "RevitDevTool-Setup.exe", [0]);
        _releases.Add(new Release(version, false, assets));
    }

    public void Replace(Release release, string asset, byte[] content) => _files[release.Asset(asset)] = content;

    public Task<Release?> GetLatestAsync(bool prerelease, CancellationToken cancellationToken)
    {
        Requests++;
        return Task.FromResult(_releases.LastOrDefault(release => prerelease || !release.Prerelease));
    }

    public Task<Release?> GetAsync(string version, CancellationToken cancellationToken) => Task.FromResult(_releases.Find(release => release.Version == version));

    public static byte[] PayloadZip(string marker, params string[] entryNames)
    {
        if (entryNames.Length == 0) entryNames = ["marker.txt", "RevitDevTool.addin", "AcadDevTool.dll"];

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in entryNames)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open());
                writer.Write(marker);
            }
        }

        return stream.ToArray();
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(_files.TryGetValue(request.RequestUri!, out var content)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));

    private void Add(Dictionary<string, Uri> assets, string version, string name, byte[] content)
    {
        var uri = new Uri($"https://releases.example.test/{version}/{name}");
        assets[name] = uri;
        _files[uri] = content;
    }
}

internal sealed class FixedResolver(params (HostApp App, string Version)[] installed) : IHostPathResolver
{
    public bool Supports(HostApp hostApp) => installed.Any(item => item.App == hostApp);

    public string? FindExecutable(HostApp hostApp, string version) => null;

    public IReadOnlyList<string> GetInstalledVersions(HostApp hostApp) =>
        installed.Where(item => item.App == hostApp).Select(item => item.Version).ToArray();
}

/// <summary>
///     A Daemon that Velopack installed at <paramref name="version"/> from <paramref name="channel"/>.
/// </summary>
internal sealed class InstalledDaemon(ReleaseReader reader, string version, string channel) : DaemonUpdater(reader)
{
    public override string? InstalledVersion => version;
    public override string InstalledChannel => channel;
    public override Task<string?> PrepareAsync(Release release, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
}

internal sealed class RecordingNotifier : IUpdateNotifier
{
    public List<UpdateNotice> Notices { get; } = [];

    public void Notify(UpdateNotice notice) => Notices.Add(notice);

    public void RequestShutdownForUpdate()
    {
    }
}
