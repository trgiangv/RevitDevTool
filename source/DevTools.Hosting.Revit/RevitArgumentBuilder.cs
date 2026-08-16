namespace DevTools.Hosting.Revit;

public sealed class RevitArgumentBuilder : IHostArgumentBuilder
{
    private static readonly Dictionary<string, string> CultureToRevitLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en-US"] = "ENU",
        ["en-GB"] = "ENG",
        ["fr-FR"] = "FRA",
        ["de-DE"] = "DEU",
        ["it-IT"] = "ITA",
        ["ja-JP"] = "JPN",
        ["ko-KR"] = "KOR",
        ["pl-PL"] = "PLK",
        ["es-ES"] = "ESP",
        ["zh-CN"] = "CHS",
        ["zh-TW"] = "CHT",
        ["pt-BR"] = "PTB",
        ["ru-RU"] = "RUS",
        ["cs-CZ"] = "CSY",
        ["hu-HU"] = "HUN",
    };

    public bool Supports(HostApp hostApp) => hostApp == HostApp.Revit;

    public IReadOnlyList<string> Build(HostLaunchRequest request, string executablePath)
    {
        if (!Supports(request.HostApp))
            throw new InvalidOperationException($"Launch not yet supported for {request.HostApp}.");

        var mapped = MapLanguage(request.LanguageCulture);
        var arguments = new List<string> { "/language", mapped };
        if (!string.IsNullOrWhiteSpace(request.FilePath))
            arguments.Add(request.FilePath!);

        return arguments;
    }

    public static string MapLanguage(string culture)
    {
        if (CultureToRevitLanguage.TryGetValue(culture, out var mapped))
            return mapped;

        throw new InvalidOperationException(
            $"Unsupported language culture '{culture}'. Use a .NET culture name such as en-US.");
    }
}
