using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
namespace RevitDevTool.Execution.Providers.FSharp;

// ReSharper disable once PartialTypeWithSinglePart
internal static partial class FSharpDependencyResolver
{
    private const string RevitVersionPattern = @"Revit\s+\d{4}";

#if NETFRAMEWORK
    private static readonly Regex RevitVersionRx = new(RevitVersionPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static Regex RevitVersionRegex() => RevitVersionRx;
#else
    [GeneratedRegex(RevitVersionPattern, RegexOptions.IgnoreCase)]
    private static partial Regex RevitVersionRegex();
#endif

    public static async Task<FSharpNugetResolutionResult> ResolveAsync(
        string entryScriptPath,
        LoadGraph graph,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var entryScript = Path.GetFullPath(entryScriptPath);
        var entryScriptName = Path.GetFileName(entryScript);
        var hostRevitVersion = Context.Application.VersionNumber;
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolvedReferenceLines = ResolveFileReferences(graph.FileReferences, hostRevitVersion, references);
        var requiresRewrite = graph.Packages.Count > 0 || resolvedReferenceLines.Count > 0;

        if (graph.Packages.Count > 0)
        {
            var packageRequests = BuildPackageRequestsOrThrow(graph.Packages);
            progress?.Report($"Resolving {packageRequests.Length} NuGet package(s) for {entryScriptName}...");

            foreach (var request in packageRequests)
            {
                progress?.Report($"Resolving NuGet {request.PackageId} ({request.Version ?? "latest"})...");
                var packageDlls = await NugetManager.ResolvePackageDllsAsync(request.PackageId, request.Version, ct).ConfigureAwait(false);
                foreach (var dllPath in packageDlls)
                    references.Add(dllPath);

                Debug.WriteLine(
                    $"[NuGetResolver] {request.PackageId} {request.Version ?? "latest"}");
            }

            progress?.Report($"NuGet resolution completed for {entryScriptName}.");
        }

        if (!requiresRewrite)
            return new FSharpNugetResolutionResult(entryScript, references.ToArray(), null);

        var rewriteResult = await FSharpScriptGraph
            .RewriteGraphAsync(entryScript, graph.Nodes, resolvedReferenceLines, ct)
            .ConfigureAwait(false);
        return new FSharpNugetResolutionResult(rewriteResult.EntryScriptPath, references.ToArray(), rewriteResult.Cleanup);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>> ResolveFileReferences(
        IReadOnlyList<ReferenceDirective> directives,
        string hostRevitVersion,
        HashSet<string> references)
    {
        var resolvedLines = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
        if (directives.Count == 0)
            return new Dictionary<string, IReadOnlyDictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var directive in directives)
        {
            var rewritten = CorrectRevitReference(directive.Reference, hostRevitVersion);
            var resolved = ResolveAbsolutePath(directive.FilePath, rewritten);

            if (!File.Exists(resolved))
            {
                Trace.TraceWarning(
                    $"Could not resolve F# reference '{rewritten}' in {directive.FilePath} (line {directive.LineNumber}). " +
                    "Keeping original #r directive for FSI.");
                continue;
            }

            references.Add(resolved);

            if (!resolvedLines.TryGetValue(directive.FilePath, out var lines))
            {
                lines = [];
                resolvedLines[directive.FilePath] = lines;
            }

            lines[directive.LineNumber] = resolved;
        }

        return resolvedLines.ToDictionary(
            item => item.Key,
            item => (IReadOnlyDictionary<int, string>)item.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static string CorrectRevitReference(string referenceValue, string hostRevitVersion)
    {
        return string.IsNullOrWhiteSpace(hostRevitVersion) 
            ? referenceValue 
            : RevitVersionRegex().Replace(referenceValue, $"Revit {hostRevitVersion}");
    }

    private static string ResolveAbsolutePath(string scriptFilePath, string referenceValue)
    {
        var trimmed = referenceValue.Trim();
        if (Path.IsPathRooted(trimmed))
            return Path.GetFullPath(trimmed);
        var baseDir = Path.GetDirectoryName(scriptFilePath) ?? string.Empty;
        return Path.GetFullPath(Path.Combine(baseDir, trimmed));
    }

    private static PackageRequest[] BuildPackageRequestsOrThrow(IReadOnlyList<PackageDirective> directives)
    {
        var grouped = directives
            .GroupBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var result = new List<PackageRequest>(grouped.Length);
        foreach (var group in grouped)
        {
            var versionSet = group
                .Select(item => item.Version)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var explicitVersions = versionSet
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (explicitVersions.Length > 1)
            {
                throw BuildVersionConflictException(
                    group.Key,
                    "multiple explicit versions were declared",
                    group);
            }

            var hasUnpinned = versionSet.Any(string.IsNullOrWhiteSpace);
            if (hasUnpinned && explicitVersions.Length > 0)
            {
                throw BuildVersionConflictException(
                    group.Key,
                    "both pinned and unpinned version declarations were found",
                    group);
            }

            result.Add(new PackageRequest(group.Key, explicitVersions.SingleOrDefault()));
        }

        return result.ToArray();
    }

    private static InvalidOperationException BuildVersionConflictException(
        string packageId,
        string reason,
        IEnumerable<PackageDirective> directives)
    {
        var locations = directives
            .Select(item => $" - {item.FilePath} (line {item.LineNumber}): {item.OriginalLine.Trim()}")
            .ToArray();

        var message =
            $"NuGet version conflict for package '{packageId}': {reason}.{Environment.NewLine}" +
            string.Join(Environment.NewLine, locations) + Environment.NewLine +
            "Please align all #r \"nuget: ...\" declarations before running script.";

        return new InvalidOperationException(message);
    }
}

internal readonly record struct FSharpNugetResolutionResult(
    string ScriptPath,
    string[] References,
    IDisposable? Cleanup);
