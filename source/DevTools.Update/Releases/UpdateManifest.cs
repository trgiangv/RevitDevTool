using System.Text.Json;

namespace DevTools.Update.Releases;

/// <summary>
///     <c>update-manifest.json</c> of one release: the payloads it contains (0046).
/// </summary>
public sealed record UpdateManifest
{
    public const string FileName = "update-manifest.json";
    public const int SupportedSchema = 1;

    public int Schema { get; init; }
    public string Version { get; init; } = "";
    public string Channel { get; init; } = "";
    public DateTimeOffset PublishedAt { get; init; }
    public string? NotesUrl { get; init; }
    public string? MinUpdaterVersion { get; init; }
    public int IpcCompat { get; init; }
    public IReadOnlyList<ManifestPayload> Payloads { get; init; } = [];

    public ManifestPayload? FindPayload(string hostVersion) => Payloads.FirstOrDefault(payload => payload.HostVersion == hostVersion);

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
///     One payload zip: the <c>Contents\&lt;host version&gt;</c> folder for all <see cref="Platforms"/> of that host version.
/// </summary>
public sealed record ManifestPayload
{
    public string HostVersion { get; init; } = "";
    public IReadOnlyList<string> Platforms { get; init; } = [];
    public string Asset { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public long Size { get; init; }
}
