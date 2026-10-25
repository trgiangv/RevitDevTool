namespace DevTools.Update;

/// <summary>
///     Configuration of the update engine (<c>Update</c> section of the Daemon <c>appsettings.json</c>).
/// </summary>
public sealed class UpdateEngineOptions
{
    public const string SectionName = "Update";

    /// <summary>
    ///     GitHub <c>owner/name</c> of the release provider.
    /// </summary>
    public string Repository { get; set; } = "trgiangv/RevitDevTool";

    /// <summary>
    ///     Optional GitHub token (<c>Update__GitHubToken</c>). CI runners share IP addresses, so the 60 requests each hour
    ///     without a token are not enough there.
    /// </summary>
    public string? GitHubToken { get; set; }

    /// <summary>
    ///     PEM ECDSA P-256 public key for the <c>.sig</c> files (0051). If it is empty, the engine accepts feeds without a signature.
    /// </summary>
    public string? PublicKey { get; set; }

    /// <summary>
    ///     Bundle root. The default is <c>%APPDATA%\Autodesk\ApplicationPlugins\RevitDevTool.bundle</c>.
    /// </summary>
    public string? BundlePath { get; set; }

    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromMinutes(2);
}
