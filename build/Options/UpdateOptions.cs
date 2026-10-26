namespace Build.Options;

/// <summary>
///     Auto-update release artifacts: year payloads, year manifest, Velopack Daemon package.
/// </summary>
/// <seealso href="../../docs/decisions/0046-per-year-release-payloads-and-update-manifest.md">0046</seealso>
/// <seealso href="../../docs/decisions/0053-velopack-for-daemon-distribution.md">0053</seealso>
[Serializable]
public sealed record UpdateOptions
{
    /// <summary>
    ///     Velopack package id. Also the folder name under <c>%LOCALAPPDATA%</c>.
    /// </summary>
    public string PackId { get; init; } = "RevitDevTool";

    /// <summary>
    ///     Oldest control/MCP pipe contract generation the Daemon of this release serves.
    /// </summary>
    public int IpcCompat { get; init; } = 1;

    /// <summary>
    ///     Daemon version that years of this release need. Defaults to the release version.
    /// </summary>
    public string? MinUpdaterVersion { get; init; }

    /// <summary>
    ///     PEM-encoded ECDSA P-256 private key that signs the update feeds.
    /// </summary>
    /// <remarks>Set from the <c>Update__SigningKey</c> secret. Signing is skipped when empty.</remarks>
    public string? SigningKey { get; init; }

    /// <summary>
    ///     Marks a release build: fail when <see cref="SigningKey"/> is empty, require a newer version, and build a delta.
    ///     <c>PublishRelease.yml</c> sets it.
    /// </summary>
    public bool RequireSignature { get; init; }
}
