using System.Text.RegularExpressions;
namespace RevitDevTool.CodeExecute.Providers.Python;

/// <summary>
/// Parser for PEP 723 Inline Script Metadata. (https://peps.python.org/pep-0723/)
/// Extracts dependency information from Python scripts.
/// </summary>
// ReSharper disable once PartialTypeWithSinglePart
public static partial class Pep723Parser
{
    private const string ScriptBlockPattern = @"^[ \t]*# /// script\s*$(.+?)^[ \t]*# ///\s*$";
    private const string DependenciesPattern = @"dependencies\s*=\s*\[(.*?)\]";
    private const string PackagePattern = """["']([^"']+)["']""";

#if NETCOREAPP
    [GeneratedRegex(ScriptBlockPattern, RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex ScriptBlockRegex();

    [GeneratedRegex(DependenciesPattern, RegexOptions.Singleline)]
    private static partial Regex DependenciesRegex();

    [GeneratedRegex(PackagePattern)]
    private static partial Regex PackageRegex();
#else
    private static Regex ScriptBlockRegex() => new(ScriptBlockPattern, RegexOptions.Multiline | RegexOptions.Singleline);
    private static Regex DependenciesRegex() => new(DependenciesPattern, RegexOptions.Singleline);
    private static Regex PackageRegex() => new(PackagePattern);
#endif

    /// <summary>
    /// Parses the given Python script content for PEP 723 metadata and returns a list of dependencies.
    /// </summary>
    /// <param name="scriptContent">The full content of the python script.</param>
    /// <returns>A list of dependency specifiers (e.g., "pandas>=1.0"). Returns empty list if no block found.</returns>
    public static List<string> ParseDependencies(string scriptContent)
    {
        var tomlContent = ExtractTomlBlockContent(scriptContent);
        
        return string.IsNullOrEmpty(tomlContent) ? [] : ParseDependenciesFromToml(tomlContent);
    }

    /// <summary>
    /// Extracts the TOML content from a PEP 723 script metadata block.
    /// </summary>
    /// <param name="scriptContent">The full content of the python script.</param>
    /// <returns>The TOML content without comment prefixes, or null if no block found.</returns>
    private static string? ExtractTomlBlockContent(string scriptContent)
    {
        var blockMatch = ScriptBlockRegex().Match(scriptContent);

        if (!blockMatch.Success)
        {
            return null;
        }

        var blockContent = blockMatch.Groups[1].Value;
        
        // Process each line: remove comment prefix
        var tomlLines = blockContent
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimStart())
            .Where(line => line.StartsWith("#"))
            .Select(line => line[1..].TrimStart());

        var result = string.Join(Environment.NewLine, tomlLines);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    /// <summary>
    /// Parses dependencies from TOML content.
    /// </summary>
    /// <param name="toml">The TOML content to parse.</param>
    /// <returns>A list of dependency specifiers.</returns>
    private static List<string> ParseDependenciesFromToml(string? toml)
    {
        if (string.IsNullOrEmpty(toml))
        {
            return [];
        }
        var match = DependenciesRegex().Match(toml);

        if (!match.Success)
        {
            return [];
        }

        var listContent = match.Groups[1].Value;
        var pkgMatches = PackageRegex().Matches(listContent);

        return pkgMatches
#if NETFRAMEWORK
            .Cast<Match>()
#endif
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .ToList();
    }
}
