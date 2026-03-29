using System.Diagnostics;
using System.IO;
using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;
using RevitDevTool.Execution.Models;
using RevitDevTool.Execution.Providers.Python;

namespace RevitDevTool.Execution.Services;

internal static class PixiPackageHelper
{
    public static async Task<IReadOnlyList<Package>> ListPackagesAsync(CancellationToken cancellationToken)
    {
        if (!PythonInstaller.IsPixiInstalled() || !Directory.Exists(PythonEnvironment.PixiProjectDir))
            return [];

        var result = await Cli.Wrap(PythonInstaller.PixiExePath)
            .WithArguments(["list", "--explicit", "--json"])
            .WithWorkingDirectory(PythonEnvironment.PixiProjectDir)
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
            return [];
        }
    }

    public static async Task InstallAsync(string packageId, string? declaredVersion, bool pypi, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageId) || !IsAvailable())
            return;

        var args = new List<string> { "add" };
        if (pypi)
            args.Add("--pypi");
        args.Add(BuildSpec(packageId, declaredVersion));

        await Cli.Wrap(PythonInstaller.PixiExePath)
            .WithArguments(args)
            .WithWorkingDirectory(PythonEnvironment.PixiProjectDir)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task RemoveAsync(string packageId, bool pypi, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageId) || !IsAvailable())
            return;

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

    private static bool IsAvailable()
    {
        if (PythonInstaller.IsPixiInstalled() && Directory.Exists(PythonEnvironment.PixiProjectDir))
            return true;

        Trace.TraceWarning("Pixi runtime is unavailable. Skipping operation.");
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
            PythonEnvironment.RequirePackages.Contains(packageId, StringComparer.OrdinalIgnoreCase));
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
        while (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"')
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
