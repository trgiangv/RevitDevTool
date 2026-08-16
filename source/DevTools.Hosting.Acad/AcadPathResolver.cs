using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DevTools.Hosting.Acad;

/// <summary>Discovers AutoCAD-family installations via registry and filesystem.</summary>
public sealed partial class AcadPathResolver : IHostPathResolver
{
    private const string AutoCadRegistryRoot = @"SOFTWARE\Autodesk\AutoCAD";

    /// <summary>Mirrors host-side AcadProductDetector.ProductMap: product ID digits -> HostApp.</summary>
    public static IReadOnlyDictionary<string, HostApp> ProductIdMap { get; } =
        new Dictionary<string, HostApp>(StringComparer.OrdinalIgnoreCase)
        {
            ["00"] = HostApp.Civil3D,
            ["01"] = HostApp.AutoCad,
            ["02"] = HostApp.AcadMap3D,
            ["04"] = HostApp.AcadArch,
            ["05"] = HostApp.AcadMech,
            ["06"] = HostApp.AcadMep,
            ["07"] = HostApp.AcadElec,
            ["17"] = HostApp.Plant3D,
        };

#if NET7_0_OR_GREATER
    [GeneratedRegex(@"ACAD-[0-9A-F]\d(?<productId>\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex ProductKeyPattern();
    private static readonly Regex ProductKeyRegex = ProductKeyPattern();
#else
    private static readonly Regex ProductKeyRegex = new(@"ACAD-[0-9A-F]\d(?<productId>\d{2})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
#endif

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
        using var root = Registry.LocalMachine.OpenSubKey(AutoCadRegistryRoot);
        if (root is null)
            yield break;

        foreach (var releaseName in root.GetSubKeyNames())
        {
            using var releaseKey = root.OpenSubKey(releaseName);
            if (releaseKey is null)
                continue;

            foreach (var entry in EnumerateProducts(releaseKey))
                yield return entry;
        }
    }

    private static IEnumerable<AcadInstallation> EnumerateProducts(RegistryKey releaseKey)
    {
        foreach (var productKeyName in releaseKey.GetSubKeyNames())
        {
            using var productKey = releaseKey.OpenSubKey(productKeyName);
            if (productKey is null)
                continue;

            var installation = TryParseInstallation(productKey, productKeyName);
            if (installation is not null)
                yield return installation;
        }
    }

    private static AcadInstallation? TryParseInstallation(RegistryKey productKey, string productKeyName)
    {
        var versionYear = productKey.GetValue("UPIRELEASE") as string;
        if (string.IsNullOrWhiteSpace(versionYear))
            return null;
        if (!int.TryParse(versionYear, out var year) || year < 2022)
            return null;

        var acadExe = ResolveAcadExe(productKey);
        if (acadExe is null)
            return null;

        var hostApp = DetectProduct(productKeyName);
        return new AcadInstallation(versionYear!, hostApp, acadExe);
    }

    private static HostApp DetectProduct(string productKeyName)
    {
        var match = ProductKeyRegex.Match(productKeyName);
        if (!match.Success)
            return HostApp.AutoCad;
        var productId = match.Groups["productId"].Value;
        return ProductIdMap.TryGetValue(productId, out var hostApp) ? hostApp : HostApp.AutoCad;
    }

    private static string? ResolveAcadExe(RegistryKey productKey)
    {
        return TryFindAcadExe(productKey.GetValue("GlobUPILocation") as string, trimTrailingSlash: true)
               ?? TryFindAcadExe(productKey.GetValue("AcadLocation") as string, trimTrailingSlash: false);
    }

    private static string? TryFindAcadExe(string? location, bool trimTrailingSlash)
    {
        if (string.IsNullOrWhiteSpace(location))
            return null;

        if (trimTrailingSlash)
        {
            var dir = Path.GetDirectoryName(location!.TrimEnd('\\', '/'));
            if (dir is not null)
            {
                var exe = Path.Combine(dir, "acad.exe");
                if (File.Exists(exe))
                    return exe;
            }
        }

        var directExe = Path.Combine(location, "acad.exe");
        return File.Exists(directExe) ? directExe : null;
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
                .Select(dir => Path.Combine(dir, "acad.exe"))
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
