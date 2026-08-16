namespace DevTools.Hosting;

public sealed record HostLaunchRequest(
    HostApp HostApp,
    string Version,
    string? FilePath,
    IReadOnlyDictionary<string, string>? Options)
{
    public const string LanguageOptionKey = "language";
    public const string DefaultLanguageCulture = "en-US";

    public string LanguageCulture
    {
        get
        {
            if (Options is not null
                && Options.TryGetValue(LanguageOptionKey, out var value)
                && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }

            return DefaultLanguageCulture;
        }
    }
}
