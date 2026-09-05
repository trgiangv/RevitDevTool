using System.IO;
using System.Text.RegularExpressions;

namespace DevTools.Execution.Providers.CSharp;

/// <summary>
/// Parses #r and #load directives from .csx scripts recursively.
/// Each .csx in the graph can declare its own #r and #load directives,
/// mirroring F# scripting where each .fsx manages its own dependencies.
/// </summary>
// ReSharper disable once PartialTypeWithSinglePart
internal static partial class CSharpDirectiveParser
{
    private const string ReferenceDirectivePattern = """^\s*#r\s+"(?<ref>[^"]+)"\s*$""";
    private const string LoadDirectivePattern = """^\s*#load\s+"(?<path>[^"]+)"\s*$""";
    private const string NugetPrefix = "nuget:";

#if NETFRAMEWORK
    private static readonly Regex ReferenceDirectiveRx = new(ReferenceDirectivePattern, RegexOptions.Compiled);
    private static readonly Regex LoadDirectiveRx = new(LoadDirectivePattern, RegexOptions.Compiled);
    private static Regex ReferenceDirectiveRegex() => ReferenceDirectiveRx;
    private static Regex LoadDirectiveRegex() => LoadDirectiveRx;
#else
    [GeneratedRegex(ReferenceDirectivePattern)]
    private static partial Regex ReferenceDirectiveRegex();

    [GeneratedRegex(LoadDirectivePattern)]
    private static partial Regex LoadDirectiveRegex();
#endif

    private static readonly string[] IgnoredPathSegments =
    [
        @"\dotnet\packs\",
        @"\dotnet\shared\",
        @"\Reference Assemblies\Microsoft\Framework\"
    ];

    /// <summary>
    /// Recursively resolves the entire script graph starting from the entry file.
    /// Returns a flattened result containing all source files (topologically ordered,
    /// dependencies before dependents) and merged references from the entire graph.
    /// </summary>
    public static ScriptGraph ResolveGraph(string entryPath, Func<string, string>? rewriteHostReference = null)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceFiles = new List<SourceFileEntry>();
        var allPackages = new List<PackageReference>();
        var allAssemblyRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rewrite = rewriteHostReference ?? (reference => reference);

        ResolveRecursive(entryPath, visited, sourceFiles, allPackages, allAssemblyRefs, rewrite);

        return new ScriptGraph(sourceFiles, allPackages, allAssemblyRefs.ToList());
    }

    private static void ResolveRecursive(
        string filePath,
        HashSet<string> visited,
        List<SourceFileEntry> sourceFiles,
        List<PackageReference> allPackages,
        HashSet<string> allAssemblyRefs,
        Func<string, string> rewriteHostReference)
    {
        var canonicalPath = Path.GetFullPath(filePath);
        if (!visited.Add(canonicalPath))
            return;

        if (!File.Exists(canonicalPath))
            return;

        var source = File.ReadAllText(canonicalPath);
        var parsed = ParseSingleFile(source, canonicalPath, rewriteHostReference);

        foreach (var loadedPath in parsed.LoadedFiles)
            ResolveRecursive(loadedPath, visited, sourceFiles, allPackages, allAssemblyRefs, rewriteHostReference);

        sourceFiles.Add(new SourceFileEntry(canonicalPath, parsed.CleanSource));

        allPackages.AddRange(parsed.Packages);

        foreach (var asmRef in parsed.AssemblyReferences)
            allAssemblyRefs.Add(asmRef);
    }

    private static ParsedFile ParseSingleFile(string source, string filePath, Func<string, string> rewriteHostReference)
    {
        var packages = new List<PackageReference>();
        var assemblyReferences = new List<string>();
        var loadedFiles = new List<string>();
        var strippedLines = new List<int>();
        var fileDir = Path.GetDirectoryName(filePath) ?? string.Empty;

        var lines = source.Split(['\r', '\n'], StringSplitOptions.None);
        for (var i = 0; i < lines.Length; i++)
        {
            if (TryParseLoadDirective(lines[i], fileDir, out var loadedPath))
            {
                strippedLines.Add(i);
                if (loadedPath != null)
                    loadedFiles.Add(loadedPath);
                continue;
            }

            if (TryParseReferenceDirective(lines[i], rewriteHostReference, packages, assemblyReferences))
                strippedLines.Add(i);
        }

        var cleanSource = BuildCleanSource(lines, strippedLines);
        return new ParsedFile(cleanSource, packages, assemblyReferences, loadedFiles);
    }

    private static bool TryParseLoadDirective(string line, string baseDir, out string? resolvedPath)
    {
        resolvedPath = null;
        var match = LoadDirectiveRegex().Match(line);
        if (!match.Success)
            return false;

        var relativePath = match.Groups["path"].Value.Trim();
        var absolutePath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
        if (File.Exists(absolutePath))
            resolvedPath = absolutePath;
        return true;
    }

    private static bool TryParseReferenceDirective(
        string line, Func<string, string> rewriteHostReference,
        List<PackageReference> packages, List<string> assemblyReferences)
    {
        var match = ReferenceDirectiveRegex().Match(line);
        if (!match.Success)
            return false;

        var reference = match.Groups["ref"].Value.Trim();

        if (IsNugetReference(reference, out var packageId, out var version))
        {
            packages.Add(new PackageReference(packageId, version));
            return true;
        }

        if (IsIgnoredRuntimeReference(reference))
            return true;

        var rewritten = rewriteHostReference(reference);
        var resolvedPath = string.IsNullOrWhiteSpace(rewritten) ? reference : rewritten;
        if (File.Exists(resolvedPath))
            assemblyReferences.Add(resolvedPath);
        return true;
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
            if (normalized.Contains(segment, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
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
            else
                result.Add("// " + lines[i]);
        }
        return string.Join("\n", result);
    }
}

internal readonly record struct PackageReference(string PackageId, string? Version);

internal readonly record struct SourceFileEntry(string Path, string CleanSource);

/// <summary>
/// Result of parsing a single .csx file (before graph merge).
/// </summary>
internal sealed class ParsedFile(
    string cleanSource,
    List<PackageReference> packages,
    List<string> assemblyReferences,
    List<string> loadedFiles)
{
    public string CleanSource { get; } = cleanSource;
    public IReadOnlyList<PackageReference> Packages { get; } = packages;
    public IReadOnlyList<string> AssemblyReferences { get; } = assemblyReferences;
    public IReadOnlyList<string> LoadedFiles { get; } = loadedFiles;
}

/// <summary>
/// Flattened result of the entire script graph.
/// SourceFiles are in topological order (dependencies first).
/// </summary>
internal sealed class ScriptGraph(
    List<SourceFileEntry> sourceFiles,
    List<PackageReference> packages,
    List<string> assemblyReferences)
{
    public IReadOnlyList<SourceFileEntry> SourceFiles { get; } = sourceFiles;
    public IReadOnlyList<PackageReference> Packages { get; } = packages;
    public IReadOnlyList<string> AssemblyReferences { get; } = assemblyReferences;
}
