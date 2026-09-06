using DevTools.Hosting;

namespace DevTools.Execution.Providers;

/// <summary>
/// Host-year preprocessor symbols for .csx (Roslyn) and .fsx (FSI <c>--define</c>).
/// Uses <see cref="IHostAppInfo.Host"/> and <see cref="IHostAppInfo.VersionNumber"/>
/// (same market year as the running host). Ladder from <see cref="VersionMinimal"/>
/// matches <c>RevitVersionMinimal</c> / <c>AutoCadVersionMinimal</c> in props.
/// </summary>
public static class CompileScriptSymbols
{
    public const int VersionMinimal = 2022;

    private static readonly string[] BaseSymbols = ["TRACE", "DEBUG"];

    public static IReadOnlyList<string> For(IHostAppInfo? hostApp)
    {
        if (hostApp is null)
            return BaseSymbols;

        var symbols = new List<string>(BaseSymbols);
        var prefix = FamilyPrefix(hostApp.Host);
        var version = hostApp.VersionNumber;
        if (prefix is null || !int.TryParse(version, out var year))
            return symbols;

        for (var y = VersionMinimal; y <= year; y++)
            symbols.Add($"{prefix}{y}_OR_GREATER");

        symbols.Add($"{prefix}{version}");
        symbols.Add(prefix);

        return symbols;
    }

    private static string? FamilyPrefix(HostApp host) => host switch
    {
        HostApp.Revit => "REVIT",
        _ when host.IsAcadFamily() => "AUTOCAD",
        _ => null,
    };
}
