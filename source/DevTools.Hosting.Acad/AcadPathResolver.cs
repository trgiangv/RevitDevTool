using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DevTools.Hosting.Acad;

/// <summary>
/// Discovers AutoCAD-family installs via <c>InstalledProducts</c>
/// (install dir + product code). Verticals share <see cref="AcadProductCatalog.ExeFileName"/>.
/// </summary>
public sealed partial class AcadPathResolver : IHostPathResolver
{
    public static IReadOnlyDictionary<string, HostApp> ProductIdMap => AcadProductCatalog.ProductIdMap;

    [GeneratedRegex(@"AutoCAD.*?(?<year>\d{4})$", RegexOptions.IgnoreCase)]
    private static partial Regex InstallFolderYear();

    public bool Supports(HostApp hostApp) => hostApp.IsAcadFamily();

    public string? FindExecutable(HostApp hostApp, string version)
    {
        if (!Supports(hostApp))
            return null;

        foreach (var entry in EnumerateFromRegistry())
        {
            if (entry.VersionYear != version)
                continue;
            if (entry.HostApp != hostApp)
                continue;
            return entry.AcadExePath;
        }

        return hostApp == HostApp.AutoCad ? FindFromFileSystem(version) : null;
    }

    public IReadOnlyList<string> GetInstalledVersions(HostApp hostApp)
    {
        if (!Supports(hostApp))
            return [];

        return EnumerateFromRegistry()
            .Where(p => p.HostApp == hostApp)
            .Select(p => p.VersionYear)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<AcadInstallation> EnumerateFromRegistry()
    {
        using var root = Registry.LocalMachine.OpenSubKey(AcadProductCatalog.RegistryRoot);
        if (root is null)
            yield break;

        foreach (var releaseName in root.GetSubKeyNames())
        {
            using var releaseKey = root.OpenSubKey(releaseName);
            if (releaseKey is null)
                continue;

            foreach (var entry in EnumerateRelease(releaseKey))
                yield return entry;
        }
    }

    private static IEnumerable<AcadInstallation> EnumerateRelease(RegistryKey releaseKey)
    {
        using var products = releaseKey.OpenSubKey(AcadProductCatalog.InstalledProductsKeyName);
        if (products is null || !TryParseInstallDir(ReadInstallDir(products), out var exePath, out var year))
            yield break;

        foreach (var productCode in products.GetSubKeyNames())
        {
            if (AcadProductCatalog.TryGetHost(productCode, out var hostApp))
                yield return new AcadInstallation(year, hostApp, exePath);
        }
    }

    private static string? ReadInstallDir(RegistryKey products) =>
        products.GetValue(null) as string ?? products.GetValue(string.Empty) as string;

    internal static bool TryParseInstallDir(string? installDir, out string exePath, out string year)
    {
        exePath = string.Empty;
        year = string.Empty;
        if (string.IsNullOrWhiteSpace(installDir))
            return false;

        var dir = installDir.TrimEnd('\\', '/');
        var exe = Path.Combine(dir, AcadProductCatalog.ExeFileName);
        if (!File.Exists(exe))
            return false;

        var folder = Path.GetFileName(dir);
        var match = InstallFolderYear().Match(folder);
        if (!match.Success)
            return false;

        var yearText = match.Groups["year"].Value;
        if (!int.TryParse(yearText, out var n) || n < HostVersions.AutodeskMinimal)
            return false;

        exePath = exe;
        year = yearText;
        return true;
    }

    private static string? FindFromFileSystem(string version)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var autodeskDir = Path.Combine(programFiles, "Autodesk");
        if (!Directory.Exists(autodeskDir))
            return null;

        string[] patterns = [$"AutoCAD {version}", $"AutoCAD {version} *"];
        foreach (var pattern in patterns)
        {
            var match = Directory.GetDirectories(autodeskDir, pattern)
                .Select(dir => Path.Combine(dir, AcadProductCatalog.ExeFileName))
                .FirstOrDefault(File.Exists);
            if (match is not null)
                return match;
        }

        return null;
    }
}

internal sealed record AcadInstallation(
    string VersionYear,
    HostApp HostApp,
    string AcadExePath);
