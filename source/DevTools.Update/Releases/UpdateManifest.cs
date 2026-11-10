using DevTools.Update.Autodesk;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevTools.Update.Releases;

/// <summary>
///     <c>update-manifest.json</c> of one release: the payloads it contains (0046).
/// </summary>
public sealed record UpdateManifest
{
    public const string FileName = "update-manifest.json";
    public const int SupportedSchema = 2;

    public int Schema { get; init; }
    public string Version { get; init; } = "";
    public string Channel { get; init; } = "";
    public DateTimeOffset PublishedAt { get; init; }
    public string? NotesUrl { get; init; }
    public string? MinUpdaterVersion { get; init; }
    public int IpcCompat { get; init; }
    public IReadOnlyList<ManifestPayload> Payloads { get; init; } = [];

    public ManifestPayload? FindPayload(AddinTarget target) => Payloads.FirstOrDefault(payload => payload.Target == target);

    public byte[] ToJson() => JsonSerializer.SerializeToUtf8Bytes(this, UpdateJsonContext.Default.UpdateManifest);

    public static UpdateManifest FromJson(byte[] json)
    {
        var manifest = JsonSerializer.Deserialize(json, UpdateJsonContext.Default.UpdateManifest)!;
        if (manifest.Schema != SupportedSchema)
            throw new UpdateIntegrityException($"Update manifest schema {manifest.Schema} is not supported.");

        return manifest;
    }
}

/// <summary>
///     One payload zip: the <c>Contents\&lt;host version&gt;</c> folder of the bundle of <see cref="Platform"/>.
/// </summary>
public sealed record ManifestPayload
{
    public string Platform { get; init; } = "";
    public string HostVersion { get; init; } = "";

    [JsonIgnore]
    public AddinTarget Target => new(Platform, HostVersion);

    /// <summary>
    ///     <c>RevitDevTool-3.1.0-2025.zip</c>, <c>AcadDevTool-3.1.0-2025.zip</c>: the bundle name, the release version, the host version.
    /// </summary>
    public static string AssetName(AddinTarget target, string version) =>
        $"{Path.GetFileNameWithoutExtension(PackageContents.BundleOf(target))}-{version}-{target.HostVersion}.zip";

    public string Asset { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public long Size { get; init; }
}
