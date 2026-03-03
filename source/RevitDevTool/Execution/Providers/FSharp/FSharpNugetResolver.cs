using System.Diagnostics;
using System.IO;
namespace RevitDevTool.Execution.Providers.FSharp;

internal static class FSharpNugetResolver
{
    public static async Task<FSharpNugetResolutionResult> ResolveAsync(
        string entryScriptPath,
        LoadGraph graph,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var entryScript = Path.GetFullPath(entryScriptPath);
        var entryScriptName = Path.GetFileName(entryScript);

        if (graph.Packages.Count == 0)
            return new FSharpNugetResolutionResult(entryScript, [], null);

        var packageRequests = BuildPackageRequestsOrThrow(graph.Packages);
        progress?.Report($"Resolving {packageRequests.Length} NuGet package(s) for {entryScriptName}...");

        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var request in packageRequests)
        {
            progress?.Report($"Resolving NuGet {request.PackageId} ({request.Version ?? "latest"})...");
            var packageDlls = await NugetManager.ResolvePackageDllsAsync(request.PackageId, request.Version, ct).ConfigureAwait(false);
            foreach (var dllPath in packageDlls)
                references.Add(dllPath);

            Debug.WriteLine(
                $"[NuGetResolver] {request.PackageId} {request.Version ?? "latest"} -> {packageDlls.Length} dll(s)");
        }

        progress?.Report($"NuGet resolution completed for {entryScriptName}.");

        var rewriteResult = await FSharpScriptGraph.RewriteGraphAsync(entryScript, graph.Nodes, ct).ConfigureAwait(false);
        return new FSharpNugetResolutionResult(rewriteResult.EntryScriptPath, references.ToArray(), rewriteResult.Cleanup);
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
