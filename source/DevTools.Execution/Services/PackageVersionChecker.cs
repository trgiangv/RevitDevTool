using System.Diagnostics;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.FSharp;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Execution.Services;

internal static class PackageVersionChecker
{
    public static async Task<IReadOnlyList<Package>> AttachLatestVersionsAsync(
        IReadOnlyList<Package> packages,
        CancellationToken cancellationToken)
    {
        if (packages.Count == 0)
            return packages;

        var uniqueKeys = packages
            .Select(p => (p.Marketplace, Id: NormalizeId(p.PackageId)))
            .Distinct()
            .ToArray();

        var fetchTasks = uniqueKeys.Select(async key =>
        {
            var latest = await FetchLatestAsync(key.Marketplace, key.Id, cancellationToken).ConfigureAwait(false);
            return (key, latest);
        });

        var results = await Task.WhenAll(fetchTasks).ConfigureAwait(false);
        var cache = results.ToDictionary(r => r.key, r => r.latest);

        return packages
            .Select(p =>
            {
                var key = (p.Marketplace, NormalizeId(p.PackageId));
                cache.TryGetValue(key, out var latest);
                return p with
                {
                    LatestVersion = latest,
                    IsLatest = IsSameVersion(p.Version, latest)
                };
            })
            .ToArray();
    }

    private static async Task<string?> FetchLatestAsync(Marketplace marketplace, string packageId, CancellationToken cancellationToken)
    {
        return marketplace switch
        {
            Marketplace.NuGet => await FetchNuGetAsync(packageId, cancellationToken).ConfigureAwait(false),
            Marketplace.PyPi => await FetchPyPiAsync(packageId, cancellationToken).ConfigureAwait(false),
            Marketplace.CondaForge => await FetchCondaAsync(packageId, cancellationToken).ConfigureAwait(false),
            _ => null
        };
    }

    private static async Task<string?> FetchNuGetAsync(string packageId, CancellationToken cancellationToken)
    {
        try
        {
            return await NugetManager.FetchLatestVersionAsync(packageId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Package] NuGet lookup failed for '{packageId}': {ex.Message}");
            return null;
        }
    }

    private static async Task<string?> FetchPyPiAsync(string packageId, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://pypi.org/pypi/{packageId}/json";
            using var doc = await NetworkService.GetJsonDocumentAsync(url, cancellationToken).ConfigureAwait(false);
            if (doc == null) return null;

            return doc.RootElement.TryGetProperty("info", out var info) &&
                   info.TryGetProperty("version", out var ver)
                ? ver.GetString()
                : null;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Package] PyPI lookup failed for '{packageId}': {ex.Message}");
            return null;
        }
    }

    private static async Task<string?> FetchCondaAsync(string packageId, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://api.anaconda.org/package/conda-forge/{packageId}";
            using var doc = await NetworkService.GetJsonDocumentAsync(url, cancellationToken).ConfigureAwait(false);
            if (doc == null) return null;

            return doc.RootElement.TryGetProperty("latest_version", out var latest)
                ? latest.GetString()
                : null;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Package] Conda lookup failed for '{packageId}': {ex.Message}");
            return null;
        }
    }

    private static bool IsSameVersion(string? current, string? latest)
    {
        if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(latest))
            return false;
        return current!.Trim().Equals(latest!.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeId(string packageId)
    {
        return packageId.Trim().Replace('_', '-').ToLowerInvariant();
    }
}
