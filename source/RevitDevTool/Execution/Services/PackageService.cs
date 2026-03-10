using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;
using RevitDevTool.Execution.Interfaces;
using RevitDevTool.Execution.Models;
using RevitDevTool.Execution.Providers.FSharp;
using RevitDevTool.Execution.Providers.Python;
using RevitDevTool.Utils;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.Execution.Services;

public sealed class PackageService : IPackageService
{
    private static readonly string NuGetCacheRoot = Path.Combine(SettingsUtils.GetApplicationDataPath(), "nuget");
    private static readonly string PixiTomlPath = Path.Combine(PythonEnvironment.PixiProjectDir, "pixi.toml");
    private const string NuGetServiceIndexUrl = "https://api.nuget.org/v3/index.json";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static string? _nugetPackageBaseUrl;

    public async Task<IReadOnlyList<Package>> ListInstalledPackagesAsync(CancellationToken cancellationToken = default)
    {
        var nugetTask = Task.Run(ListNuGetPackages, cancellationToken);
        var pixiTask = ReadPixiExplicitPackagesAsync(cancellationToken);

        await Task.WhenAll(nugetTask, pixiTask).ConfigureAwait(false);

        var packages = new List<Package>();
        packages.AddRange(nugetTask.Result);
        packages.AddRange(pixiTask.Result);

        return await AttachLatestVersionInfoAsync(packages, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemovePackageAsync(Package package, CancellationToken cancellationToken = default)
    {
        if (package.IsProtected)
            return;

        switch (package.Marketplace)
        {
            case Marketplace.NuGet:
                await RemoveNuGetPackageAsync(package, cancellationToken).ConfigureAwait(false);
                break;
            case Marketplace.CondaForge:
                await RemovePixiPackageAsync(package.PackageId, pypi: false, cancellationToken).ConfigureAwait(false);
                break;
            case Marketplace.PyPi:
                await RemovePixiPackageAsync(package.PackageId, pypi: true, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    public async Task RemoveAllAsync(Marketplace marketplace, CancellationToken cancellationToken = default)
    {
        switch (marketplace)
        {
            case Marketplace.NuGet:
                await RemoveAllNuGetAsync(cancellationToken).ConfigureAwait(false);
                break;
            case Marketplace.CondaForge:
            case Marketplace.PyPi:
                var all = await ListInstalledPackagesAsync(cancellationToken).ConfigureAwait(false);
                var targets = all
                    .Where(item => item.Marketplace == marketplace)
                    .Where(item => !item.IsProtected)
                    .ToArray();
                foreach (var item in targets)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await RemovePackageAsync(item, cancellationToken).ConfigureAwait(false);
                }
                break;
        }
    }

    public Task UpdateLatestAsync(Package package, CancellationToken cancellationToken = default)
    {
        return package.Marketplace switch
        {
            Marketplace.NuGet => NugetManager.ResolvePackageDllsAsync(package.PackageId, null, cancellationToken),
            Marketplace.CondaForge => InstallPixiPackageAsync(package.PackageId, null, pypi: false, cancellationToken),
            Marketplace.PyPi => InstallPixiPackageAsync(package.PackageId, null, pypi: true, cancellationToken),
            _ => Task.CompletedTask
        };
    }

    public async Task RepairAsync(Package package, CancellationToken cancellationToken = default)
    {
        if (package.Marketplace == Marketplace.NuGet)
        {
            await RemoveNuGetPackageAsync(package, cancellationToken).ConfigureAwait(false);
            await NugetManager.ResolvePackageDllsAsync(package.PackageId, package.Version, cancellationToken).ConfigureAwait(false);
            return;
        }

        var isPypi = package.Marketplace == Marketplace.PyPi;
        await RemovePixiPackageAsync(package.PackageId, isPypi, cancellationToken).ConfigureAwait(false);
        await InstallPixiPackageAsync(package.PackageId, package.DeclaredVersion, isPypi, cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<Package> ListNuGetPackages()
    {
        if (!Directory.Exists(NuGetCacheRoot))
            return [];

        var result = new List<Package>();
        foreach (var packageDir in Directory.GetDirectories(NuGetCacheRoot))
        {
            var packageId = Path.GetFileName(packageDir);
            if (string.IsNullOrWhiteSpace(packageId))
                continue;

            var versions = Directory.GetDirectories(packageDir)
                .Select(Path.GetFileName)
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .OrderByDescending(version => version, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (versions.Length == 0)
            {
                result.Add(new Package(Marketplace.NuGet, packageId, null));
                continue;
            }

            foreach (var version in versions)
            {
                result.Add(new Package(Marketplace.NuGet, packageId, version, version));
            }
        }

        return result;
    }

    private static async Task RemoveNuGetPackageAsync(Package package, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var packageRoot = Path.Combine(NuGetCacheRoot, package.PackageId.ToLowerInvariant());
        if (!Directory.Exists(packageRoot))
            return;

        if (string.IsNullOrWhiteSpace(package.Version))
        {
            await Task.Run(() => Directory.Delete(packageRoot, recursive: true), cancellationToken).ConfigureAwait(false);
            return;
        }

        var versionDir = Path.Combine(packageRoot, package.Version!.ToLowerInvariant());
        if (Directory.Exists(versionDir))
        {
            await Task.Run(() => Directory.Delete(versionDir, recursive: true), cancellationToken).ConfigureAwait(false);
        }

        if (!Directory.Exists(packageRoot))
            return;

        if (Directory.EnumerateFileSystemEntries(packageRoot).Any())
            return;

        await Task.Run(() => Directory.Delete(packageRoot, recursive: true), cancellationToken).ConfigureAwait(false);
    }

    private static async Task RemoveAllNuGetAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(NuGetCacheRoot))
            return;

        var dirs = Directory.GetDirectories(NuGetCacheRoot);
        foreach (var dir in dirs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Run(() => Directory.Delete(dir, recursive: true), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task RemovePixiPackageAsync(string packageId, bool pypi, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            return;

        if (!PythonInstaller.IsPixiInstalled() || !Directory.Exists(PythonEnvironment.PixiProjectDir))
        {
            Trace.TraceWarning("Pixi runtime is unavailable. Skip package removal.");
            return;
        }

        var args = new List<string> { "remove" };
        if (pypi)
            args.Add("--pypi");
        args.Add(packageId);

        await Cli.Wrap(PythonInstaller.PixiExePath)
            .WithArguments(args)
            .WithWorkingDirectory(PythonEnvironment.PixiProjectDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InstallPixiPackageAsync(string packageId, string? declaredVersion, bool pypi, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            return;

        if (!PythonInstaller.IsPixiInstalled() || !Directory.Exists(PythonEnvironment.PixiProjectDir))
        {
            Trace.TraceWarning("Pixi runtime is unavailable. Skip package install/update.");
            return;
        }

        var args = new List<string> { "add" };
        if (pypi)
            args.Add("--pypi");
        args.Add(BuildPixiSpec(packageId, declaredVersion));

        await Cli.Wrap(PythonInstaller.PixiExePath)
            .WithArguments(args)
            .WithWorkingDirectory(PythonEnvironment.PixiProjectDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<Package>> ReadPixiExplicitPackagesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(PixiTomlPath) || !PythonInstaller.IsPixiInstalled() || !Directory.Exists(PythonEnvironment.PixiProjectDir))
            return [];

        var output = await ExecutePixiListExplicitJsonAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(output))
            return [];

        try
        {
            return ParsePixiExplicitPackages(output!);
        }
        catch
        {
            return [];
        }
    }

    private static async Task<string?> ExecutePixiListExplicitJsonAsync(CancellationToken cancellationToken)
    {
        var result = await Cli.Wrap(PythonInstaller.PixiExePath)
            .WithArguments(["list", "--explicit", "--json"])
            .WithWorkingDirectory(PythonEnvironment.PixiProjectDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
            return null;

        return result.StandardOutput;
    }

    private static List<Package> ParsePixiExplicitPackages(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        var packages = new List<Package>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (TryCreateRuntimePackage(item, out var package))
                packages.Add(package);
        }

        return packages;
    }

    private static bool TryCreateRuntimePackage(JsonElement item, out Package package)
    {
        package = null!;
        if (!TryMapMarketplace(item, out var marketplace))
            return false;

        if (!TryGetString(item, "name", out var packageId))
            return false;

        if (IsSkippedPixiPackage(marketplace, packageId))
            return false;

        var installedVersion = TryGetString(item, "version", out var versionText) ? versionText : null;
        var requestedSpec = TryGetString(item, "requested_spec", out var requestedSpecText)
            ? NormalizeRequestedSpec(requestedSpecText)
            : null;

        package = new Package(
            marketplace,
            packageId,
            string.IsNullOrWhiteSpace(installedVersion) ? null : installedVersion,
            requestedSpec,
            IsRequiredPythonPackage(packageId));
        return true;
    }

    private static bool IsSkippedPixiPackage(Marketplace marketplace, string packageId)
    {
        return marketplace == Marketplace.CondaForge &&
               packageId.Equals("python", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRequiredPythonPackage(string packageId)
    {
        return PythonEnvironment.RequirePackages.Contains(packageId, StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildPixiSpec(string packageId, string? declaredVersion)
    {
        if (string.IsNullOrWhiteSpace(declaredVersion))
            return packageId;

        var version = declaredVersion!.Trim();
        if (version.Length == 0 || version == "*")
            return packageId;

        return $"{packageId}{version}";
    }

    private static string NormalizePackageId(string packageId)
    {
        return packageId.Trim().Replace('_', '-').ToLowerInvariant();
    }

    private static bool TryMapMarketplace(JsonElement item, out Marketplace marketplace)
    {
        marketplace = default;
        if (!TryGetString(item, "kind", out var kind))
            return false;

        if (kind.Equals("conda", StringComparison.OrdinalIgnoreCase))
        {
            marketplace = Marketplace.CondaForge;
            return true;
        }

        if (kind.Equals("pypi", StringComparison.OrdinalIgnoreCase))
        {
            marketplace = Marketplace.PyPi;
            return true;
        }

        return false;
    }

    private static bool TryGetString(JsonElement item, string propertyName, out string value)
    {
        value = string.Empty;
        if (!item.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind != JsonValueKind.String)
            return false;

        var text = property.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        value = text!;
        return true;
    }

    private static string? NormalizeRequestedSpec(string? requestedSpec)
    {
        if (string.IsNullOrWhiteSpace(requestedSpec))
            return null;

        var normalized = requestedSpec!.Trim();
        while (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"')
            normalized = normalized[1..^1].Trim();

        if (normalized.Length == 0 || normalized == "*")
            return null;

        return normalized;
    }

    private async Task<IReadOnlyList<Package>> AttachLatestVersionInfoAsync(IReadOnlyList<Package> packages, CancellationToken cancellationToken)
    {
        if (packages.Count == 0)
            return packages;

        var uniqueKeys = packages
            .Select(p => (p.Marketplace, Id: NormalizePackageId(p.PackageId)))
            .Distinct()
            .ToArray();

        var fetchTasks = uniqueKeys.Select(async key =>
        {
            var latest = await FetchLatestVersionAsync(key.Marketplace, key.Id, cancellationToken).ConfigureAwait(false);
            return (key, latest);
        });

        var results = await Task.WhenAll(fetchTasks).ConfigureAwait(false);
        var latestCache = results.ToDictionary(r => r.key, r => r.latest);

        return packages
            .Select(package =>
            {
                var key = (package.Marketplace, NormalizePackageId(package.PackageId));
                latestCache.TryGetValue(key, out var latest);
                var isLatest = IsSameVersion(package.Version, latest);
                return package with
                {
                    LatestVersion = latest,
                    IsLatest = isLatest
                };
            })
            .ToArray();
    }

    private async Task<string?> FetchLatestVersionAsync(Marketplace marketplace, string packageId, CancellationToken cancellationToken)
    {
        return marketplace switch
        {
            Marketplace.NuGet => await FetchLatestNuGetVersionAsync(packageId, cancellationToken).ConfigureAwait(false),
            Marketplace.PyPi => await FetchLatestPyPiVersionAsync(packageId, cancellationToken).ConfigureAwait(false),
            Marketplace.CondaForge => await FetchLatestCondaVersionAsync(packageId, cancellationToken).ConfigureAwait(false),
            _ => null
        };
    }

    private static bool IsSameVersion(string? current, string? latest)
    {
        if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(latest))
            return false;
        return current!.Trim().Equals(latest!.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string?> FetchLatestNuGetVersionAsync(string packageId, CancellationToken cancellationToken)
    {
        try
        {
            var baseUrl = await GetNuGetPackageBaseUrlAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(baseUrl))
                return null;

            var url = $"{baseUrl!.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json";
            using var response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(payload);
            var versions = doc.RootElement.GetProperty("versions")
                .EnumerateArray()
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToArray();
            return versions.LastOrDefault(v => !v.Contains('-')) ?? versions.LastOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> GetNuGetPackageBaseUrlAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_nugetPackageBaseUrl))
            return _nugetPackageBaseUrl;

        using var response = await Http.GetAsync(NuGetServiceIndexUrl, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(payload);
        foreach (var resource in doc.RootElement.GetProperty("resources").EnumerateArray())
        {
            var type = resource.TryGetProperty("@type", out var typeEl) ? typeEl.GetString() ?? string.Empty : string.Empty;
            if (!type.StartsWith("PackageBaseAddress", StringComparison.OrdinalIgnoreCase))
                continue;

            _nugetPackageBaseUrl = resource.GetProperty("@id").GetString();
            return _nugetPackageBaseUrl;
        }

        return null;
    }

    private static async Task<string?> FetchLatestPyPiVersionAsync(string packageId, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://pypi.org/pypi/{packageId}/json";
            using var response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.TryGetProperty("info", out var info) &&
                   info.TryGetProperty("version", out var ver)
                ? ver.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> FetchLatestCondaVersionAsync(string packageId, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://api.anaconda.org/package/conda-forge/{packageId}";
            using var response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.TryGetProperty("latest_version", out var latest)
                ? latest.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }
}
