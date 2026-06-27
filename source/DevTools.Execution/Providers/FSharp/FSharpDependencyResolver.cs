using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ZLogger;
namespace DevTools.Execution.Providers.FSharp;

public sealed class FSharpDependencyResolver(ILogger<FSharpDependencyResolver> logger, NugetManager nugetManager)
{
    internal async Task<FSharpNugetResolutionResult> ResolveAsync(
        string entryScriptPath,
        LoadGraph graph,
        ICompiledScriptBridge bridgeSupport,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var entryScript = Path.GetFullPath(entryScriptPath);
        var entryScriptName = Path.GetFileName(entryScript);
        var hostPattern = bridgeSupport.GetHostReferencePattern();
        var hostReplacement = bridgeSupport.GetHostReferenceReplacement();
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolvedReferenceLines = ResolveFileReferences(graph.FileReferences, hostPattern, hostReplacement, references);
        var requiresRewrite = graph.Packages.Count > 0 || resolvedReferenceLines.Count > 0;

        if (graph.Packages.Count > 0)
        {
            var packageRequests = BuildPackageRequestsOrThrow(graph.Packages);
            progress?.Report($"Resolving {packageRequests.Length} NuGet package(s) for {entryScriptName}...");

            foreach (var request in packageRequests)
            {
                progress?.Report($"Resolving NuGet {request.PackageId} ({request.Version ?? "latest"})...");
                var packageDlls = await nugetManager.ResolvePackageDllsAsync(request.PackageId, request.Version, ct).ConfigureAwait(false);
                foreach (var dllPath in packageDlls)
                    references.Add(dllPath);

                logger.ZLogDebug(
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

    private IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>> ResolveFileReferences(
        IReadOnlyList<ReferenceDirective> directives,
        string? hostPattern,
        string hostReplacement,
        HashSet<string> references)
    {
        var resolvedLines = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
        if (directives.Count == 0)
            return new Dictionary<string, IReadOnlyDictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

        var hostRegex = hostPattern is not null
            ? new Regex(hostPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)
            : null;

        foreach (var directive in directives)
        {
            var rewritten = CorrectHostReference(directive.Reference, hostRegex, hostReplacement);
            var resolved = ResolveAbsolutePath(directive.FilePath, rewritten);

            if (!File.Exists(resolved))
            {
                logger.ZLogWarning(
                    $"Could not resolve F# reference '{rewritten}' in {directive.FilePath} (line {directive.LineNumber}). Keeping original #r directive for FSI.");
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
            item => item.Key, IReadOnlyDictionary<int, string> (item) => item.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static string CorrectHostReference(string referenceValue, Regex? hostRegex, string replacement)
    {
        return hostRegex is null
            ? referenceValue
            : hostRegex.Replace(referenceValue, replacement);
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
