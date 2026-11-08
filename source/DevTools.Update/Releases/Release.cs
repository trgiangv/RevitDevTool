namespace DevTools.Update.Releases;

/// <summary>
///     One published release: the download folder that holds its assets. <see cref="ChannelIndex"/> names it.
/// </summary>
public sealed record Release(string Version, bool Prerelease, Uri BaseUrl)
{
    public Uri Asset(string name) => new(BaseUrl, Uri.EscapeDataString(name));
}
