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

internal static class Hosts
{
    public static AddinTarget Revit(string hostVersion) => new(PackageContents.RevitPlatform, hostVersion);

    public static AddinTarget Acad(string hostVersion) => new(PackageContents.AutoCadPlatform, hostVersion);
}

/// <summary>
///     A temporary <c>ApplicationPlugins</c> folder with <c>installation.json</c> beside it.
/// </summary>
internal sealed class TempPlugins : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "DevTools.Update.Tests", Guid.NewGuid().ToString("N"));

    public TempPlugins()
    {
        Bundles = new AutodeskBundles(Path.Combine(_folder, "ApplicationPlugins"));
        Directory.CreateDirectory(Bundles.Root);
    }

    public AutodeskBundles Bundles { get; }
    public string Root => Bundles.Root;
    public string InstallationPath => Path.Combine(_folder, "installation.json");
    public AutodeskBundle Revit => Bundles.Named("RevitDevTool.bundle");
    public AutodeskBundle Acad => Bundles.Named("AcadDevTool.bundle");

    public string WritePayload(AddinTarget target, string marker, params string[] files)
    {
        var folder = Bundles.PayloadPath(target);
        Directory.CreateDirectory(folder);
        foreach (var file in files.Append("marker.txt")) File.WriteAllText(Path.Combine(folder, file), marker);
        return folder;
    }

    public string Marker(AddinTarget target) => File.ReadAllText(Path.Combine(Bundles.PayloadPath(target), "marker.txt"));

    public void Dispose() => Directory.Delete(_folder, recursive: true);
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
///     The rolling <c>channels</c> release and the <c>daemon</c>/<c>addin</c> release folders, served from memory.
///     Each publish moves the index the way <c>build -- channels</c> does. The key signs the index and the manifests.
/// </summary>
internal sealed class FakeChannels : HttpMessageHandler
{
    public static readonly Uri IndexUrl = new("https://releases.example.test/channels/channels.json");
    private static readonly Uri Downloads = new("https://releases.example.test/");

    private readonly ECDsa? _key;
    private readonly Dictionary<Uri, byte[]> _files = [];

    public FakeChannels(ECDsa? key = null)
    {
        _key = key;
        ServeIndex(Index.ToJson());
    }

    public ChannelIndex Index { get; private set; } = new();

    public int IndexRequests => _indexRequests;

    private int _indexRequests;

    public string? MinUpdaterVersion { get; set; }

    public Release PublishAddin(string version, params (AddinTarget Target, string Marker)[] payloads)
    {
        var release = new Release(version, version.Contains('-', StringComparison.Ordinal), new Uri(Downloads, $"addin/v{version}/"));
        var entries = payloads.Select(payload =>
        {
            var asset = ManifestPayload.AssetName(payload.Target, version);
            var zip = PayloadZip(payload.Marker);
            _files[release.Asset(asset)] = zip;
            return new ManifestPayload
            {
                Platform = payload.Target.Platform,
                HostVersion = payload.Target.HostVersion,
                Asset = asset,
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(zip)),
                Size = zip.Length
            };
        }).ToArray();

        var manifest = new UpdateManifest
        {
            Schema = UpdateManifest.SupportedSchema,
            Version = version,
            Channel = "stable",
            MinUpdaterVersion = MinUpdaterVersion,
            Payloads = entries
        };
        AddSigned(release.Asset(UpdateManifest.FileName), manifest.ToJson());
        Move(version, channel => channel.Addin?.Version, channel => channel with { Addin = new AddinEntry(version, release.Asset(UpdateManifest.FileName)) });
        return release;
    }

    public Release PublishDaemon(string version)
    {
        var release = new Release(version, version.Contains('-', StringComparison.Ordinal), new Uri(Downloads, $"daemon/v{version}/"));
        _files[release.Asset("RevitDevTool-Setup.exe")] = [0];
        Move(version, channel => channel.Daemon?.Version, channel => channel with { Daemon = new DaemonEntry(version, release.BaseUrl) });
        return release;
    }

    public void ServeIndex(byte[] json) => AddSigned(IndexUrl, json);

    public void Add(Uri uri, byte[] content) => _files[uri] = content;

    private void Move(string version, Func<Channel, string?> current, Func<Channel, Channel> set)
    {
        var stable = !version.Contains('-', StringComparison.Ordinal);
        var lift = !stable || current(Index.Prerelease) is not { } open || Versions.IsNewer(version, open);
        Index = Index with
        {
            Stable = stable ? set(Index.Stable) : Index.Stable,
            Prerelease = lift ? set(Index.Prerelease) : Index.Prerelease
        };
        ServeIndex(Index.ToJson());
    }

    private void AddSigned(Uri uri, byte[] content)
    {
        _files[uri] = content;
        if (_key is not null)
            _files[new Uri(uri.AbsoluteUri + ReleaseSignature.Extension)] = Encoding.ASCII.GetBytes(Convert.ToBase64String(_key.SignData(content, HashAlgorithmName.SHA256)));
    }

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

    public int PayloadDownloads => _payloadDownloads;

    private int _payloadDownloads;

    public int FileRequests => _fileRequests;

    private int _fileRequests;

    public int DroppedResponses { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _fileRequests);
        if (request.RequestUri!.AbsolutePath.EndsWith(".zip", StringComparison.Ordinal)) Interlocked.Increment(ref _payloadDownloads);
        if (request.RequestUri == IndexUrl) Interlocked.Increment(ref _indexRequests);
        if (!_files.TryGetValue(request.RequestUri, out var content)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        HttpContent body = DroppedResponses-- > 0 ? new StreamContent(new DroppedStream()) : new ByteArrayContent(content);
        body.Headers.ContentLength = content.Length;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = body });
    }

    private sealed class DroppedStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("An existing connection was forcibly closed by the remote host.");

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new IOException("An existing connection was forcibly closed by the remote host.");
    }
}

internal sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
{
    private readonly Lock _lock = new();

    public void Report(T value)
    {
        lock (_lock) handler(value);
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

/// <summary>
///     An installed Daemon that has an update ready. It records how Velopack must start it again.
/// </summary>
internal sealed class UpdatableDaemon(ReleaseReader reader) : DaemonUpdater(reader)
{
    public List<bool> Restarts { get; } = [];
    public List<string> Prepared { get; } = [];
    public override string? InstalledVersion => "3.0.0";
    public override string InstalledChannel => Installations.UpdatePolicy.StableChannel;

    public override Task<string?> PrepareAsync(Release release, CancellationToken cancellationToken)
    {
        Prepared.Add(release.Version);
        return Task.FromResult<string?>(release.Version);
    }

    public override IReadOnlyList<int> GetBlockingProcesses() => [];
    public override void ApplyOnExit(bool showUpdates) => Restarts.Add(showUpdates);
}

internal sealed class RecordingNotifier : IUpdateNotifier
{
    public List<UpdateNotice> Notices { get; } = [];

    public void Notify(UpdateNotice notice) => Notices.Add(notice);

    public void RequestShutdownForUpdate()
    {
    }
}
