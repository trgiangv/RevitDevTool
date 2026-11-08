using System.Text.Json;

namespace DevTools.Update.Releases;

/// <summary>
///     <c>channels.json</c>: the newest <c>daemon</c> and <c>addin</c> release of each update channel (0059 §2).
///     The <see cref="Prerelease"/> side already names the newest of stable and prerelease.
/// </summary>
public sealed record ChannelIndex
{
    public const string FileName = "channels.json";
    public const int SupportedSchema = 1;

    public int Schema { get; init; } = SupportedSchema;
    public Channel Stable { get; init; } = new();
    public Channel Prerelease { get; init; } = new();

    public (Release? Daemon, Release? Addin) Pick(bool prerelease)
    {
        var channel = prerelease ? Prerelease : Stable;
        return (channel.Daemon?.ToRelease(), channel.Addin?.ToRelease());
    }

    public byte[] ToJson() => JsonSerializer.SerializeToUtf8Bytes(this, UpdateJsonContext.Default.ChannelIndex);

    public static ChannelIndex FromJson(byte[] json)
    {
        var index = JsonSerializer.Deserialize(json, UpdateJsonContext.Default.ChannelIndex)!;
        if (index.Schema != SupportedSchema)
            throw new UpdateIntegrityException($"Channel index schema {index.Schema} is not supported.");

        return index;
    }
}

public sealed record Channel
{
    public DaemonEntry? Daemon { get; init; }
    public AddinEntry? Addin { get; init; }
}

/// <summary>
///     <see cref="Feed"/> is the download folder of the <c>daemon</c> release, ending with <c>/</c>.
/// </summary>
public sealed record DaemonEntry(string Version, Uri Feed)
{
    public Release ToRelease() => new(Version, IsPrerelease(Version), Feed);

    internal static bool IsPrerelease(string version) => version.Contains('-', StringComparison.Ordinal);
}

/// <summary>
///     <see cref="Manifest"/> is the full URL of the <c>update-manifest.json</c> of the <c>addin</c> release.
/// </summary>
public sealed record AddinEntry(string Version, Uri Manifest)
{
    public Release ToRelease() => new(Version, DaemonEntry.IsPrerelease(Version), new Uri(Manifest, "."));
}

/// <summary>
///     Reads the channel index. The Daemon reads <see cref="UpdateEngineOptions.ChannelIndexUrl"/> through <see cref="ChannelIndexSource"/>.
/// </summary>
public interface IChannelIndexSource
{
    Task<ChannelIndex> GetAsync(CancellationToken cancellationToken);
}

/// <summary>
///     One request for <c>channels.json</c> and one for its signature. No GitHub API request.
/// </summary>
public sealed class ChannelIndexSource(ReleaseReader reader, Uri url) : IChannelIndexSource
{
    public async Task<ChannelIndex> GetAsync(CancellationToken cancellationToken) =>
        ChannelIndex.FromJson(await reader.ReadSignedAsync(url, cancellationToken).ConfigureAwait(false));
}
