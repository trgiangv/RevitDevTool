namespace DevTools.Update.Releases;

/// <summary>
///     One published release and the download address of each asset. An <see cref="IReleaseProvider"/> supplies it.
/// </summary>
public sealed record Release(string Version, bool Prerelease, IReadOnlyDictionary<string, Uri> Assets)
{
    /// <summary>
    ///     False for a release that the update pipeline did not publish (no <c>update-manifest.json</c>).
    /// </summary>
    public bool HasManifest => Assets.ContainsKey(UpdateManifest.FileName);

    public Uri Asset(string name) => Assets.TryGetValue(name, out var uri)
        ? uri
        : throw new UpdateIntegrityException($"Release {Version} has no asset {name}.");
}

/// <summary>
///     A source of releases: GitHub Releases, or a private deploy server.
/// </summary>
public interface IReleaseProvider
{
    Task<Release?> GetLatestAsync(bool prerelease, CancellationToken cancellationToken);

    Task<Release?> GetAsync(string version, CancellationToken cancellationToken);
}

/// <summary>
///     The provider refused the request because of a rate limit. The scheduler waits for <see cref="RetryAfter"/> (0048).
/// </summary>
public sealed class UpdateThrottledException(TimeSpan? retryAfter, Exception inner) : Exception("The release provider rate limit is reached.", inner)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
