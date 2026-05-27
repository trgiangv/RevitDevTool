using System.IO;
using System.Text.RegularExpressions;

namespace DevTools.Execution.Providers.CSharp;

/// <summary>
/// Parses #r directives from .csx scripts into categorized results:
/// NuGet packages, file references (with host version rewriting), and ignored runtime refs.
/// </summary>
// ReSharper disable once PartialTypeWithSinglePart
internal static partial class CSharpDirectiveParser
{
    private const string DirectivePattern = """^\s*#r\s+"(?<ref>[^"]+)"\s*$""";
    private const string NugetPrefix = "nuget:";

#if NETFRAMEWORK
    private static readonly Regex DirectiveRx = new(DirectivePattern, RegexOptions.Compiled);
    private static Regex DirectiveRegex() => DirectiveRx;
#else
    [GeneratedRegex(DirectivePattern)]
    private static partial Regex DirectiveRegex();
#endif

    private static readonly string[] IgnoredPathSegments =
    [
        @"\dotnet\packs\",
        @"\dotnet\shared\",
        @"\Reference Assemblies\Microsoft\Framework\"
    ];

    public static ParsedDirectives Parse(string source, string? hostPattern, string? hostReplacement)
    {
        var packages = new List<PackageReference>();
        var fileReferences = new List<string>();
        var strippedLines = new List<int>();

        var hostRegex = hostPattern is not null
            ? new Regex(hostPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)
            : null;

        var lines = source.Split(['\r', '\n'], StringSplitOptions.None);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = DirectiveRegex().Match(line);
            if (!match.Success)
                continue;

            strippedLines.Add(i);
            var reference = match.Groups["ref"].Value.Trim();

            if (IsNugetReference(reference, out var packageId, out var version))
            {
                packages.Add(new PackageReference(packageId, version));
                continue;
            }

            if (IsIgnoredRuntimeReference(reference))
                continue;

            var resolvedPath = RewriteHostVersion(reference, hostRegex, hostReplacement);
            if (File.Exists(resolvedPath))
                fileReferences.Add(resolvedPath);
        }

        var cleanSource = BuildCleanSource(lines, strippedLines);
        return new ParsedDirectives(cleanSource, packages, fileReferences);
    }

    private static bool IsNugetReference(string reference, out string packageId, out string? version)
    {
        packageId = string.Empty;
        version = null;

        if (!reference.StartsWith(NugetPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var value = reference[NugetPrefix.Length..].Trim();
        var commaIdx = value.IndexOf(',');
        if (commaIdx < 0)
        {
            packageId = value.Trim();
        }
        else
        {
            packageId = value[..commaIdx].Trim();
            version = value[(commaIdx + 1)..].Trim();
        }

        return !string.IsNullOrEmpty(packageId);
    }

    private static bool IsIgnoredRuntimeReference(string reference)
    {
        var normalized = reference.Replace('/', '\\');
        foreach (var segment in IgnoredPathSegments)
        {
            if (normalized.IndexOf(segment, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static string RewriteHostVersion(string reference, Regex? hostRegex, string? replacement)
    {
        if (hostRegex is null || replacement is null)
            return reference;

        return hostRegex.Replace(reference, replacement);
    }

    private static string BuildCleanSource(string[] lines, List<int> strippedLines)
    {
        if (strippedLines.Count == 0)
            return string.Join("\n", lines);

        var stripped = new HashSet<int>(strippedLines);
        var result = new List<string>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            if (!stripped.Contains(i))
                result.Add(lines[i]);
        }
        return string.Join("\n", result);
    }
}

internal readonly record struct PackageReference(string PackageId, string? Version);

internal sealed class ParsedDirectives(
    string cleanSource,
    List<PackageReference> packages,
    List<string> fileReferences)
{
    public string CleanSource { get; } = cleanSource;
    public IReadOnlyList<PackageReference> Packages { get; } = packages;
    public IReadOnlyList<string> FileReferences { get; } = fileReferences;
}
