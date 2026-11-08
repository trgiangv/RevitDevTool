namespace DevTools.Update;

/// <summary>
///     Configuration of the update engine (<c>Update</c> section of the Daemon <c>appsettings.json</c>).
/// </summary>
public sealed class UpdateEngineOptions
{
    public const string SectionName = "Update";

    /// <summary>
    ///     The signed <c>channels.json</c>, the asset of the rolling <c>channels</c> GitHub Release (0059 §2).
    /// </summary>
    public Uri ChannelIndexUrl { get; set; } = new("https://github.com/trgiangv/RevitDevTool/releases/download/channels/channels.json");

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
