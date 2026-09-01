using System.IO;
using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Python;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Execution.Services;

internal sealed class PixiPackageStore(ILogger<PixiPackageStore> logger) : IPythonPackageStore
{
    public PythonBackend Backend => PythonBackend.Pixi;

    public async Task<IReadOnlyList<Package>> ListAsync(CancellationToken cancellationToken)
    {
        if (!PixiInstaller.IsPixiInstalled() || !Directory.Exists(PixiEnvironmentProvider.PixiProjectDir))
            return [];

        var result = await Cli.Wrap(PixiInstaller.PixiExePath)
            .WithArguments(PixiEnvironmentProvider.PixiArgs.ListExplicitJson())
            .WithWorkingDirectory(PixiEnvironmentProvider.PixiProjectDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
            return [];

        try
        {
            return ParseExplicitList(result.StandardOutput);
        }
        catch
        {
            logger.ZLogWarning($"Failed to parse Pixi package list output: {result.StandardOutput}");
            return [];
        }
    }

    public Task RemoveAsync(Package package, CancellationToken cancellationToken)
        => RemoveAsync(package.PackageId, IsPypi(package), cancellationToken);

    public Task UpdateAsync(Package package, CancellationToken cancellationToken)
        => UpdateCoreAsync(package, IsPypi(package), cancellationToken);

    public async Task RepairAsync(Package package, CancellationToken cancellationToken)
    {
        var pypi = IsPypi(package);
        await RemoveAsync(package.PackageId, pypi, cancellationToken).ConfigureAwait(false);
        await InstallAsync(package.PackageId, package.DeclaredVersion, pypi, cancellationToken).ConfigureAwait(false);
    }

    private async Task InstallAsync(
        string packageId,
        string? declaredVersion,
        bool pypi,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageId) || !IsAvailable())
            return;

        await Cli.Wrap(PixiInstaller.PixiExePath)
            .WithArguments(PixiEnvironmentProvider.PixiArgs.Add([BuildSpec(packageId, declaredVersion)], pypi))
            .WithWorkingDirectory(PixiEnvironmentProvider.PixiProjectDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateCoreAsync(Package package, bool pypi, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(package.PackageId) || !IsAvailable())
            return;

        if (package.IsProtected)
        {
            await Cli.Wrap(PixiInstaller.PixiExePath)
                .WithArguments(PixiEnvironmentProvider.PixiArgs.Update(package.PackageId))
                .WithWorkingDirectory(PixiEnvironmentProvider.PixiProjectDir)
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await InstallAsync(package.PackageId, null, pypi, cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveAsync(string packageId, bool pypi, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageId) || !IsAvailable())
            return;

        await Cli.Wrap(PixiInstaller.PixiExePath)
            .WithArguments(PixiEnvironmentProvider.PixiArgs.Remove(packageId, pypi))
            .WithWorkingDirectory(PixiEnvironmentProvider.PixiProjectDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool IsPypi(Package package) => package.Marketplace == Marketplace.PyPi;

    private bool IsAvailable()
    {
        if (PixiInstaller.IsPixiInstalled() && Directory.Exists(PixiEnvironmentProvider.PixiProjectDir))
            return true;

        logger.ZLogWarning($"Pixi runtime is unavailable. Skipping operation.");
        return false;
    }

    private static List<Package> ParseExplicitList(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        var packages = new List<Package>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (TryParseEntry(item, out var package))
                packages.Add(package);
        }

        return packages;
    }

    private static bool TryParseEntry(JsonElement item, out Package package)
    {
        package = null!;

        if (!TryMapMarketplace(item, out var marketplace))
            return false;

        if (!TryGetString(item, "name", out var packageId))
            return false;

        if (marketplace == Marketplace.CondaForge &&
            packageId.Equals("python", StringComparison.OrdinalIgnoreCase))
            return false;

        var version = TryGetString(item, "version", out var v) ? v : null;
        var requestedSpec = TryGetString(item, "requested_spec", out var spec)
            ? NormalizeSpec(spec)
            : null;

        package = new Package(
            marketplace,
            packageId,
            string.IsNullOrWhiteSpace(version) ? null : version,
            requestedSpec,
            PyEnvironmentProvider.RequirePackages.ContainsKey(packageId));
        return true;
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

    private static string BuildSpec(string packageId, string? declaredVersion)
    {
        if (string.IsNullOrWhiteSpace(declaredVersion))
            return packageId;

        var version = declaredVersion!.Trim();
        return version.Length == 0 || version == "*" ? packageId : $"{packageId}{version}";
    }

    private static string? NormalizeSpec(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
            return null;

        var normalized = spec!.Trim();
        while (normalized is ['"', _, ..] && normalized[^1] == '"')
            normalized = normalized[1..^1].Trim();

        return normalized.Length == 0 || normalized == "*" ? null : normalized;
    }

    private static bool TryGetString(JsonElement item, string propertyName, out string value)
    {
        value = string.Empty;
        if (!item.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return false;

        var text = property.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        value = text!;
        return true;
    }
}
